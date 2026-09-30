using UnityEngine;
using MinistryOfPower.Data;
using MinistryOfPower.Simulation;
using MinistryOfPower.UI;
using MinistryOfPower.UI.Map;

namespace MinistryOfPower.Runtime
{
    /// <summary>
    /// Scene manager hub: owns <see cref="GameTuning"/>, wires UI / map / clock at Awake,
    /// and bootstraps Managers / World / UI parenting when Play starts from a thin scene.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Tuning")]
        [SerializeField] private GameTuning tuning;

        [Header("Managers")]
        [SerializeField] private MinistryGameRunner runner;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private MapTuning mapTuning;

        private GameTuning _runtimeTuning;

        public GameTuning Tuning
        {
            get
            {
                if (tuning != null) return tuning;
                if (_runtimeTuning == null) _runtimeTuning = GameTuning.CreateRuntimeDefaults();
                return _runtimeTuning;
            }
        }

        public MinistryGameRunner Runner => runner;
        public UIManager UI => uiManager;
        public AudioManager Audio => audioManager;
        public MapTuning Map => mapTuning;

        private void Awake()
        {
            Instance = this;
            DeskHierarchy.Ensure(this);
            ResolveReferences();
            ApplyTuning();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ApplyTuning()
        {
            GameTuning t = Tuning;
            GameClock.ApplyPaceTuning(t.secondsPerDay1x, t.speedMultiplierFast, t.speedMultiplierVeryFast);

            if (mapTuning != null)
                mapTuning.BindGameTuning(t);

            if (uiManager != null)
                uiManager.BindTuning(t);

            if (runner != null)
                runner.BindFromGameManager(this);
        }

        public void ResolveReferences()
        {
            if (runner == null) runner = GetComponent<MinistryGameRunner>();
            if (runner == null) runner = GetComponentInChildren<MinistryGameRunner>(true);
            if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();
            if (audioManager == null) audioManager = FindFirstObjectByType<AudioManager>();
            if (mapTuning == null) mapTuning = FindFirstObjectByType<MapTuning>();
        }

        public void AssignRefs(MinistryGameRunner gameRunner, UIManager ui, AudioManager audio, MapTuning map)
        {
            if (gameRunner != null) runner = gameRunner;
            if (ui != null) uiManager = ui;
            if (audio != null) audioManager = audio;
            if (map != null) mapTuning = map;
        }

        public void AssignTuningAsset(GameTuning asset)
        {
            if (asset != null) tuning = asset;
        }

        /// <summary>Play-mode / bootstrap entry: create hub if the desk scene was left empty.</summary>
        public static GameManager Ensure()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<GameManager>();
            if (existing != null)
            {
                Instance = existing;
                existing.ResolveReferences();
                existing.ApplyTuning();
                return existing;
            }

            Transform managers = DeskHierarchy.EnsureRoot("Managers");
            var go = new GameObject("GameManager");
            go.transform.SetParent(managers, false);
            var gm = go.AddComponent<GameManager>();
            go.AddComponent<MinistryGameRunner>();
            Instance = gm;
            DeskHierarchy.Ensure(gm);
            gm.ResolveReferences();
            gm.ApplyTuning();
            return gm;
        }

#if UNITY_EDITOR
        public void EditorAssign(
            GameTuning gameTuning,
            MinistryGameRunner gameRunner,
            UIManager ui,
            AudioManager audio,
            MapTuning map)
        {
            tuning = gameTuning;
            runner = gameRunner;
            uiManager = ui;
            audioManager = audio;
            mapTuning = map;
        }
#endif
    }

    /// <summary>
    /// Ensures Managers / World / UI parenting and required components exist
    /// so Play from MainMenu still works on a partially wired desk.
    /// </summary>
    public static class DeskHierarchy
    {
        public static void Ensure(GameManager hub)
        {
            if (hub == null) return;

            Transform managers = EnsureRoot("Managers");
            Transform world = EnsureRoot("World");
            EnsureRoot("UI");

            if (hub.transform.parent != managers)
                hub.transform.SetParent(managers, true);

            if (hub.gameObject.name == "MinistryRoot" || hub.gameObject.name == "MinistryGameRunner")
                hub.gameObject.name = "GameManager";

            MinistryGameRunner runner = hub.GetComponent<MinistryGameRunner>();
            if (runner == null) runner = hub.gameObject.AddComponent<MinistryGameRunner>();

            UIManager uiMgr = Object.FindFirstObjectByType<UIManager>();
            if (uiMgr == null)
            {
                var uiGo = new GameObject("UIManager");
                uiGo.transform.SetParent(managers, false);
                uiMgr = uiGo.AddComponent<UIManager>();
            }
            else if (uiMgr.transform.parent != managers)
            {
                uiMgr.transform.SetParent(managers, true);
            }

            // Prefer HUD on UIManager; fall back to any existing scene HUD (e.g. legacy MinistryRoot).
            ParadoxChromeHud hud = uiMgr.GetComponent<ParadoxChromeHud>();
            if (hud == null)
                hud = Object.FindFirstObjectByType<ParadoxChromeHud>();
            if (hud == null)
                hud = uiMgr.gameObject.AddComponent<ParadoxChromeHud>();
            else if (hud.gameObject != uiMgr.gameObject && uiMgr.GetComponent<ParadoxChromeHud>() == null)
            {
                // Leave existing HUD where it is; UIManager just references it.
            }

            uiMgr.EnsureHud();
            if (uiMgr.ParadoxHud == null)
            {
                // EnsureHud may have added one; if hub still holds the only HUD, bind that.
                var anyHud = Object.FindFirstObjectByType<ParadoxChromeHud>();
                if (anyHud != null)
                    uiMgr.EnsureHud();
            }

            AudioManager audio = Object.FindFirstObjectByType<AudioManager>();
            if (audio == null)
            {
                var audioGo = new GameObject("AudioManager");
                audioGo.transform.SetParent(managers, false);
                audio = audioGo.AddComponent<AudioManager>();
            }
            else if (audio.transform.parent != managers)
            {
                audio.transform.SetParent(managers, true);
            }

            GameObject worldHost = FindOrCreateChild(world, "MapSystems");

            DayNightWeatherController dayNight = Object.FindFirstObjectByType<DayNightWeatherController>();
            if (dayNight == null)
                dayNight = worldHost.AddComponent<DayNightWeatherController>();

            if (Object.FindFirstObjectByType<PoliticalMapPresenter>() == null)
                worldHost.AddComponent<PoliticalMapPresenter>();

            if (Object.FindFirstObjectByType<MapCameraController>() == null)
                worldHost.AddComponent<MapCameraController>();

            MapTuning mapTuning = Object.FindFirstObjectByType<MapTuning>();
            if (mapTuning == null)
                mapTuning = worldHost.AddComponent<MapTuning>();

            ParentIfRoot("Main Camera", world);
            ParentIfRoot("Directional Light", world);

            hub.AssignRefs(runner, uiMgr, audio, mapTuning);
            runner.AssignPresentation(hud != null ? hud : uiMgr.EnsureHud(), dayNight);
        }

        public static Transform EnsureRoot(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            return go.transform;
        }

        private static GameObject FindOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void ParentIfRoot(string name, Transform world)
        {
            GameObject go = GameObject.Find(name);
            if (go == null || world == null) return;
            if (go.transform.parent == null)
                go.transform.SetParent(world, true);
        }
    }
}
