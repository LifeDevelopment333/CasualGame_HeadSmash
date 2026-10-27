using UnityEngine;

namespace HeadSmash
{
    /// <summary>Short-lived, shared-material procedural particles, with no external VFX dependency.</summary>
    public sealed class SmashFx : MonoBehaviour
    {
        Vector3 velocity;
        float life, total;
        Vector3 startScale;
        bool ring;

        public static void Burst(Vector3 p, Color color, int count, Material shared)
        {
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                Destroy(go.GetComponent<Collider>());
                go.name = "Smash confetti";
                go.transform.position = p;
                go.transform.localScale = Vector3.one * Random.Range(.12f, .28f);
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = shared;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", i % 4 == 0 ? new Color(1, .8f, .12f) : color);
                renderer.SetPropertyBlock(block);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var fx = go.AddComponent<SmashFx>();
                fx.velocity = Random.insideUnitSphere * 6 + Vector3.up * 4;
                fx.life = fx.total = Random.Range(.45f, .85f);
                fx.startScale = go.transform.localScale;
            }
        }

        public static void Ring(Vector3 p, Material shared, float radius)
        {
            // A thin flat cylinder reads as an expanding cartoon shock wave.
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.name = "Dash shockwave";
            go.transform.position = p;
            go.transform.localScale = new Vector3(radius, .012f, radius);
            go.GetComponent<MeshRenderer>().sharedMaterial = shared;
            var fx = go.AddComponent<SmashFx>();
            fx.ring = true;
            fx.life = fx.total = .24f;
            fx.startScale = go.transform.localScale;
        }

        public static void Dust(Vector3 p, Material shared)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.name = "Dash dust";
            go.transform.position = p;
            go.transform.localScale = Vector3.one * .4f;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = shared;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var fx = go.AddComponent<SmashFx>();
            fx.life = fx.total = .23f;
            fx.startScale = go.transform.localScale;
        }

        void Update()
        {
            var game = FindFirstObjectByType<HeadSmashGame>();
            if (game != null && game.State == MatchState.Paused) return;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            life -= dt;
            if (life <= 0) { Destroy(gameObject); return; }
            if (ring)
            {
                float s = 1 + (1 - life / total) * 2.2f;
                transform.localScale = new Vector3(startScale.x * s, startScale.y * life / total, startScale.z * s);
            }
            else
            {
                velocity += Vector3.down * (12 * dt);
                transform.position += velocity * dt;
                transform.Rotate(new Vector3(120, 70, 90) * dt);
                transform.localScale = startScale * life / total;
            }
        }
    }
}
