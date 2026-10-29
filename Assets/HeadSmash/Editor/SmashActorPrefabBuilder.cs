using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HeadSmash.Editor
{
    /// <summary>Authors the character once. Existing prefab edits are preserved.</summary>
    public sealed class SmashActorPrefabBuilder
    {
        public const string PrefabPath = "Assets/HeadSmash/Prefabs/SmashActor.prefab";
        HeadSmashGame game;
        Transform transform, visual, head, body, leftLeg, rightLeg, marker, heading, crown, cracks;
        Transform healthFill, nameAnchor;
        TextMeshPro nameText;
        readonly List<MeshRenderer> skins = new List<MeshRenderer>();

        [MenuItem("Head Smash/Characters/Create and connect actor prefab")]
        public static void AddToCurrentScene()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
            var owner = Object.FindFirstObjectByType<HeadSmashGame>();
            if (owner == null) throw new System.InvalidOperationException("Open the HeadSmash arena first.");
            CreateForGame(owner);
            EditorSceneManager.SaveScene(owner.gameObject.scene);
            Selection.activeObject = owner.actorPrefab;
        }

        public static SmashActor CreateForGame(HeadSmashGame owner)
        {
            if (owner.actorPrefab != null)
            {
                SmashActorAnimationBuilder.EnsureAnimator(owner.actorPrefab);
                return owner.actorPrefab;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<SmashActor>(PrefabPath);
            if (prefab == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/HeadSmash/Prefabs"))
                    AssetDatabase.CreateFolder("Assets/HeadSmash", "Prefabs");
                var root = new GameObject("SmashActor");
                try
                {
                    var builder = new SmashActorPrefabBuilder { game = owner, transform = root.transform };
                    var actor = root.AddComponent<SmashActor>();
                    builder.BuildCharacter();
                    builder.Bind(actor);
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath).GetComponent<SmashActor>();
                }
                finally { Object.DestroyImmediate(root); }
            }
            SmashActorAnimationBuilder.EnsureAnimator(prefab);
            Undo.RecordObject(owner, "Connect actor prefab");
            owner.actorPrefab = prefab;
            EditorUtility.SetDirty(owner);
            EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        void Bind(SmashActor actor)
        {
            var data = new SerializedObject(actor);
            BindReference(data, "visual", visual);
            BindReference(data, "head", head);
            BindReference(data, "marker", marker);
            BindReference(data, "heading", heading);
            BindReference(data, "crown", crown);
            BindReference(data, "cracks", cracks);
            BindReference(data, "healthFill", healthFill);
            BindReference(data, "nameAnchor", nameAnchor);
            BindReference(data, "nameText", nameText);
            var skinData = data.FindProperty("skins");
            skinData.arraySize = skins.Count;
            for (int i = 0; i < skins.Count; i++) skinData.GetArrayElementAtIndex(i).objectReferenceValue = skins[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BindReference(SerializedObject data, string field, Object value) => data.FindProperty(field).objectReferenceValue = value;
        Transform Part(PrimitiveType type, string label, Transform parent, Vector3 p, Vector3 scale, int material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = label;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = p;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = game.palette[material];
            if (material == 0) skins.Add(r);
            return go.transform;
        }

        void BuildCharacter()
        {
            visual = new GameObject("Squash and stretch").transform;
            visual.SetParent(transform, false);
            body = Part(PrimitiveType.Capsule, "Tiny suit", visual, new Vector3(0, .62f, 0), new Vector3(.7f, .42f, .55f), 0);
            Part(PrimitiveType.Sphere, "Belly patch", body, new Vector3(0, -.08f, .44f), new Vector3(.6f, .43f, .18f), 7);
            leftLeg = Part(PrimitiveType.Sphere, "Left boot", visual, new Vector3(-.25f, .19f, .1f), new Vector3(.34f, .3f, .49f), 13);
            rightLeg = Part(PrimitiveType.Sphere, "Right boot", visual, new Vector3(.25f, .19f, .1f), new Vector3(.34f, .3f, .49f), 13);
            Part(PrimitiveType.Sphere, "Left mitten", visual, new Vector3(-.49f, .72f, .06f), Vector3.one * .3f, 7);
            Part(PrimitiveType.Sphere, "Right mitten", visual, new Vector3(.49f, .72f, .06f), Vector3.one * .3f, 7);
            head = new GameObject("Growing head").transform;
            head.SetParent(visual, false);
            Part(PrimitiveType.Sphere, "Head", head, Vector3.zero, new Vector3(1.62f, 1.48f, 1.5f), 0);
            Part(PrimitiveType.Sphere, "Face", head, new Vector3(0, -.03f, .53f), new Vector3(1.21f, 1.03f, .56f), 7);
            for (int i = -1; i <= 1; i += 2)
            {
                Part(PrimitiveType.Sphere, "Eye white", head, new Vector3(i * .27f, .13f, .775f), new Vector3(.29f, .38f, .13f), 12);
                Part(PrimitiveType.Sphere, "Eye", head, new Vector3(i * .26f, .12f, .837f), new Vector3(.14f, .22f, .08f), 13);
                Part(PrimitiveType.Sphere, "Eye glint", head, new Vector3(i * .26f - .025f, .17f, .876f), Vector3.one * .045f, 12);
                Part(PrimitiveType.Sphere, "Blush", head, new Vector3(i * .44f, -.15f, .76f), new Vector3(.18f, .1f, .06f), 5);
                var brow = Part(PrimitiveType.Capsule, "Determined brow", head, new Vector3(i * .27f, .37f, .78f), new Vector3(.07f, .17f, .07f), 13);
                brow.localRotation = Quaternion.Euler(0, 0, i * 72);
                Part(PrimitiveType.Sphere, "Ear", head, new Vector3(i * .8f, -.02f, 0), new Vector3(.22f, .36f, .31f), 0);
            }
            Part(PrimitiveType.Sphere, "Smile", head, new Vector3(0, -.3f, .8f), new Vector3(.25f, .11f, .06f), 13);
            Part(PrimitiveType.Sphere, "Tooth", head, new Vector3(.035f, -.275f, .832f), new Vector3(.095f, .045f, .025f), 12);
            Part(PrimitiveType.Sphere, "Hair tuft", head, new Vector3(0, .71f, -.04f), new Vector3(.47f, .34f, .51f), 0);
            Part(PrimitiveType.Sphere, "Hair tuft small", head, new Vector3(.25f, .69f, -.01f), new Vector3(.3f, .25f, .36f), 0);
            cracks = new GameObject("Damage cracks").transform;
            cracks.SetParent(head, false);
            for (int i = 0; i < 3; i++)
            {
                var crack = Part(PrimitiveType.Cube, "Crack", cracks, new Vector3(-.43f + i * .07f, .32f - i * .13f, .801f), new Vector3(.045f, .18f, .025f), 13);
                crack.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? -28 : 25);
            }
            cracks.gameObject.SetActive(false);
            crown = new GameObject("Leader crown").transform;
            crown.SetParent(head, false);
            Part(PrimitiveType.Cube, "Gold band", crown, new Vector3(0, .91f, 0), new Vector3(.69f, .15f, .4f), 8);
            for (int i = -1; i <= 1; i++)
            {
                var point = Part(PrimitiveType.Cube, "Crown point", crown, new Vector3(i * .24f, 1.08f, 0), new Vector3(.2f, .23f, .23f), 8);
                point.localRotation = Quaternion.Euler(0, 0, 45);
            }
            crown.gameObject.SetActive(false);
            marker = Part(PrimitiveType.Cylinder, "Player halo", transform, new Vector3(0, .035f, 0), new Vector3(2.05f, .015f, 2.05f), 14);
            heading = Part(PrimitiveType.Cube, "Heading", transform, new Vector3(0, .05f, 1.6f), new Vector3(.3f, .035f, .3f), 8);
            heading.localRotation = Quaternion.Euler(0, 45, 0);
            nameAnchor = new GameObject("Billboard").transform;
            nameAnchor.SetParent(transform, false);
            var textGo = new GameObject("Name");
            textGo.transform.SetParent(nameAnchor, false);
            nameText = textGo.AddComponent<TextMeshPro>();
            nameText.font = game.font;
            nameText.fontSize = 2.6f;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = new Color(.1f, .24f, .3f);
            nameText.fontStyle = FontStyles.Bold;
            nameText.rectTransform.sizeDelta = new Vector2(6, .5f);
            var healthBack = Part(PrimitiveType.Cube, "Health track", nameAnchor, new Vector3(0, -.32f, 0), new Vector3(1.25f, .08f, .045f), 13);
            healthFill = Part(PrimitiveType.Cube, "Health", healthBack, new Vector3(0, 0, -.65f), new Vector3(.96f, .7f, 1), 9);
            head.localPosition = new Vector3(0, 1.58f, 0);
            nameAnchor.localPosition = new Vector3(0, 2.85f, 0);
            nameText.text = "YOU <size=75%>0</size>";
        }

    }
}
