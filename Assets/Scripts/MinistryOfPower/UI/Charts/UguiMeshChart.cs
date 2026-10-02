using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MinistryOfPower.UI.Charts
{
    /// <summary>
    /// Lightweight UGUI mesh chart (line / area / bar) with axes + legend.
    /// Interim until XCharts (com.monitor1394.xcharts) is fully adopted in scenes;
    /// see store docs/charts-library.md.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UguiMeshChart : MaskableGraphic
    {
        public enum SeriesKind
        {
            Line,
            Area,
            Bar
        }

        public struct Series
        {
            public string Name;
            public Color Color;
            public SeriesKind Kind;
            public float[] Values;
        }

        private readonly List<Series> _series = new List<Series>(4);
        private string[] _xLabels = Array.Empty<string>();
        private Func<float, string> _yFormat = v => v.ToString("0");
        private string _title = "";
        private float _yMin;
        private float _yMax = 1f;
        private bool _autoY = true;

        private Text _titleLabel;
        private Text _yMaxLabel;
        private Text _yMinLabel;
        private Text _legendLabel;
        private readonly List<Text> _xTickLabels = new List<Text>(8);

        private static readonly UIVertex[] Quad = new UIVertex[4];

        public static UguiMeshChart Create(Transform parent, string name, Vector2 amin, Vector2 amax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UguiMeshChart));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = amin;
            rt.anchorMax = amax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var chart = go.GetComponent<UguiMeshChart>();
            chart.color = new Color(1f, 1f, 1f, 0.02f);
            chart.raycastTarget = false;
            chart.BuildChrome();
            return chart;
        }

        public void Configure(string title, Func<float, string> yFormat, bool autoY = true, float yMin = 0f, float yMax = 1f)
        {
            _title = title ?? "";
            _yFormat = yFormat ?? (v => v.ToString("0"));
            _autoY = autoY;
            _yMin = yMin;
            _yMax = Mathf.Max(yMax, yMin + 0.0001f);
            if (_titleLabel != null) _titleLabel.text = _title;
        }

        public void SetSeries(string[] xLabels, params Series[] series)
        {
            _xLabels = xLabels ?? Array.Empty<string>();
            _series.Clear();
            if (series != null)
            {
                for (int i = 0; i < series.Length; i++)
                {
                    if (series[i].Values != null && series[i].Values.Length > 0)
                        _series.Add(series[i]);
                }
            }

            RecalcY();
            RefreshChrome();
            SetVerticesDirty();
        }

        public void ClearSeries()
        {
            _series.Clear();
            _xLabels = Array.Empty<string>();
            SetVerticesDirty();
            RefreshChrome();
        }

        private void BuildChrome()
        {
            Color ink = UiFactory.Hex("E7DCC8");
            Color muted = UiFactory.Hex("C4A35A");
            _titleLabel = UiFactory.Label(transform, "ChartTitle", muted, 11, FontStyle.Bold,
                new Vector2(0.02f, 0.88f), new Vector2(0.98f, 0.995f), TextAnchor.MiddleLeft);
            _titleLabel.raycastTarget = false;

            _yMaxLabel = UiFactory.Label(transform, "YMax", ink, 9, FontStyle.Normal,
                new Vector2(0.01f, 0.72f), new Vector2(0.18f, 0.86f), TextAnchor.UpperLeft);
            _yMaxLabel.raycastTarget = false;
            _yMinLabel = UiFactory.Label(transform, "YMin", ink, 9, FontStyle.Normal,
                new Vector2(0.01f, 0.08f), new Vector2(0.18f, 0.20f), TextAnchor.LowerLeft);
            _yMinLabel.raycastTarget = false;

            _legendLabel = UiFactory.Label(transform, "Legend", ink, 9, FontStyle.Normal,
                new Vector2(0.20f, 0.01f), new Vector2(0.98f, 0.10f), TextAnchor.MiddleLeft);
            _legendLabel.raycastTarget = false;

            for (int i = 0; i < 6; i++)
            {
                var t = UiFactory.Label(transform, "XTick" + i, ink, 8, FontStyle.Normal,
                    new Vector2(0f, 0f), new Vector2(0.01f, 0.01f), TextAnchor.MiddleCenter);
                t.raycastTarget = false;
                t.gameObject.SetActive(false);
                _xTickLabels.Add(t);
            }
        }

        private void RecalcY()
        {
            if (!_autoY) return;
            float min = float.MaxValue;
            float max = float.MinValue;
            bool any = false;
            for (int s = 0; s < _series.Count; s++)
            {
                float[] v = _series[s].Values;
                for (int i = 0; i < v.Length; i++)
                {
                    any = true;
                    if (v[i] < min) min = v[i];
                    if (v[i] > max) max = v[i];
                }
            }

            if (!any)
            {
                _yMin = 0f;
                _yMax = 1f;
                return;
            }

            if (min > 0f) min = 0f;
            if (Mathf.Approximately(min, max)) max = min + 1f;
            float pad = (max - min) * 0.08f;
            _yMin = min;
            _yMax = max + pad;
        }

        private void RefreshChrome()
        {
            if (_titleLabel != null) _titleLabel.text = _title;
            if (_yMaxLabel != null) _yMaxLabel.text = _yFormat(_yMax);
            if (_yMinLabel != null) _yMinLabel.text = _yFormat(_yMin);

            if (_legendLabel != null)
            {
                if (_series.Count == 0) _legendLabel.text = "";
                else
                {
                    var parts = new string[_series.Count];
                    for (int i = 0; i < _series.Count; i++)
                        parts[i] = "■ " + (string.IsNullOrEmpty(_series[i].Name) ? ("S" + i) : _series[i].Name);
                    _legendLabel.text = string.Join("   ", parts);
                    // Color hint via first series only; names stay readable on parchment HUD.
                }
            }

            int n = _xLabels.Length;
            int show = Mathf.Min(_xTickLabels.Count, n == 0 ? 0 : Mathf.Min(6, n));
            for (int i = 0; i < _xTickLabels.Count; i++)
            {
                if (i >= show)
                {
                    _xTickLabels[i].gameObject.SetActive(false);
                    continue;
                }

                int idx = show == 1 ? 0 : (int)Mathf.Round(i * (n - 1) / (float)(show - 1));
                idx = Mathf.Clamp(idx, 0, n - 1);
                float t = n <= 1 ? 0.5f : idx / (float)(n - 1);
                float x0 = PlotLeft + t * (PlotRight - PlotLeft) - 0.04f;
                var lab = _xTickLabels[i];
                lab.gameObject.SetActive(true);
                lab.text = _xLabels[idx] ?? "";
                var rt = lab.rectTransform;
                rt.anchorMin = new Vector2(Mathf.Clamp01(x0), 0.10f);
                rt.anchorMax = new Vector2(Mathf.Clamp01(x0 + 0.08f), 0.18f);
            }
        }

        private const float PlotLeft = 0.18f;
        private const float PlotRight = 0.98f;
        private const float PlotBottom = 0.20f;
        private const float PlotTop = 0.86f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            // Dim plot backdrop
            AddRect(vh, r, PlotLeft, PlotBottom, PlotRight, PlotTop, new Color(0.08f, 0.06f, 0.04f, 0.55f));

            // Grid lines
            Color grid = new Color(0.55f, 0.48f, 0.35f, 0.25f);
            for (int g = 0; g <= 4; g++)
            {
                float u = g / 4f;
                float y = Mathf.Lerp(PlotBottom, PlotTop, u);
                AddRect(vh, r, PlotLeft, y - 0.003f, PlotRight, y + 0.003f, grid);
            }

            // Axes
            Color axis = new Color(0.90f, 0.86f, 0.78f, 0.55f);
            AddRect(vh, r, PlotLeft, PlotBottom, PlotLeft + 0.006f, PlotTop, axis);
            AddRect(vh, r, PlotLeft, PlotBottom, PlotRight, PlotBottom + 0.006f, axis);

            if (_series.Count == 0) return;

            int pointCount = 0;
            for (int s = 0; s < _series.Count; s++)
                pointCount = Mathf.Max(pointCount, _series[s].Values.Length);
            if (pointCount <= 0) return;

            // Bars first (behind), then areas, then lines
            for (int s = 0; s < _series.Count; s++)
            {
                if (_series[s].Kind == SeriesKind.Bar)
                    DrawBars(vh, r, _series[s], pointCount);
            }

            for (int s = 0; s < _series.Count; s++)
            {
                if (_series[s].Kind == SeriesKind.Area)
                    DrawArea(vh, r, _series[s], pointCount);
            }

            for (int s = 0; s < _series.Count; s++)
            {
                if (_series[s].Kind == SeriesKind.Line || _series[s].Kind == SeriesKind.Area)
                    DrawLine(vh, r, _series[s], pointCount);
            }

            // Legend color swatches
            float lx = 0.20f;
            for (int s = 0; s < _series.Count && s < 4; s++)
            {
                Color c = _series[s].Color;
                c.a = 0.95f;
                AddRect(vh, r, lx, 0.035f, lx + 0.025f, 0.085f, c);
                lx += 0.20f;
            }
        }

        private void DrawBars(VertexHelper vh, Rect r, Series series, int pointCount)
        {
            float[] v = series.Values;
            int n = v.Length;
            if (n == 0) return;
            float slot = (PlotRight - PlotLeft) / Mathf.Max(1, pointCount);
            float barW = slot * 0.55f;
            Color c = series.Color;
            c.a = 0.85f;
            for (int i = 0; i < n; i++)
            {
                float t = pointCount <= 1 ? 0.5f : (i + 0.5f) / pointCount;
                float cx = Mathf.Lerp(PlotLeft, PlotRight, t);
                float y = MapY(v[i]);
                AddRect(vh, r, cx - barW * 0.5f, PlotBottom, cx + barW * 0.5f, y, c);
            }
        }

        private void DrawArea(VertexHelper vh, Rect r, Series series, int pointCount)
        {
            float[] v = series.Values;
            int n = v.Length;
            if (n < 2) return;
            Color fill = series.Color;
            fill.a = 0.28f;
            for (int i = 0; i < n - 1; i++)
            {
                float t0 = pointCount <= 1 ? 0.5f : i / (float)(pointCount - 1);
                float t1 = pointCount <= 1 ? 0.5f : (i + 1) / (float)(pointCount - 1);
                float x0 = Mathf.Lerp(PlotLeft, PlotRight, t0);
                float x1 = Mathf.Lerp(PlotLeft, PlotRight, t1);
                float y0 = MapY(v[i]);
                float y1 = MapY(v[Mathf.Min(i + 1, n - 1)]);
                AddQuad(vh, r, x0, PlotBottom, x1, PlotBottom, x1, y1, x0, y0, fill);
            }
        }

        private void DrawLine(VertexHelper vh, Rect r, Series series, int pointCount)
        {
            float[] v = series.Values;
            int n = v.Length;
            if (n < 2) return;
            Color c = series.Color;
            c.a = 0.95f;
            const float thickness = 0.012f;
            for (int i = 0; i < n - 1; i++)
            {
                float t0 = pointCount <= 1 ? 0.5f : i / (float)(pointCount - 1);
                float t1 = pointCount <= 1 ? 0.5f : (i + 1) / (float)(pointCount - 1);
                float x0 = Mathf.Lerp(PlotLeft, PlotRight, t0);
                float x1 = Mathf.Lerp(PlotLeft, PlotRight, t1);
                float y0 = MapY(v[i]);
                float y1 = MapY(v[Mathf.Min(i + 1, n - 1)]);
                AddThickSegment(vh, r, x0, y0, x1, y1, thickness, c);
            }
        }

        private float MapY(float value)
        {
            float u = Mathf.InverseLerp(_yMin, _yMax, value);
            return Mathf.Lerp(PlotBottom, PlotTop, Mathf.Clamp01(u));
        }

        private static void AddRect(VertexHelper vh, Rect r, float u0, float v0, float u1, float v1, Color c)
        {
            AddQuad(vh, r, u0, v0, u1, v0, u1, v1, u0, v1, c);
        }

        private static void AddThickSegment(VertexHelper vh, Rect r, float x0, float y0, float x1, float y1, float thickness, Color c)
        {
            Vector2 a = new Vector2(x0, y0);
            Vector2 b = new Vector2(x1, y1);
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-8f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * (thickness * 0.5f);
            AddQuad(vh, r,
                a.x + n.x, a.y + n.y,
                b.x + n.x, b.y + n.y,
                b.x - n.x, b.y - n.y,
                a.x - n.x, a.y - n.y,
                c);
        }

        private static void AddQuad(VertexHelper vh, Rect r,
            float u0, float v0, float u1, float v1, float u2, float v2, float u3, float v3, Color c)
        {
            Quad[0] = Vert(r, u0, v0, c);
            Quad[1] = Vert(r, u1, v1, c);
            Quad[2] = Vert(r, u2, v2, c);
            Quad[3] = Vert(r, u3, v3, c);
            vh.AddUIVertexQuad(Quad);
        }

        private static UIVertex Vert(Rect r, float u, float v, Color c)
        {
            var vert = UIVertex.simpleVert;
            vert.color = c;
            vert.position = new Vector3(Mathf.Lerp(r.xMin, r.xMax, u), Mathf.Lerp(r.yMin, r.yMax, v), 0f);
            return vert;
        }
    }
}
