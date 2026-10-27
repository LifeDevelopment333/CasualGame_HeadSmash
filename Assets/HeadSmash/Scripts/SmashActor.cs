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
        public float Pulse;
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
        Transform visual, head, body, leftLeg, rightLeg, marker, crown, cracks;
        Transform healthFill, nameAnchor;
        TextMeshPro nameText;
        float cooldown, dashTime, respawnTime, thinkTime, lastHurtTime;
        float walkPhase;
        Vector3 steering = Vector3.forward;
        Vector3 botTarget;
        readonly HashSet<SmashActor> dashHits = new HashSet<SmashActor>();
        readonly List<MeshRenderer> skins = new List<MeshRenderer>();

        public void Initialize(HeadSmashGame owner, string actorName, int color, int score, bool isPlayer)
        {
            game = owner; ActorName = actorName; ColorIndex = color; IsPlayer = isPlayer; Score = score;
            transform.rotation = Quaternion.Euler(0, isPlayer ? 0 : Random.Range(0, 360), 0);
            steering = transform.forward;
            BuildCharacter();
            PreviousPosition = transform.position;
            botTarget = game.RandomPoint();
            thinkTime = Random.value;
        }

        Transform Part(PrimitiveType type, string label, Transform parent, Vector3 p, Vector3 scale, int material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = label;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = p;
            go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = game.palette[material];
            if (material == ColorIndex) skins.Add(r);
            return go.transform;
        }

        void BuildCharacter()
        {
            visual = new GameObject("Squash and stretch").transform;
            visual.SetParent(transform, false);
            body = Part(PrimitiveType.Capsule, "Tiny suit", visual, new Vector3(0, .62f, 0), new Vector3(.7f, .42f, .55f), ColorIndex);
            Part(PrimitiveType.Sphere, "Belly patch", body, new Vector3(0, -.08f, .44f), new Vector3(.6f, .43f, .18f), 7);
            leftLeg = Part(PrimitiveType.Sphere, "Left boot", visual, new Vector3(-.25f, .19f, .1f), new Vector3(.34f, .3f, .49f), 13);
            rightLeg = Part(PrimitiveType.Sphere, "Right boot", visual, new Vector3(.25f, .19f, .1f), new Vector3(.34f, .3f, .49f), 13);
            Part(PrimitiveType.Sphere, "Left mitten", visual, new Vector3(-.49f, .72f, .06f), Vector3.one * .3f, 7);
            Part(PrimitiveType.Sphere, "Right mitten", visual, new Vector3(.49f, .72f, .06f), Vector3.one * .3f, 7);
            head = new GameObject("Growing head").transform;
            head.SetParent(visual, false);
            Part(PrimitiveType.Sphere, "Head", head, Vector3.zero, new Vector3(1.62f, 1.48f, 1.5f), ColorIndex);
            Part(PrimitiveType.Sphere, "Face", head, new Vector3(0, -.03f, .53f), new Vector3(1.21f, 1.03f, .56f), 7);
            for (int i = -1; i <= 1; i += 2)
            {
                Part(PrimitiveType.Sphere, "Eye white", head, new Vector3(i * .27f, .13f, .775f), new Vector3(.29f, .38f, .13f), 12);
                Part(PrimitiveType.Sphere, "Eye", head, new Vector3(i * .26f, .12f, .837f), new Vector3(.14f, .22f, .08f), 13);
                Part(PrimitiveType.Sphere, "Eye glint", head, new Vector3(i * .26f - .025f, .17f, .876f), Vector3.one * .045f, 12);
                Part(PrimitiveType.Sphere, "Blush", head, new Vector3(i * .44f, -.15f, .76f), new Vector3(.18f, .1f, .06f), 5);
                var brow = Part(PrimitiveType.Capsule, "Determined brow", head, new Vector3(i * .27f, .37f, .78f), new Vector3(.07f, .17f, .07f), 13);
                brow.localRotation = Quaternion.Euler(0, 0, i * 72);
                Part(PrimitiveType.Sphere, "Ear", head, new Vector3(i * .8f, -.02f, 0), new Vector3(.22f, .36f, .31f), ColorIndex);
            }
            Part(PrimitiveType.Sphere, "Smile", head, new Vector3(0, -.3f, .8f), new Vector3(.25f, .11f, .06f), 13);
            Part(PrimitiveType.Sphere, "Tooth", head, new Vector3(.035f, -.275f, .832f), new Vector3(.095f, .045f, .025f), 12);
            Part(PrimitiveType.Sphere, "Hair tuft", head, new Vector3(0, .71f, -.04f), new Vector3(.47f, .34f, .51f), ColorIndex);
            Part(PrimitiveType.Sphere, "Hair tuft small", head, new Vector3(.25f, .69f, -.01f), new Vector3(.3f, .25f, .36f), ColorIndex);
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
            if (IsPlayer)
            {
                marker = Part(PrimitiveType.Cylinder, "Player halo", transform, new Vector3(0, .035f, 0), new Vector3(2.05f, .015f, 2.05f), 14);
                var arrow = Part(PrimitiveType.Cube, "Heading", transform, new Vector3(0, .05f, 1.6f), new Vector3(.3f, .035f, .3f), 8);
                arrow.localRotation = Quaternion.Euler(0, 45, 0);
            }
            nameAnchor = new GameObject("Billboard").transform;
            nameAnchor.SetParent(transform, false);
            var textGo = new GameObject("Name");
            textGo.transform.SetParent(nameAnchor, false);
            nameText = textGo.AddComponent<TextMeshPro>();
            nameText.font = game.font;
            nameText.fontSize = 2.6f;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = IsPlayer ? new Color(.1f, .24f, .3f) : new Color(.13f, .25f, .28f);
            nameText.fontStyle = FontStyles.Bold;
            nameText.rectTransform.sizeDelta = new Vector2(6, .5f);
            var healthBack = Part(PrimitiveType.Cube, "Health track", nameAnchor, new Vector3(0, -.32f, 0), new Vector3(1.25f, .08f, .045f), 13);
            healthFill = Part(PrimitiveType.Cube, "Health", healthBack, new Vector3(0, 0, -.65f), new Vector3(.96f, .7f, 1), 9);
            Animate(0, 0);
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
            Animate(Time.time, dt);
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
                Pulse = .3f;
                game.Burst(transform.position + Vector3.up * 2, game.palette[8].color, 12);
            }
        }

        public void SetDead()
        {
            Alive = false;
            AttackingThisStep = false;
            dashTime = 0;
            respawnTime = Random.Range(3, 5);
            visual.gameObject.SetActive(false);
            nameAnchor.gameObject.SetActive(false);
            if (marker != null) marker.gameObject.SetActive(false);
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
            if (marker != null) marker.gameObject.SetActive(true);
            Animate(Time.time, 0);
        }

        public void Animate(float time, float dt)
        {
            if (!Alive) return;
            walkPhase += dt * (Dashing ? 22 : 12);
            float bounce = Mathf.Abs(Mathf.Sin(walkPhase)) * .08f;
            Pulse = Mathf.Max(0, Pulse - dt);
            float squash = Pulse > 0 ? Mathf.Sin(Pulse * 22) * .12f : 0;
            visual.localScale = new Vector3(1 + squash, 1 - squash, 1 + squash);
            visual.localPosition = Vector3.up * bounce;
            head.localPosition = new Vector3(0, 1.1f + HeadScale * .48f, Dashing ? .35f : 0);
            head.localScale = Vector3.one * HeadScale;
            head.localRotation = Quaternion.Euler(Dashing ? 24 : Mathf.Sin(time * 3) * 2, 0, Dashing ? 0 : Mathf.Sin(walkPhase) * 3);
            leftLeg.localPosition = new Vector3(-.25f, .19f, .1f + Mathf.Sin(walkPhase) * .18f);
            rightLeg.localPosition = new Vector3(.25f, .19f, .1f - Mathf.Sin(walkPhase) * .18f);
            if (marker != null) marker.localScale = new Vector3(Radius * 2.65f, .015f, Radius * 2.65f);
            nameAnchor.localPosition = new Vector3(0, 1.4f + HeadScale * 1.45f, 0);
            nameAnchor.rotation = Camera.main.transform.rotation;
            nameText.text = $"{ActorName} <size=75%>{Score}</size>";
            healthFill.localScale = new Vector3(.96f * Health / 100f, .7f, 1);
            healthFill.localPosition = new Vector3(-.48f * (1 - Health / 100f), 0, -.65f);
            cracks.gameObject.SetActive(Health < 55);
            bool leader = true;
            foreach (var other in game.Actors) if (other.Score > Score || other.Score == Score && other != this && !IsPlayer) { leader = false; break; }
            crown.gameObject.SetActive(leader && Score > 40);
            bool flash = Invulnerable > 0 && Mathf.Sin(time * 18) > .45f || HitGrace > 0 && Mathf.Sin(time * 40) > 0;
            foreach (var skin in skins) skin.sharedMaterial = game.palette[flash ? 12 : ColorIndex];
        }
    }
}
