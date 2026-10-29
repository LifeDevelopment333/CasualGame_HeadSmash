using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeadSmash
{
    public sealed class SmashActor : MonoBehaviour
    {
        public string ActorName { get; private set; }
        public int ColorIndex { get; private set; }
        public bool IsPlayer { get; private set; }
        public int Score { get; private set; }
        public bool Alive { get; private set; } = true;
        public float Health = 100;
        public float Invulnerable;
        public float HitGrace;
        public Vector3 Knockback;
        public Vector3 PreviousPosition { get; private set; }
        public float HeadScale => 1 + Mathf.Min(2.1f, Mathf.Sqrt(Score / 110f) * .58f);
        public float Radius => .68f * HeadScale;
        public Vector3 Forward => transform.forward;
        public bool Dashing => dashTime > 0;
        public bool AttackingThisStep { get; private set; }
        public float CooldownFraction => Mathf.Clamp01(cooldown / game.dashCooldown);
        public int Level => 1 + Score / 80;

        HeadSmashGame game;
        [Header("Prefab references")]
        [SerializeField] Transform visual, head, headAnchor, marker, heading, crown, cracks;
        [SerializeField] Animator animator;
        [SerializeField, HideInInspector] float flashAmount;
        [SerializeField] Transform healthFill, nameAnchor;
        [SerializeField] TextMeshPro nameText;
        float cooldown, dashTime, respawnTime, thinkTime, lastHurtTime;
        static readonly int MovingId = Animator.StringToHash("Moving");
        static readonly int DashingId = Animator.StringToHash("Dashing");
        static readonly int ShieldedId = Animator.StringToHash("Shielded");
        static readonly int HurtId = Animator.StringToHash("Hurt");
        static readonly int HitId = Animator.StringToHash("Hit");
        static readonly int BonkId = Animator.StringToHash("Bonk");
        static readonly int LevelUpId = Animator.StringToHash("LevelUp");
        Vector3 steering = Vector3.forward;
        Vector3 botTarget;
        readonly HashSet<SmashActor> dashHits = new HashSet<SmashActor>();
        [SerializeField] List<MeshRenderer> skins = new List<MeshRenderer>();

        public void Initialize(HeadSmashGame owner, string actorName, int color, int score, bool isPlayer)
        {
            game = owner; ActorName = actorName; ColorIndex = color; IsPlayer = isPlayer; Score = score;
            transform.rotation = Quaternion.Euler(0, isPlayer ? 0 : Random.Range(0, 360), 0);
            steering = transform.forward;
            InitializePresentation();
            PreviousPosition = transform.position;
            botTarget = game.RandomPoint();
            thinkTime = Random.value;
        }

        void InitializePresentation()
        {
            foreach (var skin in skins) skin.sharedMaterial = game.palette[ColorIndex];
            marker.gameObject.SetActive(IsPlayer);
            heading.gameObject.SetActive(IsPlayer);
            nameText.font = game.font;
            nameText.color = IsPlayer ? new Color(.1f, .24f, .3f) : new Color(.13f, .25f, .28f);
            UpdatePresentation();
        }

        public void Tick(float dt)
        {
            if (!Alive)
            {
                if (!IsPlayer && (respawnTime -= dt) <= 0) Respawn(game.SafeSpawn(7), Mathf.RoundToInt(Score * .4f));
                return;
            }
            PreviousPosition = transform.position;
            Invulnerable = Mathf.Max(0, Invulnerable - dt);
            HitGrace = Mathf.Max(0, HitGrace - dt);
            if (HitGrace > 0) lastHurtTime = Time.time;
            if (Time.time - lastHurtTime > 6) Health = Mathf.Min(100, Health + dt * 4);
            cooldown = Mathf.Max(0, cooldown - dt);
            bool dash = false;
            if (IsPlayer)
            {
                Vector2 input = game.hud.JoystickValue;
                var keys = Keyboard.current;
                if (keys != null)
                {
                    Vector2 keyboard = new Vector2((keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0),
                        (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0) - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0));
                    if (keyboard.sqrMagnitude > .01f) input = keyboard.normalized;
                    dash = keys.spaceKey.wasPressedThisFrame;
                }
                if (input.sqrMagnitude > .04f) steering = new Vector3(input.x, 0, input.y).normalized;
                dash |= game.hud.ConsumeDash();
                if (Mouse.current != null) dash |= Mouse.current.rightButton.wasPressedThisFrame;
            }
            else Think(dt, ref dash);
            if (!Dashing && steering.sqrMagnitude > .01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(steering), dt * 380);
            if (dash) TryDash();
            float speed = Dashing ? game.dashSpeed : game.moveSpeed / Mathf.Pow(HeadScale, .18f);
            // Preserve the attack on the last movement slice, even when its timer expires below.
            AttackingThisStep = Dashing;
            transform.position = game.ClampToArena(transform.position + (Forward * speed + Knockback) * dt, Radius);
            Knockback = Vector3.Lerp(Knockback, Vector3.zero, 1 - Mathf.Exp(-dt * 8));
            dashTime = Mathf.Max(0, dashTime - dt);
            if (Dashing && Time.frameCount % 4 == 0) SmashFx.Dust(transform.position + Vector3.up * .15f, game.palette[12]);
            if (IsPlayer && Mathf.Abs(transform.position.x) > game.arenaRadius - Radius - .4f || IsPlayer && Mathf.Abs(transform.position.z) > game.arenaRadius - Radius - .4f)
                game.hud.BoundaryHint();
        }

        void Think(float dt, ref bool dash)
        {
            thinkTime -= dt;
            if (thinkTime <= 0)
            {
                thinkTime = Random.Range(.25f, .55f);
                SmashActor target = null;
                float nearest = 9 * 9;
                foreach (var other in game.Actors)
                {
                    if (other == this || !other.Alive || other.Invulnerable > 0) continue;
                    float d = (other.transform.position - transform.position).sqrMagnitude;
                    if (d < nearest) { target = other; nearest = d; }
                }
                if (target != null && (Health < 35 || target.HeadScale > HeadScale * 1.35f))
                    botTarget = transform.position + (transform.position - target.transform.position).normalized * 7;
                else if (target != null && (target.HeadScale < HeadScale * 1.12f || target.Health < 50))
                {
                    botTarget = target.transform.position + target.Forward * .5f;
                    dash = nearest < 40 && nearest > 4 && Vector3.Dot(Forward, (botTarget - transform.position).normalized) > .87f;
                }
                else
                {
                    float foodDistance = 12 * 12;
                    bool found = false;
                    foreach (var f in game.Foods)
                    {
                        float d = (f.position - transform.position).sqrMagnitude;
                        if (d < foodDistance) { foodDistance = d; botTarget = f.position; found = true; }
                    }
                    if (!found && (botTarget - transform.position).sqrMagnitude < 4) botTarget = game.RandomPoint();
                }
                botTarget = game.ClampToArena(botTarget, Radius + 1);
            }
            if (Mathf.Abs(transform.position.x) > game.arenaRadius - Radius - 1 || Mathf.Abs(transform.position.z) > game.arenaRadius - Radius - 1)
                botTarget = transform.position * .6f;
            steering = (botTarget - transform.position).normalized;
        }

        public bool TryDash()
        {
            if (!Alive || cooldown > 0 || Dashing || game.State != MatchState.Playing) return false;
            cooldown = game.dashCooldown;
            dashTime = game.dashDuration;
            dashHits.Clear();
            Invulnerable = 0; // Attacking gives up the spawn shield.
            animator.SetBool(DashingId, true);
            game.OnDash(this);
            return true;
        }

        public bool MarkHit(SmashActor victim) => dashHits.Add(victim);

        public void AddScore(int value)
        {
            int previousLevel = Level;
            Score += Mathf.Max(0, value);
            Health = Mathf.Min(100, Health + value * .12f);
            if (IsPlayer && Level > previousLevel)
            {
                game.hud.Toast($"LEVEL {Level}  /  BIGGER HEAD!", 1.7f);
                animator.SetTrigger(LevelUpId);
                game.Burst(transform.position + Vector3.up * 2, game.palette[8].color, 12);
            }
        }

        public void SetDead()
        {
            Alive = false;
            AttackingThisStep = false;
            dashTime = 0;
            respawnTime = Random.Range(3, 5);
            animator.speed = 0;
            visual.gameObject.SetActive(false);
            nameAnchor.gameObject.SetActive(false);
            if (marker != null) marker.gameObject.SetActive(false);
            if (heading != null) heading.gameObject.SetActive(false);
        }

        public void Respawn(Vector3 position, int retainedScore)
        {
            transform.position = position;
            PreviousPosition = position;
            Score = retainedScore;
            Health = 100;
            Invulnerable = 2;
            HitGrace = cooldown = dashTime = 0;
            AttackingThisStep = false;
            lastHurtTime = Time.time;
            Knockback = Vector3.zero;
            steering = (Vector3.zero - position).normalized;
            if (steering.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(steering);
            Alive = true;
            visual.gameObject.SetActive(true);
            nameAnchor.gameObject.SetActive(true);
            if (marker != null) marker.gameObject.SetActive(IsPlayer);
            if (heading != null) heading.gameObject.SetActive(IsPlayer);
            animator.Rebind();
            animator.Update(0);
            UpdatePresentation();
        }

        public void PlayImpact(bool outgoing = false)
        {
            if (Alive) animator.SetTrigger(outgoing ? BonkId : HitId);
        }

        // Gameplay values and Animator parameters; motion curves live in the animation assets.
        public void UpdatePresentation()
        {
            if (!Alive) return;
            bool playing = game.State == MatchState.Playing;
            bool animate = playing || game.State == MatchState.Ready;
            animator.speed = animate ? 1 : 0;
            if (animate)
            {
                animator.SetBool(MovingId, playing);
                animator.SetBool(DashingId, Dashing);
                animator.SetBool(ShieldedId, Invulnerable > 0);
                animator.SetBool(HurtId, HitGrace > 0);
            }
            headAnchor.localPosition = new Vector3(0, 1.1f + HeadScale * .48f, 0);
            head.localScale = Vector3.one * HeadScale;
            if (marker != null) marker.localScale = new Vector3(Radius * 2.65f, .015f, Radius * 2.65f);
            nameAnchor.localPosition = new Vector3(0, 1.4f + HeadScale * 1.45f, 0);
            nameText.text = $"{ActorName} <size=75%>{Score}</size>";
            healthFill.localScale = new Vector3(.96f * Health / 100f, .7f, 1);
            healthFill.localPosition = new Vector3(-.48f * (1 - Health / 100f), 0, -.65f);
            cracks.gameObject.SetActive(Health < 55);
            bool leader = true;
            foreach (var other in game.Actors) if (other.Score > Score || other.Score == Score && other != this && !IsPlayer) { leader = false; break; }
            crown.gameObject.SetActive(leader && Score > 40);
        }

        void LateUpdate()
        {
            if (game == null || !Alive) return;
            if (Camera.main != null) nameAnchor.rotation = Camera.main.transform.rotation;
            foreach (var skin in skins) skin.sharedMaterial = game.palette[flashAmount > .5f ? 12 : ColorIndex];
        }
    }
}
