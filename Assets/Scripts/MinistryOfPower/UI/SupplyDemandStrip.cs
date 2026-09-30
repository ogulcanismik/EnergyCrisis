using UnityEngine;
using UnityEngine.UI;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.UI
{
    /// <summary>24h demand/supply curve with playhead for the bottom dispatch panel.</summary>
    public sealed class SupplyDemandStrip : MonoBehaviour
    {
        private Image[] _demandBars;
        private Image[] _supplyBars;
        private RectTransform _playhead;
        private Text _caption;

        public static SupplyDemandStrip Create(Transform parent, Vector2 amin, Vector2 amax)
        {
            var host = UiFactory.Panel(parent, "DayStrip", amin, amax, UiFactory.Hex("15110C"));
            var strip = host.gameObject.AddComponent<SupplyDemandStrip>();
            strip.Build(host.transform);
            return strip;
        }

        private void Build(Transform parent)
        {
            _caption = UiFactory.Label(parent, "Cap", UiFactory.Hex("C4A35A"), 12, FontStyle.Bold,
                new Vector2(0.01f, 0.86f), new Vector2(0.99f, 0.99f));
            _caption.text = "24h load (amber) / supply (teal)";
            _demandBars = new Image[24];
            _supplyBars = new Image[24];
            for (int i = 0; i < 24; i++)
            {
                float x0 = 0.01f + i / 24f * 0.98f;
                float x1 = 0.01f + (i + 1) / 24f * 0.98f;
                _demandBars[i] = UiFactory.Panel(parent, "D" + i, new Vector2(x0, 0.06f), new Vector2(x1 - 0.002f, 0.42f), UiFactory.Hex("8B6914"));
                _supplyBars[i] = UiFactory.Panel(parent, "S" + i, new Vector2(x0, 0.44f), new Vector2(x1 - 0.002f, 0.82f), UiFactory.Hex("2F6B5A"));
            }

            var ph = UiFactory.Panel(parent, "Playhead", new Vector2(0.01f, 0.04f), new Vector2(0.02f, 0.84f), UiFactory.Hex("E7DCC8"));
            _playhead = ph.rectTransform;
        }

        public void Render(GameSession session)
        {
            if (session?.DayDemandCurve == null || _demandBars == null) return;
            float max = 1f;
            for (int i = 0; i < 24; i++)
            {
                max = Mathf.Max(max, session.DayDemandCurve[i], session.DaySupplyCurve[i]);
            }

            for (int i = 0; i < 24; i++)
            {
                float dh = Mathf.Clamp01(session.DayDemandCurve[i] / max) * 0.34f + 0.06f;
                float sh = Mathf.Clamp01(session.DaySupplyCurve[i] / max) * 0.34f + 0.06f;
                SetBar(_demandBars[i], 0.06f, dh);
                SetBar(_supplyBars[i], 0.44f, 0.44f + sh);
            }

            float hour = session.Clock.DayFraction * 24f;
            float t = Mathf.Clamp01(session.Clock.DayFraction);
            float x = 0.01f + t * 0.98f;
            _playhead.anchorMin = new Vector2(x, 0.04f);
            _playhead.anchorMax = new Vector2(Mathf.Min(x + 0.008f, 0.99f), 0.84f);

            float margin = session.LastReport.SupplyMw - session.LastReport.DemandMw;
            string peak = IsPeakHour(hour) ? "PEAK" : "off-peak";
            _caption.text =
                "24h dispatch · " + hour.ToString("0.0") + "h (" + peak + ") · dem " +
                DisplayUnits.Capacity(session.LastReport.DemandMw) + " / sup " +
                DisplayUnits.Capacity(session.LastReport.SupplyMw) + " · margin " +
                DisplayUnits.Capacity(margin, signed: true);
        }

        private static bool IsPeakHour(float hour)
        {
            return (hour >= 7f && hour <= 10f) || (hour >= 17f && hour <= 21f);
        }

        private static void SetBar(Image img, float yMin, float yMax)
        {
            var rt = img.rectTransform;
            Vector2 min = rt.anchorMin;
            Vector2 max = rt.anchorMax;
            min.y = yMin;
            max.y = yMax;
            rt.anchorMin = min;
            rt.anchorMax = max;
        }
    }
}
