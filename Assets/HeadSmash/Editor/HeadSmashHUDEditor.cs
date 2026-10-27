using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HeadSmash.Editor
{
    [CustomEditor(typeof(HeadSmashHUD))]
    public sealed class HeadSmashHUDEditor : UnityEditor.Editor
    {
        static readonly string[] ScreenProperties = { "introScreen", "pauseScreen", "knockoutScreen", "resultsScreen" };
        static readonly string[] ScreenNames = { "HUD", "Intro", "Pause", "Knockout", "Results" };

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Edit layout in RectTransform, colors/sprites in Image, and copy/fonts in TMP. Button On Click events are saved in the prefab. Live values use the format fields below.", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUILayout.LabelField("Edit-mode screen preview", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                for (int i = 0; i < ScreenNames.Length; i++)
                    if (GUILayout.Button(ScreenNames[i])) Preview((HeadSmashHUD)target, i);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        public static void Preview(HeadSmashHUD hud, int screen)
        {
            if (Application.isPlaying) return;
            var data = new SerializedObject(hud);
            for (int i = 0; i < ScreenProperties.Length; i++)
            {
                var go = data.FindProperty(ScreenProperties[i]).objectReferenceValue as GameObject;
                if (go == null) continue;
                Undo.RecordObject(go, "Preview UI screen");
                go.SetActive(i + 1 == screen);
                if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                EditorUtility.SetDirty(go);
            }
            if (hud.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
