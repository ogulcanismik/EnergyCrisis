using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using MinistryOfPower.Runtime;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.UI.Map
{
    /// <summary>
    /// Paradox political-map presenter: North America PNG ground + invisible 50-state pick meshes,
    /// soft interconnects, plant pins. Presentation only — sim stays on RegionId.
    /// Ocean is baked into the map art; no separate MapOcean backdrop.
    /// </summary>
    public sealed class PoliticalMapPresenter : MonoBehaviour
    {
        [SerializeField] private bool sunRichTint;
        [SerializeField] private Sprite prototypeMapSprite;

        private Transform _root;
        private Transform _plantRoot;
        private Transform _corridorRoot;
        private Transform _statesRoot;
        private TextMesh[] _labels;
        private string[] _labelCodes;
        private string[] _labelNames;
        private int _lastPlantSig = int.MinValue;
        private bool _built;
        private Material _sharedLit;
        private float _lastOrtho = -1f;
        private string _lastSelectedState = "";
        private bool _lastFullNames;

        public void Configure(bool sunRich)
        {
            if (_built && sunRichTint == sunRich) return;
            sunRichTint = sunRich;
            if (_built) RebuildColors();
        }

        public void EnsureBuilt()
        {
            if (_built && _root != null)
                return;
            // Rebuild if a prior edit-mode probe destroyed PoliticalMap while leaving _built set.
            _built = false;
            BuildWorld();
            _built = true;
        }

        private void LateUpdate()
        {
            if (!_built) return;
            RefreshLabels();
        }

        public void RefreshPlants(GameSession session)
        {
            EnsureBuilt();
            if (session?.Portfolio == null) return;

            int sig = 0;
            var plants = session.Portfolio.Plants;
            for (int i = 0; i < plants.Count; i++)
            {
                if (plants[i].IsRetired) continue;
                sig = sig * 31 + plants[i].Id.GetHashCode() + (int)plants[i].Region * 17 + (int)plants[i].Fuel;
            }

            if (sig == _lastPlantSig) return;
            _lastPlantSig = sig;

            if (_plantRoot == null)
            {
                var go = new GameObject("PlantMarkers");
                go.transform.SetParent(_root, false);
                _plantRoot = go.transform;
            }

            for (int i = _plantRoot.childCount - 1; i >= 0; i--)
                Destroy(_plantRoot.GetChild(i).gameObject);

            int[] counts = new int[3];
            for (int i = 0; i < plants.Count; i++)
            {
                PlantInstance p = plants[i];
                if (p.IsRetired) continue;
                int r = (int)p.Region;
                if (r < 0 || r > 2) r = 0;

                Vector3 basePos = UsaMapLayout.PlantAnchor(p.Region, counts[r]);
                float ang = counts[r] * 0.85f;
                counts[r]++;
                float radius = 0.35f + Mathf.Min(counts[r] - 1, 4) * 0.12f;
                Vector3 pos = basePos + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius);

                var pin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pin.name = "Plant_" + p.Id;
                pin.transform.SetParent(_plantRoot, false);
                pin.transform.position = pos;
                float h = 0.28f + Mathf.Clamp(p.CapacityMw / 1200f, 0.05f, 0.55f);
                pin.transform.localScale = new Vector3(0.32f, h, 0.32f);

                var col = pin.GetComponent<CapsuleCollider>();
                if (col != null) col.height = 2.2f;

                var marker = pin.GetComponent<PlantMarker>();
                if (marker == null) marker = pin.AddComponent<PlantMarker>();
                marker.Configure(p.Id, MapPalette.Fuel(p.Fuel));
            }
        }

        private void BuildWorld()
        {
            ClearLegacyGreybox();

            var rootGo = GameObject.Find("PoliticalMap");
            if (rootGo == null) rootGo = new GameObject("PoliticalMap");
            _root = rootGo.transform;

            // Drop prior runtime children so art/collider recalibration can rebuild cleanly.
            for (int i = _root.childCount - 1; i >= 0; i--)
                SafeDestroy(_root.GetChild(i).gameObject);

            _sharedLit = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                      ?? Shader.Find("Universal Render Pipeline/Unlit")
                                      ?? Shader.Find("Standard"));

            // Strip any leftover ocean backdrop from prior sessions / scenes.
            DestroyOceanBackdrop();

            if (UsaMapLayout.UsePrototypeArt)
                BuildPrototypeArt();

            BuildStates();
            BuildCorridors();
            RefreshLabels(force: true);
        }

        private void ClearLegacyGreybox()
        {
            DestroyIfExists("GreyboxMap");
            DestroyIfExists("Region_North");
            DestroyIfExists("Region_Coast");
            DestroyIfExists("Region_Desert");
            DestroyIfExists("PlantIcons");
        }

        private void DestroyOceanBackdrop()
        {
            DestroyIfExists("MapOcean");
            DestroyIfExists("OceanBackdrop");
            if (_root == null) return;
            Transform ocean = _root.Find("MapOcean");
            if (ocean != null) SafeDestroy(ocean.gameObject);
            Transform legacy = _root.Find("OceanBackdrop");
            if (legacy != null) SafeDestroy(legacy.gameObject);
        }

        private static void DestroyIfExists(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) return;
            SafeDestroy(go);
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }

        private void BuildPrototypeArt()
        {
            Transform existing = _root != null ? _root.Find("MapArt") : null;
            if (existing != null) SafeDestroy(existing.gameObject);

            Sprite sprite = prototypeMapSprite != null ? prototypeMapSprite : LoadPrototypeSprite();
            if (sprite == null)
            {
                Debug.LogWarning("PoliticalMapPresenter: map sprite missing at " + UsaMapLayout.PrototypeArtPath);
                return;
            }

            // SpriteRenderer (not a MeshRenderer quad): PPU owns world size/aspect so
            // placeholder art swaps do not stretch UVs into a forced XZ scale.
            var art = new GameObject("MapArt");
            art.transform.SetParent(_root, false);
            // Face +Y toward the top-down map camera. Yaw 0 keeps PNG geography
            // upright: west → −X, east → +X, Florida bottom-right of CONUS.
            // (Yaw 180 mirrored the art against UsaMapLayout centroids.)
            art.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            art.transform.position = new Vector3(
                ResolveArtCenter().x,
                ResolveArtHeight(),
                ResolveArtCenter().y);

            var sr = art.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingOrder = 0;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;

            // Uniform scale only: MapTuning may retarget width while preserving aspect.
            Vector2 natural = sprite.bounds.size;
            Vector2 target = ResolveArtSize();
            float uniform = 1f;
            if (natural.x > 0.0001f)
                uniform = target.x / natural.x;
            art.transform.localScale = new Vector3(uniform, uniform, 1f);
        }

        private static Sprite LoadPrototypeSprite()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<Sprite>(UsaMapLayout.PrototypeArtPath);
