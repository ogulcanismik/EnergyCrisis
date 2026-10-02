using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using MinistryOfPower.Runtime;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.EditorTools
{
    /// <summary>Play-mode shot of UITK mid-bottom ledger drawer (open).</summary>
    public static class LedgerDrawerCapture
    {
        private const string ArmKey = "MoP.LedgerDrawerCaptureArmed";
        private const string OutPath =
            @"C:\Users\Ogulcan\AppData\Local\Cursor\AgentStores\cursor_agent_stores\bc-05f88032-ac18-495c-a28c-2078a8d4e2b7\files\media\uitk-ledger-drawer.png";

        [MenuItem("Ministry of Power/Capture UITK Ledger Drawer")]
        public static void CaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += CaptureMenu;
                return;
            }

            GameSettings.HelpSeen = true;
            RunSetup.Ensure().ConfigureNewGame("usa_like", DifficultyId.Normal);
            EditorSceneManager.OpenScene("Assets/Scenes/MinistryDesk.unity");
            SessionState.SetBool(ArmKey, true);
            EditorApplication.isPlaying = true;
            Debug.Log("UITK ledger drawer capture armed.");
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlay;
            EditorApplication.playModeStateChanged += OnPlay;
        }

        private static void OnPlay(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ArmKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                var go = new GameObject("LedgerDrawerCaptureHost");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<LedgerDrawerCaptureHost>();
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                SessionState.SetBool(ArmKey, false);
            }
        }

        public sealed class LedgerDrawerCaptureHost : MonoBehaviour
        {
            private IEnumerator Start()
            {
                float t0 = Time.realtimeSinceStartup;
                UIDocument doc = null;
                while (Time.realtimeSinceStartup - t0 < 20f)
                {
                    doc = Object.FindFirstObjectByType<UIDocument>();
                    if (doc != null && doc.rootVisualElement != null
                        && doc.rootVisualElement.Q("LedgerDrawer") != null)
                        break;
                    yield return null;
                }

                if (doc == null || doc.rootVisualElement == null)
                {
                    Debug.LogError("UITK_LEDGER_CAPTURE_FAIL: no UIDocument tree");
                    EditorApplication.isPlaying = false;
                    yield break;
                }

                var root = doc.rootVisualElement;
                var drawer = root.Q<VisualElement>("LedgerDrawer");
                var title = root.Q<Label>("LedgerDrawerTitle");
                var budget = root.Q<Button>("LedgerBudget");
                if (drawer == null)
                {
                    Debug.LogError("UITK_LEDGER_CAPTURE_FAIL: missing LedgerDrawer");
                    EditorApplication.isPlaying = false;
                    yield break;
                }

                // Prefer real binder path (button.clicked) when available.
                if (budget != null)
                {
                    using var pooled = ClickEvent.GetPooled(new Event
                    {
                        type = EventType.MouseUp,
                        button = 0,
                        mousePosition = budget.worldBound.center
                    });
                    pooled.target = budget;
                    budget.SendEvent(pooled);
                }

                if (!drawer.ClassListContains("drawer--open-bottom"))
                {
                    if (title != null) title.text = "Budget";
                    drawer.RemoveFromClassList("drawer--closed-bottom");
                    drawer.AddToClassList("drawer--open-bottom");
                    Debug.Log("UITK_LEDGER_CAPTURE: forced open classes");
                }

                yield return new WaitForSecondsRealtime(0.45f);
                yield return new WaitForEndOfFrame();

                Directory.CreateDirectory(Path.GetDirectoryName(OutPath)!);
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(OutPath, tex.EncodeToPNG());
                Object.Destroy(tex);
                Debug.Log("UITK_LEDGER_CAPTURE_OK " + OutPath
                          + " open=" + drawer.ClassListContains("drawer--open-bottom")
                          + " title=" + (title != null ? title.text : "?"));
                EditorApplication.isPlaying = false;
            }
        }
    }
}
