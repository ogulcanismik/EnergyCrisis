using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MinistryOfPower.UI.Map
{
    /// <summary>
    /// Paradox-style map camera: right-mouse pan, scroll zoom.
    /// Left-click selection stays on DayNightWeatherController / markers.
    /// Reads limits from <see cref="MapTuning"/> when present.
    /// </summary>
    public sealed class MapCameraController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;

        [Tooltip("1 = pan distance matches pointer travel on screen (ortho-aware).")]
        [SerializeField] [Range(0.25f, 2f)] private float panSensitivity = 1f;

        [Tooltip("Flip vertical grab direction if the map feels upside-down under the cursor.")]
        [SerializeField] private bool invertY;

        [Tooltip("0 = precise stop on release. Higher = short inertia that decays quickly.")]
        [SerializeField] [Range(0f, 40f)] private float panDamping;

        [SerializeField] private float zoomStep = 1.1f;
        [SerializeField] private float minOrtho = UsaMapLayout.MinOrthoSize;
        [SerializeField] private float maxOrtho = UsaMapLayout.MaxOrthoSize;
        [SerializeField] private Vector2 panBounds = new Vector2(14f, 10f);
        [SerializeField] private MapTuning mapTuning;

        private bool _panning;
        private Vector2 _lastPointer;
        private Vector3 _velocity;
        private bool _framed;

        public Camera TargetCamera => targetCamera;

        public void Bind(Camera cam)
        {
            targetCamera = cam;
            ApplyTuningFromScene();
            ApplyDefaultFrame(force: !_framed);
        }

        public void ApplyTuningFromScene()
        {
            if (mapTuning == null) mapTuning = MapTuning.FindActive();
            if (mapTuning == null) return;
            minOrtho = mapTuning.MinOrthoSize;
            maxOrtho = mapTuning.MaxOrthoSize;
            panBounds = mapTuning.PanBounds;
            panSensitivity = mapTuning.PanSensitivity;
            invertY = mapTuning.InvertY;
            panDamping = mapTuning.PanDamping;
            zoomStep = mapTuning.ZoomStep;
        }

        public void ApplyDefaultFrame(bool force = false)
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;
            if (_framed && !force) return;

            if (mapTuning == null) mapTuning = MapTuning.FindActive();
            float ortho = mapTuning != null ? mapTuning.DefaultOrthoSize : UsaMapLayout.DefaultOrthoSize;

            targetCamera.orthographic = true;
            targetCamera.orthographicSize = ortho;
            targetCamera.transform.position = UsaMapLayout.DefaultCameraPosition;
            // Near-top-down so Shell B chrome frames a flat political map.
            targetCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _framed = true;
            _velocity = Vector3.zero;
        }

        private void Update()
        {
            if (targetCamera == null) return;

            HandleZoom();
            HandlePan();
        }

        private void HandleZoom()
        {
            float scroll = ReadScrollY();
            if (Mathf.Abs(scroll) < 0.01f) return;

            float size = targetCamera.orthographicSize;
            if (scroll > 0f) size /= zoomStep;
            else size *= zoomStep;
            targetCamera.orthographicSize = Mathf.Clamp(size, minOrtho, maxOrtho);
        }

        private void HandlePan()
        {
            bool held = IsRightHeld();
            Vector2 pointer = ReadPointer();

            if (held && !_panning)
            {
                _panning = true;
                _lastPointer = pointer;
                _velocity = Vector3.zero;
            }
            else if (!held && _panning)
            {
                _panning = false;
            }

            if (_panning)
            {
                Vector2 delta = pointer - _lastPointer;
                _lastPointer = pointer;
                if (delta.sqrMagnitude >= 0.01f)
                {
                    Vector3 step = ScreenDeltaToWorld(delta);
                    ApplyPanDelta(step);
                    // Capture instantaneous drag velocity for optional release inertia.
                    float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
                    _velocity = step / dt;
                }
                else
                {
                    _velocity = Vector3.zero;
                }

                return;
            }

            if (panDamping <= 0.01f || _velocity.sqrMagnitude < 0.0001f)
            {
                _velocity = Vector3.zero;
                return;
            }

            // Light coast that stops quickly — prefer precise drag over slippery feel.
            ApplyPanDelta(_velocity * Time.unscaledDeltaTime);
            _velocity = Vector3.Lerp(_velocity, Vector3.zero, 1f - Mathf.Exp(-panDamping * Time.unscaledDeltaTime));
            if (_velocity.sqrMagnitude < 0.0001f) _velocity = Vector3.zero;
        }

        private Vector3 ScreenDeltaToWorld(Vector2 screenDelta)
        {
            // Ortho height in world units is 2 * orthographicSize across Screen.height pixels.
            float unitsPerPixel = (2f * targetCamera.orthographicSize) /
                                  Mathf.Max(1f, Screen.height);
            float scale = unitsPerPixel * panSensitivity;
            float ySign = invertY ? 1f : -1f;

            // Grab the map: pointer right → content follows right → camera moves left.
            Vector3 right = targetCamera.transform.right;
            Vector3 up = targetCamera.transform.up;
            return (right * (-screenDelta.x) + up * (ySign * screenDelta.y)) * scale;
        }

        private void ApplyPanDelta(Vector3 worldDelta)
        {
            Vector3 pos = targetCamera.transform.position + worldDelta;
            pos.x = Mathf.Clamp(pos.x, -panBounds.x, panBounds.x);
            pos.z = Mathf.Clamp(pos.z, -panBounds.y, panBounds.y);
            targetCamera.transform.position = pos;
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

        private static Vector2 ReadPointer()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        private static float ReadScrollY()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse != null ? mouse.scroll.ReadValue().y : 0f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }
    }
}
