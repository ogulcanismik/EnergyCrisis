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
        private float _dayAccumulator;
        private readonly List<string> _logLines = new List<string>(48);
        private bool _eventsWired;
        private int _lastAutosaveDay = -1;

        public GameSession Session => _session;

        private void Awake()
        {
            // Guard against stale runtime leftover from the deleted diegetic desk.
            Transform leftover = transform.Find("MinistryDeskCanvas");
            if (leftover != null)
            {
                Destroy(leftover.gameObject);
            }

            if (paradoxHud == null) paradoxHud = GetComponent<ParadoxChromeHud>();
            if (dayNight == null) dayNight = GetComponent<DayNightWeatherController>();

            if (paradoxHud == null) paradoxHud = gameObject.AddComponent<ParadoxChromeHud>();
            if (dayNight == null) dayNight = gameObject.AddComponent<DayNightWeatherController>();
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

            if (_session.Clock.IsPaused)
            {
                return;
            }

            float secondsPerDay = GameClock.SecondsPerDay(_session.Clock.Speed);
            _dayAccumulator += Time.unscaledDeltaTime;

            while (_dayAccumulator >= secondsPerDay)
            {
                _dayAccumulator -= secondsPerDay;
                _session.AdvanceDay();
                MaybeAutosave();
                RefreshUi();
            }

            float visualFrac = Mathf.Repeat(0.32f + (_dayAccumulator / secondsPerDay), 1f);
            _session.Clock.Restore(
                _session.Clock.Year,
                _session.Clock.AbsoluteDay,
                _session.Clock.DayIndex,
                _session.Clock.QuarterIndex,
                visualFrac,
                _session.Clock.Speed);
        }

        public void StepOneDay()
        {
            if (_session.AwaitingCrisisDecision || _session.IsGameOver) return;
            _session.SetSpeed(GameSpeed.Paused);
            _dayAccumulator = 0f;
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
            SaveGameSystem.TrySave(QuicksaveSlot, _session.CaptureSave(QuicksaveSlot), out string msg);
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
                slot =>
                {
                    SaveGameSystem.TrySave(slot, _session.CaptureSave(slot), out string msg);
                    PushLog(slot == QuicksaveSlot ? "Quicksave: " + msg : msg);
                    if (slot == QuicksaveSlot) paradoxHud?.FlashQuicksave();
                    HandleTip("first_save");
                    RefreshUi();
                },
                LoadFromSlot,
                () => SceneManager.LoadScene("MainMenu"));

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
                    config = fromAsset;
                    if (assetBuilds.Count > 0) builds = assetBuilds;
                }
            }

            if (alternateScenario != null && scenarioId == alternateScenario.Id)
            {
                config = alternateScenario.ToConfig();
                var assetBuilds = alternateScenario.ToBuildConfigs();
                if (assetBuilds.Count > 0) builds = assetBuilds;
            }

            if (builds.Count < 5)
            {
                builds = PrototypeContentFactory.CreateFullCatalog();
            }

            _logLines.Clear();
            _dayAccumulator = 0f;
            _lastAutosaveDay = -1;
            _session.Start(config, builds, DifficultyConfig.Create(difficulty));
            GameSpeed startSpeed = GameSettings.DefaultSpeed;
            if (startSpeed == GameSpeed.Paused) startSpeed = GameSpeed.Normal;
            _session.SetSpeed(GameSettings.HelpSeen ? startSpeed : GameSpeed.Paused);
            PushLog($"Difficulty {_session.Difficulty.DisplayName}: {_session.Difficulty.Blurb}");
            PushLog($"Event frequency ×{_session.Difficulty.EventFrequencyMultiplier:0.00} · treasury {_session.Budget:0}");
            paradoxHud?.HideEvent();
            RefreshUi();
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
            _session.LoadFromSave(data, builds);
            _session.SetSpeed(GameSpeed.Paused);
            _dayAccumulator = 0f;
            _lastAutosaveDay = _session.Clock != null ? _session.Clock.AbsoluteDay : -1;
            PushLog(message);
            paradoxHud?.HideEvent();
            RefreshUi();
        }

        private void PushLog(string line)
        {
            _logLines.Add(line);
            if (_logLines.Count > 40) _logLines.RemoveAt(0);
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
