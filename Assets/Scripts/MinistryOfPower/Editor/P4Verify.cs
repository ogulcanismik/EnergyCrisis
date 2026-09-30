using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MinistryOfPower.Runtime;
using MinistryOfPower.Simulation;
using MinistryOfPower.UI;

namespace MinistryOfPower.EditorTools
{
    /// <summary>Batch / menu verify for P4 systems. Writes results under persistentDataPath.</summary>
    public static class P4Verify
    {
        private const string OutName = "mop_p4_verify.txt";

        [MenuItem("Ministry of Power/Verify P4")]
        public static void VerifyMenu()
        {
            string path = Path.Combine(Application.persistentDataPath, OutName);
            File.WriteAllText(path, RunSmoke(), Encoding.UTF8);
            Debug.Log("P4 verify written: " + path);
            EditorUtility.RevealInFinder(path);
        }

        public static void VerifyBatch()
        {
            string path = Path.Combine(Application.persistentDataPath, OutName);
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(path, RunSmoke() + "\n" + RunPlay(), Encoding.UTF8);
            Debug.Log("P4 batch verify: " + path);
#if UNITY_EDITOR
            EditorApplication.Exit(0);
#endif
        }

        private static string RunSmoke()
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine("=== P4 SIM SMOKE ===");
            try
            {
                PrototypeContentFactory.CreateUsaLike(out ScenarioConfig fed, out List<BuildDefinitionConfig> builds);
                PrototypeContentFactory.CreateSunRich(out ScenarioConfig sun, out _);
                sb.Append("fed budget=").Append(fed.StartingBudget)
                    .Append(" retirex=").Append(fed.LobbyRetireMultiplier)
                    .Append(" solar=").Append(fed.SolarResource)
                    .Append(" import=").Append(fed.ImportCapacityMw).AppendLine();
                sb.Append("sun budget=").Append(sun.StartingBudget)
                    .Append(" retirex=").Append(sun.LobbyRetireMultiplier)
                    .Append(" solar=").Append(sun.SolarResource)
                    .Append(" import=").Append(sun.ImportCapacityMw).AppendLine();

                var s = new GameSession();
                s.Start(fed, builds, DifficultyConfig.Create(DifficultyId.Normal));
                sb.Append("campaignYears=").Append(s.Mandate.CampaignYears).AppendLine();
                sb.Append("mandateHud=").Append(s.Mandate.FormatHud(s)).AppendLine();

                int n = 0, c = 0, d = 0;
                for (int i = 0; i < s.Portfolio.Plants.Count; i++)
                {
                    PlantInstance p = s.Portfolio.Plants[i];
                    if (p.IsRetired) continue;
                    if (p.Region == RegionId.North) n++;
                    else if (p.Region == RegionId.Coast) c++;
                    else d++;
                }

                sb.Append("plants N/C/D=").Append(n).Append('/').Append(c).Append('/').Append(d).AppendLine();

                s.SetSelectedRegion(RegionId.Desert);
                s.TryStartBuild("build_solar", out string msg);
                sb.Append("order=").Append(msg).AppendLine();
                bool desert = false;
                for (int i = 0; i < s.Builds.Orders.Count; i++)
                    if (s.Builds.Orders[i].Region == RegionId.Desert) desert = true;
                sb.Append("desertSite=").Append(desert).AppendLine();

                float oil0 = s.FuelMarket.Oil;
                for (int i = 0; i < 8; i++)
                {
                    s.AdvanceDay();
                    if (s.AwaitingCrisisDecision)
                        s.TryResolveCrisis(CrisisChoice.Absorb, out _);
                }

                sb.Append("oil ").Append(oil0.ToString("0.00")).Append("->")
                    .Append(s.FuelMarket.Oil.ToString("0.00"))
                    .Append(" blend=").Append(s.FuelPriceIndex.ToString("0.00")).AppendLine();

                int guard = 0;
                while (s.Ledger.LastIncome <= 0.01f && guard++ < 200)
                {
                    s.AdvanceDay();
                    if (s.AwaitingCrisisDecision)
                        s.TryResolveCrisis(CrisisChoice.Absorb, out _);
                }

                sb.Append("ledger income=").Append(s.Ledger.LastIncome.ToString("0.0"))
                    .Append(" upkeep=").Append(s.Ledger.LastUpkeep.ToString("0.0"))
                    .Append(" build=").Append(s.Ledger.LastBuildDrain.ToString("0.0")).AppendLine();

                var crash = MajorEventSpawner.BuildMeterCrash(SeatMeters.Create(30, 70, 20, 60), Season.Winter);
                s.History.Record(crash, s.Clock, "pending");
                sb.Append("history=").Append(s.History.Entries.Count).AppendLine();
                string detail = RegionCatalog.BuildDetail(s, RegionId.Desert);
                sb.Append("selectedMarked=").Append(detail.IndexOf("SELECTED", StringComparison.Ordinal) >= 0).AppendLine();
                sb.AppendLine("P4 sim smoke OK");

                sb.AppendLine("=== P5 SIM SMOKE ===");
                for (int i = 0; i < 100; i++)
                {
                    s.AdvanceDay();
                    if (s.AwaitingCrisisDecision)
                        s.TryResolveCrisis(CrisisChoice.Absorb, out _);
                }

                string spark = s.Mandate.FormatSparkline();
                sb.Append("sparkline=").Append(spark).AppendLine();
                sb.Append("sparkSamples=").Append(s.Mandate.CleanSamples.Count).AppendLine();
                bool hasCold = false, hasStorm = false;
                if (fed.EventWeights != null)
                {
                    for (int i = 0; i < fed.EventWeights.Count; i++)
                    {
                        if (fed.EventWeights[i].Kind == PendingEventKind.ColdSnap) hasCold = true;
                        if (fed.EventWeights[i].Kind == PendingEventKind.StormOutage) hasStorm = true;
                    }
                }

                sb.Append("deckCold=").Append(hasCold).Append(" deckStorm=").Append(hasStorm).AppendLine();
                sb.Append("newestSaveSlot=").Append(SaveGameSystem.FindNewestSlot()).AppendLine();
                sb.AppendLine("P5 sim smoke OK");
            }
            catch (Exception ex)
            {
                sb.AppendLine("FAIL: " + ex);
            }

