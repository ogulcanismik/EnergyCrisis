using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using MinistryOfPower.Data;
using MinistryOfPower.Simulation;
using MinistryOfPower.UI;

namespace MinistryOfPower.Runtime
{
    /// <summary>
    /// Thin Unity adapter for grey-box Paradox desk: time, save/load, chrome HUD, day-night.
    /// </summary>
    public sealed class MinistryGameRunner : MonoBehaviour
    {
        public const int QuicksaveSlot = 0;
        public const int AutosaveEveryDays = 30;

        [SerializeField] private ScenarioDefinition startingScenario;
        [SerializeField] private ScenarioDefinition alternateScenario;
        [SerializeField] private ParadoxChromeHud paradoxHud;
        [SerializeField] private DayNightWeatherController dayNight;
        [SerializeField] private bool autoStart = true;

        private readonly GameSession _session = new GameSession();
        private float _hourAccumulator;
        private bool _eventsWired;
        private int _lastAutosaveDay = -1;
        private int _lastRenderedHour = -1;
        private PauseMenuOverlay _pauseMenu;
        private GameManager _gameManager;

        public GameSession Session => _session;

        private void Awake()
        {
            // Prefer explicit GameManager hub when present (Play from menu / greybox).
            if (GameManager.Instance == null && GetComponent<GameManager>() == null)
                GameManager.Ensure();

            Transform leftover = transform.Find("MinistryDeskCanvas");
            if (leftover != null)
            {
                Destroy(leftover.gameObject);
            }

            ResolvePresentation();
        }

        public void BindFromGameManager(GameManager gm)
        {
            _gameManager = gm;
            if (gm != null && gm.UI != null)
            {
                paradoxHud = gm.UI.EnsureHud();
                gm.UI.BindTuning(gm.Tuning);
            }

            ResolvePresentation();
        }

        public void AssignPresentation(ParadoxChromeHud hud, DayNightWeatherController weather)
        {
            if (hud != null) paradoxHud = hud;
            if (weather != null) dayNight = weather;
        }

        private void ResolvePresentation()
        {
            if (paradoxHud == null)
            {
                var ui = FindFirstObjectByType<UIManager>();
                if (ui != null) paradoxHud = ui.EnsureHud();
            }

            if (paradoxHud == null) paradoxHud = GetComponent<ParadoxChromeHud>();
            if (dayNight == null) dayNight = FindFirstObjectByType<DayNightWeatherController>();
            if (dayNight == null) dayNight = GetComponent<DayNightWeatherController>();

            if (paradoxHud == null) paradoxHud = gameObject.AddComponent<ParadoxChromeHud>();
            if (dayNight == null) dayNight = gameObject.AddComponent<DayNightWeatherController>();

            if (_gameManager == null) _gameManager = GameManager.Instance;
            if (_gameManager != null)
                paradoxHud.ApplyTuning(_gameManager.Tuning);
        }

        private void Start()
        {
            WireUi();
            dayNight.Bind(this);
            if (!autoStart) return;

            RunSetup setup = RunSetup.Instance;
            if (setup != null && setup.LoadSlot >= 0)
            {
                LoadFromSlot(setup.LoadSlot);
            }
            else if (setup != null)
            {
                StartNew(setup.ScenarioId, setup.Difficulty);
            }
            else
            {
                StartNew("usa_like", DifficultyId.Normal);
            }
        }

        private void Update()
        {
            if (WasF5Pressed() && _session.Clock != null && !_session.IsGameOver)
            {
                QuickSave("F5 quicksave");
            }

            if (_session.Clock == null || _session.IsGameOver || _session.AwaitingCrisisDecision)
            {
                return;
            }

            // Pause must fully halt the clock (no hour creep).
            if (_session.Clock.IsPaused)
            {
                return;
            }

            float secondsPerHour = GameClock.SecondsPerHour(_session.Clock.Speed);
            if (secondsPerHour <= 0f || float.IsInfinity(secondsPerHour)) return;

            _hourAccumulator += Time.unscaledDeltaTime;
            bool dayRolled = false;

            while (_hourAccumulator >= secondsPerHour)
            {
                _hourAccumulator -= secondsPerHour;
                float nextFrac = _session.Clock.DayFraction + (1f / GameClock.HoursPerDay);

                if (nextFrac >= 1f)
                {
                    _session.AdvanceDay();
                    MaybeAutosave();
                    nextFrac -= 1f;
                    dayRolled = true;
                }

                ApplyDayFraction(nextFrac);
            }

            int hour = (int)(_session.Clock.DayFraction * GameClock.HoursPerDay);
            if (dayRolled)
            {
                RefreshUi();
                _lastRenderedHour = hour;
            }
            else if (hour != _lastRenderedHour)
            {
                _lastRenderedHour = hour;
                paradoxHud?.RefreshTimeChrome();
            }
        }

