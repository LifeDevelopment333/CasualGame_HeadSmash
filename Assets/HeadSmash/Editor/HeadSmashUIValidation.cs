using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HeadSmash.Editor
{
    public static class HeadSmashUIValidation
    {
        public static string[] CheckAssets()
        {
            var passed = new List<string>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeadSmashUIBuilder.PrefabPath);
            Check(prefab != null && prefab.GetComponent<Canvas>() != null, "UI is a saved native Canvas prefab", passed);
            var hud = prefab.GetComponent<HeadSmashHUD>();
            var properties = new SerializedObject(hud).GetIterator();
            while (properties.NextVisible(true))
                if (properties.propertyType == SerializedPropertyType.ObjectReference)
                    Check(properties.objectReferenceValue != null, "Assigned UI reference: " + properties.name, passed);
            var buttons = prefab.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            Check(buttons.Length == 9 && buttons.All(b => b.onClick.GetPersistentEventCount() == 1 && b.onClick.GetPersistentTarget(0) == hud), "All nine buttons have Inspector-visible persistent events", passed);
            Check(prefab.GetComponentsInChildren<TMP_Text>(true).All(t => t.font != null), "Every editable label has a saved TMP font", passed);
            Check(prefab.GetComponentsInChildren<UnityEngine.UI.Image>(true).All(i => i.sprite != null && AssetDatabase.Contains(i.sprite)), "UI sprites are persistent project assets", passed);
            Check(prefab.GetComponentsInChildren<Transform>(true).All(t => t.GetComponents<Component>().All(c => c != null)), "No missing scripts in the UI prefab", passed);
            var game = UnityEngine.Object.FindFirstObjectByType<HeadSmashGame>();
            Check(game != null && game.hud != null && PrefabUtility.IsPartOfPrefabInstance(game.hud), "Arena uses the editable UI prefab instance", passed);
            Check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "Exactly one active EventSystem", passed);
            return passed.ToArray();
        }

        public static string[] CheckPlayMode()
        {
            var g = UnityEngine.Object.FindFirstObjectByType<HeadSmashGame>();
            if (!Application.isPlaying || g == null) throw new InvalidOperationException("Enter Play mode first.");
            var passed = new List<string>();
            var hud = g.hud;
            bool enabledBefore = g.enabled;
            g.enabled = false;
            var data = new SerializedObject(hud);
            var intro = (GameObject)data.FindProperty("introScreen").objectReferenceValue;
            var pause = (GameObject)data.FindProperty("pauseScreen").objectReferenceValue;
            var ko = (GameObject)data.FindProperty("knockoutScreen").objectReferenceValue;
            var results = (GameObject)data.FindProperty("resultsScreen").objectReferenceValue;
            var play = intro.GetComponentInChildren<UnityEngine.UI.Button>(true);
            var copy = play.GetComponentInChildren<TextMeshProUGUI>();
            string originalText = copy.text;
            Color originalColor = copy.color;
            Vector2 originalPosition = play.GetComponent<RectTransform>().anchoredPosition;
            try
            {
                g.Restart();
                Check(UnityEngine.Object.FindObjectsByType<HeadSmashHUD>(FindObjectsSortMode.None).Length == 1 && UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length == 1,
                    "Play reuses the authored Canvas without duplicating it", passed);
                copy.text = "MY EDITABLE BUTTON";
                copy.color = new Color(.65f, .1f, .4f);
                play.GetComponent<RectTransform>().anchoredPosition = originalPosition + new Vector2(7, 3);
                hud.ShowIntro(); hud.Refresh();
                Check(copy.text == "MY EDITABLE BUTTON" && copy.color != originalColor && play.GetComponent<RectTransform>().anchoredPosition == originalPosition + new Vector2(7, 3),
                    "Screen display preserves authored copy, color and position", passed);
                int count = intro.transform.parent.GetComponentsInChildren<Transform>(true).Length;
                for (int i = 0; i < 4; i++) { hud.ShowIntro(); hud.ShowPause(); hud.HideModal(); }
                Check(count == intro.transform.parent.GetComponentsInChildren<Transform>(true).Length,
                    "Screen transitions reuse existing objects", passed);

                typeof(HeadSmashGame).GetProperty("State").GetSetMethod(true).Invoke(g, new object[] { MatchState.Ready });
                hud.ShowIntro();
                Canvas.ForceUpdateCanvases();
                var eventData = new PointerEventData(EventSystem.current)
                {
                    pointerId = 7,
                    position = RectTransformUtility.WorldToScreenPoint(null, play.transform.position)
                };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, hits);
                Check(hits.Count > 0 && hits[0].gameObject == play.gameObject, "Start button is reachable through the real UI raycaster", passed);
                play.onClick.Invoke();
                Check(!intro.activeSelf, "Start button persistent event hides the intro", passed);
                var pauseButton = hud.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "Pause");
                pauseButton.onClick.Invoke();
                Check(g.State == MatchState.Paused && pause.activeSelf, "Pause button opens the saved pause screen", passed);
                pause.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "Resume").onClick.Invoke();
                Check(g.State == MatchState.Playing && !pause.activeSelf, "Resume button returns to the arena", passed);

                var stick = (SmashJoystick)data.FindProperty("stick").objectReferenceValue;
                Vector2 center = RectTransformUtility.WorldToScreenPoint(null, stick.transform.position);
                eventData.position = center + Vector2.right * 60;
                stick.OnPointerDown(eventData);
                Check(stick.Value.x > .2f, "Saved joystick receives directional pointer input", passed);
                stick.OnPointerUp(eventData);
                Check(stick.Value == Vector2.zero, "Joystick release resets movement input", passed);
                var dash = hud.GetComponentInChildren<SmashPress>(true);
                dash.OnPointerDown(eventData);
                Check(hud.ConsumeDash(), "Skill Button fires its saved event on pointer down", passed);
                dash.OnPointerUp(eventData); dash.OnPointerClick(eventData);
                Check(!hud.ConsumeDash(), "Skill release does not fire a duplicate action", passed);

                g.Shatter(g.Player, g.Actors[1]);
                Check(ko.activeSelf && !intro.activeSelf && !pause.activeSelf, "Player defeat selects the saved knockout screen", passed);
                ko.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "Respawn").onClick.Invoke();
                Check(g.State == MatchState.Playing && !ko.activeSelf, "Saved respawn button returns to play", passed);
                hud.ShowResults();
                Check(results.activeSelf, "Results reuse the authored result screen", passed);
                results.GetComponentInChildren<UnityEngine.UI.Button>(true).onClick.Invoke();
                Check(g.State == MatchState.Playing && !results.activeSelf, "Saved replay button starts a fresh round", passed);
                return passed.ToArray();
            }
            finally
            {
                copy.text = originalText; copy.color = originalColor;
                play.GetComponent<RectTransform>().anchoredPosition = originalPosition;
                g.enabled = enabledBefore;
                g.Restart(); g.TogglePause();
            }
        }

        static void Check(bool condition, string message, List<string> passed)
        {
            if (!condition) throw new Exception("FAIL: " + message);
            passed.Add("PASS: " + message);
        }
    }
}