#else
            return Resources.Load<Sprite>("Map/north-america");
#endif
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

        private void BuildStates()
        {
            var statesRoot = new GameObject("States");
            statesRoot.transform.SetParent(_root, false);
            _statesRoot = statesRoot.transform;

            int n = UsaMapLayout.States.Length;
            _labels = new TextMesh[n];
            _labelCodes = new string[n];
            _labelNames = new string[n];

            bool artMode = UsaMapLayout.UsePrototypeArt;

            for (int i = 0; i < n; i++)
            {
                UsaMapLayout.StatePoly poly = UsaMapLayout.States[i];
                var go = new GameObject("State_" + poly.Code);
                go.transform.SetParent(_statesRoot, false);

                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = PolygonMeshUtil.BuildFlatMesh(poly.Ring, UsaMapLayout.RegionHeight, "Mesh_" + poly.Code);

                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(_sharedLit);
                // Invisible fills when PNG is the visible map; keep meshes for colliders.
                Color fill = artMode
                    ? new Color(0f, 0f, 0f, 0f)
                    : UsaMapLayout.StateFill(poly.Code, sunRichTint);
                mr.sharedMaterial.color = fill;
                if (artMode)
                {
                    mr.enabled = false;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }

                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;

                // Art Alaska is a navy silhouette — optional invisible inset pick; never draw fill/border/label.
                bool skipAkVisual = artMode && poly.Code == "AK";
                bool akPickOff = poly.Code == "AK" && !UsaMapLayout.AlaskaPickEnabled;
                if (akPickOff) mc.enabled = false;

                var marker = go.AddComponent<RegionMarker>();
                marker.Configure(poly.Region, poly.Code, poly.FullName, fill);
                if (akPickOff) marker.enabled = false;

                if (!artMode)
                    BuildBorder(go.transform, poly);

                if (!skipAkVisual)
                {
                    _labels[i] = BuildLabel(go.transform, poly);
                    _labelCodes[i] = poly.Code;
                    _labelNames[i] = poly.FullName;
                }
                else
                {
                    _labels[i] = null;
                    _labelCodes[i] = poly.Code;
                    _labelNames[i] = poly.FullName;
                }
            }
        }

        private void BuildBorder(Transform parent, UsaMapLayout.StatePoly poly)
        {
            var borderGo = new GameObject("Border");
            borderGo.transform.SetParent(parent, false);
            var lr = borderGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.widthMultiplier = 0.045f;
            lr.numCornerVertices = 2;
            lr.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
            lr.startColor = lr.endColor = MapPalette.Border;
            Vector3[] pts = PolygonMeshUtil.OutlineWorld(poly.Ring, UsaMapLayout.RegionHeight + 0.1f, closed: true);
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
        }

        private static TextMesh BuildLabel(Transform parent, UsaMapLayout.StatePoly poly)
        {
            Vector2 c2 = UsaMapLayout.Centroid2(poly.Ring);
            Vector3 c = new Vector3(c2.x, UsaMapLayout.RegionHeight + 0.1f, c2.y);
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.position = c + Vector3.up * 0.08f;
            labelGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = poly.Code;
            tm.fontSize = 24;
            tm.characterSize = 0.055f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.95f, 0.93f, 0.88f, 0.82f);
            tm.fontStyle = FontStyle.Bold;
            return tm;
        }

        private void BuildCorridors()
        {
            var corridorGo = new GameObject("TransmissionCorridors");
            corridorGo.transform.SetParent(_root, false);
            _corridorRoot = corridorGo.transform;

            bool soft = UsaMapLayout.UsePrototypeArt;
            Color softTint = soft ? new Color(0.78f, 0.7f, 0.38f, 0.28f) : default;

            for (int i = 0; i < UsaMapLayout.Corridors.Length; i++)
            {
                UsaMapLayout.Corridor c = UsaMapLayout.Corridors[i];
                Vector3 a = UsaMapLayout.Centroid(c.From) + Vector3.up * 0.12f;
                Vector3 b = UsaMapLayout.Centroid(c.To) + Vector3.up * 0.12f;

                var lineGo = new GameObject("Cable_" + c.From + "_" + c.To);
                lineGo.transform.SetParent(_corridorRoot, false);
                var lr = lineGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.widthMultiplier = soft ? 0.035f : 0.07f;
                lr.numCapVertices = 2;
                lr.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
                Color col = soft ? softTint : c.Color;
                lr.startColor = lr.endColor = col;
                lr.positionCount = 2;
                lr.SetPositions(new[] { a, b });
            }
        }

        private void RefreshLabels(bool force = false)
        {
            if (_labels == null) return;

            Camera cam = Camera.main;
            float ortho = cam != null && cam.orthographic ? cam.orthographicSize : UsaMapLayout.DefaultOrthoSize;
            string selected = RegionMarker.SelectedStateCode ?? "";
            bool fullNames = ortho <= UsaMapLayout.FullNameOrthoThreshold;

            if (!force
                && Mathf.Abs(ortho - _lastOrtho) < 0.05f
                && selected == _lastSelectedState
                && fullNames == _lastFullNames)
                return;

            _lastOrtho = ortho;
            _lastSelectedState = selected;
            _lastFullNames = fullNames;

            for (int i = 0; i < _labels.Length; i++)
            {
                TextMesh tm = _labels[i];
                if (tm == null) continue;

                bool isSelected = !string.IsNullOrEmpty(selected) && selected == _labelCodes[i];
                bool showFull = fullNames || isSelected;
                tm.text = showFull ? _labelNames[i] : _labelCodes[i];
                tm.characterSize = showFull ? 0.042f : 0.055f;
                tm.color = isSelected
                    ? new Color(1f, 0.95f, 0.7f, 0.95f)
                    : new Color(0.95f, 0.93f, 0.88f, showFull ? 0.9f : 0.78f);
            }
        }

        private void RebuildColors()
        {
            if (_root == null) return;
            if (UsaMapLayout.UsePrototypeArt) return;

            if (_statesRoot == null) _statesRoot = _root.Find("States");
            if (_statesRoot == null) return;
            for (int i = 0; i < _statesRoot.childCount; i++)
            {
                var marker = _statesRoot.GetChild(i).GetComponent<RegionMarker>();
                if (marker == null) continue;
                Color fill = UsaMapLayout.StateFill(marker.StateCode, sunRichTint);
                marker.Configure(marker.RegionId, marker.StateCode, marker.FullName, fill);
            }
        }

        private void ApplyColor(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            if (r.sharedMaterial == null || r.sharedMaterial == _sharedLit)
                r.sharedMaterial = new Material(_sharedLit);
            else
                r.sharedMaterial = new Material(r.sharedMaterial);
            r.sharedMaterial.color = color;
        }
    }
}
