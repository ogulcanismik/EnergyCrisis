using UnityEngine;

namespace MinistryOfPower.Data
{
    /// <summary>
    /// Designer-facing pace / UI / map knobs. Assign on <see cref="Runtime.GameManager"/>.
    /// Defaults match shipping grey-box (168s/day at 1×, existing chrome layout).
    /// </summary>
    [CreateAssetMenu(menuName = "Ministry of Power/Game Tuning", fileName = "GameTuning_Default")]
    public sealed class GameTuning : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Data/Tuning/GameTuning_Default.asset";

        [Header("Pace")]
        [Tooltip("Real seconds for one in-game day at 1×. Default 168 (~2.8 min; 7s per hour).")]
        [Min(1f)]
        public float secondsPerDay1x = 168f;

        [Tooltip("Day length divisor at the Fast (2×) HUD button.")]
        [Min(1f)]
        public float speedMultiplierFast = 2f;

        [Tooltip("Day length divisor at the Very Fast (5×) HUD button.")]
        [Min(1f)]
        public float speedMultiplierVeryFast = 5f;

        [Header("UI — Time controls")]
        [Tooltip("Scales the right-side time cluster width (1 = shipping layout).")]
        [Range(0.6f, 1.4f)]
        public float timeControlScale = 1f;

        [Range(0.04f, 0.10f)]
        public float timeSpeedSlotWidth = 0.056f;

        [Min(8)]
        public int timeControlFontSize = 10;

        [Header("UI — Drawers")]
        [Range(0.08f, 0.16f)]
        public float leftRailWidth = 0.11f;

        [Tooltip("Right edge of the left content drawer (normalized screen X).")]
        [Range(0.28f, 0.55f)]
        public float leftDrawerMaxX = 0.40f;

        [Tooltip("Left edge of the right Orders drawer.")]
        [Range(0.70f, 0.88f)]
        public float rightDrawerMinX = 0.78f;

        [Tooltip("Normalized height of the thin bottom chart-chip strip.")]
        [Range(0.045f, 0.08f)]
        public float chromeBottomHeight = 0.052f;

        [Header("UI — Charts & type")]
        [Tooltip("Normalized height band for report chart slots inside the left panel.")]
        [Range(0.25f, 0.55f)]
        public float reportChartHeight = 0.40f;

        [Min(9)]
        public int hudBodyFontSize = 11;

        [Min(12)]
        public int hudTitleFontSize = 18;

        [Header("Map — Camera")]
        [Min(1f)]
        public float defaultOrthoSize = 12.5f;

        [Min(1f)]
        public float minOrthoSize = 4.5f;

        [Min(2f)]
        public float maxOrthoSize = 18f;

        public Vector2 panBounds = new Vector2(22f, 16f);

        [Tooltip("1 = pan distance matches pointer travel on screen (orthoSize / screen height).")]
        [Range(0.25f, 2f)]
        public float panSensitivity = 1f;

        [Tooltip("Flip vertical grab direction if RMB drag feels backwards.")]
        public bool invertY;

        [Tooltip("0 = precise stop on release. Higher = short inertia that decays quickly.")]
        [Range(0f, 40f)]
        public float panDamping;

        [Min(1.01f)]
        public float zoomStep = 1.1f;

        [Header("Map — PNG art")]
        [Tooltip("World XZ size of the North America map sprite. Shared with MapTuning.")]
        public Vector2 pngWorldSize = new Vector2(71.52f, 43.57f);

        [Tooltip("World XZ center offset of the map sprite (CONUS left-biased in the PNG).")]
        public Vector2 pngWorldOffset = new Vector2(11.29f, 2.06f);

        [Header("Map — Day/night terminator")]
        [Tooltip("Max opacity of the night side overlay (day side stays clear).")]
        [Range(0.2f, 0.95f)]
        public float terminatorNightAlpha = 0.72f;

        [Tooltip("Softness of the terminator band in illumination space.")]
        [Range(0.04f, 0.4f)]
        public float terminatorSoftness = 0.14f;

        [Tooltip("How much CONUS longitude the overlay treats as lit span (higher = sharper day/night contrast).")]
        [Range(0.4f, 2f)]
        public float terminatorLonSpan = 1.05f;

        [Tooltip("Max seasonal N–S tilt of the terminator in degrees.")]
        [Range(0f, 25f)]
        public float terminatorSeasonTiltDegrees = 12f;

        public Color terminatorNightColor = new Color(0.02f, 0.04f, 0.10f, 1f);

        [Header("Economy display")]
        [Tooltip("Informational only — DisplayUnits formatting stays fixed. Starting treasury is difficulty/scenario.")]
        public bool displayTreasuryAsBillions = true;

        /// <summary>Runtime-only fallback when no asset is assigned (Play from empty scene).</summary>
        public static GameTuning CreateRuntimeDefaults()
        {
            var t = CreateInstance<GameTuning>();
            t.name = "GameTuning_RuntimeDefaults";
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
    }
}
