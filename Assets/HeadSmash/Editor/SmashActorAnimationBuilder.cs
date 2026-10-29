using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HeadSmash.Editor
{
    /// <summary>One-time migration. Authored clips, controller, and model edits are preserved on subsequent runs.</summary>
    public static class SmashActorAnimationBuilder
    {
        public const string Folder = "Assets/HeadSmash/Animations";
        public const string ControllerPath = Folder + "/SmashActor.controller";

        [MenuItem("Head Smash/Characters/Add Animator to actor prefab")]
        public static void Migrate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            var prefab = AssetDatabase.LoadAssetAtPath<SmashActor>(SmashActorPrefabBuilder.PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Create the actor prefab first.");
            EnsureAnimator(prefab);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        }

        public static void EnsureAnimator(SmashActor prefab)
        {
            var saved = new SerializedObject(prefab);
            var existing = saved.FindProperty("animator").objectReferenceValue as Animator;
            if (existing != null && existing.runtimeAnimatorController != null
                && saved.FindProperty("headAnchor").objectReferenceValue != null) return;
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/HeadSmash", "Animations");
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var actor = root.GetComponent<SmashActor>();
                var data = new SerializedObject(actor);
                var head = (Transform)data.FindProperty("head").objectReferenceValue;
                var visual = (Transform)data.FindProperty("visual").objectReferenceValue;
                var anchor = visual.Find("Head anchor");
                if (anchor == null)
                {
                    anchor = new GameObject("Head anchor").transform;
                    anchor.SetParent(visual, false);
                    anchor.localPosition = head.localPosition;
                    var motion = new GameObject("Head motion").transform;
                    motion.SetParent(anchor, false);
                    head.SetParent(motion, false);
                    head.localPosition = Vector3.zero;
                }
                var animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                if (controller == null) controller = CreateController(root.transform, visual, anchor.Find("Head motion"));
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;
                data.FindProperty("animator").objectReferenceValue = animator;
                data.FindProperty("headAnchor").objectReferenceValue = anchor;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        static AnimatorController CreateController(Transform root, Transform visual, Transform headMotion)
        {
            string visualPath = AnimationUtility.CalculateTransformPath(visual, root);
            string headPath = AnimationUtility.CalculateTransformPath(headMotion, root);
            string leftPath = AnimationUtility.CalculateTransformPath(visual.Find("Left boot"), root);
            string rightPath = AnimationUtility.CalculateTransformPath(visual.Find("Right boot"), root);
            var idle = MotionClip("Idle", 2 * Mathf.PI / 3, visualPath, headPath, leftPath, rightPath, false, false);
            var walk = MotionClip("Walk", 2 * Mathf.PI / 12, visualPath, headPath, leftPath, rightPath, true, false);
            var dash = MotionClip("Dash", 2 * Mathf.PI / 22, visualPath, headPath, leftPath, rightPath, true, true);
            var neutral = ScaleClip("ReactionNeutral", visualPath, true, new[] { 0f, .1f }, new[] { 1f, 1f });
            var hit = ScaleClip("Hit", visualPath, false, new[] { 0f, .045f, .12f, .21f, .3f }, new[] { 1f, 1.12f, .93f, 1.035f, 1f });
            var bonk = ScaleClip("Bonk", visualPath, false, new[] { 0f, .035f, .09f, .17f }, new[] { 1f, .9f, 1.06f, 1f });
            var levelUp = ScaleClip("LevelUp", visualPath, false, new[] { 0f, .08f, .18f, .29f, .4f }, new[] { 1f, .88f, 1.14f, .96f, 1f });
            var normal = FlashClip("FlashNormal", .1f, 0);
            var shield = FlashClip("ShieldFlash", 2 * Mathf.PI / 18, .45f);
            var hurt = FlashClip("HitFlash", 2 * Mathf.PI / 40, .001f);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (string parameter in new[] { "Moving", "Dashing", "Shielded", "Hurt" })
                controller.AddParameter(parameter, AnimatorControllerParameterType.Bool);
            foreach (string parameter in new[] { "Hit", "Bonk", "LevelUp" })
                controller.AddParameter(parameter, AnimatorControllerParameterType.Trigger);
            var layers = controller.layers;
            layers[0].name = "Locomotion";
            controller.layers = layers;
            var motion = controller.layers[0].stateMachine;
            motion.name = "Locomotion";
            var idleState = State(motion, "Idle", idle, new Vector3(240, 0));
            var walkState = State(motion, "Walk", walk, new Vector3(480, 0));
            var dashState = State(motion, "Dash", dash, new Vector3(480, 150));
            motion.defaultState = idleState;
            When(Transition(idleState, walkState), "Moving", true, "Dashing", false);
            When(Transition(walkState, idleState), "Moving", false, "Dashing", false);
            When(Transition(idleState, dashState), "Dashing", true);
            When(Transition(walkState, dashState), "Dashing", true);
            When(Transition(dashState, walkState), "Dashing", false, "Moving", true);
            When(Transition(dashState, idleState), "Dashing", false, "Moving", false);

            var reactions = Layer(controller, "Reactions");
            var neutralState = State(reactions, "Neutral", neutral, new Vector3(240, 0));
            reactions.defaultState = neutralState;
            Reaction(reactions, neutralState, "Hit", hit, new Vector3(480, -100));
            Reaction(reactions, neutralState, "Bonk", bonk, new Vector3(480, 50));
            Reaction(reactions, neutralState, "LevelUp", levelUp, new Vector3(480, 200));

            var flashes = Layer(controller, "Status flash");
            var normalState = State(flashes, "Normal", normal, new Vector3(240, 0));
            var shieldState = State(flashes, "Shield", shield, new Vector3(480, -80));
            var hurtState = State(flashes, "Hurt", hurt, new Vector3(480, 100));
            flashes.defaultState = normalState;
            When(Transition(normalState, shieldState, 0), "Shielded", true, "Hurt", false);
            When(Transition(normalState, hurtState, 0), "Hurt", true);
            When(Transition(shieldState, hurtState, 0), "Hurt", true);
            When(Transition(shieldState, normalState, 0), "Shielded", false, "Hurt", false);
            When(Transition(hurtState, shieldState, 0), "Hurt", false, "Shielded", true);
            When(Transition(hurtState, normalState, 0), "Hurt", false, "Shielded", false);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorStateMachine Layer(AnimatorController controller, string name)
        {
            controller.AddLayer(name);
            var layers = controller.layers;
            layers[layers.Length - 1].defaultWeight = 1;
            controller.layers = layers;
            return controller.layers[layers.Length - 1].stateMachine;
        }

        static AnimatorState State(AnimatorStateMachine machine, string name, AnimationClip clip, Vector3 position)
        {
            var state = machine.AddState(name, position);
            state.motion = clip;
            state.writeDefaultValues = false;
            return state;
        }

        static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float duration = .06f)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            return transition;
        }

        static void When(AnimatorStateTransition transition, string parameter, bool value, string second = null, bool secondValue = false)
        {
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, parameter);
            if (second != null) transition.AddCondition(secondValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, second);
        }

        static void Reaction(AnimatorStateMachine machine, AnimatorState neutral, string name, AnimationClip clip, Vector3 position)
        {
            var state = State(machine, name, clip, position);
            var enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.hasFixedDuration = true;
            enter.duration = .025f;
            enter.canTransitionToSelf = true;
            enter.AddCondition(AnimatorConditionMode.If, 0, name);
            var exit = Transition(state, neutral, .025f);
            exit.hasExitTime = true;
            exit.exitTime = 1;
        }

        static AnimationClip Clip(string name, bool loop)
        {
            var clip = new AnimationClip { name = name, frameRate = 60, wrapMode = loop ? WrapMode.Loop : WrapMode.Once };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, Folder + "/" + name + ".anim");
            return clip;
        }

        static AnimationClip MotionClip(string name, float duration, string visual, string head, string left, string right, bool moving, bool dashing)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + name + ".anim");
            if (existing != null) return existing;
            var clip = Clip(name, true);
            Sample(clip, visual, typeof(Transform), "m_LocalPosition.y", duration,
                t => moving ? Mathf.Abs(Mathf.Sin(t / duration * Mathf.PI * 2)) * .08f : (1 - Mathf.Cos(t / duration * Mathf.PI * 2)) * .012f);
            Sample(clip, left, typeof(Transform), "m_LocalPosition.z", duration,
                t => .1f + (moving ? Mathf.Sin(t / duration * Mathf.PI * 2) * .18f : 0));
            Sample(clip, right, typeof(Transform), "m_LocalPosition.z", duration,
                t => .1f - (moving ? Mathf.Sin(t / duration * Mathf.PI * 2) * .18f : 0));
            Sample(clip, head, typeof(Transform), "m_LocalPosition.z", duration, t => dashing ? .35f : 0);
            Sample(clip, head, typeof(Transform), "localEulerAnglesRaw.x", duration,
                t => dashing ? 24 : Mathf.Sin(t / duration * Mathf.PI * 2) * 2);
            Sample(clip, head, typeof(Transform), "localEulerAnglesRaw.z", duration,
                t => moving && !dashing ? Mathf.Sin(t / duration * Mathf.PI * 2) * 3 : 0);
            return clip;
        }

        static AnimationClip ScaleClip(string name, string path, bool loop, float[] times, float[] widths)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + name + ".anim");
            if (existing != null) return existing;
            var clip = Clip(name, loop);
            foreach (string axis in new[] { "x", "y", "z" })
            {
                var keys = new Keyframe[times.Length];
                for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(times[i], axis == "y" ? 2 - widths[i] : widths[i]);
                var curve = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale." + axis), curve);
            }
            return clip;
        }

        static AnimationClip FlashClip(string name, float duration, float threshold)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + name + ".anim");
            if (existing != null) return existing;
            var clip = Clip(name, true);
            Sample(clip, "", typeof(SmashActor), "flashAmount", duration,
                t => name == "FlashNormal" ? 0 : Mathf.Sin(t / duration * Mathf.PI * 2) > threshold ? 1 : 0, true);
            return clip;
        }

        static void Sample(AnimationClip clip, string path, Type type, string property, float duration, Func<float, float> value, bool stepped = false)
        {
            int count = Mathf.CeilToInt(duration * 60);
            var keys = new Keyframe[count + 1];
            for (int i = 0; i <= count; i++) keys[i] = new Keyframe(duration * i / count, value(duration * i / count));
            var curve = new AnimationCurve(keys);
            for (int i = 0; i <= count; i++)
            {
                var mode = stepped ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.ClampedAuto;
                AnimationUtility.SetKeyLeftTangentMode(curve, i, mode);
                AnimationUtility.SetKeyRightTangentMode(curve, i, mode);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
        }
    }
}
