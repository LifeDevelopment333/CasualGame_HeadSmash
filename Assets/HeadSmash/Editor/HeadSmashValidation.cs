using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HeadSmash.Editor
{
    /// <summary>Integration checks against the real runtime simulation. Run in Play mode.</summary>
    public static class HeadSmashValidation
    {
        [MenuItem("Head Smash/Validate gameplay (Play mode)")]
        public static void RunMenu() => Debug.Log(string.Join("\n", Run()));

        public static string[] Run()
        {
            var g = UnityEngine.Object.FindFirstObjectByType<HeadSmashGame>();
            if (!Application.isPlaying || g == null) throw new InvalidOperationException("Open the arena and enter Play mode first.");
            var passed = new List<string>();
            float originalSpeed = g.dashSpeed;
            int originalBest = g.BestScore;
            bool wasEnabled = g.enabled;
            g.enabled = false;
            try
            {
                g.Restart();
                var p = g.Player;
                var bot = g.Actors[1];
                for (int i = 1; i < g.Actors.Count; i++)
                {
                    g.Actors[i].Respawn(new Vector3(20, 0, -20 + i * 2), 20);
                    g.Actors[i].Tick(0);
                }
                Check(g.Actors.Count == g.opponentCount + 1 && g.Foods.Count >= g.ambientFood, "Round creates player, AI rivals and XP", passed);
                Vector3 start = p.transform.position;
                p.Tick(.05f);
                Check((p.transform.position - start).magnitude > .1f, "Automatic forward movement", passed);
                p.Health = 50;
                float initialScale = p.HeadScale;
                p.AddScore(80);
                Check(p.Level == 2 && p.HeadScale > initialScale && p.Health > 50, "XP increases head size, level and health", passed);

                p.Respawn(Vector3.zero, 0);
                p.Invulnerable = 0;
                p.transform.rotation = Quaternion.identity;
                Check(p.TryDash() && !p.TryDash(), "Dash starts and rejects immediate reuse", passed);
                p.Tick(.4f);
                Check(!p.Dashing && !p.TryDash(), "Dash ends while cooldown remains active", passed);
                p.Tick(2);
                Check(p.TryDash(), "Dash becomes available after cooldown", passed);

                p.Respawn(Vector3.zero, 0);
                p.Invulnerable = 0;
                p.transform.rotation = Quaternion.identity;
                bot.Respawn(new Vector3(0, 0, 5), 200);
                bot.Invulnerable = 0;
                bot.transform.rotation = Quaternion.Euler(0, 180, 0);
                bot.Tick(0);
                g.dashSpeed = 200;
                p.TryDash();
                p.Tick(.05f); // Cross the entire target in one step: no final overlap.
                Call(g, "ResolveContacts");
                float afterHit = bot.Health;
                Check(afterHit > 0 && afterHit < 100 && g.HitsLanded == 1, "Swept dash detects a crossed target without final overlap", passed);
                bot.HitGrace = 0;
                Call(g, "ResolveContacts");
                Check(Mathf.Approximately(bot.Health, afterHit), "One dash cannot damage the same target twice", passed);
                g.dashSpeed = originalSpeed;

                p.Respawn(Vector3.zero, 0);
                p.Invulnerable = 0;
                p.transform.rotation = Quaternion.identity;
                p.TryDash();
                p.Tick(.26f);
                bot.Respawn(new Vector3(0, 0, 6.6f), 0);
                bot.Invulnerable = 0;
                bot.Tick(0);
                p.Tick(.05f);
                Call(g, "ResolveContacts");
                Check(!p.Dashing && bot.Health < 100, "The final slice of a dash still deals damage", passed);

                p.Respawn(Vector3.zero, 0);
                p.Invulnerable = 0;
                p.transform.rotation = Quaternion.identity;
                bot.Respawn(new Vector3(0, 0, 1), 0);
                bot.Invulnerable = 0;
                bot.Health = 10;
                bot.Tick(0);
                int foodBefore = g.Foods.Count;
                p.TryDash(); p.Tick(.02f);
                Call(g, "ResolveContacts");
                Check(!bot.Alive && g.Knockouts == 1 && g.Foods.Count > foodBefore && p.Score >= 15, "Lethal headbutt shatters a rival and drops XP", passed);
                bot.Tick(6);
                Check(bot.Alive && bot.Health == 100 && bot.Invulnerable > 0, "Defeated AI respawns with a shield", passed);

                for (int i = 1; i < g.Actors.Count; i++)
                {
                    g.Actors[i].Respawn(new Vector3(20, 0, -20 + i * 2), 20);
                    g.Actors[i].Tick(0);
                }
                int xp = p.Score;
                p.Health = 50;
                g.SpawnFood(p.transform.position, 20);
                Call(g, "UpdateFood", .05f);
                Check(p.Score >= xp + 20 && p.Health > 50 && g.FoodCollected > 0, "Nearby XP is collected, scored and heals the player", passed);
                p.transform.position = new Vector3(500, 0, -500);
                p.Tick(0);
                Check(Mathf.Abs(p.transform.position.x) <= g.arenaRadius - p.Radius + .001f && Mathf.Abs(p.transform.position.z) <= g.arenaRadius - p.Radius + .001f, "Arena bounds contain the growing head", passed);

                xp = p.Score;
                g.Shatter(p, bot);
                Check(g.State == MatchState.KnockedOut && !p.Alive, "Player defeat opens the knockout flow", passed);
                g.RespawnPlayer();
                Check(g.State == MatchState.Playing && p.Alive && p.Health == 100 && p.Score == Mathf.FloorToInt(xp * .55f) && p.Invulnerable == 3, "Continue retains 55% XP and grants a fresh shield", passed);

                g.TogglePause();
                Vector3 pausedPosition = p.transform.position;
                float pausedTimer = g.Remaining;
                Call(g, "Update");
                Check(g.State == MatchState.Paused && p.transform.position == pausedPosition && g.Remaining == pausedTimer, "Pause freezes movement and the match clock", passed);
                g.TogglePause();
                SetProperty(g, "Remaining", .00001f);
                Call(g, "Update");
                Check(g.State == MatchState.Finished && g.Remaining == 0, "Expired timer opens final ranking and replay", passed);
                g.Restart();
                Check(g.State == MatchState.Playing && g.Knockouts == 0 && g.Player.Score == 0 && g.Remaining == g.roundDuration, "New round resets score, timer and knockouts", passed);
                passed.Add("PASS: " + passed.Count + " gameplay integration checks");
                return passed.ToArray();
            }
            finally
            {
                g.dashSpeed = originalSpeed;
                SetProperty(g, "BestScore", originalBest);
                PlayerPrefs.SetInt("HeadSmash.Best", originalBest);
                PlayerPrefs.Save();
                g.enabled = wasEnabled;
                g.Restart();
                g.TogglePause();
            }
        }

        static void Check(bool result, string label, List<string> passed)
        {
            if (!result) throw new Exception("FAIL: " + label);
            passed.Add("PASS: " + label);
        }
        static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target, new[] { value });
    }
}
