using System;
using UnityEngine;
using UnityEngine.UI;

namespace MinistryOfPower.UI
{
    internal static class UiFactory
    {
        public static Font Font
        {
            get
            {
                var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return f != null ? f : Font.CreateDynamicFontFromOSFont("Arial", 16);
            }
        }

        public static Image Panel(Transform parent, string name, Vector2 amin, Vector2 amax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = amin;
            rt.anchorMax = amax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string name, Color color, int size, FontStyle style,
            Vector2 amin, Vector2 amax, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = amin;
            rt.anchorMax = amax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button Button(Transform parent, string name, string label, Vector2 amin, Vector2 amax,
            Action onClick, Color bg, Color fg, int fontSize = 16, string tooltip = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = amin;
            rt.anchorMax = amax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = bg;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => onClick?.Invoke());
            Label(go.transform, "Label", fg, fontSize, FontStyle.Bold,
                new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f), TextAnchor.MiddleCenter).text = label;
            if (!string.IsNullOrEmpty(tooltip))
            {
                go.AddComponent<HoverTooltip>().SetTip(tooltip);
            }

            return button;
        }

        public static void AttachTooltip(GameObject go, string tooltip)
        {
            if (go == null || string.IsNullOrEmpty(tooltip)) return;
            var tip = go.GetComponent<HoverTooltip>();
            if (tip == null) tip = go.AddComponent<HoverTooltip>();
            if (tip.Tip == tooltip) return;
            tip.SetTip(tooltip);
        }

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.gray;
        }

        public static void EnsureEventSystem(Transform parent)
        {
            if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            {
                return;
            }

            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            es.transform.SetParent(parent, false);
            var inputSys = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSys != null) es.AddComponent(inputSys);
            else es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }
}