        private void ApplyDayFraction(float dayFraction)
        {
            GameClock c = _session.Clock;
            c.Restore(c.Year, c.AbsoluteDay, c.DayIndex, c.QuarterIndex, dayFraction, c.Speed);
        }

        public void StepOneDay()
        {
            if (_session.AwaitingCrisisDecision || _session.IsGameOver) return;
            _session.SetSpeed(GameSpeed.Paused);
            _hourAccumulator = 0f;
            _session.AdvanceDay();
            MaybeAutosave();
            RefreshUi();
        }

        private void MaybeAutosave()
        {
            if (_session.Clock == null) return;
            int day = _session.Clock.AbsoluteDay;
            if (day <= 0 || day % AutosaveEveryDays != 0) return;
            if (day == _lastAutosaveDay) return;
            _lastAutosaveDay = day;
            QuickSave("Autosave (every " + AutosaveEveryDays + "d)");
        }

        private void QuickSave(string reason)
        {
            SaveGameSystem.TrySave(QuicksaveSlot, GameSessionSaveMapper.Capture(_session, QuicksaveSlot), out string msg);
            PushLog(reason + ": " + msg);
            paradoxHud?.FlashQuicksave();
            HandleTip("first_save");
            RefreshUi();
        }

        private void WireUi()
        {
            if (paradoxHud == null) return;

            paradoxHud.Bind(
                () => _session,
                () => { _session.SetSpeed(GameSpeed.Paused); RefreshUi(); },
                () => { _session.SetSpeed(GameSpeed.Slow); RefreshUi(); },
                () => { _session.SetSpeed(GameSpeed.Normal); RefreshUi(); },
                () => { _session.SetSpeed(GameSpeed.Fast); RefreshUi(); },
                () => { _session.SetSpeed(GameSpeed.VeryFast); RefreshUi(); },
                StepOneDay,
                id =>
                {
                    _session.TryStartBuild(id, out string msg);
                    PushLog(msg);
                    RefreshUi();
                },
                orderId =>
                {
                    _session.TryCancelBuild(orderId, out string msg);
                    PushLog(msg);
                    RefreshUi();
                },
                plantId =>
                {
                    _session.TryRetirePlant(string.IsNullOrEmpty(plantId) ? null : plantId, out string msg);
                    PushLog(msg);
                    RefreshUi();
                },
                choice =>
                {
                    if (_session.TryResolveCrisis(choice, out string msg))
                    {
                        PushLog(msg);
                        paradoxHud.HideEvent();
                    }

                    RefreshUi();
                },
                choice =>
                {
                    _session.TryCabinetAction(choice, out string msg);
                    PushLog(msg);
                    RefreshUi();
                },
                () =>
                {
                    _session.TryBuyEmergencyImport(out string msg);
                    PushLog(msg);
                    RefreshUi();
                },
                () =>
                {
                    _session.TrySignPrivateReserveDeal(out string msg);
                    PushLog(msg);
                    RefreshUi();
                },
                () => SceneManager.LoadScene("MainMenu"));

            _pauseMenu = PauseMenuOverlay.Ensure(transform);
            var uiMgr = FindFirstObjectByType<UIManager>();
            if (uiMgr != null)
                uiMgr.EnsurePauseMenu(transform);
            _pauseMenu.Bind(
                () => _session,
                () =>
                {
                    _session.SetSpeed(GameSpeed.Paused);
                    RefreshUi();
                },
                speed =>
                {
                    _session.SetSpeed(speed);
                    RefreshUi();
                },
                slot =>
                {
                    SaveGameSystem.TrySave(slot, GameSessionSaveMapper.Capture(_session, slot), out string msg);
                    PushLog(slot == QuicksaveSlot ? "Quicksave: " + msg : msg);
                    if (slot == QuicksaveSlot) paradoxHud?.FlashQuicksave();
                    HandleTip("first_save");
                    RefreshUi();
                },
                LoadFromSlot,
                () => SceneManager.LoadScene("MainMenu"),
                QuitGame);

            if (!_eventsWired)
            {
                _session.EventRaised += evt =>
                {
                    paradoxHud.ShowEvent(evt);
                    RefreshUi();
                };
                _session.LogEmitted += PushLog;
                _session.GameOver += RefreshUi;
                _session.TipRaised += HandleTip;
                _session.YearEnded += report =>
                {
                    paradoxHud?.ShowYearReport(report);
                    RefreshUi();
                };
                _eventsWired = true;
            }
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandleTip(string beat)
        {
            switch (beat)
            {
                case "first_build":
                    if (!TutorialBeats.SeenBuild)
                    {
                        TutorialBeats.SeenBuild = true;
                        paradoxHud?.ShowTip(TutorialBeats.TipBuild);
                    }
                    break;
                case "first_event":
                    if (!TutorialBeats.SeenEvent)
                    {
                        TutorialBeats.SeenEvent = true;
                        paradoxHud?.ShowTip(TutorialBeats.TipEvent);
                    }
                    break;
                case "first_winter":
                    if (!TutorialBeats.SeenWinter)
                    {
                        TutorialBeats.SeenWinter = true;
                        paradoxHud?.ShowTip(TutorialBeats.TipWinter);
                    }
                    break;
                case "first_save":
                    if (!TutorialBeats.SeenSave)
                    {
                        TutorialBeats.SeenSave = true;
                        paradoxHud?.ShowTip(TutorialBeats.TipSave);
                    }
                    break;
            }
        }

        public void StartNew(string scenarioId, DifficultyId difficulty)
        {
            PrototypeContentFactory.TryCreateById(scenarioId, out ScenarioConfig config, out List<BuildDefinitionConfig> builds);

            if (startingScenario != null && (scenarioId == startingScenario.Id || scenarioId == "usa_like"))
            {
                var fromAsset = startingScenario.ToConfig();
                var assetBuilds = startingScenario.ToBuildConfigs();
                if (fromAsset.Id == scenarioId || scenarioId == "usa_like")
                {
                    config = MergeScenarioParity(config, fromAsset);
                    // D1: never replace 10-build factory catalog with a partial SO list.
                    builds = PrototypeContentFactory.MergeCatalog(builds, assetBuilds);
                }
            }

            if (alternateScenario != null && scenarioId == alternateScenario.Id)
            {
                config = MergeScenarioParity(config, alternateScenario.ToConfig());
                var assetBuilds = alternateScenario.ToBuildConfigs();
                builds = PrototypeContentFactory.MergeCatalog(builds, assetBuilds);
            }

            if (builds == null || builds.Count < 5)
            {
                builds = PrototypeContentFactory.CreateFullCatalog();
            }

            _hourAccumulator = 0f;
            _lastAutosaveDay = -1;
            _lastRenderedHour = -1;
            _session.Start(config, builds, DifficultyConfig.Create(difficulty));
            GameSpeed startSpeed = GameSettings.DefaultSpeed;
            if (startSpeed == GameSpeed.Paused) startSpeed = GameSpeed.Normal;
            _session.SetSpeed(GameSettings.HelpSeen ? startSpeed : GameSpeed.Paused);
            PushLog($"Difficulty {_session.Difficulty.DisplayName}: {_session.Difficulty.Blurb}");
            PushLog($"Event frequency ×{_session.Difficulty.EventFrequencyMultiplier:0.00} · treasury {_session.Budget:0}");
            paradoxHud?.HideEvent();
            paradoxHud?.InvalidateTooltipCache();
            RefreshUi();
        }

        /// <summary>
        /// SO config wins authored numbers; factory fills LobbyRetire / blurb / campaign years when SO left defaults.
        /// </summary>
        private static ScenarioConfig MergeScenarioParity(ScenarioConfig factory, ScenarioConfig so)
        {
            if (so == null) return factory;
            if (factory == null) return so;

            if (string.IsNullOrEmpty(so.DifferentiationBlurb))
                so.DifferentiationBlurb = factory.DifferentiationBlurb;
            if (Mathf.Abs(so.LobbyRetireMultiplier - 1f) < 0.001f && Mathf.Abs(factory.LobbyRetireMultiplier - 1f) > 0.001f)
                so.LobbyRetireMultiplier = factory.LobbyRetireMultiplier;
            if (so.CampaignYears <= 0)
                so.CampaignYears = factory.CampaignYears > 0 ? factory.CampaignYears : MandateTracker.DefaultCampaignYears;
            return so;
        }

        public void LoadFromSlot(int slot)
        {
            if (!SaveGameSystem.TryLoad(slot, out GameSaveData data, out string message))
            {
                PushLog(message);
                StartNew("usa_like", DifficultyId.Normal);
                return;
            }

            PrototypeContentFactory.TryCreateById(data.ScenarioId, out _, out List<BuildDefinitionConfig> builds);
            if (builds.Count < 5) builds = PrototypeContentFactory.CreateFullCatalog();
            GameSessionSaveMapper.Apply(_session, data, builds);
            _session.SetSpeed(GameSpeed.Paused);
            _hourAccumulator = 0f;
            _lastRenderedHour = -1;
            _lastAutosaveDay = _session.Clock != null ? _session.Clock.AbsoluteDay : -1;
            PushLog(message);
            paradoxHud?.HideEvent();
            paradoxHud?.InvalidateTooltipCache();
            RefreshUi();
        }

        private void PushLog(string line)
        {
            // Single log owner: ParadoxChromeHud (UI audit P1-3).
            paradoxHud?.PushLog(line);
        }

        private void RefreshUi()
        {
            paradoxHud?.Render();
        }

        private static bool WasF5Pressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.f5Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F5);
#else
            try { return Input.GetKeyDown(KeyCode.F5); }
            catch (System.InvalidOperationException) { return false; }
#endif
        }

#if UNITY_EDITOR
        public void EditorAssignScenarios(ScenarioDefinition primary, ScenarioDefinition alternate)
        {
            startingScenario = primary;
            alternateScenario = alternate;
        }
#endif
    }
}
