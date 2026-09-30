using UnityEngine;
using MinistryOfPower.Data;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.UI.Map
{
    /// <summary>
    /// Country-scale day/night terminator overlay for the USA map art.
    /// Soft night band driven by <see cref="GameClock.DayFraction"/>; does not use a moving light.
    /// </summary>
    public sealed class MapDayTerminator : MonoBehaviour
    {
        public const string ShaderName = "MinistryOfPower/MapTerminator";
        public const string OverlayName = "MapDayTerminator";

        [SerializeField] private GameTuning gameTuning;
        [SerializeField] [Range(0.2f, 0.95f)] private float nightAlpha = 0.72f;
        [SerializeField] [Range(0.04f, 0.4f)] private float edgeSoftness = 0.14f;
        [SerializeField] [Range(0.4f, 2f)] private float lonSpan = 1.05f;
        [SerializeField] [Range(0f, 25f)] private float seasonTiltDegrees = 12f;
        [SerializeField] private Color nightColor = new Color(0.02f, 0.04f, 0.10f, 1f);
        [SerializeField] private float overlayHeightOffset = 0.025f;

        private Transform _overlay;
        private Material _material;
        private static readonly int NightColorId = Shader.PropertyToID("_NightColor");
        private static readonly int NightAlphaId = Shader.PropertyToID("_NightAlpha");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int DayFractionId = Shader.PropertyToID("_DayFraction");
        private static readonly int SeasonTiltId = Shader.PropertyToID("_SeasonTilt");
        private static readonly int LonSpanId = Shader.PropertyToID("_LonSpan");

        public void BindGameTuning(GameTuning tuning)
        {
            gameTuning = tuning;
            PullTuning();
        }

        public void EnsureOverlay()
        {
            PullTuning();
            if (_overlay != null && _material != null)
            {
                AlignToMapArt();
                return;
            }

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogWarning("MapDayTerminator: shader '" + ShaderName + "' missing.");
                return;
            }

            Transform mapRoot = ResolveMapRoot();
            var existing = mapRoot.Find(OverlayName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = OverlayName;
                go.transform.SetParent(mapRoot, false);
            }

            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }

            _material = new Material(shader) { name = "MapTerminator_Runtime", hideFlags = HideFlags.DontSave };
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            // Draw above MapArt but under plant pins / labels.
            mr.sortingOrder = 2;

            _overlay = go.transform;
            AlignToMapArt();
            ApplyStaticMaterialProps();
        }

        public void ApplyClock(GameClock clock)
        {
            if (clock == null) return;
            if (_overlay == null || _material == null) EnsureOverlay();
            if (_material == null) return;

            PullTuning();
            ApplyStaticMaterialProps();
            _material.SetFloat(DayFractionId, Mathf.Repeat(clock.DayFraction, 1f));
            _material.SetFloat(SeasonTiltId, SeasonTiltRadians(clock.CurrentSeason));
        }

        private float SeasonTiltRadians(Season season)
        {
            float deg = seasonTiltDegrees;
            // Northern-hemisphere lean: winter terminator tips west-of-south, summer opposite.
            switch (season)
            {
                case Season.Winter: return deg * Mathf.Deg2Rad;
                case Season.Summer: return -deg * Mathf.Deg2Rad;
                case Season.Spring: return deg * 0.35f * Mathf.Deg2Rad;
                case Season.Autumn: return -deg * 0.35f * Mathf.Deg2Rad;
                default: return 0f;
            }
        }

        private void PullTuning()
        {
            if (gameTuning == null) return;
            nightAlpha = gameTuning.terminatorNightAlpha;
            edgeSoftness = gameTuning.terminatorSoftness;
            lonSpan = gameTuning.terminatorLonSpan;
            seasonTiltDegrees = gameTuning.terminatorSeasonTiltDegrees;
            nightColor = gameTuning.terminatorNightColor;
        }

        private void ApplyStaticMaterialProps()
        {
            if (_material == null) return;
            _material.SetColor(NightColorId, nightColor);
            _material.SetFloat(NightAlphaId, nightAlpha);
            _material.SetFloat(SoftnessId, edgeSoftness);
            _material.SetFloat(LonSpanId, lonSpan);
        }

        private void AlignToMapArt()
        {
            if (_overlay == null) return;

            Transform mapArt = null;
            Transform mapRoot = ResolveMapRoot();
            if (mapRoot != null) mapArt = mapRoot.Find("MapArt");
            if (mapArt == null)
            {
                var go = GameObject.Find("MapArt");
                if (go != null) mapArt = go.transform;
            }

            if (mapArt != null)
            {
                // Match SpriteRenderer (or legacy quad) pose; sit just above the art plane.
                _overlay.rotation = mapArt.rotation;
                Vector3 p = mapArt.position;
                _overlay.position = new Vector3(p.x, p.y + overlayHeightOffset, p.z);

                var sr = mapArt.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    // Unit quad local XY → world XZ under the shared Euler(90,0,0) pose.
                    Bounds b = sr.bounds;
                    _overlay.localScale = new Vector3(
                        Mathf.Max(0.01f, b.size.x),
                        Mathf.Max(0.01f, b.size.z),
                        1f);
                    return;
                }

                // Legacy MeshRenderer quad: localScale already encodes world XZ size.
                Vector3 s = mapArt.localScale;
                _overlay.localScale = new Vector3(
                    Mathf.Max(0.01f, Mathf.Abs(s.x)),
                    Mathf.Max(0.01f, Mathf.Abs(s.y)),
                    1f);
                return;
            }

            Vector2 size = ResolveArtSize();
            Vector2 center = ResolveArtCenter();
            float y = ResolveArtHeight() + overlayHeightOffset;

            _overlay.rotation = Quaternion.Euler(90f, 0f, 0f);
            _overlay.position = new Vector3(center.x, y, center.y);
            _overlay.localScale = new Vector3(size.x, size.y, 1f);
        }

        private static Transform ResolveMapRoot()
        {
            var root = GameObject.Find("PoliticalMap");
            if (root == null) root = new GameObject("PoliticalMap");
            return root.transform;
        }

        private static Vector2 ResolveArtSize()
        {
            MapTuning mt = MapTuning.FindActive();
            return mt != null ? mt.PngWorldSize : UsaMapLayout.ArtWorldSize;
        }

        private static Vector2 ResolveArtCenter()
        {
            MapTuning mt = MapTuning.FindActive();
            return mt != null ? mt.PngWorldOffset : UsaMapLayout.ArtWorldCenter;
        }

        private static float ResolveArtHeight()
        {
            MapTuning mt = MapTuning.FindActive();
            return mt != null ? mt.ArtHeight : UsaMapLayout.ArtHeight;
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                if (Application.isPlaying) Object.Destroy(_material);
                else Object.DestroyImmediate(_material);
                _material = null;
            }
        }
    }
}
