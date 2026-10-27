using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeadSmash
{
    public enum MatchState { Ready, Playing, Paused, KnockedOut, Finished }

    /// <summary>The local arena simulation. All combat uses swept planar circles, independent of frame rate.</summary>
    public sealed class HeadSmashGame : MonoBehaviour
    {
        [Header("Arena")]
        [Min(8)] public float arenaRadius = 29f;
        [Range(2, 24)] public int opponentCount = 12;
        [Min(30)] public float roundDuration = 180f;
        [Range(40, 400)] public int ambientFood = 210;
        [Header("Combat")]
        public float moveSpeed = 4.6f;
        public float dashSpeed = 19f;
        public float dashDuration = .3f;
        public float dashCooldown = 1.65f;
        public float baseDamage = 64f;
        [Header("Presentation")]
        public TMP_FontAsset font;
        public Material[] palette;
        public HeadSmashHUD hud;

        public readonly List<SmashActor> Actors = new List<SmashActor>();
        public readonly List<Food> Foods = new List<Food>();
        public SmashActor Player { get; private set; }
        public MatchState State { get; private set; } = MatchState.Ready;
        public float Remaining { get; private set; }
        public int Knockouts { get; private set; }
        public int BestScore { get; private set; }
        public int FoodCollected { get; private set; }
        public int HitsLanded { get; private set; }
        public int PlayerRank => Ranking().FindIndex(a => a == Player) + 1;
        public int PeakScore { get; private set; }
        public bool SoundEnabled { get; private set; } = true;

        static readonly string[] Names = { "MANGO", "MOCHI", "BONK", "PUDDING", "KIWI", "BUBBLE", "TOFU", "PEACH", "WOBBLE", "DUMPLING", "BEAN", "PICKLE", "COCO", "NUGGET", "PIP", "MINT", "POPPY", "COOKIE", "PLUM", "GUMMY", "LIME", "JELLY", "BAM", "YUZU" };
        float refillTimer;
        float comboUntil;
        int combo;
        Transform dynamicRoot;
        Camera cam;
        float shake;
        Vector3 cameraFocus;
        AudioSource audioSource;
        AudioClip collectClip, dashClip, hitClip, breakClip;

        public sealed class Food
        {
            public Transform view;
            public Vector3 position;
            public int value;
            public float phase;
        }

        void Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            cam = Camera.main;
            dynamicRoot = new GameObject("Live arena").transform;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0;
            collectClip = Tone("Pop", 700, 1100, .075f, .11f);
            dashClip = Tone("Whoosh", 210, 90, .16f, .12f);
            hitClip = Tone("Bonk", 160, 60, .16f, .22f);
            breakClip = Tone("Smash", 330, 50, .32f, .22f);
            if (hud == null) hud = gameObject.AddComponent<HeadSmashHUD>();
            hud.Build(this);
            BestScore = PlayerPrefs.GetInt("HeadSmash.Best", 0);
            PrepareRound();
            State = MatchState.Ready;
            hud.ShowIntro();
        }

        public void PrepareRound()
        {
            foreach (var a in Actors) if (a != null) Destroy(a.gameObject);
            foreach (var f in Foods) if (f.view != null) Destroy(f.view.gameObject);
            Actors.Clear(); Foods.Clear();
            Remaining = roundDuration;
            Knockouts = FoodCollected = HitsLanded = PeakScore = 0;
            combo = 0;
            Player = SpawnActor("YOU", 0, Vector3.zero, 0, true);
            Player.Invulnerable = 3f;
            for (int i = 0; i < opponentCount; i++)
            {
                Vector3 p = i == 0 ? new Vector3(1.5f, 0, 8f) : SafeSpawn(9f);
                SpawnActor(Names[i % Names.Length], i % 6 + 1, p, i == 0 ? 0 : Random.Range(15, 230), false);
            }
            for (int i = 0; i < ambientFood; i++) SpawnFood(RandomPoint(), Random.value < .16f ? 8 : 3);
            // A short breadcrumb trail makes the first growth immediately discoverable.
            for (int i = 0; i < 8; i++) SpawnFood(new Vector3(Mathf.Sin(i * .8f) * 1.3f, 0, 2 + i * 1.1f), 5);
            cameraFocus = Player.transform.position;
            UpdateCamera(1, true);
        }

        SmashActor SpawnActor(string actorName, int color, Vector3 position, int score, bool player)
        {
            var go = new GameObject(actorName);
            go.transform.SetParent(dynamicRoot);
            go.transform.position = position;
            var a = go.AddComponent<SmashActor>();
            a.Initialize(this, actorName, color, score, player);
            Actors.Add(a);
            return a;
        }

        public void Begin()
        {
            State = MatchState.Playing;
            hud.HideModal();
            hud.Toast("COLLECT. GROW. HEADBUTT!", 2.8f);
        }

        public void Restart()
        {
            PrepareRound();
            Begin();
        }

        public void RespawnPlayer()
        {
            Player.Respawn(SafeSpawn(8), Mathf.FloorToInt(Player.Score * .55f));
            Player.Invulnerable = 3f;
            State = MatchState.Playing;
            hud.HideModal();
            hud.Toast("BACK IN!  3s SHIELD", 2.2f);
        }

        public void TogglePause()
        {
            if (State == MatchState.Playing) { State = MatchState.Paused; hud.ShowPause(); }
            else if (State == MatchState.Paused) { State = MatchState.Playing; hud.HideModal(); }
        }

        public void ToggleSound() { SoundEnabled = !SoundEnabled; }

        void Update()
        {
            var keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.escapeKey.wasPressedThisFrame) TogglePause();
                if (keys.enterKey.wasPressedThisFrame)
                {
                    if (State == MatchState.Ready) Begin();
                    else if (State == MatchState.KnockedOut) RespawnPlayer();
                    else if (State == MatchState.Finished) Restart();
                }
            }
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (State == MatchState.Playing)
            {
                Remaining = Mathf.Max(0, Remaining - dt);
                foreach (var actor in Actors) actor.Tick(dt);
                ResolveContacts();
                if (State == MatchState.Playing)
                {
                    UpdateFood(dt);
                    refillTimer -= dt;
                    if (refillTimer <= 0) { refillTimer = .4f; if (Foods.Count < ambientFood) SpawnFood(RandomPoint(), 3); }
                }
                PeakScore = Mathf.Max(PeakScore, Player.Score);
                if (Remaining <= 0 && State == MatchState.Playing) FinishRound();
            }
            else if (State == MatchState.Ready)
            {
                foreach (var actor in Actors) actor.Animate(Time.time, dt);
            }
            if (State != MatchState.Paused) UpdateCamera(dt, false);
            hud.Refresh();
        }

        void FinishRound()
        {
            SaveBest();
            State = MatchState.Finished;
            hud.ShowResults();
        }

        void SaveBest()
        {
            PeakScore = Mathf.Max(PeakScore, Player.Score);
            if (PeakScore <= BestScore) return;
            BestScore = PeakScore;
            PlayerPrefs.SetInt("HeadSmash.Best", BestScore);
            PlayerPrefs.Save();
        }

        void ResolveContacts()
        {
            for (int i = 0; i < Actors.Count; i++)
            for (int j = i + 1; j < Actors.Count; j++)
            {
                if (State != MatchState.Playing) return;
                var a = Actors[i]; var b = Actors[j];
                if (!a.Alive || !b.Alive) continue;
                float radius = a.Radius + b.Radius;
                // Relative swept motion avoids tunnelling even during two opposing dashes.
                Vector3 start = a.PreviousPosition - b.PreviousPosition;
                Vector3 end = a.transform.position - b.transform.position;
                Vector3 motion = end - start;
                float t = motion.sqrMagnitude > .0001f ? Mathf.Clamp01(-Vector3.Dot(start, motion) / motion.sqrMagnitude) : 0;
                if ((start + motion * t).sqrMagnitude >= radius * radius) continue;
                bool adash = a.AttackingThisStep; bool bdash = b.AttackingThisStep;
                if (adash) TryHit(a, b);
                if (bdash && b.Alive && a.Alive) TryHit(b, a);
                Vector3 separation = a.transform.position - b.transform.position;
                float distance = separation.magnitude;
                if (distance < radius && a.Alive && b.Alive)
                {
                    Vector3 normal = distance > .01f ? separation / distance : Vector3.right;
                    float overlap = (radius - distance) * .5f;
                    a.transform.position = ClampToArena(a.transform.position + normal * overlap, a.Radius);
                    b.transform.position = ClampToArena(b.transform.position - normal * overlap, b.Radius);
                }
            }
        }

        void TryHit(SmashActor attacker, SmashActor victim)
        {
            if (victim.Invulnerable > 0 || victim.HitGrace > 0 || !attacker.MarkHit(victim)) return;
            bool rear = Vector3.Dot(attacker.Forward, victim.Forward) > .45f;
            float ratio = Mathf.Clamp(attacker.HeadScale / victim.HeadScale, .72f, 1.5f);
            float damage = baseDamage * ratio * (rear ? 1.35f : 1f);
            victim.Health = Mathf.Max(0, victim.Health - damage);
            victim.HitGrace = .55f;
            victim.Knockback += attacker.Forward * 10f;
            victim.Pulse = .3f;
            attacker.Pulse = .17f;
            Burst(victim.transform.position + Vector3.up * 1.5f, palette[victim.ColorIndex].color, 12);
            if (attacker.IsPlayer)
            {
                HitsLanded++;
                shake = .2f;
                hud.FloatText(rear ? "BACK HIT!" : "BONK!", victim.transform.position, Color.white);
                Sound(hitClip);
            }
            else if (victim.IsPlayer) { shake = .25f; Sound(hitClip); }
            if (victim.Health <= 0) Shatter(victim, attacker);
        }

        public void Shatter(SmashActor victim, SmashActor attacker)
        {
            if (!victim.Alive) return;
            Vector3 p = victim.transform.position;
            victim.SetDead();
            int reward = Mathf.Clamp(65 + Mathf.RoundToInt(victim.Score * .65f), 65, 430);
            int count = Mathf.Min(28, reward / 5);
            for (int i = 0; i < count; i++)
            {
                Vector2 scatter = Random.insideUnitCircle * 3.2f;
                SpawnFood(ClampToArena(p + new Vector3(scatter.x, 0, scatter.y), 1), reward / count);
            }
            Burst(p + Vector3.up * 1.5f, palette[victim.ColorIndex].color, 24);
            if (attacker != null) attacker.AddScore(15);
            if (attacker != null && attacker.IsPlayer)
            {
                Knockouts++;
                combo = Time.time < comboUntil ? combo + 1 : 1;
                comboUntil = Time.time + 7;
                hud.Toast(combo > 1 ? $"{combo}x SMASH STREAK!" : $"SMASH!  {victim.ActorName}", 2f);
                hud.FloatText("SMASH!", p, new Color(1, .75f, .15f));
                shake = .4f;
                Sound(breakClip);
            }
            if (victim.IsPlayer)
            {
                SaveBest();
                State = MatchState.KnockedOut;
                hud.ShowKnockout(attacker == null ? "THE ARENA" : attacker.ActorName);
                Sound(breakClip);
            }
        }

        public void SpawnFood(Vector3 position, int value)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = value > 8 ? "Mega XP" : "XP";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(dynamicRoot);
            position.y = .33f;
            go.transform.position = position;
            float size = value > 8 ? .44f : .23f + value * .012f;
            go.transform.localScale = Vector3.one * size;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = palette[value > 8 ? 8 : Random.Range(9, 12)];
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Foods.Add(new Food { view = go.transform, position = position, value = value, phase = Random.value * 6.28f });
        }

        void UpdateFood(float dt)
        {
            for (int i = Foods.Count - 1; i >= 0; i--)
            {
                var f = Foods[i];
                SmashActor collector = null;
                float closest = float.MaxValue;
                foreach (var a in Actors)
                {
                    if (!a.Alive) continue;
                    float distance = (a.transform.position - f.position).sqrMagnitude;
                    float reach = a.Radius + .65f;
                    if (distance < reach * reach && distance < closest) { collector = a; closest = distance; }
                }
                if (collector != null)
                {
                    Vector3 target = collector.transform.position + Vector3.up * .55f;
                    f.position = Vector3.MoveTowards(f.position, target, dt * 15f);
                    if ((f.position - target).sqrMagnitude < .55f)
                    {
                        collector.AddScore(f.value);
                        if (collector.IsPlayer) { FoodCollected++; Sound(collectClip, .65f); }
                        Destroy(f.view.gameObject);
                        Foods.RemoveAt(i);
                        continue;
                    }
                }
                f.view.position = f.position + Vector3.up * (Mathf.Sin(Time.time * 2.8f + f.phase) * .08f);
            }
        }

        public List<SmashActor> Ranking() => Actors.OrderByDescending(a => a.Score).ToList();
        public Vector3 RandomPoint() => new Vector3(Random.Range(-arenaRadius + 2, arenaRadius - 2), 0, Random.Range(-arenaRadius + 2, arenaRadius - 2));
        public Vector3 SafeSpawn(float distance)
        {
            Vector3 p = RandomPoint();
            for (int i = 0; i < 60; i++)
            {
                p = RandomPoint();
                if (Actors.All(a => !a.Alive || (a.transform.position - p).sqrMagnitude > distance * distance)) break;
            }
            return p;
        }
        public Vector3 ClampToArena(Vector3 p, float radius)
        {
            p.x = Mathf.Clamp(p.x, -arenaRadius + radius, arenaRadius - radius);
            p.z = Mathf.Clamp(p.z, -arenaRadius + radius, arenaRadius - radius);
            p.y = 0;
            return p;
        }

        public void OnDash(SmashActor a)
        {
            if (a.IsPlayer) Sound(dashClip);
            SmashFx.Ring(a.transform.position + Vector3.up * .06f, palette[a.ColorIndex], a.Radius * 1.5f);
        }

        void UpdateCamera(float dt, bool snap)
        {
            if (cam == null || Player == null) return;
            Vector3 target = Player.transform.position + Player.Forward * 1.2f;
            cameraFocus = snap ? target : Vector3.Lerp(cameraFocus, target, 1 - Mathf.Exp(-dt * 6));
            float size = 11.4f + (Player.HeadScale - 1) * 1.9f;
            cam.orthographicSize = snap ? size : Mathf.Lerp(cam.orthographicSize, size, dt * 3);
            Vector3 offset = new Vector3(0, 27, -19);
            cam.transform.position = cameraFocus + offset;
            cam.transform.rotation = Quaternion.LookRotation(-offset);
            shake = Mathf.Max(0, shake - dt);
            if (shake > 0) cam.transform.position += Random.insideUnitSphere * shake * .65f;
        }

        public void Burst(Vector3 p, Color color, int count) => SmashFx.Burst(p, color, count, palette[12]);

        public void Sound(AudioClip clip, float volume = 1)
        {
            if (!SoundEnabled || audioSource == null) return;
            // Keep a dense pickup trail from stacking dozens of simultaneous notes.
            if (clip == collectClip && audioSource.isPlaying) return;
            audioSource.pitch = Random.Range(.94f, 1.06f);
            audioSource.PlayOneShot(clip, volume);
        }
        static AudioClip Tone(string title, float from, float to, float seconds, float volume)
        {
            int n = Mathf.RoundToInt(22050 * seconds);
            float[] data = new float[n];
            float phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase += Mathf.Lerp(from, to, t) * Mathf.PI * 2 / 22050;
                data[i] = (Mathf.Sin(phase) + Mathf.Sin(phase * 1.5f) * .25f) * volume * Mathf.Sin(t * Mathf.PI) * (1 - t);
            }
            var clip = AudioClip.Create(title, n, 1, 22050, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnDestroy()
        {
            foreach (var clip in new[] { collectClip, dashClip, hitClip, breakClip }) if (clip != null) Destroy(clip);
            if (dynamicRoot != null) Destroy(dynamicRoot.gameObject);
        }
    }
}
