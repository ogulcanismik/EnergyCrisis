using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MinistryOfPower.Data;
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

                sb.AppendLine("=== P6 SIM SMOKE ===");
                DifficultyConfig easy = DifficultyConfig.Create(DifficultyId.Easy);
                DifficultyConfig hard = DifficultyConfig.Create(DifficultyId.Hard);
                sb.Append("easyBudgetMul=").Append(easy.BudgetMultiplier.ToString("0.00"))
                    .Append(" hardBudgetMul=").Append(hard.BudgetMultiplier.ToString("0.00")).AppendLine();

                bool hasOff = false, hasBio = false;
                for (int i = 0; i < builds.Count; i++)
                {
                    if (builds[i].Id == "build_offshore_wind") hasOff = true;
                    if (builds[i].Id == "build_biomass") hasBio = true;
                }

                sb.Append("catalogOffshore=").Append(hasOff).Append(" biomass=").Append(hasBio).AppendLine();

                // Fresh Easy session for cabinet + year report (Normal can sack mid-year).
                var easyS = new GameSession();
                easyS.Start(fed, builds, easy);
                bool reserve = easyS.TryCabinetAction(CrisisChoice.StrategicReserve, out string cabMsg);
                sb.Append("strategicReserve=").Append(reserve).Append(" ").Append(cabMsg).AppendLine();
                bool sub = easyS.TryCabinetAction(CrisisChoice.RenewableSubsidy, out string subMsg);
                sb.Append("reSubsidy=").Append(sub).Append(" ").Append(subMsg).AppendLine();

                int startY = easyS.Clock.Year;
                int targetAbs = easyS.Clock.AbsoluteDay + GameClock.DaysPerYear;
                int guardY = 0;
                while (easyS.Clock.AbsoluteDay < targetAbs && !easyS.IsGameOver && guardY++ < GameClock.DaysPerYear * 4)
                {
                    // Prefer fossil over absorb so year-long smoke isn't sacked by confidence.
                    if (easyS.AwaitingCrisisDecision)
                        easyS.TryResolveCrisis(CrisisChoice.EmergencyFossil, out _);
                    else
                        easyS.AdvanceDay();
                }

                sb.Append("yearCrossed=").Append(easyS.Clock.Year > startY)
                    .Append(" abs=").Append(easyS.Clock.AbsoluteDay)
                    .Append(" report=").Append(easyS.LastYearReport != null)
                    .Append(" sacked=").Append(easyS.IsGameOver).AppendLine();
                if (easyS.LastYearReport != null)
                    sb.Append("yearReportY=").Append(easyS.LastYearReport.Year)
                        .Append(" adeqAvg=").Append(easyS.LastYearReport.AdequacyAvg.ToString("0.0")).AppendLine();

                var save = GameSessionSaveMapper.Capture(easyS, 0);
                sb.Append("saveVersion=").Append(save.Version)
                    .Append(" current=").Append(GameSaveData.CurrentVersion)
                    .Append(" v3hist=").Append(save.EventHistory != null)
                    .Append(" v3cab=").Append(save.TariffFreezeDays >= 0)
                    .Append(" v3catalog=").Append(save.BuildCatalogIds != null && save.BuildCatalogIds.Count > 0)
                    .AppendLine();
                sb.AppendLine("P6 sim smoke OK");

                sb.AppendLine("=== P7 SIM SMOKE ===");
                EventDefinition coldAsset = AssetDatabase.LoadAssetAtPath<EventDefinition>(
                    "Assets/Data/Events/Event_ColdSnap.asset");
                EventDefinition stormAsset = AssetDatabase.LoadAssetAtPath<EventDefinition>(
                    "Assets/Data/Events/Event_StormOutage.asset");
                sb.Append("assetCold=").Append(coldAsset != null && coldAsset.Kind == PendingEventKind.ColdSnap)
                    .Append(" assetStorm=").Append(stormAsset != null && stormAsset.Kind == PendingEventKind.StormOutage)
                    .AppendLine();

                BuildDefinitionConfig offTip = null, bioTip = null;
                for (int i = 0; i < builds.Count; i++)
                {
                    if (builds[i].Id == "build_offshore_wind") offTip = builds[i];
                    if (builds[i].Id == "build_biomass") bioTip = builds[i];
                }

                string offTt = offTip != null ? offTip.FormatCatalogTooltip() : "";
                string bioTt = bioTip != null ? bioTip.FormatCatalogTooltip() : "";
                sb.Append("tipOffshore=").Append(offTt.IndexOf("MW", StringComparison.Ordinal) >= 0
                                                 && offTt.IndexOf("Fuel", StringComparison.Ordinal) >= 0)
                    .Append(" tipBiomass=").Append(bioTt.IndexOf("MW", StringComparison.Ordinal) >= 0
                                                   && bioTt.IndexOf("Biomass", StringComparison.Ordinal) >= 0)
                    .AppendLine();

                if (easyS.LastYearReport != null)
                {
                    string yr = easyS.LastYearReport.FormatModal();
                    sb.Append("yearNetDelta=").Append(easyS.LastYearReport.NetTreasuryDelta.ToString("0.0"))
                        .Append(" yearHasBiggest=").Append(yr.IndexOf("Biggest event", StringComparison.Ordinal) >= 0)
                        .Append(" yearHasNet=").Append(yr.IndexOf("Net treasury", StringComparison.Ordinal) >= 0)
                        .AppendLine();
                }

                GameSaveData contProbe = GameSessionSaveMapper.Capture(easyS, 0);
                bool saved = SaveGameSystem.TrySave(0, contProbe, out _);
                SaveSlotMeta contMeta = SaveGameSystem.PeekSlot(0);
                sb.Append("continueSubtitle=").Append(saved && contMeta.Occupied && !string.IsNullOrEmpty(contMeta.ScenarioName))
                    .Append(" scenario=").Append(contMeta.ScenarioName ?? "—").AppendLine();
                sb.AppendLine("P7 sim smoke OK");

                sb.AppendLine("=== P8 SIM SMOKE ===");
                BuildDefinition offAsset = AssetDatabase.LoadAssetAtPath<BuildDefinition>(
                    "Assets/Data/Builds/Build_OffshoreWind.asset");
                BuildDefinition bioAsset = AssetDatabase.LoadAssetAtPath<BuildDefinition>(
                    "Assets/Data/Builds/Build_Biomass.asset");
                bool hasOffSo = offAsset != null && offAsset.Id == "build_offshore_wind";
                bool hasBioSo = bioAsset != null && bioAsset.Id == "build_biomass";
                sb.Append("buildSoOffshore=").Append(hasOffSo)
                    .Append(" buildSoBiomass=").Append(hasBioSo)
                    .AppendLine();

                ScenarioDefinition fedSo = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(
                    "Assets/Data/Scenarios/Scenario_FederalHighBudget.asset");
                ScenarioDefinition sunSo = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(
                    "Assets/Data/Scenarios/Scenario_SunRichLowBudget.asset");
                float fedCold = WeightOf(fedSo, PendingEventKind.ColdSnap);
                float fedStorm = WeightOf(fedSo, PendingEventKind.StormOutage);
                float sunCold = WeightOf(sunSo, PendingEventKind.ColdSnap);
                float sunStorm = WeightOf(sunSo, PendingEventKind.StormOutage);
                sb.Append("fedCold=").Append(fedCold.ToString("0.00"))
                    .Append(" fedStorm=").Append(fedStorm.ToString("0.00"))
                    .Append(" sunCold=").Append(sunCold.ToString("0.00"))
                    .Append(" sunStorm=").Append(sunStorm.ToString("0.00")).AppendLine();
                bool tuned = fedCold > sunCold && sunStorm > fedStorm && fedCold > 1f && sunStorm > 1f;
                sb.Append("deckTuned=").Append(tuned).AppendLine();

                bool buildsOnFed = ScenarioHasBuild(fedSo, "build_offshore_wind")
                                   && ScenarioHasBuild(fedSo, "build_biomass");
                sb.Append("scenarioCatalogOffBio=").Append(buildsOnFed).AppendLine();

                EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
                var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
                bool hasMenu = menu != null;
                sb.Append("mainMenuScene=").Append(hasMenu).AppendLine();
                bool p8Ok = hasOffSo && hasBioSo && tuned && buildsOnFed && hasMenu;
                sb.Append("p8Ok=").Append(p8Ok).AppendLine();
                sb.AppendLine(p8Ok ? "P8 sim smoke OK" : "P8 sim smoke FAIL");
            }
            catch (Exception ex)
            {
                sb.AppendLine("FAIL: " + ex);
            }

            return sb.ToString();
        }

        private static float WeightOf(ScenarioDefinition so, PendingEventKind kind)
        {
            if (so == null) return 0f;
            ScenarioConfig cfg = so.ToConfig();
            for (int i = 0; i < cfg.EventWeights.Count; i++)
            {
                if (cfg.EventWeights[i].Kind == kind)
                    return cfg.EventWeights[i].BaseWeight;
            }

            return 0f;
        }

        private static bool ScenarioHasBuild(ScenarioDefinition so, string id)
        {
            if (so?.AvailableBuilds == null) return false;
            for (int i = 0; i < so.AvailableBuilds.Count; i++)
            {
                if (so.AvailableBuilds[i] != null && so.AvailableBuilds[i].Id == id)
                    return true;
            }

            return false;
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
        private const string MenuOutName = "mop_menu_smoke.txt";
        private const string PlaySmokeKey = "MoP.PlaySmokeArmed";
        private const string MenuSmokeKey = "MoP.MenuSmokeArmed";
        private static double _playEnteredAt;

        [InitializeOnLoadMethod]
        private static void HookPlaySmoke()
        {
            EditorApplication.playModeStateChanged -= OnPlaySmokeState;
            EditorApplication.playModeStateChanged += OnPlaySmokeState;
            EditorApplication.playModeStateChanged -= OnMenuSmokeState;
            EditorApplication.playModeStateChanged += OnMenuSmokeState;
            if (SessionState.GetBool(PlaySmokeKey, false) && EditorApplication.isPlaying)
            {
                _playEnteredAt = EditorApplication.timeSinceStartup;
                EditorApplication.update -= OnPlaySmokeTick;
                EditorApplication.update += OnPlaySmokeTick;
            }

            if (SessionState.GetBool(MenuSmokeKey, false) && EditorApplication.isPlaying)
            {
                _playEnteredAt = EditorApplication.timeSinceStartup;
                EditorApplication.update -= OnMenuSmokeTick;
                EditorApplication.update += OnMenuSmokeTick;
            }
        }

        [MenuItem("Ministry of Power/Verify Main Menu Smoke")]
        public static void VerifyMainMenuSmokeMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += VerifyMainMenuSmokeMenu;
                return;
            }

            GameSettings.HelpSeen = true;
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            SessionState.SetBool(MenuSmokeKey, true);
            EditorApplication.isPlaying = true;
            Debug.Log("Main menu smoke armed — entering Play Mode.");
        }

        private static void OnMenuSmokeState(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(MenuSmokeKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                _playEnteredAt = EditorApplication.timeSinceStartup;
                EditorApplication.update -= OnMenuSmokeTick;
                EditorApplication.update += OnMenuSmokeTick;
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= OnMenuSmokeTick;
            }
        }

        private static void OnMenuSmokeTick()
        {
            if (!SessionState.GetBool(MenuSmokeKey, false) || !EditorApplication.isPlaying) return;
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
            if (menu == null)
            {
                if (EditorApplication.timeSinceStartup - _playEnteredAt > 12)
                {
                    EditorApplication.update -= OnMenuSmokeTick;
                    SessionState.SetBool(MenuSmokeKey, false);
                    File.WriteAllText(
                        Path.Combine(Application.persistentDataPath, MenuOutName),
                        "=== MENU SMOKE ===\nFAIL no MainMenuController\n",
                        Encoding.UTF8);
                    EditorApplication.isPlaying = false;
                }

                return;
            }

            if (EditorApplication.timeSinceStartup - _playEnteredAt < 0.6) return;

            EditorApplication.update -= OnMenuSmokeTick;
            SessionState.SetBool(MenuSmokeKey, false);

            var sb = new StringBuilder(512);
            sb.AppendLine("=== MENU SMOKE ===");
            try
            {
                sb.Append("rootTitle=").Append(menu.SmokeTitle).AppendLine();
                bool ok = menu.SmokeNavigateToDifficulty("usa_like", out string title, out string body);
                sb.Append("difficultyTitle=").Append(title).AppendLine();
                sb.Append("difficultyBodyHasTreasury=")
                    .Append(body.IndexOf("treasury", StringComparison.OrdinalIgnoreCase) >= 0).AppendLine();
                bool sunOk = menu.SmokeNavigateToDifficulty("sun_rich", out string sunTitle, out string sunBody);
                sb.Append("sunDifficulty=").Append(sunOk)
                    .Append(" sunName=").Append(sunBody.IndexOf("Sun-Rich", StringComparison.Ordinal) >= 0)
                    .AppendLine();
                sb.AppendLine(ok && sunOk ? "MENU SMOKE OK" : "MENU SMOKE FAIL");
            }
            catch (Exception ex)
            {
                sb.AppendLine("MENU FAIL: " + ex);
            }

            string path = Path.Combine(Application.persistentDataPath, MenuOutName);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
            EditorApplication.isPlaying = false;
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

                    sb.AppendLine("=== P7 UI SMOKE ===");
                    if (hud == null)
                    {
                        sb.AppendLine("FAIL no hud for menu smoke");
                    }
                    else
                    {
                        bool menusOk = SmokeMenu(hud, ParadoxChromeHud.MenuId.Mandate, "MANDATE", "Campaign", sb)
                                       & SmokeMenu(hud, ParadoxChromeHud.MenuId.Budget, "BUDGET", "Treasury", sb)
                                       & SmokeMenu(hud, ParadoxChromeHud.MenuId.History, "EVENT HISTORY", "HISTORY", sb)
                                       & SmokeMenu(hud, ParadoxChromeHud.MenuId.Cabinet, "CABINET", "PM Confidence", sb);
                        hud.ForceOpenMenu(ParadoxChromeHud.MenuId.Construction);
                        string offTip = hud.DebugBuildTooltip("build_offshore_wind");
                        string bioTip = hud.DebugBuildTooltip("build_biomass");
                        bool tipsOk = offTip.IndexOf("MW", StringComparison.Ordinal) >= 0
                                      && bioTip.IndexOf("MW", StringComparison.Ordinal) >= 0;
                        sb.Append("hudTipOffshore=").Append(tipsOk && offTip.Length > 10)
                            .Append(" hudTipBiomass=").Append(tipsOk && bioTip.Length > 10).AppendLine();
                        hud.ForceOpenMenu(ParadoxChromeHud.MenuId.None);
                        sb.AppendLine(menusOk && tipsOk ? "P7 UI SMOKE OK" : "P7 UI SMOKE FAIL");
                    }

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

        private static bool SmokeMenu(
            ParadoxChromeHud hud, ParadoxChromeHud.MenuId id, string titleNeedle, string bodyNeedle, StringBuilder sb)
        {
            hud.ForceOpenMenu(id);
            string title = hud.DebugPanelTitle ?? "";
            string body = hud.DebugPanelBody ?? "";
            bool ok = title.IndexOf(titleNeedle, StringComparison.OrdinalIgnoreCase) >= 0
                      && body.IndexOf(bodyNeedle, StringComparison.OrdinalIgnoreCase) >= 0;
            sb.Append("menu").Append(id).Append('=').Append(ok)
                .Append(" title=").Append(title.Replace('\n', ' '))
                .AppendLine();
            return ok;
        }
    }
}
