using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using MinistryOfPower.Simulation;

namespace MinistryOfPower.Runtime
{
    /// <summary>
    /// Grey-box day-night lighting + simple weather tint driven by GameSession clock.
    /// </summary>
    public sealed class DayNightWeatherController : MonoBehaviour
    {
        [SerializeField] private Light sunLight;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Material groundMaterial;

        private MinistryGameRunner _runner;
        private GameObject _ground;
        private GameObject _skyDisc;
        private Transform _plantIconRoot;
        private int _lastPlantSig = int.MinValue;

        public void Bind(MinistryGameRunner runner)
        {
            _runner = runner;
            EnsureWorld();
        }

        private void Update()
        {
            // Input System–safe map clicks (OnMouseDown is unreliable when legacy input is off).
            if (!WasPrimaryClick()) return;
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;
            Ray ray = targetCamera.ScreenPointToRay(ReadPointerScreen());
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f)) return;

            var plant = hit.collider.GetComponentInParent<PlantMarker>();
            if (plant != null && !string.IsNullOrEmpty(plant.PlantId))
            {
                PlantMarker.Select(plant.PlantId);
                return;
            }

            var region = hit.collider.GetComponentInParent<RegionMarker>();
            if (region != null) RegionMarker.Select(region.RegionId);
        }

        private static bool WasPrimaryClick()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        private static Vector3 ReadPointerScreen()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 p = mouse.position.ReadValue();
                return new Vector3(p.x, p.y, 0f);
            }

            return Vector3.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector3.zero;
