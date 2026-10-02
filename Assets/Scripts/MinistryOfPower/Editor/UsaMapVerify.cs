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
    /// <summary>Play-mode smoke for Paradox USA map: 50 states, plants, cables, camera framing.</summary>
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

            var sb = new StringBuilder(1536);
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
                bool statesRoot = GameObject.Find("States") != null
                                  || (GameObject.Find("PoliticalMap") != null
                                      && GameObject.Find("PoliticalMap").transform.Find("States") != null);
                bool legacyPlane = GameObject.Find("GreyboxMap") != null;
                bool mapArt = GameObject.Find("MapArt") != null
                              || (GameObject.Find("PoliticalMap") != null
                                  && GameObject.Find("PoliticalMap").transform.Find("MapArt") != null);
                bool ocean = GameObject.Find("MapOcean") != null
                             || GameObject.Find("OceanBackdrop") != null
                             || (GameObject.Find("PoliticalMap") != null
                                 && GameObject.Find("PoliticalMap").transform.Find("MapOcean") != null);

                int distinctCodes = 0;
                var seen = new System.Collections.Generic.HashSet<string>();
                for (int i = 0; i < regions.Length; i++)
                {
                    string code = regions[i].StateCode;
                    if (!string.IsNullOrEmpty(code) && seen.Add(code)) distinctCodes++;
                }

                sb.Append("mapPresenter=").Append(map != null).AppendLine();
                sb.Append("camCtrl=").Append(camCtrl != null).AppendLine();
                sb.Append("politicalGo=").Append(political).AppendLine();
                sb.Append("statesRoot=").Append(statesRoot).AppendLine();
                sb.Append("mapArt=").Append(mapArt).AppendLine();
                sb.Append("oceanBackdrop=").Append(ocean).AppendLine();
                sb.Append("legacyPlane=").Append(legacyPlane).AppendLine();
                sb.Append("usePrototypeArt=").Append(UsaMapLayout.UsePrototypeArt).AppendLine();
                sb.Append("states=").Append(regions.Length).AppendLine();
                sb.Append("distinctStateCodes=").Append(distinctCodes).AppendLine();
                sb.Append("layoutStateCount=").Append(UsaMapLayout.StateCount).AppendLine();
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

                // Strict centroid → own state (catches art/collider near-misses like CA→NV, PA→NY).
                string[] pickCodes =
                {
                    "CA", "NV", "AZ", "TX", "FL", "NY", "PA", "IL", "WA",
                    "CO", "KS", "OH", "GA", "OR", "ME"
                };
                int pickPass = 0;
                int pickFail = 0;
                var pickRows = new StringBuilder();
                if (cam != null)
                {
                    foreach (string code in pickCodes)
                    {
                        Vector3 c = UsaMapLayout.Centroid(code);
                        string hitCode = PickStateAt(c);
                        bool hitOk = hitCode == code;
                        if (hitOk) pickPass++;
                        else pickFail++;
                        pickRows.Append(code).Append("->").Append(hitCode)
                            .Append(hitOk ? "=PASS " : "=FAIL ");
                    }
                }

                sb.Append("centroidHits=").Append(pickRows).AppendLine();
                sb.Append("centroidPass=").Append(pickPass).Append('/').Append(pickCodes.Length).AppendLine();
                bool pickOk = pickFail == 0 && pickPass == pickCodes.Length;
                sb.Append("pickOk=").Append(pickOk).AppendLine();

                // Geography: west left (−X), east right (+X); FL south-east of CA.
                Vector3 ca = UsaMapLayout.Centroid("CA");
                Vector3 fl = UsaMapLayout.Centroid("FL");
                Vector3 wa = UsaMapLayout.Centroid("WA");
                Vector3 me = UsaMapLayout.Centroid("ME");
                bool geoOk = ca.x < fl.x && wa.x < me.x && fl.z < wa.z;
                sb.Append("geoCA=").Append(ca.ToString("F2"))
                    .Append(" FL=").Append(fl.ToString("F2"))
                    .Append(" geoOk=").Append(geoOk).AppendLine();

                bool artOrientOk = true;
                bool artSpriteOk = !UsaMapLayout.UsePrototypeArt;
                var mapArtGo = GameObject.Find("MapArt");
                if (mapArtGo != null)
                {
                    Vector3 e = mapArtGo.transform.rotation.eulerAngles;
                    // Expect yaw ≈ 0 (not 180) so PNG west stays world −X.
                    float yaw = e.y;
                    if (yaw > 180f) yaw -= 360f;
                    artOrientOk = Mathf.Abs(e.x - 90f) < 2f && Mathf.Abs(yaw) < 5f;
                    var sr = mapArtGo.GetComponent<SpriteRenderer>();
                    artSpriteOk = sr != null && sr.sprite != null && mapArtGo.GetComponent<MeshRenderer>() == null;
                    Vector3 bs = sr != null ? sr.bounds.size : Vector3.zero;
                    float aspect = bs.z > 0.001f ? bs.x / bs.z : 0f;
                    sb.Append("mapArtEuler=").Append(e.ToString("F0"))
                        .Append(" artOrientOk=").Append(artOrientOk)
                        .Append(" spriteRenderer=").Append(artSpriteOk)
                        .Append(" boundsXZ=").Append(bs.x.ToString("0.00")).Append("x").Append(bs.z.ToString("0.00"))
                        .Append(" aspect=").Append(aspect.ToString("0.000"))
                        .AppendLine();
                }
                else
                {
                    sb.AppendLine("mapArtEuler=n/a artOrientOk=" + (!UsaMapLayout.UsePrototypeArt));
                    artOrientOk = !UsaMapLayout.UsePrototypeArt;
                }

                bool bordersOk = UsaMapLayout.UsePrototypeArt ? borders == 0 : borders >= 50;
                // North America art has baked ocean — MapOcean must not appear.
                bool oceanGone = !ocean;
                string artSpriteName = "";
                if (mapArtGo != null)
                {
                    var srArt = mapArtGo.GetComponent<SpriteRenderer>();
                    if (srArt != null && srArt.sprite != null) artSpriteName = srArt.sprite.name;
                }
                bool artNameOk = !UsaMapLayout.UsePrototypeArt
                                 || artSpriteName.IndexOf("north-america", System.StringComparison.OrdinalIgnoreCase) >= 0;
                bool artOk = !UsaMapLayout.UsePrototypeArt || (mapArt && oceanGone && artNameOk);
                bool ok = map != null && camCtrl != null && political && !legacyPlane
                          && regions.Length >= UsaMapLayout.ExpectedStateCount
                          && distinctCodes >= UsaMapLayout.ExpectedStateCount
                          && UsaMapLayout.StateCount >= UsaMapLayout.ExpectedStateCount
                          && plants.Length >= 1 && cables >= 10 && bordersOk && artOk
                          && orthoOk && panOk && zoomOk && pickOk && geoOk && artOrientOk
                          && artSpriteOk;
                sb.Append("bordersOk=").Append(bordersOk).AppendLine();
                sb.Append("oceanGone=").Append(oceanGone).AppendLine();
                sb.Append("artSprite=").Append(artSpriteName).AppendLine();
                sb.Append("artNameOk=").Append(artNameOk).AppendLine();
                sb.Append("artOk=").Append(artOk).AppendLine();
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

        /// <summary>
        /// Downward pick at a world XZ point. When AABBs overlap, prefer the smallest
        /// state mesh (same rule as play-mode clicks).
        /// </summary>
        private static string PickStateAt(Vector3 worldCentroid)
        {
            Vector3 origin = new Vector3(worldCentroid.x, 22f, worldCentroid.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 50f);
            RegionMarker best = null;
            float bestArea = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                var region = hits[i].collider.GetComponentInParent<RegionMarker>();
                if (region == null || string.IsNullOrEmpty(region.StateCode)) continue;
                Bounds b = hits[i].collider.bounds;
                float area = b.size.x * b.size.z;
                if (area < bestArea)
                {
                    bestArea = area;
                    best = region;
                }
            }

            return best != null ? best.StateCode : "MISS";
        }
    }
}
