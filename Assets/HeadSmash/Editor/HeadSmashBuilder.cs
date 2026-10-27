using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HeadSmash.Editor
{
    public static class HeadSmashBuilder
    {
        public const string ScenePath = "Assets/HeadSmash/Scenes/HeadSmashArena.unity";
        const string Art = "Assets/HeadSmash/Art/";
        static Material[] materials;

        [MenuItem("Head Smash/Create or rebuild prototype arena")]
        public static void CreateArena()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before rebuilding the arena.");
            Directory.CreateDirectory(Art);
            Directory.CreateDirectory("Assets/HeadSmash/Scenes");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) throw new System.InvalidOperationException("Import TMP Essentials first with TMP_PackageResourceImporter.ImportResources(true, false, false).");
            BuildMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var game = new GameObject("Head Smash / Game").AddComponent<HeadSmashGame>();
            game.font = font;
            game.palette = materials;
            var world = new GameObject("Arena / Mint playground").transform;
            Part("Water", PrimitiveType.Cube, world, new Vector3(0, -1.4f, 0), new Vector3(180, .4f, 180), 17);
            Part("Island foundation", PrimitiveType.Cube, world, new Vector3(0, -.7f, 0), new Vector3(63, 1.2f, 63), 18);
            Part("Arena floor", PrimitiveType.Cube, world, new Vector3(0, -.15f, 0), new Vector3(60, .3f, 60), 15);
            var grid = new GameObject("Subtle floor grid").transform;
            grid.SetParent(world, false);
            for (int i = -7; i <= 7; i++)
            {
                Part("Grid X", PrimitiveType.Cube, grid, new Vector3(i * 4, .007f, 0), new Vector3(.035f, .012f, 58), 16);
                Part("Grid Z", PrimitiveType.Cube, grid, new Vector3(0, .007f, i * 4), new Vector3(58, .012f, .035f), 16);
            }
            var walls = new GameObject("Soft arena bumpers").transform;
            walls.SetParent(world, false);
            for (int side = 0; side < 4; side++)
            for (int i = 0; i < 15; i++)
            {
                float offset = -28 + i * 4;
                Vector3 p = side < 2 ? new Vector3(offset, .45f, side == 0 ? -30 : 30) : new Vector3(side == 2 ? -30 : 30, .45f, offset);
                Vector3 scale = side < 2 ? new Vector3(3.85f, .9f, .9f) : new Vector3(.9f, .9f, 3.85f);
                Part("Candy bumper", PrimitiveType.Cube, walls, p, scale, i % 3 == 0 ? 8 : 12);
            }
            var props = new GameObject("Outside / Trees and pebbles").transform;
            props.SetParent(world, false);
            Random.InitState(527);
            for (int i = 0; i < 28; i++)
            {
                float angle = i * Mathf.PI * 2 / 28;
                Vector3 p = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * Random.Range(36, 44);
                var tree = new GameObject("Bubble tree").transform;
                tree.SetParent(props, false); tree.localPosition = p;
                float size = Random.Range(.8f, 1.5f);
                Part("Trunk", PrimitiveType.Cylinder, tree, new Vector3(0, .2f, 0), new Vector3(.45f, 1.1f, .45f) * size, 19);
                Part("Canopy", PrimitiveType.Sphere, tree, new Vector3(0, 2, 0), new Vector3(2.5f, 2.8f, 2.3f) * size, i % 2 == 0 ? 20 : 21);
                Part("Canopy puff", PrimitiveType.Sphere, tree, new Vector3(.7f, 1.7f, .2f), Vector3.one * 1.7f * size, i % 2 == 0 ? 20 : 21);
            }
            // Four islands of decorative flowers leave the play space unobstructed.
            for (int i = 0; i < 36; i++)
            {
                float edge = i % 2 == 0 ? -31.6f : 31.6f;
                Vector3 p = i % 4 < 2 ? new Vector3(edge, -.05f, Random.Range(-29f, 29f)) : new Vector3(Random.Range(-29f, 29f), -.05f, edge);
                Part("Pebble", PrimitiveType.Sphere, props, p, new Vector3(.45f, .2f, .4f) * Random.Range(.7f, 1.3f), i % 3 == 0 ? 5 : 12);
            }
            var logo = new GameObject("Arena floor emblem").AddComponent<TextMeshPro>();
            logo.transform.SetParent(world, false);
            logo.transform.position = new Vector3(0, .024f, 0);
            logo.transform.rotation = Quaternion.Euler(90, 0, 0);
            logo.font = font;
            logo.fontSize = 12;
            logo.text = "HEAD\nSMASH";
            logo.alignment = TextAlignmentOptions.Center;
            logo.fontStyle = FontStyles.Bold;
            logo.color = new Color(.38f, .71f, .56f);
            logo.rectTransform.sizeDelta = new Vector2(16, 9);
            logo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 11.4f;
            camera.transform.position = new Vector3(0, 27, -19);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0, -27, 19));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.46f, .78f, .83f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 180;
            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = 1;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var sun = new GameObject("Sun / warm soft shadows").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.8f;
            sun.color = new Color(1, .96f, .87f);
            sun.transform.rotation = Quaternion.Euler(52, -28, 0);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .42f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.68f, .77f, .8f);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            QualitySettings.SetQualityLevel(0, true);
            QualitySettings.shadowDistance = 75;
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            if (UniversalRenderPipeline.asset != null)
            {
                UniversalRenderPipeline.asset.supportsHDR = true;
                UniversalRenderPipeline.asset.shadowDistance = 75;
                EditorUtility.SetDirty(UniversalRenderPipeline.asset);
            }
            BuildVolume();
            PlayerSettings.companyName = "HeadSmashPrototype";
            PlayerSettings.productName = "Head Smash";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = game.gameObject;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.LookAt(new Vector3(0, 0, 0), camera.transform.rotation, 35);
            Debug.Log("HEAD SMASH: Arena created. Open HeadSmashArena and press Play.");
        }

        static void BuildMaterials()
        {
            string[] names = { "Player tangerine", "Mango gold", "Mochi lilac", "Bonk aqua", "Pudding blue", "Kiwi pink", "Bubble lime", "Warm cream", "XP gold", "XP mint", "XP rose", "XP blue", "Porcelain", "Ink", "Player halo", "Mint floor", "Grid", "Water", "Island edge", "Tree trunk", "Tree mint", "Tree jade" };
            string[] colors = { "FF7748", "FFCB47", "B19AEE", "4DCCB7", "6AA6EF", "F28DB4", "B2CE63", "FFF1D7", "FFD34F", "88ECC2", "FFA6BF", "9CCFFF", "FFFEF3", "233C4B", "FFE99B", "B6E6C2", "A2D7B2", "75C5CF", "75BFA4", "C49471", "78CDA5", "51B79D" };
            materials = new Material[names.Length];
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.Exception("URP Lit shader missing.");
            for (int i = 0; i < names.Length; i++)
            {
                string path = Art + names[i] + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
                ColorUtility.TryParseHtmlString("#" + colors[i], out Color c);
                m.color = c;
                m.SetFloat("_Smoothness", .18f);
                m.SetFloat("_Metallic", 0);
                if (i >= 8 && i <= 11)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", c * .18f);
                }
                EditorUtility.SetDirty(m);
                materials[i] = m;
            }
        }

        static void BuildVolume()
        {
            string path = Art + "Arena atmosphere.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            if (!profile.TryGet<Tonemapping>(out var tone)) { tone = profile.Add<Tonemapping>(true); AssetDatabase.AddObjectToAsset(tone, profile); }
            tone.mode.Override(TonemappingMode.Neutral);
            if (!profile.TryGet<Bloom>(out var bloom)) { bloom = profile.Add<Bloom>(true); AssetDatabase.AddObjectToAsset(bloom, profile); }
            bloom.intensity.Override(.25f);
            bloom.threshold.Override(1f);
            if (!profile.TryGet<ColorAdjustments>(out var adjust)) { adjust = profile.Add<ColorAdjustments>(true); AssetDatabase.AddObjectToAsset(adjust, profile); }
            adjust.saturation.Override(5);
            adjust.contrast.Override(4);
            if (!profile.TryGet<Vignette>(out var vignette)) { vignette = profile.Add<Vignette>(true); AssetDatabase.AddObjectToAsset(vignette, profile); }
            vignette.intensity.Override(.13f);
            vignette.smoothness.Override(.45f);
            var volume = new GameObject("Global Volume / clean mobile").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            volume.gameObject.layer = 0;
            EditorUtility.SetDirty(profile);
        }

        static Transform Part(string name, PrimitiveType shape, Transform parent, Vector3 p, Vector3 scale, int material)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = p;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = materials[material];
            return go.transform;
        }

        [MenuItem("Head Smash/Build Windows prototype")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/HeadSmash");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/HeadSmash/HeadSmash.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Head Smash build failed: " + report.summary.result);
            Debug.Log("HEAD SMASH: Windows build ready in Builds/HeadSmash.");
        }

        [MenuItem("Head Smash/Build Android APK")]
        public static void BuildAndroid()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Stop Play mode before building.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new System.InvalidOperationException("Switch the active build target to Android first.");

            Directory.CreateDirectory("Builds/Android");
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, "com.headsmash.prototype");
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.Android.useCustomKeystore = false;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            AssetDatabase.SaveAssets();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Android/HeadSmash.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Head Smash APK build failed: " + report.summary.result);
            Debug.Log($"HEAD SMASH: Android APK ready in Builds/Android/HeadSmash.apk ({report.summary.totalSize / 1048576f:F1} MB).");
        }
    }
}
