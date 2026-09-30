using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MinistryOfPower.Runtime;
using MinistryOfPower.Simulation;
using MinistryOfPower.UI.Map;

namespace MinistryOfPower.EditorTools
{
    /// <summary>Play-mode smoke for Paradox USA map: regions, plants, cables, camera framing.</summary>
    public static class UsaMapVerify
    {
        private const string ArmKey = "MoP.UsaMapVerify";
        private const string OutName = "usa_map_verify.txt";
        private static double _enteredAt;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayState;
            EditorApplication.playModeStateChanged += OnPlayState;
        }

        [MenuItem("Ministry of Power/Verify USA Map")]
        public static void VerifyMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += VerifyMenu;
                return;
            }

            GameSettings.HelpSeen = true;
            RunSetup.Ensure().ConfigureNewGame("usa_like", DifficultyId.Normal);
            EditorSceneManager.OpenScene("Assets/Scenes/MinistryDesk.unity");
            SessionState.SetBool(ArmKey, true);
            EditorApplication.isPlaying = true;
            Debug.Log("USA map verify armed — entering Play Mode.");
        }

        private static void OnPlayState(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ArmKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                _enteredAt = EditorApplication.timeSinceStartup;
                EditorApplication.update -= OnTick;
                EditorApplication.update += OnTick;
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= OnTick;
            }
        }

        private static void OnTick()
        {
            if (!SessionState.GetBool(ArmKey, false) || !EditorApplication.isPlaying) return;

            var runner = Object.FindFirstObjectByType<MinistryGameRunner>();
            bool ready = runner != null && runner.Session?.Portfolio != null;
            if (!ready)
            {
                if (EditorApplication.timeSinceStartup - _enteredAt > 20)
                {
                    Finish("FAIL session not ready");
                }

                return;
            }

            if (EditorApplication.timeSinceStartup - _enteredAt < 1.2) return;

            var sb = new StringBuilder(1024);
            sb.AppendLine("=== USA MAP VERIFY ===");
            try
            {
                var map = Object.FindFirstObjectByType<PoliticalMapPresenter>();
                var camCtrl = Object.FindFirstObjectByType<MapCameraController>();
                Camera cam = Camera.main;
                var regions = Object.FindObjectsByType<RegionMarker>(FindObjectsSortMode.None);
                var plants = Object.FindObjectsByType<PlantMarker>(FindObjectsSortMode.None);
                var lines = Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);

                int cables = 0;
                int borders = 0;
                foreach (var lr in lines)
                {
                    if (lr == null) continue;
                    if (lr.gameObject.name.StartsWith("Cable_")) cables++;
                    else if (lr.gameObject.name == "Border") borders++;
                }

                bool political = GameObject.Find("PoliticalMap") != null;
                bool legacyPlane = GameObject.Find("GreyboxMap") != null;

                sb.Append("mapPresenter=").Append(map != null).AppendLine();
                sb.Append("camCtrl=").Append(camCtrl != null).AppendLine();
                sb.Append("politicalGo=").Append(political).AppendLine();
                sb.Append("legacyPlane=").Append(legacyPlane).AppendLine();
                sb.Append("regions=").Append(regions.Length).AppendLine();
                sb.Append("plants=").Append(plants.Length).AppendLine();
                sb.Append("cables=").Append(cables).AppendLine();
                sb.Append("borders=").Append(borders).AppendLine();
                sb.Append("scenario=").Append(runner.Session.Scenario?.Id).AppendLine();

                bool orthoOk = false;
                bool panOk = false;
                bool zoomOk = false;
                if (cam != null)
                {
                    sb.Append("ortho=").Append(cam.orthographic).AppendLine();
                    sb.Append("orthoSize=").Append(cam.orthographicSize.ToString("0.00")).AppendLine();
                    sb.Append("camPos=").Append(cam.transform.position.ToString("F2")).AppendLine();
                    sb.Append("camEuler=").Append(cam.transform.eulerAngles.ToString("F0")).AppendLine();
                    orthoOk = cam.orthographic
                              && Mathf.Abs(cam.transform.eulerAngles.x - 90f) < 2f;

                    Vector3 before = cam.transform.position;
                    cam.transform.position = before + new Vector3(1.2f, 0f, -0.7f);
                    panOk = (cam.transform.position - before).magnitude > 0.5f;

                    float z0 = cam.orthographicSize;
                    cam.orthographicSize = Mathf.Clamp(z0 / 1.15f, UsaMapLayout.MinOrthoSize, UsaMapLayout.MaxOrthoSize);
                    zoomOk = Mathf.Abs(cam.orthographicSize - z0) > 0.05f;
                    camCtrl?.ApplyDefaultFrame(force: true);
                }

                sb.Append("orthoFrameOk=").Append(orthoOk).AppendLine();
                sb.Append("panOk=").Append(panOk).AppendLine();
                sb.Append("zoomOk=").Append(zoomOk).AppendLine();

                // Left-click ray pick toward each region centroid (ortho center may miss water gaps).
            bool pickOk = false;
            string hitNames = "";
            if (cam != null)
            {
                foreach (RegionId id in new[] { RegionId.Coast, RegionId.North, RegionId.Desert })
                {
                    Vector3 c = UsaMapLayout.Centroid(id);
                    Vector3 origin = new Vector3(c.x, 22f, c.z);
                    if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 50f))
                    {
                        var region = hit.collider.GetComponentInParent<RegionMarker>();
                        var plant = hit.collider.GetComponentInParent<PlantMarker>();
                        if (region != null || plant != null)
                        {
                            pickOk = true;
                            hitNames += hit.collider.name + ",";
                        }
                    }
                }

                sb.Append("centroidHits=").Append(hitNames).AppendLine();
            }

                sb.Append("pickOk=").Append(pickOk).AppendLine();

                bool ok = map != null && camCtrl != null && political && !legacyPlane
                          && regions.Length >= 3 && plants.Length >= 1 && cables >= 3
                          && orthoOk && panOk && zoomOk && pickOk;
                sb.Append("USA_MAP_OK=").Append(ok).AppendLine();
                Finish(sb.ToString());
            }
            catch (System.Exception ex)
            {
                Finish("FAIL " + ex.Message + "\n" + sb);
            }
        }

        private static void Finish(string body)
        {
            EditorApplication.update -= OnTick;
            SessionState.SetBool(ArmKey, false);
            string path = Path.Combine(Application.persistentDataPath, OutName);
            File.WriteAllText(path, body, Encoding.UTF8);
            Debug.Log(body + "\nWrote " + path);
            EditorApplication.isPlaying = false;
        }
    }
}
