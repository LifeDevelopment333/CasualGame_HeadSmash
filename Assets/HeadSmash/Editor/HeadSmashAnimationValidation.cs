using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace HeadSmash.Editor
{
    /// <summary>Checks the actual controller, evaluated poses, and pause lifecycle in Play mode.</summary>
    public static class HeadSmashAnimationValidation
    {
        [MenuItem("Head Smash/Validate character animations (Play mode)")]
        public static async void RunMenu()
        {
            try { Debug.Log(string.Join("\n", await Run())); }
            catch (Exception error) { Debug.LogException(error); }
        }

        public static async Task<string[]> Run()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<HeadSmashGame>();
            if (!Application.isPlaying || game == null) throw new InvalidOperationException("Open the arena and enter Play mode first.");
            var passed = new List<string>();
            bool wasEnabled = game.enabled;
            game.enabled = false;
            try
            {
                game.Restart();
                var actor = game.Player;
                actor.UpdatePresentation();
                var animator = actor.GetComponent<Animator>();
                var visual = actor.transform.Find("Squash and stretch");
                var leftBoot = visual.Find("Left boot");
                var headMotion = visual.Find("Head anchor/Head motion");
                var head = headMotion.Find("Growing head");
                Check(game.Actors.TrueForAll(a => a.GetComponent<Animator>() != null
                    && a.GetComponent<Animator>().runtimeAnimatorController == animator.runtimeAnimatorController),
                    "Player and AI use the saved Animator Controller", passed);
                Check(!animator.applyRootMotion && animator.layerCount == 3, "Three animation layers with root motion disabled", passed);
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (binding.type == typeof(Transform))
                        Check(binding.path.Length > 0 && actor.transform.Find(binding.path) != null,
                            clip.name + ": resolves binding " + binding.path + "/" + binding.propertyName, passed);

                animator.SetBool("Moving", false);
                animator.SetBool("Dashing", false);
                Advance(animator, .15f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion.Idle"), "Idle state evaluates", passed);
                actor.UpdatePresentation();
                Advance(animator, .15f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion.Walk"), "Playing transitions to Walk", passed);
                float bootPosition = leftBoot.localPosition.z;
                Advance(animator, .1f);
                Check(Mathf.Abs(leftBoot.localPosition.z - bootPosition) > .01f, "Walk clip moves the boots", passed);

                actor.Invulnerable = 0;
                Check(actor.TryDash(), "Gameplay starts a dash", passed);
                actor.UpdatePresentation();
                Advance(animator, .15f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion.Dash")
                    && Mathf.Abs(Mathf.DeltaAngle(headMotion.localEulerAngles.x, 24)) < .1f
                    && Mathf.Abs(headMotion.localPosition.z - .35f) < .001f, "Dash clip supplies the headbutt pose", passed);
                actor.Tick(.4f);
                actor.UpdatePresentation();
                Advance(animator, .15f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion.Walk"), "Dash completion returns to Walk", passed);

                actor.PlayImpact();
                Advance(animator, .07f);
                Check(animator.GetCurrentAnimatorStateInfo(1).IsName("Reactions.Hit")
                    && Mathf.Abs(visual.localScale.x - 1) > .01f, "Hit trigger plays squash and stretch", passed);
                actor.PlayImpact(true);
                Advance(animator, .07f);
                Check(animator.GetCurrentAnimatorStateInfo(1).IsName("Reactions.Bonk"), "Successful attack plays Bonk", passed);
                Advance(animator, .3f);
                Check(animator.GetCurrentAnimatorStateInfo(1).IsName("Reactions.Neutral")
                    && Vector3.Distance(visual.localScale, Vector3.one) < .001f, "Reaction completes and restores neutral scale", passed);
                actor.AddScore(80);
                actor.UpdatePresentation();
                Advance(animator, .1f);
                Check(animator.GetCurrentAnimatorStateInfo(1).IsName("Reactions.LevelUp"), "Level gain triggers LevelUp", passed);
                Check(head.localScale.x > 1 && Mathf.Approximately(head.localScale.x, actor.HeadScale),
                    "Animator preserves score-driven head growth", passed);

                actor.Invulnerable = 2;
                actor.HitGrace = 0;
                actor.UpdatePresentation();
                bool on = false, off = false;
                for (int i = 0; i < 30; i++)
                {
                    animator.Update(.02f);
                    float flash = new SerializedObject(actor).FindProperty("flashAmount").floatValue;
                    on |= flash > .5f;
                    off |= flash < .5f;
                }
                Check(animator.GetCurrentAnimatorStateInfo(2).IsName("Status flash.Shield") && on && off,
                    "ShieldFlash clip alternates the flash value", passed);
                actor.HitGrace = .5f;
                actor.UpdatePresentation();
                Advance(animator, .06f);
                Check(animator.GetCurrentAnimatorStateInfo(2).IsName("Status flash.Hurt"), "Hurt flash takes priority over the shield", passed);
                actor.Invulnerable = actor.HitGrace = 0;
                actor.UpdatePresentation();
                Advance(animator, .06f);
                Check(animator.GetCurrentAnimatorStateInfo(2).IsName("Status flash.Normal")
                    && new SerializedObject(actor).FindProperty("flashAmount").floatValue < .5f,
                    "Status completion restores the actor color", passed);

                game.TogglePause();
                foreach (var a in game.Actors) a.UpdatePresentation();
                float pausedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                var pausedPose = leftBoot.localPosition;
                int frame = Time.frameCount;
                await Task.Delay(180);
                Check(Time.frameCount > frame, "Play mode advances real frames during validation", passed);
                Check(game.Actors.TrueForAll(a => a.GetComponent<Animator>().speed == 0)
                    && Mathf.Approximately(pausedTime, animator.GetCurrentAnimatorStateInfo(0).normalizedTime)
                    && Vector3.Distance(pausedPose, leftBoot.localPosition) < .0001f, "Pause freezes the controller and pose for all actors", passed);
                game.TogglePause();
                actor.UpdatePresentation();
                Advance(animator, .1f);
                Check(animator.speed == 1 && !Mathf.Approximately(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, pausedTime),
                    "Resume advances animation again", passed);

                actor.PlayImpact();
                Advance(animator, .05f);
                actor.SetDead();
                Check(!visual.gameObject.activeSelf && animator.speed == 0, "Death hides the model and stops animation", passed);
                actor.Respawn(Vector3.zero, 0);
                Advance(animator, .15f);
                Check(visual.gameObject.activeSelf && animator.GetCurrentAnimatorStateInfo(1).IsName("Reactions.Neutral")
                    && Vector3.Distance(visual.localScale, Vector3.one) < .001f
                    && Mathf.Approximately(head.localScale.x, 1), "Respawn clears reactions and resets the grown pose", passed);
                passed.Add("PASS: character Animator integration checks");
                return passed.ToArray();
            }
            finally
            {
                game.enabled = wasEnabled;
                game.Restart();
                game.TogglePause();
                foreach (var actor in game.Actors) actor.UpdatePresentation();
            }
        }

        static void Advance(Animator animator, float seconds)
        {
            int steps = Mathf.CeilToInt(seconds * 60);
            for (int i = 0; i < steps; i++) animator.Update(seconds / steps);
        }

        static void Check(bool result, string label, List<string> passed)
        {
            if (!result) throw new Exception("FAIL: " + label);
            passed.Add("PASS: " + label);
        }
    }
}
