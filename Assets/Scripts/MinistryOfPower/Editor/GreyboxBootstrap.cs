using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MinistryOfPower.Data;
using MinistryOfPower.Runtime;
using MinistryOfPower.UI;
using MinistryOfPower.UI.Map;

namespace MinistryOfPower.EditorTools
{
    public static class GreyboxBootstrap
    {
        public const string MenuPath = "Ministry of Power/Bootstrap Greybox Scenes";

        [MenuItem(MenuPath)]
        public static void Bootstrap()
        {
            PrototypeBootstrap.Bootstrap();
            EnsureGameTuningAsset();

            EnsureMainMenuScene();
            EnsureMinistryDeskWired();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/MinistryDesk.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", false)
            };

            AssetDatabase.SaveAssets();
            Debug.Log("Greybox scenes bootstrapped. Boot = MainMenu. Managers / World / UI + GameTuning wired.");
        }

        /// <summary>Idempotent desk ensure used by menu bootstrap and MCP agents.</summary>
        [MenuItem("Ministry of Power/Ensure Desk Managers")]
        public static void GreyboxEnsure()
        {
            EnsureGameTuningAsset();
            EnsureMinistryDeskWired();
            AssetDatabase.SaveAssets();
            Debug.Log("Desk managers ensured (GameManager / UIManager / AudioManager / MapTuning).");
        }

        public static GameTuning EnsureGameTuningAsset()
        {
            const string folder = "Assets/Data/Tuning";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data"))
                    AssetDatabase.CreateFolder("Assets", "Data");
                AssetDatabase.CreateFolder("Assets/Data", "Tuning");
            }

            var existing = AssetDatabase.LoadAssetAtPath<GameTuning>(GameTuning.DefaultAssetPath);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<GameTuning>();
            AssetDatabase.CreateAsset(asset, GameTuning.DefaultAssetPath);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void EnsureMainMenuScene()
        {
            const string path = "Assets/Scenes/MainMenu.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var root = new GameObject("MainMenuRoot");
            root.AddComponent<MainMenuController>();

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.backgroundColor = new Color(0.1f, 0.08f, 0.06f);
                cam.clearFlags = CameraClearFlags.SolidColor;
            }

