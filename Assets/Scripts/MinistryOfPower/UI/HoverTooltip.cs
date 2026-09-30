using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MinistryOfPower.Runtime;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MinistryOfPower.UI
{
    /// <summary>Hover tooltip host — one floating panel under a canvas.</summary>
    public sealed class TooltipService : MonoBehaviour
    {
        private static TooltipService _instance;
        private RectTransform _panel;
        private Text _label;
        private Canvas _canvas;
        private bool _visible;

        public static TooltipService Ensure(Transform canvasRoot)
        {
            if (_instance != null) return _instance;
            var go = new GameObject("TooltipService", typeof(RectTransform), typeof(TooltipService));
            go.transform.SetParent(canvasRoot, false);
            _instance = go.GetComponent<TooltipService>();
            _instance.Build(canvasRoot.GetComponentInParent<Canvas>() ?? canvasRoot.GetComponent<Canvas>());
            return _instance;
        }

        private void Build(Canvas canvas)
        {
            _canvas = canvas;
            var img = UiFactory.Panel(transform, "Tip", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), UiFactory.Hex("1A140FCC"));
            _panel = img.GetComponent<RectTransform>();
            _panel.pivot = new Vector2(0f, 1f);
            _panel.sizeDelta = new Vector2(320f, 120f);
            _panel.anchoredPosition = Vector2.zero;
            _label = UiFactory.Label(_panel, "TipText", UiFactory.Hex("E7DCC8"), 13, FontStyle.Normal,
                new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f), TextAnchor.UpperLeft);
            Hide();
        }

        public void Show(string text, Vector2 screenPos)
        {
            if (!GameSettings.ShowTooltips || string.IsNullOrEmpty(text))
            {
                Hide();
                return;
            }

            _visible = true;
            gameObject.SetActive(true);
            _panel.gameObject.SetActive(true);
            _label.text = text;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, screenPos, null, out Vector2 local);
            _panel.anchoredPosition = local + new Vector2(12f, -12f);
        }

        public void Hide()
        {
            _visible = false;
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_visible || _panel == null || !_panel.gameObject.activeSelf) return;
            if (!GameSettings.ShowTooltips)
            {
                Hide();
                return;
            }

            Vector2 mouse = ReadMouseScreenPosition();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, mouse, null, out Vector2 local);
            _panel.anchoredPosition = local + new Vector2(12f, -12f);
        }

        private static Vector2 ReadMouseScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null) return mouse.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try { return Input.mousePosition; }
            catch (System.InvalidOperationException) { return Vector2.zero; }
#else
            return Vector2.zero;
#endif
        }
    }

    public sealed class HoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private string tip;
        private TooltipService _service;

        public string Tip => tip;
        public void SetTip(string text) => tip = text;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_service == null)
            {
                Canvas c = GetComponentInParent<Canvas>();
                if (c != null) _service = TooltipService.Ensure(c.transform);
            }

            _service?.Show(tip, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _service?.Hide();
        }
    }
}
