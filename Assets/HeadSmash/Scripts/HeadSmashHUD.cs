using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace HeadSmash
{
    public sealed class HeadSmashHUD : MonoBehaviour
    {
        static readonly Color Ink = new Color(.1f, .18f, .25f);
        static readonly Color Cream = new Color(.99f, .985f, .93f);
        static readonly Color Orange = new Color(1, .38f, .18f);
        static readonly Color Mint = new Color(.47f, .94f, .72f);
        HeadSmashGame game;
        RectTransform root, safe, modal, toast;
        SmashJoystick stick;
        TextMeshProUGUI score, level, healthLabel, timer, dashLabel, ranking, rankValue, toastLabel, hint, soundLabel;
        UnityEngine.UI.Image health, progress, cooldown, dashFace;
        bool dashRequested;
        float toastUntil;
        float nextBoardRefresh;
        float hintUntil;
        Sprite rounded, circle;
        Texture2D roundedTexture, circleTexture;
        readonly List<RectTransform> dots = new List<RectTransform>();
        readonly List<RectTransform> floating = new List<RectTransform>();
        Rect lastSafe;
        Vector2Int lastScreen;
        public Vector2 JoystickValue => stick == null ? Vector2.zero : stick.Value;

        public void Build(HeadSmashGame owner)
        {
            game = owner;
            rounded = Shape(false, out roundedTexture);
            circle = Shape(true, out circleTexture);
            var go = new GameObject("HEAD SMASH / HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            root = go.GetComponent<RectTransform>();
            safe = new GameObject("Safe area", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(root, false);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one; safe.sizeDelta = Vector2.zero;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("Input / EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem.transform.SetParent(transform, false);
                eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            BuildScore(); BuildBoard(); BuildControls(); BuildMinimap();
            var timePanel = Panel("Round clock", safe, new Vector2(.5f, 1), new Vector2(0, -28), new Vector2(174, 78), Ink);
            Label(timePanel, "ROUND  /  01", 12, new Vector2(0, -15), new Vector2(160, 20), new Color(.64f, .81f, .79f), TextAlignmentOptions.Center);
            timer = Label(timePanel, "03:00", 33, new Vector2(0, -47), new Vector2(150, 40), Cream, TextAlignmentOptions.Center);
            var pause = Button("Pause", safe, new Vector2(.5f, 1), new Vector2(116, -42), new Vector2(46, 46), "II", 20, Cream, game.TogglePause);
            pause.GetComponent<UnityEngine.UI.Image>().color = new Color(1, 1, 1, .88f);
            soundLabel = Button("Sound", safe, new Vector2(.5f, 1), new Vector2(-116, -42), new Vector2(46, 46), "SFX", 13, Cream, game.ToggleSound).GetComponentInChildren<TextMeshProUGUI>();
            var toastPanel = Panel("Notifications", safe, new Vector2(.5f, 1), new Vector2(0, -124), new Vector2(470, 54), Orange);
            toast = toastPanel;
            toastLabel = Label(toast, "", 23, new Vector2(0, -27), new Vector2(450, 44), Cream, TextAlignmentOptions.Center);
            toast.gameObject.SetActive(false);
            hint = Label(safe, "COLLECT XP  /  GROW YOUR HEAD  /  SMASH THE ARENA", 16, new Vector2(0, 31), new Vector2(740, 30), Ink, TextAlignmentOptions.Center, new Vector2(.5f, 0));
            hint.fontStyle = FontStyles.Bold;
            UpdateSafeArea();
        }

        void BuildScore()
        {
            var brand = Panel("Brand", safe, new Vector2(0, 1), new Vector2(28, -26), new Vector2(284, 67), Ink);
            Label(brand, "HEAD <color=#FF9A55>SMASH</color>", 33, new Vector2(0, -33), new Vector2(268, 52), Cream, TextAlignmentOptions.Center);
            var card = Panel("Your growth", safe, new Vector2(0, 1), new Vector2(28, -106), new Vector2(284, 172), Cream);
            Label(card, "YOUR HEAD", 14, new Vector2(23, -23), new Vector2(180, 25), new Color(.37f, .48f, .49f));
            score = Label(card, "0", 52, new Vector2(23, -65), new Vector2(180, 65), Ink);
            Label(card, "XP", 16, new Vector2(242, -69), new Vector2(35, 30), new Color(.37f, .48f, .49f));
            level = Label(card, "LV. 01", 16, new Vector2(23, -113), new Vector2(210, 27), Ink);
            var track = Panel("Level track", card, new Vector2(0, 1), new Vector2(23, -139), new Vector2(238, 10), new Color(.85f, .89f, .83f));
            progress = Fill(track, Orange);
            var hp = Panel("Health", safe, new Vector2(0, 1), new Vector2(28, -291), new Vector2(284, 42), Ink);
            var bar = Panel("Health track", hp, new Vector2(0, 1), new Vector2(12, -13), new Vector2(179, 15), new Color(.22f, .31f, .35f));
            health = Fill(bar, Mint);
            healthLabel = Label(hp, "100 HP", 14, new Vector2(206, -21), new Vector2(72, 25), Cream);
        }

        void BuildBoard()
        {
            var card = Panel("Live leaderboard", safe, new Vector2(1, 1), new Vector2(-28, -26), new Vector2(266, 310), Ink);
            Label(card, "TOP HEADS", 21, new Vector2(23, -30), new Vector2(220, 35), Cream);
            Label(card, $"LOCAL ARENA  /  {game.opponentCount + 1} PLAYERS", 11, new Vector2(23, -61), new Vector2(230, 20), new Color(.63f, .77f, .77f));
            ranking = Label(card, "", 18, new Vector2(23, -156), new Vector2(222, 155), Cream);
            ranking.lineSpacing = 14;
            var own = Panel("Own rank", card, new Vector2(1, 0), new Vector2(-13, 13), new Vector2(240, 47), new Color(.24f, .36f, .39f));
            rankValue = Label(own, "#13  YOU", 19, new Vector2(17, -23), new Vector2(210, 40), Mint);
        }

        void BuildMinimap()
        {
            var map = Panel("Arena radar", safe, new Vector2(1, 1), new Vector2(-28, -352), new Vector2(160, 160), new Color(.1f, .21f, .25f, .84f));
            Label(map, "ARENA", 10, new Vector2(0, -13), new Vector2(140, 20), new Color(.64f, .81f, .79f), TextAlignmentOptions.Center);
            for (int i = 0; i < game.opponentCount + 1; i++)
            {
                var dot = Panel("Radar dot", map, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * (i == 0 ? 10 : 6), i == 0 ? Cream : Orange, true);
                dot.pivot = Vector2.one * .5f;
                dots.Add(dot);
            }
        }

        void BuildControls()
        {
            var baseRect = Panel("Movement joystick", safe, new Vector2(0, 0), new Vector2(132, 135), new Vector2(174, 174), new Color(.1f, .23f, .27f, .17f), true);
            baseRect.pivot = Vector2.one * .5f;
            var inner = Panel("Inner ring", baseRect, new Vector2(.5f, .5f), Vector2.zero, new Vector2(144, 144), new Color(1, 1, 1, .25f), true);
            inner.pivot = Vector2.one * .5f;
            var knob = Panel("Stick", baseRect, new Vector2(.5f, .5f), Vector2.zero, new Vector2(77, 77), Cream, true);
            knob.pivot = Vector2.one * .5f;
            Label(knob, "+", 35, Vector2.zero, Vector2.one * 55, Ink, TextAlignmentOptions.Center, Vector2.one * .5f);
            stick = baseRect.gameObject.AddComponent<SmashJoystick>();
            stick.knob = knob;
            baseRect.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            Label(safe, "MOVE / WASD", 14, new Vector2(132, 32), new Vector2(220, 30), Ink, TextAlignmentOptions.Center, Vector2.zero);
            var dash = Panel("Headbutt", safe, new Vector2(1, 0), new Vector2(-128, 136), new Vector2(164, 164), Orange, true);
            dash.pivot = Vector2.one * .5f;
            dashFace = dash.GetComponent<UnityEngine.UI.Image>();
            dashFace.raycastTarget = true;
            var press = dash.gameObject.AddComponent<SmashPress>();
            press.pressed = () => { if (game.State == MatchState.Playing) dashRequested = true; };
            var head = Panel("Headbutt icon", dash, Vector2.one * .5f, new Vector2(0, 11), new Vector2(65, 57), Cream);
            head.pivot = Vector2.one * .5f;
            for (int i = -1; i <= 1; i += 2)
            {
                var eye = Panel("Eye", head, Vector2.one * .5f, new Vector2(i * 14, 0), new Vector2(7, 12), Ink, true);
                eye.pivot = Vector2.one * .5f;
            }
            Label(dash, "SMASH", 20, new Vector2(0, -42), new Vector2(138, 34), Cream, TextAlignmentOptions.Center, Vector2.one * .5f);
            cooldown = Panel("Cooldown radial", dash, Vector2.one * .5f, Vector2.zero, new Vector2(164, 164), new Color(.1f, .17f, .22f, .7f), true).GetComponent<UnityEngine.UI.Image>();
            cooldown.rectTransform.pivot = Vector2.one * .5f;
            cooldown.type = UnityEngine.UI.Image.Type.Filled;
            cooldown.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            cooldown.fillOrigin = 2;
            cooldown.fillClockwise = false;
            dashLabel = Label(dash, "", 36, Vector2.zero, new Vector2(130, 50), Cream, TextAlignmentOptions.Center, Vector2.one * .5f);
            Label(safe, "HEADBUTT / SPACE", 14, new Vector2(-128, 32), new Vector2(230, 30), Ink, TextAlignmentOptions.Center, new Vector2(1, 0));
        }

        public bool ConsumeDash() { bool value = dashRequested; dashRequested = false; return value; }

        public void Refresh()
        {
            if (game.Player == null) return;
            UpdateSafeArea();
            var player = game.Player;
            score.text = player.Score.ToString("N0");
            level.text = $"LV. {player.Level:00}   <color=#67827E>{80 - player.Score % 80} XP TO GROW</color>";
            progress.fillAmount = player.Score % 80 / 80f;
            health.fillAmount = player.Health / 100f;
            health.color = player.Health < 35 ? Orange : Mint;
            healthLabel.text = $"{Mathf.CeilToInt(player.Health)} HP";
            int seconds = Mathf.CeilToInt(game.Remaining);
            timer.text = $"{seconds / 60:00}:{seconds % 60:00}";
            timer.color = seconds < 30 ? new Color(1, .57f, .35f) : Cream;
            cooldown.fillAmount = player.CooldownFraction;
            dashLabel.text = player.CooldownFraction > 0 ? (player.CooldownFraction * game.dashCooldown).ToString("0.0") : "";
            soundLabel.text = game.SoundEnabled ? "SFX" : "OFF";
            toast.gameObject.SetActive(Time.unscaledTime < toastUntil);
            if (Time.unscaledTime > nextBoardRefresh)
            {
                nextBoardRefresh = Time.unscaledTime + .25f;
                var board = game.Ranking();
                string rows = "";
                for (int i = 0; i < Mathf.Min(5, board.Count); i++)
                {
                    var a = board[i];
                    string color = a.IsPlayer ? "8DF1B9" : "FBFAED";
                    rows += $"<color=#{color}><mspace=16>{i + 1:00}</mspace>  {a.ActorName}<pos=170>{a.Score}</color>\n";
                }
                ranking.text = rows.TrimEnd();
                rankValue.text = $"#{game.PlayerRank:00}   YOU <pos=166>{player.Score}";
            }
            for (int i = 0; i < dots.Count && i < game.Actors.Count; i++)
            {
                var a = game.Actors[i];
                dots[i].gameObject.SetActive(a.Alive);
                dots[i].anchoredPosition = new Vector2(a.transform.position.x, a.transform.position.z) / game.arenaRadius * 61;
            }
            if (Time.unscaledTime > hintUntil)
                hint.text = player.Invulnerable > 0 ? "SPAWN SHIELD  /  ATTACKING ENDS YOUR SHIELD" : "COLLECT XP  /  ATTACK FROM BEHIND  /  BECOME #1";
        }

        public void BoundaryHint()
        {
            hint.text = "ARENA EDGE  /  STEER BACK TOWARD THE CENTER";
            hintUntil = Time.unscaledTime + 1;
        }

        public void Toast(string text, float seconds)
        {
            toastLabel.text = text;
            toastUntil = Time.unscaledTime + seconds;
        }

        RectTransform Modal(string eyebrow, string title, string subtitle)
        {
            HideModal();
            stick.ResetStick(); dashRequested = false;
            modal = Panel("Modal blocker", safe, Vector2.one * .5f, Vector2.zero, new Vector2(5000, 5000), new Color(.05f, .16f, .21f, .65f));
            modal.pivot = Vector2.one * .5f;
            modal.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var card = Panel("Match card", modal, Vector2.one * .5f, Vector2.zero, new Vector2(670, 600), Cream);
            card.pivot = Vector2.one * .5f;
            Label(card, eyebrow, 15, new Vector2(0, -44), new Vector2(600, 30), new Color(.32f, .48f, .47f), TextAlignmentOptions.Center);
            var heading = Label(card, title, 68, new Vector2(0, -138), new Vector2(620, 150), Orange, TextAlignmentOptions.Center);
            heading.fontStyle = FontStyles.Bold;
            heading.enableAutoSizing = true;
            heading.fontSizeMin = 38;
            heading.fontSizeMax = 68;
            Label(card, subtitle, 21, new Vector2(0, -240), new Vector2(575, 66), Ink, TextAlignmentOptions.Center);
            return card;
        }

        public void ShowIntro()
        {
            var card = Modal("A LITTLE BODY. A VERY BIG AMBITION.", "HEAD <color=#192D3F>SMASH</color>", "Smash heads. Eat the pieces.\nGrow into the biggest head in the arena.");
            var a = Panel("Move instruction", card, new Vector2(.5f, 1), new Vector2(-151, -300), new Vector2(278, 112), new Color(.88f, .93f, .85f));
            a.pivot = new Vector2(.5f, 1);
            Label(a, "01  STEER", 20, new Vector2(0, -30), new Vector2(260, 38), Ink, TextAlignmentOptions.Center);
            Label(a, "LEFT STICK / WASD\nAlways moving. Keep turning!", 15, new Vector2(0, -77), new Vector2(260, 55), Ink, TextAlignmentOptions.Center);
            var b = Panel("Smash instruction", card, new Vector2(.5f, 1), new Vector2(151, -300), new Vector2(278, 112), new Color(1, .88f, .72f));
            b.pivot = new Vector2(.5f, 1);
            Label(b, "02  HEADBUTT", 20, new Vector2(0, -30), new Vector2(260, 38), Ink, TextAlignmentOptions.Center);
            Label(b, "RIGHT BUTTON / SPACE\nRear hits deal extra damage.", 15, new Vector2(0, -77), new Vector2(260, 55), Ink, TextAlignmentOptions.Center);
            Button("Play", card, new Vector2(.5f, 1), new Vector2(0, -471), new Vector2(574, 70), "LET'S SMASH!", 28, Orange, game.Begin);
            Label(card, "3 MINUTES  /  12 AI RIVALS  /  LOCAL PROTOTYPE", 13, new Vector2(0, -552), new Vector2(610, 25), new Color(.36f, .48f, .47f), TextAlignmentOptions.Center);
        }

        public void ShowPause()
        {
            var card = Modal("TAKE A BREATHER", "PAUSED", "Your next big idea can wait a moment.");
            Button("Resume", card, new Vector2(.5f, 1), new Vector2(0, -353), new Vector2(530, 70), "KEEP SMASHING", 25, Orange, game.TogglePause);
            Button("Restart", card, new Vector2(.5f, 1), new Vector2(0, -447), new Vector2(530, 62), "NEW ROUND", 21, new Color(.83f, .9f, .83f), game.Restart);
            Label(card, "ESC TO RESUME", 14, new Vector2(0, -546), new Vector2(570, 25), Ink, TextAlignmentOptions.Center);
        }

        public void ShowKnockout(string attacker)
        {
            var card = Modal($"SMASHED BY {attacker}", "OH, SNAP!", $"{game.Player.Score} XP  /  {game.Knockouts} SMASHES  /  RANK #{game.PlayerRank}\nThe biggest heads know how to bounce back.");
            Button("Respawn", card, new Vector2(.5f, 1), new Vector2(0, -350), new Vector2(530, 70), "BOUNCE BACK", 26, Orange, game.RespawnPlayer);
            Label(card, "Keep 55% of your XP. Return with a 3-second shield.", 16, new Vector2(0, -418), new Vector2(580, 44), Ink, TextAlignmentOptions.Center);
            Button("Restart", card, new Vector2(.5f, 1), new Vector2(0, -495), new Vector2(530, 56), "START A NEW ROUND", 19, new Color(.83f, .9f, .83f), game.Restart);
        }

        public void ShowResults()
        {
            var card = Modal("THE ARENA HAS SPOKEN", game.PlayerRank == 1 ? "BIG HEAD ENERGY!" : "NICE HEAD!", $"FINAL RANK  #{game.PlayerRank}   /   {game.Player.Score} XP\n{game.Knockouts} heads smashed. Personal best: {game.BestScore} XP.");
            Button("Replay", card, new Vector2(.5f, 1), new Vector2(0, -362), new Vector2(530, 76), "ONE MORE ROUND", 27, Orange, game.Restart);
            Label(card, "TIP: Eat scattered XP to heal. Aim for the back!", 17, new Vector2(0, -465), new Vector2(585, 60), Ink, TextAlignmentOptions.Center);
        }

        public void HideModal()
        {
            if (modal != null) { modal.gameObject.SetActive(false); Destroy(modal.gameObject); modal = null; }
            if (stick != null) stick.ResetStick();
            dashRequested = false;
        }

        public void FloatText(string text, Vector3 worldPosition, Color color)
        {
            if (floating.Count > 14) return;
            Vector3 screen = Camera.main.WorldToScreenPoint(worldPosition + Vector3.up * 2);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out Vector2 local);
            var label = Label(safe, text, 32, local, new Vector2(360, 60), color, TextAlignmentOptions.Center, Vector2.one * .5f);
            label.outlineWidth = .2f;
            label.outlineColor = Ink;
            floating.Add(label.rectTransform);
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
            if (label != null) { floating.Remove(label.rectTransform); Destroy(label.gameObject); }
        }

        void UpdateSafeArea()
        {
            if (safe == null || lastSafe == Screen.safeArea && lastScreen == new Vector2Int(Screen.width, Screen.height)) return;
            lastSafe = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            safe.anchorMin = lastSafe.position / new Vector2(Screen.width, Screen.height);
            safe.anchorMax = (lastSafe.position + lastSafe.size) / new Vector2(Screen.width, Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }

        RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color, bool round = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = round ? circle : rounded;
            image.type = round ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        TextMeshProUGUI Label(Transform parent, string text, float size, Vector2 position, Vector2 dimensions, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, Vector2? anchor = null)
        {
            bool leftAligned = alignment == TextAlignmentOptions.MidlineLeft || alignment == TextAlignmentOptions.Left;
            Vector2 a = anchor ?? new Vector2(leftAligned ? 0 : .5f, 1);
            var go = new GameObject(text.Length > 20 ? "Label" : text, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = a;
            rect.pivot = new Vector2(.5f, .5f);
            // Text uses its visual left edge for left alignment, which makes card composition predictable.
            if (leftAligned) rect.pivot = new Vector2(0, .5f);
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = position;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = game.font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        RectTransform Button(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string text, float fontSize, Color color, Action onClick)
        {
            var rect = Panel(name, parent, anchor, position, size, color);
            rect.pivot = Vector2.one * .5f;
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
            var colors = button.colors;
            colors.highlightedColor = new Color(.94f, .97f, .89f);
            colors.pressedColor = new Color(.75f, .83f, .74f);
            button.colors = colors;
            Label(rect, text, fontSize, Vector2.zero, size - new Vector2(18, 8), color == Orange ? Cream : Ink, TextAlignmentOptions.Center, Vector2.one * .5f);
            return rect;
        }

        UnityEngine.UI.Image Fill(RectTransform parent, Color color)
        {
            var r = Panel("Fill", parent, Vector2.zero, Vector2.zero, Vector2.zero, color);
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            var image = r.GetComponent<UnityEngine.UI.Image>();
            image.type = UnityEngine.UI.Image.Type.Filled;
            image.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            return image;
        }

        static Sprite Shape(bool isCircle, out Texture2D texture)
        {
            const int n = 64;
            texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
            texture.name = isCircle ? "HUD circle" : "HUD roundrect";
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float distance;
                if (isCircle) distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(32, 32)) - 31;
                else
                {
                    Vector2 d = new Vector2(Mathf.Abs(x - 31.5f) - 20, Mathf.Abs(y - 31.5f) - 20);
                    distance = new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0) - 11;
                }
                pixels[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(.5f - distance));
            }
            texture.SetPixels(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, n, n), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, isCircle ? Vector4.zero : Vector4.one * 16);
        }

        void OnDestroy()
        {
            if (rounded != null) Destroy(rounded);
            if (circle != null) Destroy(circle);
            if (roundedTexture != null) Destroy(roundedTexture);
            if (circleTexture != null) Destroy(circleTexture);
        }
    }
}
