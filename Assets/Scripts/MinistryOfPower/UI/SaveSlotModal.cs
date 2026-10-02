using System;
using UnityEngine;
using UnityEngine.UI;
using MinistryOfPower.Runtime;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.UI
{
    public enum SaveSlotModalMode
    {
        Save,
        Load
    }

    /// <summary>
    /// Standard PC slot picker (save or load). Reused by ESC pause and main menu.
    /// </summary>
    public sealed class SaveSlotModal : MonoBehaviour
    {
        private GameObject _root;
        private GameObject _listPanel;
        private GameObject _confirmPanel;
        private Text _title;
        private Text _body;
        private Text _confirmTitle;
        private Text _confirmBody;
        private Transform _slotRoot;
        private SaveSlotModalMode _mode;
        private Action<int> _onChosen;
        private Action _onClosed;
        private int _pendingOverwriteSlot = -1;

        public bool IsOpen => _root != null && _root.activeSelf;

        public static SaveSlotModal Ensure(Transform parent)
        {
            var existing = parent.GetComponentInChildren<SaveSlotModal>(true);
            if (existing != null) return existing;
            var go = new GameObject("SaveSlotModal", typeof(RectTransform), typeof(SaveSlotModal));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go.GetComponent<SaveSlotModal>();
        }

        public void Open(SaveSlotModalMode mode, Action<int> onChosen, Action onClosed = null)
        {
            EnsureBuilt();
            _mode = mode;
            _onChosen = onChosen;
            _onClosed = onClosed;
            _pendingOverwriteSlot = -1;
            _confirmPanel.SetActive(false);
            _listPanel.SetActive(true);
            _title.text = mode == SaveSlotModalMode.Save ? "SAVE GAME" : "LOAD GAME";
            _body.text = mode == SaveSlotModalMode.Save
                ? "Choose a slot. Occupied slots ask before overwrite. Slot 1 is Quick Save (F5 / autosave)."
                : "Choose a slot to load.";
            RebuildSlots();
            _root.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
            _pendingOverwriteSlot = -1;
            Action closed = _onClosed;
            _onClosed = null;
            _onChosen = null;
            closed?.Invoke();
        }

        private void EnsureBuilt()
        {
            if (_root != null) return;

            Color paper = UiFactory.Hex("E7DCC8");
            Color accent = UiFactory.Hex("C4A35A");
            Color panel = UiFactory.Hex("241C14");
            Color dim = new Color(0f, 0f, 0f, 0.72f);

            var backdrop = UiFactory.Panel(transform, "Backdrop", Vector2.zero, Vector2.one, dim);
            _root = backdrop.gameObject;
            var backBtn = backdrop.gameObject.AddComponent<Button>();
            backBtn.transition = Selectable.Transition.None;
            backBtn.onClick.AddListener(Close);

            var card = UiFactory.Panel(_root.transform, "Card", new Vector2(0.28f, 0.12f), new Vector2(0.72f, 0.88f), panel);
            // Swallow clicks on the card so backdrop-close does not fire.
            card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

            _listPanel = card.gameObject;
            _title = UiFactory.Label(card.transform, "Title", accent, 26, FontStyle.Bold,
                new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.96f), TextAnchor.MiddleCenter);
            _body = UiFactory.Label(card.transform, "Body", paper, 13, FontStyle.Normal,
                new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.88f), TextAnchor.UpperCenter);

            var slotHost = UiFactory.Panel(card.transform, "Slots", new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.76f),
                new Color(0f, 0f, 0f, 0.2f));
            _slotRoot = slotHost.transform;

            UiFactory.Button(card.transform, "Cancel", "Cancel",
                new Vector2(0.3f, 0.04f), new Vector2(0.7f, 0.12f),
                Close, UiFactory.Hex("3A2E22"), paper, 14);

            var confirm = UiFactory.Panel(_root.transform, "OverwriteConfirm",
                new Vector2(0.32f, 0.32f), new Vector2(0.68f, 0.68f), UiFactory.Hex("2B2118"));
            _confirmPanel = confirm.gameObject;
            confirm.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            _confirmTitle = UiFactory.Label(confirm.transform, "CT", accent, 20, FontStyle.Bold,
                new Vector2(0.06f, 0.72f), new Vector2(0.94f, 0.92f), TextAnchor.MiddleCenter);
            _confirmBody = UiFactory.Label(confirm.transform, "CB", paper, 14, FontStyle.Normal,
                new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.72f), TextAnchor.UpperCenter);
            UiFactory.Button(confirm.transform, "Yes", "Overwrite",
                new Vector2(0.08f, 0.08f), new Vector2(0.48f, 0.28f),
                ConfirmOverwrite, UiFactory.Hex("6B3030"), paper, 14);
            UiFactory.Button(confirm.transform, "No", "Cancel",
                new Vector2(0.52f, 0.08f), new Vector2(0.92f, 0.28f),
                CancelOverwrite, UiFactory.Hex("3A2E22"), paper, 14);
            _confirmPanel.SetActive(false);
            _root.SetActive(false);
        }

        private void RebuildSlots()
        {
            for (int i = _slotRoot.childCount - 1; i >= 0; i--)
                Destroy(_slotRoot.GetChild(i).gameObject);

            SaveSlotMeta[] slots = SaveGameSystem.ListSlots();
            float y = 0.98f;
            float row = 0.17f;
            for (int i = 0; i < slots.Length; i++)
            {
                SaveSlotMeta meta = slots[i];
                int slot = i;
                float y0 = y - row;
                string label = FormatSlotLabel(meta);
                bool interactive = _mode == SaveSlotModalMode.Save || meta.Occupied;
                Color bg = interactive ? UiFactory.Hex("3A2E22") : UiFactory.Hex("2A2218");
                Color fg = interactive ? UiFactory.Hex("E7DCC8") : UiFactory.Hex("7A6E5E");
                Action click = () => OnSlotClicked(slot, meta);
                if (!interactive) click = () => { };
                UiFactory.Button(_slotRoot, "Slot" + i, label,
                    new Vector2(0.02f, y0), new Vector2(0.98f, y),
                    click, bg, fg, 12);
                y = y0 - 0.02f;
            }
        }

        private void OnSlotClicked(int slot, SaveSlotMeta meta)
        {
            if (_mode == SaveSlotModalMode.Load)
            {
                if (!meta.Occupied) return;
                Choose(slot);
                return;
            }

            if (meta.Occupied)
            {
                _pendingOverwriteSlot = slot;
                _confirmTitle.text = "OVERWRITE SLOT " + (slot + 1) + "?";
                _confirmBody.text = FormatSlotLabel(meta) + "\n\nThis cannot be undone.";
                _listPanel.SetActive(true);
                _confirmPanel.SetActive(true);
                return;
            }

            Choose(slot);
        }

        private void ConfirmOverwrite()
        {
            int slot = _pendingOverwriteSlot;
            _pendingOverwriteSlot = -1;
            _confirmPanel.SetActive(false);
            if (slot >= 0) Choose(slot);
        }

        private void CancelOverwrite()
        {
            _pendingOverwriteSlot = -1;
            _confirmPanel.SetActive(false);
        }

        private void Choose(int slot)
        {
            Action<int> chosen = _onChosen;
            Close();
            chosen?.Invoke(slot);
        }

        public static string FormatSlotLabel(SaveSlotMeta meta)
        {
            if (meta == null || !meta.Occupied)
            {
                string emptyTag = meta != null && meta.Slot == MinistryGameRunner.QuicksaveSlot
                    ? " (Quick Save)"
                    : "";
                return "Slot " + ((meta?.Slot ?? 0) + 1) + emptyTag + ": Empty";
            }

            string qs = meta.Slot == MinistryGameRunner.QuicksaveSlot ? " · Quick Save" : "";
            string when = meta.SavedUtcTicks > 0
                ? new DateTime(meta.SavedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "—";
            string conf = meta.Confidence.ToString("0");
            string money = DisplayUnits.Money(meta.Budget);
            return "Slot " + (meta.Slot + 1) + qs + "\n"
                   + meta.ScenarioName + " · " + meta.DifficultyName + " · " + meta.DateLabel + "\n"
                   + "Saved " + when + " · Conf " + conf + " · " + money;
        }
    }
}
