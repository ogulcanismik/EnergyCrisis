using UnityEngine;
using UnityEngine.UI;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.UI
{
    /// <summary>Simple 24h demand/supply strip for grey-box HUD.</summary>
    public sealed class SupplyDemandStrip : MonoBehaviour
    {
        private Image[] _demandBars;
        private Image[] _supplyBars;
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
            _caption = UiFactory.Label(parent, "Cap", UiFactory.Hex("C4A35A"), 11, FontStyle.Bold,
                new Vector2(0.02f, 0.82f), new Vector2(0.98f, 0.98f));
            _caption.text = "24h demand (amber) / supply (teal)";
            _demandBars = new Image[24];
            _supplyBars = new Image[24];
            for (int i = 0; i < 24; i++)
            {
                float x0 = i / 24f;
                float x1 = (i + 1) / 24f;
                _demandBars[i] = UiFactory.Panel(parent, "D" + i, new Vector2(x0, 0.08f), new Vector2(x1, 0.4f), UiFactory.Hex("8B6914"));
                _supplyBars[i] = UiFactory.Panel(parent, "S" + i, new Vector2(x0, 0.42f), new Vector2(x1, 0.78f), UiFactory.Hex("2F6B5A"));
            }
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
                float dh = Mathf.Clamp01(session.DayDemandCurve[i] / max) * 0.32f + 0.08f;
                float sh = Mathf.Clamp01(session.DaySupplyCurve[i] / max) * 0.32f + 0.08f;
                SetBar(_demandBars[i], 0.08f, dh);
                SetBar(_supplyBars[i], 0.42f, 0.42f + sh);
            }

            float hour = session.Clock.DayFraction * 24f;
            _caption.text = $"24h strip · hour ~{hour:0.0} · dem {session.LastReport.DemandMw:0} / sup {session.LastReport.SupplyMw:0} MW";
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
