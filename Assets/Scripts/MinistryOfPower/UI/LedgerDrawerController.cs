using UnityEngine;
using UnityEngine.UIElements;

namespace MinistryOfPower.UI
{
    /// <summary>
    /// Thin UITK binder: exclusive mid-bottom ledger drawer open/close via USS classes.
    /// Layout and motion live in MainUIVisualTree.uxml / MainUIStyle.uss for UI Builder edits.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class LedgerDrawerController : MonoBehaviour
    {
        private const string ClosedClass = "drawer--closed-bottom";
        private const string OpenClass = "drawer--open-bottom";

        private static readonly (string ButtonName, string Title)[] Ledgers =
        {
            ("LedgerMix", "Energy Mix"),
            ("LedgerFuel", "Fuel"),
            ("LedgerMandate", "Mandate"),
            ("LedgerBudget", "Budget"),
            ("LedgerHistory", "History"),
        };

        private UIDocument _document;
        private VisualElement _drawer;
        private Label _title;
        private string _openButtonName;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Bind()
        {
            var root = _document != null ? _document.rootVisualElement : null;
            if (root == null) return;

            _drawer = root.Q<VisualElement>("LedgerDrawer");
            _title = root.Q<Label>("LedgerDrawerTitle");
            var close = root.Q<Button>("LedgerDrawerClose");

            if (_drawer == null) return;

            ApplyClosed();

            foreach (var (buttonName, title) in Ledgers)
            {
                var button = root.Q<Button>(buttonName);
                if (button == null) continue;
                var capturedName = buttonName;
                var capturedTitle = title;
                button.clicked += () => OnLedgerClicked(capturedName, capturedTitle);
            }

            if (close != null)
                close.clicked += CloseDrawer;
        }

        private void Unbind()
        {
            // UIDocument tears down the tree on disable; skip per-button unsubscribe.
            _drawer = null;
            _title = null;
            _openButtonName = null;
        }

        private void OnLedgerClicked(string buttonName, string title)
        {
            if (_openButtonName == buttonName)
            {
                CloseDrawer();
                return;
            }

            OpenDrawer(buttonName, title);
        }

        private void OpenDrawer(string buttonName, string title)
        {
            if (_drawer == null) return;
            _openButtonName = buttonName;
            if (_title != null)
                _title.text = title;
            _drawer.RemoveFromClassList(ClosedClass);
            _drawer.AddToClassList(OpenClass);
        }

        private void CloseDrawer()
        {
            if (_drawer == null) return;
            _openButtonName = null;
            ApplyClosed();
        }

        private void ApplyClosed()
        {
            _drawer.RemoveFromClassList(OpenClass);
            _drawer.AddToClassList(ClosedClass);
        }
    }
}