            EditorSceneManager.SaveScene(scene, path);
        }

        private static void EnsureMinistryDeskWired()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MinistryDesk.unity", OpenSceneMode.Single);
            GameTuning tuning = EnsureGameTuningAsset();

            // Migrate legacy MinistryRoot → GameManager under Managers/
            GameObject legacy = GameObject.Find("MinistryRoot");
            Transform managers = EnsureRoot("Managers");
            Transform world = EnsureRoot("World");
            EnsureRoot("UI");

            GameObject gmGo = GameObject.Find("GameManager");
            if (gmGo == null && legacy != null)
            {
                legacy.name = "GameManager";
                gmGo = legacy;
            }

            if (gmGo == null)
                gmGo = new GameObject("GameManager");

            gmGo.transform.SetParent(managers, true);

            var gameManager = gmGo.GetComponent<GameManager>();
            if (gameManager == null) gameManager = gmGo.AddComponent<GameManager>();

            var runner = gmGo.GetComponent<MinistryGameRunner>();
            if (runner == null) runner = gmGo.AddComponent<MinistryGameRunner>();

            // Strip presentation components from GameManager — they live under UI / World.
            StripComponent<ParadoxChromeHud>(gmGo);
            StripComponent<DayNightWeatherController>(gmGo);
            StripComponent<PoliticalMapPresenter>(gmGo);
            StripComponent<MapCameraController>(gmGo);

            // UIManager
            GameObject uiGo = GameObject.Find("UIManager");
            if (uiGo == null)
            {
                uiGo = new GameObject("UIManager");
                uiGo.transform.SetParent(managers, false);
            }
            else
            {
                uiGo.transform.SetParent(managers, true);
            }

            var uiManager = uiGo.GetComponent<UIManager>();
            if (uiManager == null) uiManager = uiGo.AddComponent<UIManager>();
            if (uiGo.GetComponent<ParadoxChromeHud>() == null)
                uiGo.AddComponent<ParadoxChromeHud>();

            // AudioManager stub
            GameObject audioGo = GameObject.Find("AudioManager");
            if (audioGo == null)
            {
                audioGo = new GameObject("AudioManager");
                audioGo.transform.SetParent(managers, false);
            }
            else
            {
                audioGo.transform.SetParent(managers, true);
            }

            var audioManager = audioGo.GetComponent<AudioManager>();
            if (audioManager == null) audioManager = audioGo.AddComponent<AudioManager>();

            // World map systems
            Transform mapSystemsT = world.Find("MapSystems");
            GameObject mapSystems = mapSystemsT != null ? mapSystemsT.gameObject : new GameObject("MapSystems");
            mapSystems.transform.SetParent(world, false);

            if (mapSystems.GetComponent<DayNightWeatherController>() == null)
                mapSystems.AddComponent<DayNightWeatherController>();
            if (mapSystems.GetComponent<PoliticalMapPresenter>() == null)
                mapSystems.AddComponent<PoliticalMapPresenter>();
            if (mapSystems.GetComponent<MapCameraController>() == null)
                mapSystems.AddComponent<MapCameraController>();
            var mapTuning = mapSystems.GetComponent<MapTuning>();
            if (mapTuning == null) mapTuning = mapSystems.AddComponent<MapTuning>();
            mapTuning.BindGameTuning(tuning);

            ParentIfRoot("Main Camera", world);
            ParentIfRoot("Directional Light", world);

            // Wire serialized refs
            var gmSo = new SerializedObject(gameManager);
            gmSo.FindProperty("tuning").objectReferenceValue = tuning;
            gmSo.FindProperty("runner").objectReferenceValue = runner;
            gmSo.FindProperty("uiManager").objectReferenceValue = uiManager;
            gmSo.FindProperty("audioManager").objectReferenceValue = audioManager;
            gmSo.FindProperty("mapTuning").objectReferenceValue = mapTuning;
            gmSo.ApplyModifiedPropertiesWithoutUndo();

            ScenarioDefinition usa = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>("Assets/Data/Scenarios/Scenario_FederalHighBudget.asset");
            ScenarioDefinition sun = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>("Assets/Data/Scenarios/Scenario_SunRichLowBudget.asset");
            var runnerSo = new SerializedObject(runner);
            runnerSo.FindProperty("startingScenario").objectReferenceValue = usa;
            runnerSo.FindProperty("alternateScenario").objectReferenceValue = sun;
            runnerSo.FindProperty("autoStart").boolValue = true;
            runnerSo.FindProperty("paradoxHud").objectReferenceValue = uiGo.GetComponent<ParadoxChromeHud>();
            runnerSo.FindProperty("dayNight").objectReferenceValue = mapSystems.GetComponent<DayNightWeatherController>();
            runnerSo.ApplyModifiedPropertiesWithoutUndo();

            var uiSo = new SerializedObject(uiManager);
            uiSo.FindProperty("paradoxHud").objectReferenceValue = uiGo.GetComponent<ParadoxChromeHud>();
            uiSo.FindProperty("tuning").objectReferenceValue = tuning;
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            // Paradox chrome is the only desk HUD — strip any leftover diegetic desk.
            foreach (var mb in gmGo.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name == "MinistryDeskView")
                    Object.DestroyImmediate(mb);
            }

            Transform leftoverCanvas = gmGo.transform.Find("MinistryDeskCanvas");
            if (leftoverCanvas != null) Object.DestroyImmediate(leftoverCanvas.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Transform EnsureRoot(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            return go.transform;
        }

        private static void ParentIfRoot(string name, Transform parent)
        {
            GameObject go = GameObject.Find(name);
            if (go == null || parent == null) return;
            if (go.transform.parent == null)
                go.transform.SetParent(parent, true);
        }

        private static void StripComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c != null) Object.DestroyImmediate(c);
        }
    }
}
