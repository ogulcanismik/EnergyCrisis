using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MinistryOfPower.Data;
using MinistryOfPower.Runtime;
using MinistryOfPower.UI;

namespace MinistryOfPower.EditorTools
{
    public static class GreyboxBootstrap
    {
        public const string MenuPath = "Ministry of Power/Bootstrap Greybox Scenes";

        [MenuItem(MenuPath)]
        public static void Bootstrap()
        {
            PrototypeBootstrap.Bootstrap();

            EnsureMainMenuScene();
            EnsureMinistryDeskWired();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/MinistryDesk.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", false)
            };

            AssetDatabase.SaveAssets();
            Debug.Log("Greybox scenes bootstrapped. Boot = MainMenu.");
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
            GameObject root = GameObject.Find("MinistryRoot");
            if (root == null)
            {
                root = new GameObject("MinistryRoot");
            }

            var runner = root.GetComponent<MinistryGameRunner>();
            if (runner == null) runner = root.AddComponent<MinistryGameRunner>();

            if (root.GetComponent<ParadoxChromeHud>() == null) root.AddComponent<ParadoxChromeHud>();
            if (root.GetComponent<DayNightWeatherController>() == null) root.AddComponent<DayNightWeatherController>();

            // Paradox chrome is the only desk HUD — strip any leftover diegetic desk.
            foreach (var mb in root.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName == "MinistryDeskView")
                {
                    Object.DestroyImmediate(mb);
                }
            }

            Transform leftoverCanvas = root.transform.Find("MinistryDeskCanvas");
            if (leftoverCanvas != null) Object.DestroyImmediate(leftoverCanvas.gameObject);

            ScenarioDefinition usa = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>("Assets/Data/Scenarios/Scenario_FederalHighBudget.asset");
            ScenarioDefinition sun = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>("Assets/Data/Scenarios/Scenario_SunRichLowBudget.asset");
            var so = new SerializedObject(runner);
            so.FindProperty("startingScenario").objectReferenceValue = usa;
            so.FindProperty("alternateScenario").objectReferenceValue = sun;
            so.FindProperty("autoStart").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
