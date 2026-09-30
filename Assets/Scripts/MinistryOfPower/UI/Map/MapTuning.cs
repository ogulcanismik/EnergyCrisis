using UnityEngine;
using MinistryOfPower.Data;

namespace MinistryOfPower.UI.Map
{
    /// <summary>
    /// Scene-local map art / camera knobs shared with the PNG map agent.
    /// When a <see cref="GameTuning"/> is assigned (or pushed from GameManager), those values win;
    /// otherwise the serialized fields below are used.
    /// </summary>
    public sealed class MapTuning : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private GameTuning gameTuning;

        [Header("PNG art")]
        [SerializeField] private Vector2 pngWorldSize = new Vector2(26.2f, 19.65f);
        [SerializeField] private Vector2 pngWorldOffset = new Vector2(-0.1f, -0.85f);
        [SerializeField] private float artHeight = UsaMapLayout.ArtHeight;

        [Header("Ocean backdrop")]
        [SerializeField] private Vector2 oceanWorldSize = UsaMapLayout.OceanSize;
        [SerializeField] private Vector2 oceanWorldOffset = UsaMapLayout.OceanWorldCenter;
        [SerializeField] private int oceanSortingOrder = UsaMapLayout.OceanSortingOrder;

        [Header("Camera")]
        [SerializeField] private float defaultOrthoSize = UsaMapLayout.DefaultOrthoSize;
        [SerializeField] private float minOrthoSize = UsaMapLayout.MinOrthoSize;
        [SerializeField] private float maxOrthoSize = UsaMapLayout.MaxOrthoSize;
        [SerializeField] private Vector2 panBounds = new Vector2(14f, 10f);

        [Tooltip("1 = pan distance matches pointer travel on screen (ortho-aware).")]
        [SerializeField] [Range(0.25f, 2f)] private float panSensitivity = 1f;

        [Tooltip("Flip vertical grab direction if the map feels upside-down under the cursor.")]
        [SerializeField] private bool invertY;

        [Tooltip("0 = precise stop on release. Higher = short inertia that decays quickly.")]
        [SerializeField] [Range(0f, 40f)] private float panDamping;

        [SerializeField] private float zoomStep = 1.1f;

        public Vector2 PngWorldSize => gameTuning != null ? gameTuning.pngWorldSize : pngWorldSize;
        public Vector2 PngWorldOffset => gameTuning != null ? gameTuning.pngWorldOffset : pngWorldOffset;
        public float ArtHeight => artHeight;
        public Vector2 OceanWorldSize => gameTuning != null ? gameTuning.oceanWorldSize : oceanWorldSize;
        public Vector2 OceanWorldOffset => gameTuning != null ? gameTuning.oceanWorldOffset : oceanWorldOffset;
        public int OceanSortingOrder => gameTuning != null ? gameTuning.oceanSortingOrder : oceanSortingOrder;
        public float DefaultOrthoSize => gameTuning != null ? gameTuning.defaultOrthoSize : defaultOrthoSize;
        public float MinOrthoSize => gameTuning != null ? gameTuning.minOrthoSize : minOrthoSize;
        public float MaxOrthoSize => gameTuning != null ? gameTuning.maxOrthoSize : maxOrthoSize;
        public Vector2 PanBounds => gameTuning != null ? gameTuning.panBounds : panBounds;
        public float PanSensitivity => gameTuning != null ? gameTuning.panSensitivity : panSensitivity;
        public bool InvertY => gameTuning != null ? gameTuning.invertY : invertY;
        public float PanDamping => gameTuning != null ? gameTuning.panDamping : panDamping;
        public float ZoomStep => gameTuning != null ? gameTuning.zoomStep : zoomStep;

        public void BindGameTuning(GameTuning tuning)
        {
            gameTuning = tuning;
            if (tuning == null) return;
            pngWorldSize = tuning.pngWorldSize;
            pngWorldOffset = tuning.pngWorldOffset;
            oceanWorldSize = tuning.oceanWorldSize;
            oceanWorldOffset = tuning.oceanWorldOffset;
            oceanSortingOrder = tuning.oceanSortingOrder;
            defaultOrthoSize = tuning.defaultOrthoSize;
            minOrthoSize = tuning.minOrthoSize;
            maxOrthoSize = tuning.maxOrthoSize;
            panBounds = tuning.panBounds;
            panSensitivity = tuning.panSensitivity;
            invertY = tuning.invertY;
            panDamping = tuning.panDamping;
            zoomStep = tuning.zoomStep;
        }

        /// <summary>Find MapTuning in scene, or null (callers fall back to UsaMapLayout defaults).</summary>
        public static MapTuning FindActive()
        {
            return Object.FindFirstObjectByType<MapTuning>();
        }
    }
}