            return sb.ToString();
        }

        private static string RunPlay()
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("=== P4 PLAY STRUCTURE ===");
            try
            {
                GameSettings.HelpSeen = true;
                RunSetup.Ensure().ConfigureNewGame("usa_like", DifficultyId.Normal);
                EditorSceneManager.OpenScene("Assets/Scenes/MinistryDesk.unity");
                // Don't enter play mode in batch — instantiate runner statically if present
                var runner = UnityEngine.Object.FindFirstObjectByType<MinistryGameRunner>();
                sb.Append("runnerInScene=").Append(runner != null).AppendLine();
                sb.AppendLine("P4 play structure note: enter Play in Editor for full HUD; sim smoke covers systems.");
            }
            catch (Exception ex)
            {
                sb.AppendLine("PLAY FAIL: " + ex.Message);
            }

            return sb.ToString();
        }

        private const string PlayOutName = "mop_play_smoke.txt";
        private const string PlaySmokeKey = "MoP.PlaySmokeArmed";
        private static double _playEnteredAt;

        [InitializeOnLoadMethod]
        private static void HookPlaySmoke()
        {
            EditorApplication.playModeStateChanged -= OnPlaySmokeState;
            EditorApplication.playModeStateChanged += OnPlaySmokeState;
            if (SessionState.GetBool(PlaySmokeKey, false) && EditorApplication.isPlaying)
            {
                _playEnteredAt = EditorApplication.timeSinceStartup;
                EditorApplication.update -= OnPlaySmokeTick;
                EditorApplication.update += OnPlaySmokeTick;
            }
        }

        [MenuItem("Ministry of Power/Verify Play Smoke")]
        public static void VerifyPlaySmokeMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += VerifyPlaySmokeMenu;
                return;
            }

            GameSettings.HelpSeen = true;
            RunSetup.Ensure().ConfigureNewGame("usa_like", DifficultyId.Normal);
            EditorSceneManager.OpenScene("Assets/Scenes/MinistryDesk.unity");
            SessionState.SetBool(PlaySmokeKey, true);
            EditorApplication.isPlaying = true;
            Debug.Log("Play smoke armed — entering Play Mode.");
        }

        private static void OnPlaySmokeState(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(PlaySmokeKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                _playEnteredAt = EditorApplication.timeSinceStartup;
                EditorApplication.update -= OnPlaySmokeTick;
                EditorApplication.update += OnPlaySmokeTick;
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= OnPlaySmokeTick;
            }
        }

        private static void OnPlaySmokeTick()
        {
            if (!SessionState.GetBool(PlaySmokeKey, false) || !EditorApplication.isPlaying) return;

            var runnerEarly = UnityEngine.Object.FindFirstObjectByType<MinistryGameRunner>();
            bool ready = runnerEarly != null
                         && runnerEarly.Session != null
                         && runnerEarly.Session.Mandate != null
                         && runnerEarly.Session.FuelMarket != null;
            if (!ready)
            {
                if (EditorApplication.timeSinceStartup - _playEnteredAt > 20)
                {
                    EditorApplication.update -= OnPlaySmokeTick;
                    SessionState.SetBool(PlaySmokeKey, false);
                    File.WriteAllText(
                        Path.Combine(Application.persistentDataPath, PlayOutName),
                        "=== PLAY SMOKE ===\nFAIL session not ready\n",
                        Encoding.UTF8);
                    EditorApplication.isPlaying = false;
                }

                return;
            }

            if (EditorApplication.timeSinceStartup - _playEnteredAt < 1.0) return;

            EditorApplication.update -= OnPlaySmokeTick;
            SessionState.SetBool(PlaySmokeKey, false);

            var sb = new StringBuilder(1024);
            sb.AppendLine("=== PLAY SMOKE ===");
            try
            {
                var runner = runnerEarly;
                var hud = UnityEngine.Object.FindFirstObjectByType<ParadoxChromeHud>();
                sb.Append("runner=").Append(runner != null).AppendLine();
                sb.Append("hud=").Append(hud != null).AppendLine();
                GameSession s = runner != null ? runner.Session : null;
                if (s == null || s.Mandate == null)
                {
                    sb.AppendLine("FAIL no session");
                }
                else
                {
                    sb.Append("mandate=").Append(s.Mandate.FormatHud(s)).AppendLine();
                    sb.Append("fuel=").Append(s.FuelMarket.FormatLine()).AppendLine();
                    sb.Append("plants=").Append(s.Portfolio.Plants.Count).AppendLine();
                    sb.Append("budget=").Append(s.Budget.ToString("0.0")).AppendLine();
                    sb.Append("plantMarkers=")
                        .Append(UnityEngine.Object.FindObjectsByType<PlantMarker>(FindObjectsSortMode.None).Length)
                        .AppendLine();
                    for (int i = 0; i < 6; i++)
                    {
                        s.AdvanceDay();
                        if (s.AwaitingCrisisDecision)
                            s.TryResolveCrisis(CrisisChoice.Absorb, out _);
                    }

                    sb.Append("spark=").Append(s.Mandate.FormatSparkline()).AppendLine();
                    sb.Append("history=").Append(s.History.Entries.Count).AppendLine();
                    sb.AppendLine("PLAY SMOKE OK");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("PLAY FAIL: " + ex);
            }

            string path = Path.Combine(Application.persistentDataPath, PlayOutName);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
            Debug.Log("Play smoke written: " + path);
            EditorApplication.isPlaying = false;
        }
    }
}
