using UnityEngine;
using MinistryOfPower.Runtime;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.UI.Map
{
    /// <summary>
    /// Paradox political-map presenter: USA grey-box regions, interconnects, plant pins.
    /// Presentation only — simulation stays in GameSession / RegionCatalog.
    /// </summary>
    public sealed class PoliticalMapPresenter : MonoBehaviour
    {
        [SerializeField] private bool sunRichTint;

        private Transform _root;
        private Transform _plantRoot;
        private Transform _corridorRoot;
        private int _lastPlantSig = int.MinValue;
        private bool _built;
        private Material _sharedLit;

        public void Configure(bool sunRich)
        {
            if (_built && sunRichTint == sunRich) return;
            sunRichTint = sunRich;
            if (_built) RebuildColors();
        }

        public void EnsureBuilt()
        {
            if (_built) return;
            BuildWorld();
            _built = true;
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

                Vector3 basePos = UsaMapLayout.PlantAnchor(p.Region);
                float ang = counts[r] * 0.85f;
                counts[r]++;
                Vector3 pos = basePos + new Vector3(Mathf.Cos(ang) * 1.15f, 0f, Mathf.Sin(ang) * 1.15f);

                var pin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pin.name = "Plant_" + p.Id;
                pin.transform.SetParent(_plantRoot, false);
                pin.transform.position = pos;
                float h = 0.28f + Mathf.Clamp(p.CapacityMw / 1200f, 0.05f, 0.55f);
                pin.transform.localScale = new Vector3(0.38f, h, 0.38f);

                // Flatten collider slightly taller for easier picks under ortho cam.
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

            _sharedLit = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                      ?? Shader.Find("Universal Render Pipeline/Unlit")
                                      ?? Shader.Find("Standard"));

            BuildOcean();
            BuildRegions();
            BuildCorridors();
        }

        private void ClearLegacyGreybox()
        {
            DestroyIfExists("GreyboxMap");
            DestroyIfExists("Region_North");
            DestroyIfExists("Region_Coast");
            DestroyIfExists("Region_Desert");
            DestroyIfExists("PlantIcons");
        }

        private static void DestroyIfExists(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) Destroy(go);
        }

        private void BuildOcean()
        {
            var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ocean.name = "OceanBackdrop";
            ocean.transform.SetParent(_root, false);
            ocean.transform.position = new Vector3(0f, UsaMapLayout.OceanHeight, 0f);
            ocean.transform.localScale = new Vector3(
                UsaMapLayout.OceanSize.x / 10f,
                1f,
                UsaMapLayout.OceanSize.y / 10f);
            ApplyColor(ocean, UsaMapLayout.OceanFill(sunRichTint));
            // Ocean is not selectable — remove collider so region meshes receive hits.
            var col = ocean.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private void BuildRegions()
        {
            var regionsRoot = new GameObject("Regions");
            regionsRoot.transform.SetParent(_root, false);

            for (int i = 0; i < UsaMapLayout.Regions.Length; i++)
            {
                UsaMapLayout.RegionPoly poly = UsaMapLayout.Regions[i];
                var go = new GameObject("Region_" + poly.Id);
                go.transform.SetParent(regionsRoot.transform, false);

                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = PolygonMeshUtil.BuildFlatMesh(poly.Ring, UsaMapLayout.RegionHeight, "Mesh_" + poly.Id);

                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(_sharedLit);
                mr.sharedMaterial.color = UsaMapLayout.RegionFill(poly.Id, sunRichTint);

                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;

                var marker = go.AddComponent<RegionMarker>();
                marker.Configure(poly.Id, UsaMapLayout.RegionFill(poly.Id, sunRichTint));

                BuildBorder(go.transform, poly);
                BuildLabel(go.transform, poly);
            }
        }

        private void BuildBorder(Transform parent, UsaMapLayout.RegionPoly poly)
        {
            var borderGo = new GameObject("Border");
            borderGo.transform.SetParent(parent, false);
            var lr = borderGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.widthMultiplier = 0.08f;
            lr.numCornerVertices = 2;
            lr.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
            lr.startColor = lr.endColor = MapPalette.Border;
                Vector3[] pts = PolygonMeshUtil.OutlineWorld(poly.Ring, UsaMapLayout.RegionHeight + 0.1f, closed: true);
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
        }

        private void BuildLabel(Transform parent, UsaMapLayout.RegionPoly poly)
        {
            Vector3 c = UsaMapLayout.Centroid(poly.Id);
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.position = c + Vector3.up * 0.08f;
            // Face camera looking down -Y (ortho political map).
            labelGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = poly.MapLabel;
            tm.fontSize = 28;
            tm.characterSize = 0.12f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.95f, 0.93f, 0.88f, 0.85f);
            tm.fontStyle = FontStyle.Bold;
        }

        private void BuildCorridors()
        {
            var corridorGo = new GameObject("TransmissionCorridors");
            corridorGo.transform.SetParent(_root, false);
            _corridorRoot = corridorGo.transform;

            for (int i = 0; i < UsaMapLayout.Corridors.Length; i++)
            {
                UsaMapLayout.Corridor c = UsaMapLayout.Corridors[i];
                Vector3 a = UsaMapLayout.Centroid(c.From) + Vector3.up * 0.12f;
                Vector3 b = UsaMapLayout.Centroid(c.To) + Vector3.up * 0.12f;

                var lineGo = new GameObject("Cable_" + c.From + "_" + c.To);
                lineGo.transform.SetParent(_corridorRoot, false);
                var lr = lineGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.widthMultiplier = 0.14f;
                lr.numCapVertices = 4;
                lr.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
                lr.startColor = lr.endColor = c.Color;
                lr.positionCount = 2;
                lr.SetPositions(new[] { a, b });
            }
        }

        private void RebuildColors()
        {
            if (_root == null) return;
            Transform ocean = _root.Find("OceanBackdrop");
            if (ocean != null) ApplyColor(ocean.gameObject, UsaMapLayout.OceanFill(sunRichTint));

            Transform regions = _root.Find("Regions");
            if (regions == null) return;
            for (int i = 0; i < regions.childCount; i++)
            {
                var marker = regions.GetChild(i).GetComponent<RegionMarker>();
                if (marker == null) continue;
                Color fill = UsaMapLayout.RegionFill(marker.RegionId, sunRichTint);
                marker.Configure(marker.RegionId, fill);
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
