using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using MinistryOfPower.Simulation;
using MinistryOfPower.UI.Map;

namespace MinistryOfPower.Runtime
{
    /// <summary>
    /// Grey-box day-night lighting + weather tint. Map geometry lives in
    /// <see cref="PoliticalMapPresenter"/>; this component owns lighting and left-click picks.
    /// </summary>
    public sealed class DayNightWeatherController : MonoBehaviour
    {
        [SerializeField] private Light sunLight;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private PoliticalMapPresenter mapPresenter;
        [SerializeField] private MapCameraController mapCamera;

        private MinistryGameRunner _runner;

        public void Bind(MinistryGameRunner runner)
        {
            _runner = runner;
            EnsureWorld();
        }

        private void Update()
        {
            // Input System–safe map clicks (OnMouseDown is unreliable when legacy input is off).
            if (!WasPrimaryClick()) return;
            if (IsRightHeld()) return; // never steal while RMB-panning
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;
            Ray ray = targetCamera.ScreenPointToRay(ReadPointerScreen());
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f)) return;

            var plant = hit.collider.GetComponentInParent<PlantMarker>();
            if (plant != null && !string.IsNullOrEmpty(plant.PlantId))
            {
                PlantMarker.Select(plant.PlantId);
                return;
            }

            var region = hit.collider.GetComponentInParent<RegionMarker>();
            if (region != null) RegionMarker.Select(region.RegionId, region.StateCode);
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

            bool sunRich = _runner != null
                           && _runner.Session?.Scenario != null
                           && _runner.Session.Scenario.Id == "sun_rich";
            mapPresenter.Configure(sunRich);
            mapPresenter.EnsureBuilt();
            mapCamera.Bind(targetCamera);

            // Soft top light for flat political map (sun still animates in LateUpdate).
            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Euler(70f, -30f, 0f);
                sunLight.shadows = LightShadows.None;
            }
        }

        private void LateUpdate()
        {
            if (_runner == null || _runner.Session == null || _runner.Session.Clock == null)
                return;

            SyncScenarioTint();
            mapPresenter?.RefreshPlants(_runner.Session);

            GameClock clock = _runner.Session.Clock;
            float t = clock.DayFraction;
            float elev = Mathf.Sin((t - 0.25f) * Mathf.PI * 2f);
            float intensity = Mathf.Clamp01(elev * 0.9f + 0.15f);
            if (sunLight != null)
            {
                // Keep mostly top-down; nudge azimuth for day cycle without wrecking map readability.
                sunLight.transform.rotation = Quaternion.Euler(55f + elev * 25f, -30f + t * 40f, 0f);
                sunLight.intensity = intensity * WeatherLightMul(_runner.Session.CurrentWeather);
                sunLight.color = Color.Lerp(new Color(0.4f, 0.45f, 0.7f), new Color(1f, 0.96f, 0.88f), intensity);
            }

            if (targetCamera != null)
            {
                Color daySky = new Color(0.38f, 0.48f, 0.58f);
                Color nightSky = new Color(0.05f, 0.06f, 0.1f);
                Color sky = Color.Lerp(nightSky, daySky, intensity);
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
