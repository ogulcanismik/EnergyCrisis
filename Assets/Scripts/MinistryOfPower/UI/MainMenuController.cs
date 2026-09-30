using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MinistryOfPower.Runtime;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.UI
{
    /// <summary>
    /// Main menu: New Game → Scenario → Difficulty; Load; Settings; Quit.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string GameSceneName = "MinistryDesk";

        private enum Screen { Root, Scenario, Difficulty, Load, Settings }

        private Screen _screen = Screen.Root;
        private string _scenarioId = "usa_like";
        private DifficultyId _difficulty = DifficultyId.Normal;
        private Text _title;
        private Text _body;
        private Transform _buttonRoot;

        private void Start()
        {
            BuildUi();
            ShowRoot();
        }

        private void BuildUi()
        {
            UiFactory.EnsureEventSystem(transform);
            var canvasGo = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            UiFactory.Panel(canvasGo.transform, "Bg", Vector2.zero, Vector2.one, UiFactory.Hex("1A1410"));
            UiFactory.Panel(canvasGo.transform, "Panel", new Vector2(0.28f, 0.12f), new Vector2(0.72f, 0.88f), UiFactory.Hex("2B2118"));
            Transform panel = canvasGo.transform.Find("Panel");
            _title = UiFactory.Label(panel, "Title", UiFactory.Hex("C4A35A"), 36, FontStyle.Bold,
                new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.96f), TextAnchor.MiddleCenter);
            _body = UiFactory.Label(panel, "Body", UiFactory.Hex("E7DCC8"), 16, FontStyle.Normal,
                new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.82f), TextAnchor.UpperCenter);
            var btnHost = UiFactory.Panel(panel, "Buttons", new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.6f), new Color(0, 0, 0, 0.15f));
            _buttonRoot = btnHost.transform;
            TooltipService.Ensure(canvasGo.transform);
        }

        private void ClearButtons()
        {
            for (int i = _buttonRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_buttonRoot.GetChild(i).gameObject);
            }
        }

        private void ShowRoot()
        {
            _screen = Screen.Root;
            _title.text = "THE MINISTRY OF POWER";
            _body.text = "Grey-box Paradox desk — New Game, Load, Settings, or Quit.";
            ClearButtons();
            float y = 0.88f;
            int continueSlot = SaveGameSystem.FindNewestSlot();
            if (continueSlot >= 0)
            {
                SaveSlotMeta meta = SaveGameSystem.PeekSlot(continueSlot);
                AddBtn($"Continue — {meta.ScenarioName} · {meta.DifficultyName} · {meta.DateLabel}",
                    () => LoadSlot(continueSlot), ref y, 0.14f);
            }

            AddBtn("New Game", ShowScenario, ref y);
            AddBtn(SaveGameSystem.AnySaveExists() ? "Load Game…" : "Load Game (empty)", ShowLoad, ref y);
            AddBtn("Settings", ShowSettings, ref y);
            AddBtn("Quit", QuitGame, ref y);
        }

        private void ShowScenario()
        {
            _screen = Screen.Scenario;
            _title.text = "SELECT SCENARIO";
            PrototypeContentFactory.CreateUsaLike(out ScenarioConfig fed, out _);
            PrototypeContentFactory.CreateSunRich(out ScenarioConfig sun, out _);
            _body.text = "Hands differ hard — lobby, solar CF, imports, retire pain. Difficulty next.";
            ClearButtons();
            float y = 0.88f;
            AddBtn($"Federal High Budget\n{fed.DifferentiationBlurb}", () => { _scenarioId = "usa_like"; ShowDifficulty(); }, ref y, 0.22f);
            AddBtn($"Sun-Rich Low Budget\n{sun.DifferentiationBlurb}", () => { _scenarioId = "sun_rich"; ShowDifficulty(); }, ref y, 0.22f);
            AddBtn("Back", ShowRoot, ref y);
        }

        private void ShowDifficulty()
        {
            _screen = Screen.Difficulty;
            _title.text = "SELECT DIFFICULTY";
            PrototypeContentFactory.TryCreateById(_scenarioId, out ScenarioConfig sc, out _);
            float baseBudget = sc.StartingBudget;
            DifficultyConfig easy = DifficultyConfig.Create(DifficultyId.Easy);
            DifficultyConfig normal = DifficultyConfig.Create(DifficultyId.Normal);
            DifficultyConfig hard = DifficultyConfig.Create(DifficultyId.Hard);
            _body.text = $"{sc.DisplayName}: {sc.DifferentiationBlurb}\nDifficulty still scales treasury / shocks / lobby drain.";
            ClearButtons();
            float y = 0.88f;
            AddBtn($"EASY — treasury ~{baseBudget * easy.BudgetMultiplier:0}\n{easy.Blurb}", () => StartNew(DifficultyId.Easy), ref y, 0.18f);
            AddBtn($"NORMAL — treasury ~{baseBudget * normal.BudgetMultiplier:0}\n{normal.Blurb}", () => StartNew(DifficultyId.Normal), ref y, 0.18f);
            AddBtn($"HARD — treasury ~{baseBudget * hard.BudgetMultiplier:0}\n{hard.Blurb}", () => StartNew(DifficultyId.Hard), ref y, 0.18f);
            AddBtn("Back", ShowScenario, ref y, 0.1f);
        }

        private void ShowLoad()
        {
            _screen = Screen.Load;
            _title.text = "LOAD GAME";
            _body.text = "Choose a save slot.";
            ClearButtons();
            float y = 0.9f;
            SaveSlotMeta[] slots = SaveGameSystem.ListSlots();
            for (int i = 0; i < slots.Length; i++)
            {
                SaveSlotMeta s = slots[i];
                string label = s.Occupied
                    ? $"Slot {i + 1}: {s.ScenarioName} · {s.DifficultyName} · {s.DateLabel}"
                    : $"Slot {i + 1}: empty";
                int slot = i;
                AddBtn(label, s.Occupied ? () => LoadSlot(slot) : () => { }, ref y, 0.1f);
            }

            AddBtn("Back", ShowRoot, ref y, 0.1f);
        }

        private void ShowSettings()
        {
            _screen = Screen.Settings;
            _title.text = "SETTINGS";
            RefreshSettingsBody();
            ClearButtons();
            float y = 0.85f;
            AddBtn(GameSettings.ShowTooltips ? "Tooltips: ON" : "Tooltips: OFF",
                () => { GameSettings.ShowTooltips = !GameSettings.ShowTooltips; ShowSettings(); }, ref y, 0.12f);
            AddBtn($"Master volume: {GameSettings.MasterVolume:0.0} (stub cycle)",
                () =>
                {
                    float v = GameSettings.MasterVolume + 0.2f;
                    if (v > 1.01f) v = 0f;
                    GameSettings.MasterVolume = v;
                    ShowSettings();
                }, ref y, 0.12f);
            AddBtn($"Default game speed: {GameSettings.DefaultSpeed}",
                () =>
                {
                    int next = ((int)GameSettings.DefaultSpeed + 1) % 5;
                    if (next == 0) next = 1; // skip paused as default
                    GameSettings.DefaultSpeed = (GameSpeed)next;
                    ShowSettings();
                }, ref y, 0.12f);
            AddBtn("Reset first-run help", () => { GameSettings.HelpSeen = false; ShowSettings(); }, ref y, 0.12f);
            AddBtn("Reset tutorial beats", () => { TutorialBeats.ResetAll(); ShowSettings(); }, ref y, 0.12f);
            AddBtn("Back", ShowRoot, ref y, 0.12f);
        }

        private void RefreshSettingsBody()
        {
            _body.text =
                $"Tooltips {(GameSettings.ShowTooltips ? "shown" : "hidden")} · " +
                $"Volume {GameSettings.MasterVolume:0.0} · Default speed {GameSettings.DefaultSpeed}";
        }

        private void AddBtn(string label, Action action, ref float yTop, float height = 0.12f)
        {
            float yBot = yTop - height;
            UiFactory.Button(_buttonRoot, "B_" + label.GetHashCode(), label,
                new Vector2(0.04f, yBot), new Vector2(0.96f, yTop),
                action, UiFactory.Hex("3A2E22"), UiFactory.Hex("E7DCC8"), 15);
            yTop = yBot - 0.02f;
        }

        private void StartNew(DifficultyId difficulty)
        {
            _difficulty = difficulty;
            RunSetup.Ensure().ConfigureNewGame(_scenarioId, _difficulty);
            SceneManager.LoadScene(GameSceneName);
        }

        private void LoadSlot(int slot)
        {
            RunSetup.Ensure().ConfigureLoad(slot);
            SceneManager.LoadScene(GameSceneName);
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
