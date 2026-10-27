using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HeadSmash
{
    /// <summary>Updates the authored Canvas. Static copy, layout and button events live in the prefab.</summary>
    public sealed class HeadSmashHUD : MonoBehaviour
    {
        [Header("Canvas and controls")]
        [SerializeField] RectTransform safe;
        [SerializeField] SmashJoystick stick;
        [SerializeField] TextMeshProUGUI soundLabel, dashLabel;
        [SerializeField] UnityEngine.UI.Image cooldown;
        [Header("Live stats — use the format fields below to change dynamic copy")]
        [SerializeField] TextMeshProUGUI score, level, healthLabel, timer, ranking, rankValue;
        [SerializeField] UnityEngine.UI.Image health, progress;
        [Header("Authored screens")]
        [SerializeField] GameObject introScreen, pauseScreen, knockoutScreen, resultsScreen;
        [SerializeField] TextMeshProUGUI knockoutAttacker, knockoutSummary, resultsTitle, resultsSummary;
        [Header("Notifications and feedback prefab")]
        [SerializeField] GameObject toast;
        [SerializeField] TextMeshProUGUI toastLabel, hint;
        [SerializeField] RectTransform floatingLayer;
        [SerializeField] TextMeshProUGUI floatingTextPrefab;
        [Header("Radar — repeated markers use the editable template")]
        [SerializeField] RectTransform radarArea, playerDot, opponentDotTemplate;
        [Header("Dynamic text formats — numbered placeholders are game values")]
        [SerializeField] string levelFormat = "LV. {0:00}   <color=#67827E>{1} XP TO GROW</color>";
        [SerializeField] string healthFormat = "{0} HP";
        [SerializeField] string timerFormat = "{0:00}:{1:00}";
        [SerializeField] string rankFormat = "#{0:00}   YOU <pos=166>{1}";
        [SerializeField] string attackerFormat = "SMASHED BY {0}";
        [TextArea, SerializeField] string knockoutFormat = "{0} XP  /  {1} SMASHES  /  RANK #{2}\nThe biggest heads know how to bounce back.";
        [TextArea, SerializeField] string resultsFormat = "FINAL RANK  #{0}   /   {1} XP\n{2} heads smashed. Personal best: {3} XP.";
        [SerializeField] string victoryTitle = "BIG HEAD ENERGY!";
        [SerializeField] string soundOffText = "OFF";
        [SerializeField] string shieldHint = "SPAWN SHIELD  /  ATTACKING ENDS YOUR SHIELD";
        [SerializeField] string boundaryHint = "ARENA EDGE  /  STEER BACK TOWARD THE CENTER";
        [Header("Warning colors — normal colors come from the Image / TMP components")]
        [SerializeField] Color lowHealthColor = new Color(1, .38f, .18f);
        [SerializeField] Color lowTimeColor = new Color(1, .57f, .35f);
        [SerializeField] Color ownRankingColor = new Color(.55f, .95f, .73f);

        HeadSmashGame game;
        bool dashRequested;
        float toastUntil, nextBoardRefresh, hintUntil;
        Color normalHealthColor, normalTimeColor;
        string normalHint, soundOnText, normalResultsTitle;
        readonly List<RectTransform> dots = new List<RectTransform>();
        readonly List<TextMeshProUGUI> floating = new List<TextMeshProUGUI>();
        Rect lastSafe;
        Vector2Int lastScreen;
        public Vector2 JoystickValue => stick == null ? Vector2.zero : stick.Value;

        public void Initialize(HeadSmashGame owner)
        {
            game = owner;
            normalHealthColor = health.color; normalTimeColor = timer.color;
            normalHint = hint.text; soundOnText = soundLabel.text; normalResultsTitle = resultsTitle.text;
            toast.SetActive(false);
            opponentDotTemplate.gameObject.SetActive(false);
            dots.Add(playerDot);
            for (int i = 0; i < game.opponentCount; i++)
            {
                var dot = Instantiate(opponentDotTemplate, radarArea);
                dot.name = $"OpponentMarker_{i + 1:00}";
                dot.gameObject.SetActive(true);
                dots.Add(dot);
            }
            UpdateSafeArea();
        }

        // Persistent Button.onClick targets, visible in the Inspector.
        public void OnPlay() { if (game != null && game.State == MatchState.Ready) game.Begin(); }
        public void OnPause() { if (game != null) game.TogglePause(); }
        public void OnRestart() { if (game != null) game.Restart(); }
        public void OnRespawn() { if (game != null && game.State == MatchState.KnockedOut) game.RespawnPlayer(); }
        public void OnSound() { if (game != null) game.ToggleSound(); }
        public void OnDash() { if (game != null && game.State == MatchState.Playing) dashRequested = true; }
        public bool ConsumeDash() { bool value = dashRequested; dashRequested = false; return value; }

        public void Refresh()
        {
            if (game == null || game.Player == null) return;
            UpdateSafeArea();
            var player = game.Player;
            score.text = player.Score.ToString("N0");
            level.text = string.Format(levelFormat, player.Level, 80 - player.Score % 80);
            progress.fillAmount = player.Score % 80 / 80f;
            health.fillAmount = player.Health / 100f;
            health.color = player.Health < 35 ? lowHealthColor : normalHealthColor;
            healthLabel.text = string.Format(healthFormat, Mathf.CeilToInt(player.Health));
            int seconds = Mathf.CeilToInt(game.Remaining);
            timer.text = string.Format(timerFormat, seconds / 60, seconds % 60);
            timer.color = seconds < 30 ? lowTimeColor : normalTimeColor;
            cooldown.fillAmount = player.CooldownFraction;
            dashLabel.text = player.CooldownFraction > 0 ? (player.CooldownFraction * game.dashCooldown).ToString("0.0") : "";
            soundLabel.text = game.SoundEnabled ? soundOnText : soundOffText;
            toast.SetActive(Time.unscaledTime < toastUntil);
            if (Time.unscaledTime >= nextBoardRefresh)
            {
                nextBoardRefresh = Time.unscaledTime + .25f;
                var board = game.Ranking();
                string rows = "";
                for (int i = 0; i < Mathf.Min(5, board.Count); i++)
                {
                    var a = board[i];
                    string color = ColorUtility.ToHtmlStringRGB(a.IsPlayer ? ownRankingColor : ranking.color);
                    rows += $"<color=#{color}><mspace=16>{i + 1:00}</mspace>  {a.ActorName}<pos=170>{a.Score}</color>\n";
                }
                ranking.text = rows.TrimEnd();
                rankValue.text = string.Format(rankFormat, game.PlayerRank, player.Score);
            }
            for (int i = 0; i < dots.Count; i++)
            {
                bool visible = i < game.Actors.Count && game.Actors[i].Alive;
                dots[i].gameObject.SetActive(visible);
                if (!visible) continue;
                Vector3 p = game.Actors[i].transform.position;
                dots[i].anchoredPosition = new Vector2(p.x / game.arenaRadius * radarArea.rect.width * .5f,
                    p.z / game.arenaRadius * radarArea.rect.height * .5f);
            }
            if (Time.unscaledTime > hintUntil) hint.text = player.Invulnerable > 0 ? shieldHint : normalHint;
        }

        public void BoundaryHint() { hint.text = boundaryHint; hintUntil = Time.unscaledTime + 1; }
        public void Toast(string text, float seconds) { toastLabel.text = text; toastUntil = Time.unscaledTime + seconds; }
        public void ShowIntro() => ShowScreen(introScreen);
        public void ShowPause() => ShowScreen(pauseScreen);
        public void ShowKnockout(string attacker)
        {
            knockoutAttacker.text = string.Format(attackerFormat, attacker);
            knockoutSummary.text = string.Format(knockoutFormat, game.Player.Score, game.Knockouts, game.PlayerRank);
            ShowScreen(knockoutScreen);
        }
        public void ShowResults()
        {
            resultsTitle.text = game.PlayerRank == 1 ? victoryTitle : normalResultsTitle;
            resultsSummary.text = string.Format(resultsFormat, game.PlayerRank, game.Player.Score, game.Knockouts, game.BestScore);
            ShowScreen(resultsScreen);
        }
        public void HideModal() => ShowScreen(null);
        void ShowScreen(GameObject selected)
        {
            foreach (var screen in new[] { introScreen, pauseScreen, knockoutScreen, resultsScreen })
                if (screen != null) screen.SetActive(screen == selected);
            if (stick != null) stick.ResetStick();
            dashRequested = false;
            nextBoardRefresh = 0;
        }

        public void FloatText(string text, Vector3 worldPosition, Color color)
        {
            if (floatingTextPrefab == null || floating.Count >= 15 || Camera.main == null) return;
            Vector3 screen = Camera.main.WorldToScreenPoint(worldPosition + Vector3.up * 2);
            if (screen.z < 0) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(floatingLayer, screen, null, out Vector2 local);
            var label = Instantiate(floatingTextPrefab, floatingLayer);
            label.name = "HitFeedback";
            label.rectTransform.anchoredPosition = local;
            label.text = text; label.color = color;
            label.gameObject.SetActive(true);
            floating.Add(label);
            StartCoroutine(Float(label));
        }
        System.Collections.IEnumerator Float(TextMeshProUGUI label)
        {
            float t = 0;
            while (t < .9f && label != null)
            {
                if (game.State != MatchState.Paused)
                {
                    t += Time.deltaTime;
                    label.rectTransform.anchoredPosition += Vector2.up * (Time.deltaTime * 65);
                    label.alpha = Mathf.Clamp01((.9f - t) * 3);
                }
                yield return null;
            }
            if (label != null) { floating.Remove(label); Destroy(label.gameObject); }
        }

        void UpdateSafeArea()
        {
            if (safe == null || Screen.width <= 0 || Screen.height <= 0 || lastSafe == Screen.safeArea && lastScreen == new Vector2Int(Screen.width, Screen.height)) return;
            lastSafe = Screen.safeArea; lastScreen = new Vector2Int(Screen.width, Screen.height);
            safe.anchorMin = lastSafe.position / new Vector2(Screen.width, Screen.height);
            safe.anchorMax = (lastSafe.position + lastSafe.size) / new Vector2(Screen.width, Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }
    }
}

