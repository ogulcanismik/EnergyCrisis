using System;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using MinistryOfPower.Runtime;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.UI
{
    /// <summary>
    /// System ESC pause overlay (dimmed centered menu). Desk chrome stays underneath; game clock pauses while open.
    /// </summary>
    public sealed class PauseMenuOverlay : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _root;
        private GameObject _mainPanel;
        private GameObject _settingsPanel;
        private GameObject _resignConfirm;
        private Text _settingsBody;
        private SaveSlotModal _slotModal;

        private Func<GameSession> _session;
        private Action _onClockPaused;
        private Action<GameSpeed> _onClockResume;
        private Action<int> _onSaveSlot;
        private Action<int> _onLoadSlot;
        private Action _onResign;
        private Action _onQuit;

        private bool _open;
        private GameSpeed _speedBeforePause = GameSpeed.Normal;
        private bool _ignoreEscUntilRelease;

        public bool IsOpen => _open;

        public static PauseMenuOverlay Ensure(Transform host)
        {
            var existing = host.GetComponent<PauseMenuOverlay>();
            if (existing != null) return existing;
            return host.gameObject.AddComponent<PauseMenuOverlay>();
        }

        public void Bind(
            Func<GameSession> session,
            Action onClockPaused,
            Action<GameSpeed> onClockResume,
            Action<int> onSaveSlot,
            Action<int> onLoadSlot,
            Action onResign,
            Action onQuit)
        {
            _session = session;
            _onClockPaused = onClockPaused;
            _onClockResume = onClockResume;
            _onSaveSlot = onSaveSlot;
            _onLoadSlot = onLoadSlot;
            _onResign = onResign;
            _onQuit = onQuit;
            EnsureBuilt();
        }

        public void Open()
        {
            if (_open) return;
            EnsureBuilt();
            GameSession s = _session?.Invoke();
            if (s?.Clock != null)
                _speedBeforePause = s.Clock.Speed == GameSpeed.Paused ? GameSpeed.Normal : s.Clock.Speed;
            else
                _speedBeforePause = GameSpeed.Normal;

            _onClockPaused?.Invoke();
            ShowMain();
            _root.SetActive(true);
            _open = true;
            _ignoreEscUntilRelease = true;
        }

        public void Close(bool resumeClock = true)
        {
            if (!_open && (_root == null || !_root.activeSelf)) return;
            if (_slotModal != null && _slotModal.IsOpen) _slotModal.Close();
            if (_resignConfirm != null) _resignConfirm.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            if (_mainPanel != null) _mainPanel.SetActive(true);
            if (_root != null) _root.SetActive(false);
            _open = false;
            if (resumeClock)
            {
                GameSpeed resume = _speedBeforePause == GameSpeed.Paused
                    ? GameSpeed.Normal
                    : _speedBeforePause;
                _onClockResume?.Invoke(resume);
            }
        }

        public void Toggle()
        {
            if (_open) Close(true);
            else Open();
        }

        private void Update()
        {
            if (!WasEscPressed()) return;
            if (_ignoreEscUntilRelease)
            {
                if (!IsEscHeld()) _ignoreEscUntilRelease = false;
                return;
            }

            if (_slotModal != null && _slotModal.IsOpen)
            {
                _slotModal.Close();
                return;
            }

            if (_resignConfirm != null && _resignConfirm.activeSelf)
            {
                _resignConfirm.SetActive(false);
                ShowMain();
                return;
            }

            if (_settingsPanel != null && _settingsPanel.activeSelf)
            {
                ShowMain();
                return;
            }

            Toggle();
        }

        private void EnsureBuilt()
        {
            if (_canvas != null) return;

            UiFactory.EnsureEventSystem(transform);
            var canvasGo = new GameObject("PauseMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 80;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            Color dim = new Color(0f, 0f, 0f, 0.65f);
            Color panel = UiFactory.Hex("241C14");
            Color accent = UiFactory.Hex("C4A35A");
            Color paper = UiFactory.Hex("E7DCC8");

            var backdrop = UiFactory.Panel(canvasGo.transform, "Backdrop", Vector2.zero, Vector2.one, dim);
            _root = backdrop.gameObject;
            var backBtn = backdrop.gameObject.AddComponent<Button>();
            backBtn.transition = Selectable.Transition.None;
            backBtn.onClick.AddListener(() => Close(true));

            var card = UiFactory.Panel(_root.transform, "MenuCard",
                new Vector2(0.35f, 0.18f), new Vector2(0.65f, 0.82f), panel);
            card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

            _mainPanel = card.gameObject;
            UiFactory.Label(card.transform, "Title", accent, 28, FontStyle.Bold,
                new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.96f), TextAnchor.MiddleCenter).text = "PAUSED";

            float y = 0.82f;
            AddMenuButton(card.transform, "Resume", "Resume", ref y, () => Close(true));
            AddMenuButton(card.transform, "Save", "Save Game", ref y, OpenSave);
            AddMenuButton(card.transform, "Load", "Load Game", ref y, OpenLoad);
            AddMenuButton(card.transform, "Settings", "Settings", ref y, ShowSettings);
            AddMenuButton(card.transform, "Resign", "Resign / Main Menu", ref y, ShowResignConfirm);
            AddMenuButton(card.transform, "Quit", "Quit", ref y, () => _onQuit?.Invoke());

            BuildSettingsPanel(_root.transform, accent, paper, panel);
            BuildResignConfirm(_root.transform, accent, paper);
            _slotModal = SaveSlotModal.Ensure(_root.transform);
            _root.SetActive(false);
        }

        private void BuildSettingsPanel(Transform parent, Color accent, Color paper, Color panel)
        {
            var settings = UiFactory.Panel(parent, "SettingsCard",
                new Vector2(0.32f, 0.18f), new Vector2(0.68f, 0.82f), panel);
            _settingsPanel = settings.gameObject;
            settings.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            UiFactory.Label(settings.transform, "ST", accent, 24, FontStyle.Bold,
                new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.96f), TextAnchor.MiddleCenter).text = "SETTINGS";
            _settingsBody = UiFactory.Label(settings.transform, "SB", paper, 13, FontStyle.Normal,
                new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.84f), TextAnchor.UpperCenter);
            float y = 0.68f;
            AddMenuButton(settings.transform, "TipToggle", "Tooltips", ref y, () =>
            {
                GameSettings.ShowTooltips = !GameSettings.ShowTooltips;
                RefreshSettings();
            }, 0.1f);
            AddMenuButton(settings.transform, "VolCycle", "Master volume", ref y, () =>
            {
                float v = GameSettings.MasterVolume + 0.2f;
                if (v > 1.01f) v = 0f;
                GameSettings.MasterVolume = v;
                RefreshSettings();
            }, 0.1f);
            AddMenuButton(settings.transform, "SpeedCycle", "Default speed", ref y, () =>
            {
                int next = ((int)GameSettings.DefaultSpeed + 1) % 5;
                if (next == 0) next = 1;
                GameSettings.DefaultSpeed = (GameSpeed)next;
                RefreshSettings();
            }, 0.1f);
            AddMenuButton(settings.transform, "BackSettings", "Back", ref y, ShowMain, 0.1f);
            _settingsPanel.SetActive(false);
        }

        private void BuildResignConfirm(Transform parent, Color accent, Color paper)
        {
            var box = UiFactory.Panel(parent, "ResignConfirm",
                new Vector2(0.32f, 0.34f), new Vector2(0.68f, 0.66f), UiFactory.Hex("2B2118"));
            _resignConfirm = box.gameObject;
            box.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            UiFactory.Label(box.transform, "RT", accent, 20, FontStyle.Bold,
                new Vector2(0.06f, 0.7f), new Vector2(0.94f, 0.92f), TextAnchor.MiddleCenter).text = "RESIGN?";
            UiFactory.Label(box.transform, "RB", paper, 14, FontStyle.Normal,
                new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.7f), TextAnchor.UpperCenter).text =
                "Return to the main menu.\nUnsaved progress since last save is lost.";
            UiFactory.Button(box.transform, "YesResign", "Resign",
                new Vector2(0.08f, 0.08f), new Vector2(0.48f, 0.28f),
                () =>
                {
                    Close(false);
                    _onResign?.Invoke();
                }, UiFactory.Hex("6B3030"), paper, 14);
            UiFactory.Button(box.transform, "NoResign", "Cancel",
                new Vector2(0.52f, 0.08f), new Vector2(0.92f, 0.28f),
                ShowMain, UiFactory.Hex("3A2E22"), paper, 14);
            _resignConfirm.SetActive(false);
        }

        private void AddMenuButton(Transform parent, string name, string label, ref float yTop, Action action, float height = 0.1f)
        {
            float yBot = yTop - height;
            UiFactory.Button(parent, name, label,
                new Vector2(0.12f, yBot), new Vector2(0.88f, yTop),
                action, UiFactory.Hex("3A2E22"), UiFactory.Hex("E7DCC8"), 16);
            yTop = yBot - 0.025f;
        }

        private void ShowMain()
        {
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            if (_resignConfirm != null) _resignConfirm.SetActive(false);
            if (_mainPanel != null) _mainPanel.SetActive(true);
        }

        private void ShowSettings()
        {
            if (_mainPanel != null) _mainPanel.SetActive(false);
            if (_resignConfirm != null) _resignConfirm.SetActive(false);
            RefreshSettings();
            if (_settingsPanel != null) _settingsPanel.SetActive(true);
        }

        private void RefreshSettings()
        {
            if (_settingsBody == null) return;
            _settingsBody.text =
                "Tooltips " + (GameSettings.ShowTooltips ? "ON" : "OFF") +
                " · Volume " + GameSettings.MasterVolume.ToString("0.0") +
                " · Default speed " + GameSettings.DefaultSpeed;
        }

        private void ShowResignConfirm()
        {
            if (_mainPanel != null) _mainPanel.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            if (_resignConfirm != null) _resignConfirm.SetActive(true);
        }

        private void OpenSave()
        {
            _slotModal.Open(SaveSlotModalMode.Save, slot => _onSaveSlot?.Invoke(slot));
        }

        private void OpenLoad()
        {
            _slotModal.Open(SaveSlotModalMode.Load, slot =>
            {
                _onLoadSlot?.Invoke(slot);
                Close(false);
            });
        }

        private static bool WasEscPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                return Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
            try { return Input.GetKeyDown(KeyCode.Escape); }
            catch { return false; }
        }

        private static bool IsEscHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                return Keyboard.current.escapeKey.isPressed;
#endif
            try { return Input.GetKey(KeyCode.Escape); }
            catch { return false; }
        }
    }
}