#endif
        }

        private void EnsureWorld()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (sunLight == null)
            {
                var lightGo = GameObject.Find("Directional Light");
                if (lightGo != null) sunLight = lightGo.GetComponent<Light>();
            }

            if (_ground == null)
            {
                _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                _ground.name = "GreyboxMap";
                _ground.transform.position = new Vector3(0f, 0f, 0f);
                _ground.transform.localScale = new Vector3(4f, 1f, 4f);
                var renderer = _ground.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                                     ?? Shader.Find("Standard"));
                    renderer.material.color = new Color(0.35f, 0.4f, 0.32f);
                }
            }

            // Clickable region markers (grey-box map)
            EnsureMarker(RegionId.North, "Region_North", new Vector3(-6f, 0.2f, 6f));
            EnsureMarker(RegionId.Coast, "Region_Coast", new Vector3(7f, 0.2f, -2f));
            EnsureMarker(RegionId.Desert, "Region_Desert", new Vector3(-4f, 0.2f, -7f));

            if (targetCamera != null)
            {
                targetCamera.transform.position = new Vector3(0f, 18f, -14f);
                targetCamera.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
            }
        }

        private static void EnsureMarker(RegionId id, string name, Vector3 pos)
        {
            GameObject cube = GameObject.Find(name);
            if (cube == null)
            {
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = name;
                cube.transform.position = pos;
                cube.transform.localScale = new Vector3(2.2f, 0.4f, 2.2f);
            }

            var marker = cube.GetComponent<RegionMarker>();
            if (marker == null) marker = cube.AddComponent<RegionMarker>();
            marker.Configure(id, RegionMarker.BaseColor(id));
        }

        private void LateUpdate()
        {
            if (_runner == null || _runner.Session == null || _runner.Session.Clock == null)
            {
                return;
            }

            RefreshPlantIcons(_runner.Session);

            GameClock clock = _runner.Session.Clock;
            float t = clock.DayFraction;
            // Sun angle: midnight low, noon high
            float elev = Mathf.Sin((t - 0.25f) * Mathf.PI * 2f);
            float intensity = Mathf.Clamp01(elev * 0.9f + 0.15f);
            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Euler(elev * 70f + 10f, 40f + t * 60f, 0f);
                sunLight.intensity = intensity * WeatherLightMul(_runner.Session.CurrentWeather);
                sunLight.color = Color.Lerp(new Color(0.35f, 0.4f, 0.7f), new Color(1f, 0.95f, 0.85f), intensity);
            }

            if (targetCamera != null)
            {
                Color daySky = new Color(0.45f, 0.62f, 0.85f);
                Color nightSky = new Color(0.05f, 0.06f, 0.12f);
                Color sky = Color.Lerp(nightSky, daySky, intensity);
                sky = ApplyWeatherSky(sky, _runner.Session.CurrentWeather);
                targetCamera.backgroundColor = sky;
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
            }

            if (_ground != null)
            {
                var r = _ground.GetComponent<Renderer>();
                if (r != null && r.material != null)
                {
                    Color baseG = new Color(0.32f, 0.38f, 0.28f);
                    r.material.color = Color.Lerp(baseG * 0.35f, baseG, intensity);
                }
            }
        }

        private void RefreshPlantIcons(GameSession session)
        {
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

            if (_plantIconRoot == null)
            {
                var go = new GameObject("PlantIcons");
                _plantIconRoot = go.transform;
            }

            for (int i = _plantIconRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_plantIconRoot.GetChild(i).gameObject);
            }

            int[] counts = new int[3];
            for (int i = 0; i < plants.Count; i++)
            {
                PlantInstance p = plants[i];
                if (p.IsRetired) continue;
                int r = (int)p.Region;
                if (r < 0 || r > 2) r = 0;
                Vector3 basePos = RegionBase(p.Region);
                float ang = counts[r] * 0.7f;
                counts[r]++;
                Vector3 pos = basePos + new Vector3(Mathf.Cos(ang) * 1.4f, 0.55f, Mathf.Sin(ang) * 1.4f);
                var icon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                icon.name = "Plant_" + p.Id;
                icon.transform.SetParent(_plantIconRoot, false);
                icon.transform.position = pos;
                icon.transform.localScale = new Vector3(0.45f, 0.35f + p.CapacityMw / 800f, 0.45f);
                var marker = icon.GetComponent<PlantMarker>();
                if (marker == null) marker = icon.AddComponent<PlantMarker>();
                marker.Configure(p.Id, FuelColor(p.Fuel));
            }
        }

        private static Vector3 RegionBase(RegionId id)
        {
            switch (id)
            {
                case RegionId.North: return new Vector3(-6f, 0.2f, 6f);
                case RegionId.Coast: return new Vector3(7f, 0.2f, -2f);
                case RegionId.Desert: return new Vector3(-4f, 0.2f, -7f);
                default: return Vector3.zero;
            }
        }

        private static Color FuelColor(FuelKind fuel)
        {
            switch (fuel)
            {
                case FuelKind.Coal: return new Color(0.25f, 0.22f, 0.2f);
                case FuelKind.Gas: return new Color(0.55f, 0.45f, 0.25f);
                case FuelKind.Oil: return new Color(0.45f, 0.2f, 0.15f);
                case FuelKind.Solar: return new Color(0.85f, 0.7f, 0.2f);
                case FuelKind.Wind: return new Color(0.45f, 0.65f, 0.75f);
                case FuelKind.Hydro: return new Color(0.25f, 0.45f, 0.7f);
                case FuelKind.Nuclear: return new Color(0.4f, 0.75f, 0.45f);
                case FuelKind.Storage: return new Color(0.55f, 0.4f, 0.7f);
                default: return Color.gray;
            }
        }

        private static float WeatherLightMul(WeatherKind w)
        {
            switch (w)
            {
                case WeatherKind.Overcast: return 0.65f;
                case WeatherKind.Storm: return 0.45f;
                case WeatherKind.HeatHaze: return 1.1f;
                default: return 1f;
            }
        }

        private static Color ApplyWeatherSky(Color sky, WeatherKind w)
        {
            switch (w)
            {
                case WeatherKind.Overcast: return Color.Lerp(sky, new Color(0.45f, 0.45f, 0.48f), 0.55f);
                case WeatherKind.Storm: return Color.Lerp(sky, new Color(0.25f, 0.28f, 0.32f), 0.7f);
                case WeatherKind.HeatHaze: return Color.Lerp(sky, new Color(0.7f, 0.55f, 0.35f), 0.35f);
                default: return sky;
            }
        }
    }
}
