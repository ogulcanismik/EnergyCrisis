using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using MinistryOfPower.Data;
using MinistryOfPower.Simulation;
using MinistryOfPower.UI.Map;

namespace MinistryOfPower.Runtime
{
    /// <summary>
    /// Grey-box day-night presentation + weather tint. Map geometry lives in
    /// <see cref="PoliticalMapPresenter"/>; day/night look is a terminator overlay
    /// (<see cref="MapDayTerminator"/>), not an orbiting light. This component also owns left-click picks.
    /// </summary>
    public sealed class DayNightWeatherController : MonoBehaviour
    {
        [SerializeField] private Light sunLight;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private PoliticalMapPresenter mapPresenter;
        [SerializeField] private MapCameraController mapCamera;
        [SerializeField] private MapDayTerminator dayTerminator;
        [SerializeField] private GameTuning gameTuning;

        private MinistryGameRunner _runner;

        public void Bind(MinistryGameRunner runner)
        {
            _runner = runner;
            EnsureWorld();
        }

        public void BindGameTuning(GameTuning tuning)
        {
            gameTuning = tuning;
            if (dayTerminator != null) dayTerminator.BindGameTuning(tuning);
        }

        private void Update()
        {
            // Input System–safe map clicks (OnMouseDown is unreliable when legacy input is off).
            if (!WasPrimaryClick()) return;
            if (IsRightHeld()) return; // never steal while RMB-panning
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;
            Ray ray = targetCamera.ScreenPointToRay(ReadPointerScreen());
            RaycastHit[] hits = Physics.RaycastAll(ray, 500f);
            if (hits == null || hits.Length == 0) return;

            // Plants win over state meshes when both are under the cursor.
            PlantMarker bestPlant = null;
            float bestPlantDist = float.MaxValue;
            RegionMarker bestRegion = null;
            float bestRegionArea = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                var plant = h.collider.GetComponentInParent<PlantMarker>();
                if (plant != null && !string.IsNullOrEmpty(plant.PlantId) && h.distance < bestPlantDist)
                {
                    bestPlantDist = h.distance;
                    bestPlant = plant;
                }

                var region = h.collider.GetComponentInParent<RegionMarker>();
                if (region == null || string.IsNullOrEmpty(region.StateCode)) continue;
                Bounds b = h.collider.bounds;
                float area = b.size.x * b.size.z;
                if (area < bestRegionArea)
                {
                    bestRegionArea = area;
                    bestRegion = region;
                }
            }

            if (bestPlant != null)
            {
                PlantMarker.Select(bestPlant.PlantId);
                return;
            }

            if (bestRegion != null)
                RegionMarker.Select(bestRegion.RegionId, bestRegion.StateCode);
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

        private static bool IsRightHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.rightButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
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

            if (mapPresenter == null)
            {
                mapPresenter = GetComponent<PoliticalMapPresenter>();
                if (mapPresenter == null) mapPresenter = gameObject.AddComponent<PoliticalMapPresenter>();
            }

            if (mapCamera == null)
            {
                mapCamera = GetComponent<MapCameraController>();
                if (mapCamera == null) mapCamera = gameObject.AddComponent<MapCameraController>();
            }

            if (dayTerminator == null)
            {
                dayTerminator = GetComponent<MapDayTerminator>();
                if (dayTerminator == null) dayTerminator = gameObject.AddComponent<MapDayTerminator>();
            }

            if (gameTuning == null)
            {
                var gm = FindFirstObjectByType<GameManager>();
                if (gm != null) gameTuning = gm.Tuning;
            }

            bool sunRich = _runner != null
                           && _runner.Session?.Scenario != null
                           && _runner.Session.Scenario.Id == "sun_rich";
            mapPresenter.Configure(sunRich);
            mapPresenter.EnsureBuilt();
            mapCamera.Bind(targetCamera);

            dayTerminator.BindGameTuning(gameTuning);
            dayTerminator.EnsureOverlay();

            // Stable top-down key light for map readability — day/night is the terminator overlay.
            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Euler(70f, -30f, 0f);
                sunLight.shadows = LightShadows.None;
                sunLight.intensity = 1.05f;
                sunLight.color = new Color(1f, 0.98f, 0.94f);
            }
        }

        private void LateUpdate()
        {
            if (_runner == null || _runner.Session == null || _runner.Session.Clock == null)
                return;

            SyncScenarioTint();
            mapPresenter?.RefreshPlants(_runner.Session);

            GameClock clock = _runner.Session.Clock;
            dayTerminator?.ApplyClock(clock);

            // Keep ambient light readable; weather only gently dims (not a day-night orbit).
            if (sunLight != null)
            {
                float weatherMul = WeatherLightMul(_runner.Session.CurrentWeather);
                sunLight.intensity = 1.05f * weatherMul;
                sunLight.color = new Color(1f, 0.98f, 0.94f);
                sunLight.transform.rotation = Quaternion.Euler(70f, -30f, 0f);
            }

            if (targetCamera != null)
            {
                float t = clock.DayFraction;
                float elev = Mathf.Sin((t - 0.25f) * Mathf.PI * 2f);
                float dayness = Mathf.Clamp01(elev * 0.9f + 0.15f);
                Color daySky = new Color(0.38f, 0.48f, 0.58f);
                Color nightSky = new Color(0.05f, 0.06f, 0.1f);
                Color sky = Color.Lerp(nightSky, daySky, dayness);
                sky = ApplyWeatherSky(sky, _runner.Session.CurrentWeather);
                targetCamera.backgroundColor = sky;
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
            }
        }

        private void SyncScenarioTint()
        {
            if (mapPresenter == null || _runner?.Session?.Scenario == null) return;
            bool sunRich = _runner.Session.Scenario.Id == "sun_rich";
            mapPresenter.Configure(sunRich);
        }

        private static float WeatherLightMul(WeatherKind w)
        {
            switch (w)
            {
                case WeatherKind.Overcast: return 0.85f;
                case WeatherKind.Storm: return 0.7f;
                case WeatherKind.HeatHaze: return 1.05f;
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
