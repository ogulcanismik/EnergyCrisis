using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.UI
{
    /// <summary>
    /// Diegetic ministry desk: wood-toned panels, meters, in-tray crisis modal, build actions.
    /// Builds its own Canvas hierarchy at runtime so the scene only needs this component + runner.
    /// </summary>
    public sealed class MinistryDeskView : MonoBehaviour
    {
        private Text dateText;
        private Text budgetText;
        private Text speedText;
        private Text briefText;
        private Text metersText;
        private Text portfolioText;
        private Text buildsText;
        private Text projectionsText;
        private Text logText;
        private Text eventTitleText;
        private Text eventBodyText;
        private GameObject eventModal;
        private GameObject gameOverBanner;

        private Action _onPause, _onSlow, _onNormal, _onFast, _onVeryFast;
        private Action _onBuildSolar, _onBuildNuclear, _onBuildStorage, _onRetireFossil;
        private Action _onAbsorb, _onEmergencyFossil, _onTariffFreeze;
        private Action _onScenarioA, _onScenarioB, _onStepDay;

        private Button _scenarioAButton;
        private Button _scenarioBButton;
        private readonly StringBuilder _sb = new StringBuilder(1024);

        private void Awake()
        {
            EnsureUi();
        }

        public void Bind(
            Action onPause,
            Action onSlow,
            Action onNormal,
            Action onFast,
            Action onVeryFast,
            Action onBuildSolar,
            Action onBuildNuclear,
            Action onBuildStorage,
            Action onRetireFossil,
            Action onAbsorb,
            Action onEmergencyFossil,
            Action onTariffFreeze,
            Action onScenarioA,
            Action onScenarioB,
            Action onStepDay)
        {
            _onPause = onPause;
            _onSlow = onSlow;
            _onNormal = onNormal;
            _onFast = onFast;
            _onVeryFast = onVeryFast;
            _onBuildSolar = onBuildSolar;
            _onBuildNuclear = onBuildNuclear;
            _onBuildStorage = onBuildStorage;
            _onRetireFossil = onRetireFossil;
            _onAbsorb = onAbsorb;
            _onEmergencyFossil = onEmergencyFossil;
            _onTariffFreeze = onTariffFreeze;
            _onScenarioA = onScenarioA;
            _onScenarioB = onScenarioB;
            _onStepDay = onStepDay;
        }

        public void SetScenarioChoices(string a, string b)
        {
            if (_scenarioAButton != null)
            {
                SetButtonLabel(_scenarioAButton, a);
            }

            if (_scenarioBButton != null)
            {
                SetButtonLabel(_scenarioBButton, b);
            }
        }

        public void ShowEventModal(PendingEvent evt)
        {
            EnsureUi();
            if (eventModal == null)
            {
                return;
            }

            eventModal.SetActive(true);
            if (eventTitleText != null) eventTitleText.text = evt.Title;
            if (eventBodyText != null)
            {
                string exposure = string.IsNullOrEmpty(evt.ExposureLabel)
                    ? "Exposure"
                    : evt.ExposureLabel;
                eventBodyText.text =
                    $"{evt.Body}\n\n{exposure}: {evt.Exposure01 * 100f:0}%\nSeverity: {evt.Severity01 * 100f:0}%\n\nAbsorb the headlines, or postpone with a ministerial cost.";
            }
        }

        public void HideEventModal()
        {
            if (eventModal != null)
            {
                eventModal.SetActive(false);
            }
        }

        public void Render(GameSession session, List<string> logLines)
        {
            EnsureUi();
            if (session?.Clock == null)
            {
                return;
            }

            SeatMeters m = session.Meters;
            if (dateText != null)
            {
                dateText.text = $"MINISTRY DESK  ·  {session.Clock.FormatDate()}  ·  Y{session.Clock.Year} Q{session.Clock.QuarterIndex + 1} D{session.Clock.DayIndex + 1}";
            }

            if (budgetText != null)
            {
                budgetText.text = $"Treasury  {session.Budget:0.0}   ·   Fuel idx  {session.FuelPriceIndex * session.OilShockMultiplier:0.00}";
            }

            if (speedText != null)
            {
                speedText.text = session.IsGameOver
                    ? "STATUS: SACKED"
                    : $"Speed: {session.Clock.Speed}";
            }

            if (metersText != null)
            {
                metersText.text =
                    $"ADEQUACY        {Bar(m.Adequacy)}  {m.Adequacy:0.0}\n" +
                    $"AFFORDABILITY   {Bar(m.Affordability)}  {m.Affordability:0.0}\n" +
                    $"TRANSITION      {Bar(m.Transition)}  {m.Transition:0.0}\n" +
                    $"CONFIDENCE      {Bar(m.Confidence)}  {m.Confidence:0.0}";
            }

            if (briefText != null)
            {
                string brief = string.IsNullOrEmpty(session.LastReport.Brief)
                    ? session.Scenario.Description
                    : session.LastReport.Brief;
                if (session.LastReport.DemandMw > 0f)
                {
                    brief += $"\nSupply {session.LastReport.SupplyMw:0} / Demand {session.LastReport.DemandMw:0} MW";
                }

                briefText.text = brief;
            }

            if (portfolioText != null)
            {
                _sb.Length = 0;
                _sb.AppendLine("PORTFOLIO");
                IReadOnlyList<PlantInstance> plants = session.Portfolio.Plants;
                for (int i = 0; i < plants.Count; i++)
                {
                    PlantInstance p = plants[i];
                    if (p.IsRetired)
                    {
                        continue;
                    }

                    _sb.Append(p.DisplayName)
                        .Append("  ")
                        .Append(p.Fuel)
                        .Append("  ")
                        .Append(p.CapacityMw.ToString("0"))
                        .Append(" MW\n");
                }

                portfolioText.text = _sb.ToString();
            }

            if (buildsText != null || projectionsText != null)
            {
                string projections = DeskProjections.Build(
                    session,
                    session.Builds.Orders,
                    session.Scenario.QuarterlyIncome);
                if (buildsText != null)
                {
                    buildsText.text = projections;
                }

                if (projectionsText != null)
                {
                    projectionsText.text = projections;
                }
            }

            if (logText != null)
            {
                _sb.Length = 0;
                _sb.AppendLine("DESK LOG");
                int start = Math.Max(0, logLines.Count - 12);
                for (int i = start; i < logLines.Count; i++)
                {
                    _sb.AppendLine("· " + logLines[i]);
                }

                logText.text = _sb.ToString();
            }

            if (gameOverBanner != null)
            {
                gameOverBanner.SetActive(session.IsGameOver);
            }
        }

        private static string Bar(float value01to100)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(value01to100 / 10f), 0, 10);
            _staticBar.Length = 0;
            _staticBar.Append('[');
            for (int i = 0; i < 10; i++)
            {
                _staticBar.Append(i < filled ? '■' : '·');
            }

            _staticBar.Append(']');
            return _staticBar.ToString();
        }

        private static readonly StringBuilder _staticBar = new StringBuilder(16);

        private void EnsureUi()
        {
            if (dateText != null)
            {
                return;
            }

            var canvasGo = new GameObject("MinistryDeskCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
                esGo.transform.SetParent(transform, false);
                // Prefer Input System UI module when present; fall back to legacy standalone.
                var inputSysType = System.Type.GetType(
                    "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputSysType != null)
                {
                    esGo.AddComponent(inputSysType);
                }
                else
                {
                    esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                }
            }

            Color wood = Hex("2B2118");
            Color blotter = Hex("1E1812");
            Color brass = Hex("C4A35A");
            Color paper = Hex("E7DCC8");
            Color ink = Hex("1A140F");
            Color panel = Hex("3A2E22");

            Image bg = CreatePanel(canvasGo.transform, "Blotter", stretch: true);
            bg.color = blotter;

            // Subtle vignette panels
            Image leftRail = CreatePanel(canvasGo.transform, "LeftRail", new Vector2(0, 0), new Vector2(0.28f, 1f));
            leftRail.color = wood;
            Image rightRail = CreatePanel(canvasGo.transform, "RightRail", new Vector2(0.72f, 0), new Vector2(1f, 1f));
            rightRail.color = wood;
            Image topBanner = CreatePanel(canvasGo.transform, "TopBanner", new Vector2(0.28f, 0.86f), new Vector2(0.72f, 1f));
            topBanner.color = panel;

            dateText = CreateText(topBanner.transform, "Date", paper, 26, FontStyle.Bold,
                new Vector2(0.02f, 0.35f), new Vector2(0.98f, 0.95f));
            budgetText = CreateText(topBanner.transform, "Budget", brass, 18, FontStyle.Normal,
                new Vector2(0.02f, 0.05f), new Vector2(0.7f, 0.4f));
            speedText = CreateText(topBanner.transform, "Speed", brass, 18, FontStyle.Normal,
                new Vector2(0.7f, 0.05f), new Vector2(0.98f, 0.4f), TextAnchor.MiddleRight);

            Image meterPanel = CreatePanel(canvasGo.transform, "Meters", new Vector2(0.3f, 0.55f), new Vector2(0.7f, 0.84f));
            meterPanel.color = Hex("31261C");
            metersText = CreateText(meterPanel.transform, "MetersText", paper, 20, FontStyle.Bold,
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f), TextAnchor.UpperLeft);

            Image briefPanel = CreatePanel(canvasGo.transform, "Brief", new Vector2(0.3f, 0.38f), new Vector2(0.7f, 0.54f));
            briefPanel.color = Hex("4A3B2A");
            briefText = CreateText(briefPanel.transform, "BriefText", paper, 18, FontStyle.Italic,
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f), TextAnchor.UpperLeft);

            portfolioText = CreateText(leftRail.transform, "Portfolio", paper, 15, FontStyle.Normal,
                new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f), TextAnchor.UpperLeft);
            buildsText = CreateText(rightRail.transform, "Projections", paper, 14, FontStyle.Normal,
                new Vector2(0.06f, 0.38f), new Vector2(0.94f, 0.96f), TextAnchor.UpperLeft);
            logText = CreateText(rightRail.transform, "Log", Hex("D2C3A8"), 13, FontStyle.Normal,
                new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.36f), TextAnchor.UpperLeft);

            // Speed row
            float y = 0.30f;
            CreateButton(canvasGo.transform, "Pause", "❚❚", new Vector2(0.30f, y), new Vector2(0.36f, y + 0.06f), () => _onPause?.Invoke(), brass, ink);
            CreateButton(canvasGo.transform, "Slow", "▶", new Vector2(0.37f, y), new Vector2(0.43f, y + 0.06f), () => _onSlow?.Invoke(), brass, ink);
            CreateButton(canvasGo.transform, "Normal", "▶▶", new Vector2(0.44f, y), new Vector2(0.50f, y + 0.06f), () => _onNormal?.Invoke(), brass, ink);
            CreateButton(canvasGo.transform, "Fast", "▶▶▶", new Vector2(0.51f, y), new Vector2(0.57f, y + 0.06f), () => _onFast?.Invoke(), brass, ink);
            CreateButton(canvasGo.transform, "VFast", ">>>>", new Vector2(0.58f, y), new Vector2(0.64f, y + 0.06f), () => _onVeryFast?.Invoke(), brass, ink);
            CreateButton(canvasGo.transform, "Step", "Day+1", new Vector2(0.65f, y), new Vector2(0.70f, y + 0.06f), () => _onStepDay?.Invoke(), Hex("8B6914"), paper);

            // Actions
            float y2 = 0.22f;
            CreateButton(canvasGo.transform, "Solar", "Order Solar (6q)", new Vector2(0.30f, y2), new Vector2(0.43f, y2 + 0.06f), () => _onBuildSolar?.Invoke(), Hex("3E5C3A"), paper);
            CreateButton(canvasGo.transform, "Nuke", "Order Nuclear (24q)", new Vector2(0.44f, y2), new Vector2(0.57f, y2 + 0.06f), () => _onBuildNuclear?.Invoke(), Hex("3A4A5C"), paper);
            CreateButton(canvasGo.transform, "Storage", "Order Storage", new Vector2(0.58f, y2), new Vector2(0.70f, y2 + 0.06f), () => _onBuildStorage?.Invoke(), Hex("4A3E5C"), paper);

            float y3 = 0.14f;
            CreateButton(canvasGo.transform, "Retire", "Retire a Fossil Plant", new Vector2(0.30f, y3), new Vector2(0.50f, y3 + 0.06f), () => _onRetireFossil?.Invoke(), Hex("6B3030"), paper);
            _scenarioAButton = CreateButton(canvasGo.transform, "ScenA", "Federal High Budget", new Vector2(0.51f, y3), new Vector2(0.60f, y3 + 0.06f), () => _onScenarioA?.Invoke(), panel, paper);
            _scenarioBButton = CreateButton(canvasGo.transform, "ScenB", "Sun-Rich Low Budget", new Vector2(0.61f, y3), new Vector2(0.70f, y3 + 0.06f), () => _onScenarioB?.Invoke(), panel, paper);

            // Event modal
            eventModal = CreatePanel(canvasGo.transform, "EventModal", new Vector2(0.22f, 0.18f), new Vector2(0.78f, 0.78f)).gameObject;
            eventModal.GetComponent<Image>().color = Hex("241C14");
            Image modalBorder = CreatePanel(eventModal.transform, "Border", new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f));
            modalBorder.color = Hex("5A4630");
            eventTitleText = CreateText(eventModal.transform, "EvtTitle", brass, 28, FontStyle.Bold,
                new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.94f));
            eventBodyText = CreateText(eventModal.transform, "EvtBody", paper, 18, FontStyle.Normal,
                new Vector2(0.06f, 0.32f), new Vector2(0.94f, 0.76f), TextAnchor.UpperLeft);
            CreateButton(eventModal.transform, "Absorb", "Absorb Hit", new Vector2(0.08f, 0.08f), new Vector2(0.34f, 0.2f), () => _onAbsorb?.Invoke(), Hex("6B3030"), paper);
            CreateButton(eventModal.transform, "Fossil", "Emergency Fossil", new Vector2(0.36f, 0.08f), new Vector2(0.64f, 0.2f), () => _onEmergencyFossil?.Invoke(), Hex("6B4E30"), paper);
            CreateButton(eventModal.transform, "Tariff", "Tariff Freeze", new Vector2(0.66f, 0.08f), new Vector2(0.92f, 0.2f), () => _onTariffFreeze?.Invoke(), Hex("30506B"), paper);
            eventModal.SetActive(false);

            gameOverBanner = CreatePanel(canvasGo.transform, "GameOver", new Vector2(0.3f, 0.45f), new Vector2(0.7f, 0.58f)).gameObject;
            gameOverBanner.GetComponent<Image>().color = Hex("4A1010");
            CreateText(gameOverBanner.transform, "GoText", paper, 30, FontStyle.Bold,
                new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.9f), TextAnchor.MiddleCenter).text = "SACKED — Confidence collapsed";
            gameOverBanner.SetActive(false);
        }

        private static Image CreatePanel(Transform parent, string name, Vector2? anchorMin = null, Vector2? anchorMax = null, bool stretch = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (stretch)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                rt.anchorMin = anchorMin ?? Vector2.zero;
                rt.anchorMax = anchorMax ?? Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            return go.GetComponent<Image>();
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Color color,
            int size,
            FontStyle style,
            Vector2 anchorMin,
            Vector2 anchorMax,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
            {
                text.font = Font.CreateDynamicFontFromOSFont("Arial", size);
            }

            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = name;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Action onClick,
            Color bg,
            Color fg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = bg;
            var button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(bg, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(bg, Color.black, 0.2f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());

            Text text = CreateText(go.transform, "Label", fg, 15, FontStyle.Bold,
                new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f), TextAnchor.MiddleCenter);
            text.text = label;
            return button;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }

        private static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString("#" + hex, out Color c))
            {
                return c;
            }

            return Color.gray;
        }
    }
}
