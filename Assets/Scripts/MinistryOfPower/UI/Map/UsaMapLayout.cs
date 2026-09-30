using UnityEngine;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.UI.Map
{
    /// <summary>
    /// Paradox-style continental USA political map: 50 states as selectable polygons.
    /// Simulation still uses RegionId (North/Coast/Desert); each state maps into one group.
    /// Coordinates are XZ map space (Y up). AK/HI are inset boxes.
    /// </summary>
    public static class UsaMapLayout
    {
        public const float RegionHeight = 0.05f;
        public const float OceanHeight = -0.02f;
        public const int ExpectedStateCount = 50;

        /// <summary>Default orthographic framing for Shell B center canvas.</summary>
        public static readonly Vector3 DefaultCameraPosition = new Vector3(0f, 22f, -0.5f);
        public const float DefaultOrthoSize = 12.5f;
        public const float MinOrthoSize = 4.5f;
        public const float MaxOrthoSize = 18f;

        /// <summary>Below this ortho size (or on select), labels show full state names.</summary>
        public const float FullNameOrthoThreshold = 8.5f;

        public static readonly Vector2 OceanSize = new Vector2(40f, 28f);

        public struct StatePoly
        {
            public string Code;
            public string FullName;
            public RegionId Region;
            public Color Fill;
            public Vector2[] Ring;
        }

        public struct Corridor
        {
            public string From;
            public string To;
            public Color Color;
        }

        /// <summary>Default plant-siting state for each legacy sim region.</summary>
        public static string HomeState(RegionId id)
        {
            switch (id)
            {
                case RegionId.North: return "IL";
                case RegionId.Coast: return "PA";
                case RegionId.Desert: return "AZ";
                default: return "PA";
            }
        }

        public static StatePoly[] States { get; } =
        {
            new StatePoly
            {
                Code = "AL",
                FullName = "Alabama",
                Region = RegionId.Coast,
                Fill = new Color(0.344f, 0.428f, 0.540f, 1f),
                Ring = new[] { new Vector2(2.92f, -1.59f), new Vector2(4.24f, -1.59f), new Vector2(4.24f, -4.16f), new Vector2(2.92f, -4.16f) }
            new StatePoly
            {
                Code = "AK",
                FullName = "Alaska",
                Region = RegionId.North,
                Fill = new Color(0.396f, 0.462f, 0.505f, 1f),
                Ring = new[] { new Vector2(-11.50f, -5.20f), new Vector2(-8.30f, -5.20f), new Vector2(-8.30f, -7.20f), new Vector2(-11.50f, -7.20f) }
            new StatePoly
            {
                Code = "AZ",
                FullName = "Arizona",
                Region = RegionId.Desert,
                Fill = new Color(0.504f, 0.428f, 0.330f, 1f),
                Ring = new[] { new Vector2(-7.73f, -0.42f), new Vector2(-5.61f, -0.42f), new Vector2(-5.61f, -3.48f), new Vector2(-7.73f, -3.48f) }
            new StatePoly
            {
                Code = "AR",
                FullName = "Arkansas",
                Region = RegionId.Coast,
                Fill = new Color(0.336f, 0.422f, 0.535f, 1f),
                Ring = new[] { new Vector2(0.47f, -0.65f), new Vector2(2.30f, -0.65f), new Vector2(2.30f, -2.53f), new Vector2(0.47f, -2.53f) }
            new StatePoly
            {
                Code = "CA",
                FullName = "California",
                Region = RegionId.Desert,
                Fill = new Color(0.544f, 0.458f, 0.355f, 1f),
                Ring = new[] { new Vector2(-11.55f, 2.45f), new Vector2(-7.78f, 2.45f), new Vector2(-7.78f, -2.65f), new Vector2(-11.55f, -2.65f) }
            new StatePoly
            {
                Code = "CO",
                FullName = "Colorado",
                Region = RegionId.Desert,
                Fill = new Color(0.544f, 0.458f, 0.355f, 1f),
                Ring = new[] { new Vector2(-5.39f, 2.02f), new Vector2(-2.79f, 2.02f), new Vector2(-2.79f, -0.13f), new Vector2(-5.39f, -0.13f) }
            new StatePoly
            {
                Code = "CT",
                FullName = "Connecticut",
                Region = RegionId.Coast,
                Fill = new Color(0.368f, 0.446f, 0.555f, 1f),
                Ring = new[] { new Vector2(8.91f, 2.76f), new Vector2(9.60f, 2.76f), new Vector2(9.60f, 2.17f), new Vector2(8.91f, 2.17f) }
            new StatePoly
            {
                Code = "DE",
                FullName = "Delaware",
                Region = RegionId.Coast,
                Fill = new Color(0.368f, 0.446f, 0.555f, 1f),
                Ring = new[] { new Vector2(8.00f, 1.38f), new Vector2(8.36f, 1.38f), new Vector2(8.36f, 0.63f), new Vector2(8.00f, 0.63f) }
            new StatePoly
            {
                Code = "FL",
                FullName = "Florida",
                Region = RegionId.Coast,
                Fill = new Color(0.384f, 0.458f, 0.565f, 1f),
                Ring = new[] { new Vector2(3.37f, -4.02f), new Vector2(6.15f, -4.02f), new Vector2(6.15f, -7.51f), new Vector2(3.37f, -7.51f) }
            new StatePoly
            {
                Code = "GA",
                FullName = "Georgia",
                Region = RegionId.Coast,
                Fill = new Color(0.360f, 0.440f, 0.550f, 1f),
                Ring = new[] { new Vector2(4.12f, -1.58f), new Vector2(5.88f, -1.58f), new Vector2(5.88f, -4.05f), new Vector2(4.12f, -4.05f) }
            new StatePoly
            {
                Code = "HI",
                FullName = "Hawaii",
                Region = RegionId.Desert,
                Fill = new Color(0.536f, 0.452f, 0.350f, 1f),
                Ring = new[] { new Vector2(-7.50f, -6.00f), new Vector2(-5.50f, -6.00f), new Vector2(-5.50f, -7.00f), new Vector2(-7.50f, -7.00f) }
            new StatePoly
            {
                Code = "ID",
                FullName = "Idaho",
                Region = RegionId.North,
                Fill = new Color(0.404f, 0.468f, 0.510f, 1f),
                Ring = new[] { new Vector2(-8.70f, 6.70f), new Vector2(-6.43f, 6.70f), new Vector2(-6.43f, 2.94f), new Vector2(-8.70f, 2.94f) }
            new StatePoly
            {
                Code = "IL",
                FullName = "Illinois",
                Region = RegionId.North,
                Fill = new Color(0.412f, 0.474f, 0.515f, 1f),
                Ring = new[] { new Vector2(1.72f, 2.86f), new Vector2(3.37f, 2.86f), new Vector2(3.37f, -0.09f), new Vector2(1.72f, -0.09f) }
            new StatePoly
            {
                Code = "IN",
                FullName = "Indiana",
                Region = RegionId.North,
                Fill = new Color(0.428f, 0.486f, 0.525f, 1f),
                Ring = new[] { new Vector2(3.08f, 2.49f), new Vector2(4.29f, 2.49f), new Vector2(4.29f, 0.35f), new Vector2(3.08f, 0.35f) }
            new StatePoly
            {
                Code = "IA",
                FullName = "Iowa",
                Region = RegionId.North,
                Fill = new Color(0.436f, 0.492f, 0.530f, 1f),
                Ring = new[] { new Vector2(-0.32f, 3.53f), new Vector2(2.06f, 3.53f), new Vector2(2.06f, 1.87f), new Vector2(-0.32f, 1.87f) }
            new StatePoly
            {
                Code = "KS",
                FullName = "Kansas",
                Region = RegionId.North,
                Fill = new Color(0.428f, 0.486f, 0.525f, 1f),
                Ring = new[] { new Vector2(-2.53f, 1.45f), new Vector2(0.21f, 1.45f), new Vector2(0.21f, -0.16f), new Vector2(-2.53f, -0.16f) }
            new StatePoly
            {
                Code = "KY",
                FullName = "Kentucky",
                Region = RegionId.Coast,
                Fill = new Color(0.360f, 0.440f, 0.550f, 1f),
                Ring = new[] { new Vector2(2.56f, 0.92f), new Vector2(5.38f, 0.92f), new Vector2(5.38f, -0.47f), new Vector2(2.56f, -0.47f) }
            new StatePoly
            {
                Code = "LA",
                FullName = "Louisiana",
                Region = RegionId.Coast,
                Fill = new Color(0.344f, 0.428f, 0.540f, 1f),
                Ring = new[] { new Vector2(0.72f, -2.76f), new Vector2(2.62f, -2.76f), new Vector2(2.62f, -4.96f), new Vector2(0.72f, -4.96f) }
            new StatePoly
            {
                Code = "ME",
                FullName = "Maine",
                Region = RegionId.Coast,
                Fill = new Color(0.384f, 0.458f, 0.565f, 1f),
                Ring = new[] { new Vector2(10.01f, 5.88f), new Vector2(11.55f, 5.88f), new Vector2(11.55f, 3.52f), new Vector2(10.01f, 3.52f) }
            new StatePoly
            {
                Code = "MD",
                FullName = "Maryland",
                Region = RegionId.Coast,
                Fill = new Color(0.376f, 0.452f, 0.560f, 1f),
                Ring = new[] { new Vector2(6.60f, 1.31f), new Vector2(8.25f, 1.31f), new Vector2(8.25f, 0.34f), new Vector2(6.60f, 0.34f) }
            new StatePoly
            {
                Code = "MA",
                FullName = "Massachusetts",
                Region = RegionId.Coast,
                Fill = new Color(0.352f, 0.434f, 0.545f, 1f),
                Ring = new[] { new Vector2(9.02f, 3.22f), new Vector2(10.34f, 3.22f), new Vector2(10.34f, 2.30f), new Vector2(9.02f, 2.30f) }
            new StatePoly
            {
                Code = "MI",
                FullName = "Michigan",
                Region = RegionId.North,
                Fill = new Color(0.420f, 0.480f, 0.520f, 1f),
                Ring = new[] { new Vector2(2.24f, 6.29f), new Vector2(5.17f, 6.29f), new Vector2(5.17f, 2.75f), new Vector2(2.24f, 2.75f) }
            new StatePoly
            {
                Code = "MN",
                FullName = "Minnesota",
                Region = RegionId.North,
                Fill = new Color(0.404f, 0.468f, 0.510f, 1f),
                Ring = new[] { new Vector2(-0.53f, 6.97f), new Vector2(2.28f, 6.97f), new Vector2(2.28f, 3.80f), new Vector2(-0.53f, 3.80f) }
            new StatePoly
            {
                Code = "MS",
                FullName = "Mississippi",
                Region = RegionId.Coast,
                Fill = new Color(0.384f, 0.458f, 0.565f, 1f),
                Ring = new[] { new Vector2(1.62f, -1.59f), new Vector2(2.94f, -1.59f), new Vector2(2.94f, -4.16f), new Vector2(1.62f, -4.16f) }
            new StatePoly
            {
                Code = "MO",
                FullName = "Missouri",
                Region = RegionId.North,
                Fill = new Color(0.412f, 0.474f, 0.515f, 1f),
                Ring = new[] { new Vector2(0.01f, 1.76f), new Vector2(2.47f, 1.76f), new Vector2(2.47f, -0.71f), new Vector2(0.01f, -0.71f) }
            new StatePoly
            {
                Code = "MT",
                FullName = "Montana",
                Region = RegionId.North,
                Fill = new Color(0.396f, 0.462f, 0.505f, 1f),
                Ring = new[] { new Vector2(-8.13f, 6.77f), new Vector2(-3.70f, 6.77f), new Vector2(-3.70f, 4.30f), new Vector2(-8.13f, 4.30f) }
            new StatePoly
            {
                Code = "NE",
                FullName = "Nebraska",
                Region = RegionId.North,
                Fill = new Color(0.396f, 0.462f, 0.505f, 1f),
                Ring = new[] { new Vector2(-3.32f, 3.24f), new Vector2(-0.10f, 3.24f), new Vector2(-0.10f, 1.63f), new Vector2(-3.32f, 1.63f) }
            new StatePoly
            {
                Code = "NV",
                FullName = "Nevada",
                Region = RegionId.Desert,
                Fill = new Color(0.520f, 0.440f, 0.340f, 1f),
                Ring = new[] { new Vector2(-9.84f, 2.52f), new Vector2(-7.65f, 2.52f), new Vector2(-7.65f, -1.23f), new Vector2(-9.84f, -1.23f) }
            new StatePoly
            {
                Code = "NH",
                FullName = "New Hampshire",
                Region = RegionId.Coast,
                Fill = new Color(0.360f, 0.440f, 0.550f, 1f),
                Ring = new[] { new Vector2(9.35f, 4.62f), new Vector2(10.05f, 4.62f), new Vector2(10.05f, 3.23f), new Vector2(9.35f, 3.23f) }
            new StatePoly
            {
                Code = "NJ",
                FullName = "New Jersey",
                Region = RegionId.Coast,
                Fill = new Color(0.376f, 0.452f, 0.560f, 1f),
                Ring = new[] { new Vector2(8.13f, 2.30f), new Vector2(8.75f, 2.30f), new Vector2(8.75f, 0.96f), new Vector2(8.13f, 0.96f) }
            new StatePoly
            {
                Code = "NM",
                FullName = "New Mexico",
                Region = RegionId.Desert,
                Fill = new Color(0.504f, 0.428f, 0.330f, 1f),
                Ring = new[] { new Vector2(-5.41f, -0.42f), new Vector2(-3.17f, -0.42f), new Vector2(-3.17f, -3.48f), new Vector2(-5.41f, -3.48f) }
            new StatePoly
            {
                Code = "NY",
                FullName = "New York",
                Region = RegionId.Coast,
                Fill = new Color(0.384f, 0.458f, 0.565f, 1f),
                Ring = new[] { new Vector2(6.55f, 4.39f), new Vector2(9.44f, 4.39f), new Vector2(9.44f, 1.97f), new Vector2(6.55f, 1.97f) }
            new StatePoly
            {
                Code = "NC",
                FullName = "North Carolina",
                Region = RegionId.Coast,
                Fill = new Color(0.376f, 0.452f, 0.560f, 1f),
                Ring = new[] { new Vector2(4.73f, -0.57f), new Vector2(7.96f, -0.57f), new Vector2(7.96f, -2.07f), new Vector2(4.73f, -2.07f) }
            new StatePoly
            {
                Code = "ND",
                FullName = "North Dakota",
                Region = RegionId.North,
                Fill = new Color(0.444f, 0.498f, 0.535f, 1f),
                Ring = new[] { new Vector2(-3.35f, 6.81f), new Vector2(-0.60f, 6.81f), new Vector2(-0.60f, 5.15f), new Vector2(-3.35f, 5.15f) }
            new StatePoly
            {
                Code = "OH",
                FullName = "Ohio",
                Region = RegionId.North,
                Fill = new Color(0.428f, 0.486f, 0.525f, 1f),
                Ring = new[] { new Vector2(4.44f, 2.62f), new Vector2(6.01f, 2.62f), new Vector2(6.01f, 0.69f), new Vector2(4.44f, 0.69f) }
            new StatePoly
            {
                Code = "OK",
                FullName = "Oklahoma",
                Region = RegionId.Desert,
                Fill = new Color(0.496f, 0.422f, 0.325f, 1f),
                Ring = new[] { new Vector2(-2.88f, -0.35f), new Vector2(0.27f, -0.35f), new Vector2(0.27f, -2.18f), new Vector2(-2.88f, -2.18f) }
            new StatePoly
            {
                Code = "OR",
                FullName = "Oregon",
                Region = RegionId.Desert,
                Fill = new Color(0.496f, 0.422f, 0.325f, 1f),
                Ring = new[] { new Vector2(-11.67f, 5.17f), new Vector2(-8.71f, 5.17f), new Vector2(-8.71f, 2.86f), new Vector2(-11.67f, 2.86f) }
            new StatePoly
            {
                Code = "PA",
                FullName = "Pennsylvania",
                Region = RegionId.Coast,
                Fill = new Color(0.376f, 0.452f, 0.560f, 1f),
                Ring = new[] { new Vector2(6.22f, 2.83f), new Vector2(8.34f, 2.83f), new Vector2(8.34f, 1.44f), new Vector2(6.22f, 1.44f) }
            new StatePoly
            {
                Code = "RI",
                FullName = "Rhode Island",
                Region = RegionId.Coast,
                Fill = new Color(0.344f, 0.428f, 0.540f, 1f),
                Ring = new[] { new Vector2(9.58f, 2.70f), new Vector2(9.94f, 2.70f), new Vector2(9.94f, 2.22f), new Vector2(9.58f, 2.22f) }
            new StatePoly
            {
                Code = "SC",
                FullName = "South Carolina",
                Region = RegionId.Coast,
                Fill = new Color(0.360f, 0.440f, 0.550f, 1f),
                Ring = new[] { new Vector2(5.02f, -1.42f), new Vector2(6.82f, -1.42f), new Vector2(6.82f, -3.14f), new Vector2(5.02f, -3.14f) }
            new StatePoly
            {
                Code = "SD",
                FullName = "South Dakota",
                Region = RegionId.North,
                Fill = new Color(0.428f, 0.486f, 0.525f, 1f),
                Ring = new[] { new Vector2(-3.34f, 4.95f), new Vector2(-0.52f, 4.95f), new Vector2(-0.52f, 3.13f), new Vector2(-3.34f, 3.13f) }
            new StatePoly
            {
                Code = "TN",
                FullName = "Tennessee",
                Region = RegionId.Coast,
                Fill = new Color(0.344f, 0.428f, 0.540f, 1f),
                Ring = new[] { new Vector2(2.29f, -0.48f), new Vector2(5.48f, -0.48f), new Vector2(5.48f, -1.39f), new Vector2(2.29f, -1.39f) }
            new StatePoly
            {
                Code = "TX",
                FullName = "Texas",
                Region = RegionId.Desert,
                Fill = new Color(0.528f, 0.446f, 0.345f, 1f),
                Ring = new[] { new Vector2(-4.25f, -0.87f), new Vector2(0.55f, -0.87f), new Vector2(0.55f, -6.61f), new Vector2(-4.25f, -6.61f) }
            new StatePoly
            {
                Code = "UT",
                FullName = "Utah",
                Region = RegionId.Desert,
                Fill = new Color(0.504f, 0.428f, 0.330f, 1f),
                Ring = new[] { new Vector2(-7.46f, 2.58f), new Vector2(-5.60f, 2.58f), new Vector2(-5.60f, -0.10f), new Vector2(-7.46f, -0.10f) }
            new StatePoly
            {
                Code = "VT",
                FullName = "Vermont",
                Region = RegionId.Coast,
                Fill = new Color(0.352f, 0.434f, 0.545f, 1f),
                Ring = new[] { new Vector2(9.03f, 4.45f), new Vector2(9.72f, 4.45f), new Vector2(9.72f, 3.22f), new Vector2(9.03f, 3.22f) }
            new StatePoly
            {
                Code = "VA",
                FullName = "Virginia",
                Region = RegionId.Coast,
                Fill = new Color(0.368f, 0.446f, 0.555f, 1f),
                Ring = new[] { new Vector2(4.97f, 1.15f), new Vector2(8.08f, 1.15f), new Vector2(8.08f, -0.46f), new Vector2(4.97f, -0.46f) }
            new StatePoly
            {
                Code = "WA",
                FullName = "Washington",
                Region = RegionId.North,
                Fill = new Color(0.436f, 0.492f, 0.530f, 1f),
                Ring = new[] { new Vector2(-11.76f, 6.80f), new Vector2(-8.87f, 6.80f), new Vector2(-8.87f, 4.92f), new Vector2(-11.76f, 4.92f) }
            new StatePoly
            {
                Code = "WV",
                FullName = "West Virginia",
                Region = RegionId.Coast,
                Fill = new Color(0.376f, 0.452f, 0.560f, 1f),
                Ring = new[] { new Vector2(5.35f, 1.79f), new Vector2(7.14f, 1.79f), new Vector2(7.14f, -0.03f), new Vector2(5.35f, -0.03f) }
            new StatePoly
            {
                Code = "WI",
                FullName = "Wisconsin",
                Region = RegionId.North,
                Fill = new Color(0.444f, 0.498f, 0.535f, 1f),
                Ring = new[] { new Vector2(1.18f, 5.63f), new Vector2(3.41f, 5.63f), new Vector2(3.41f, 3.17f), new Vector2(1.18f, 3.17f) }
            new StatePoly
            {
                Code = "WY",
                FullName = "Wyoming",
                Region = RegionId.North,
                Fill = new Color(0.404f, 0.468f, 0.510f, 1f),
                Ring = new[] { new Vector2(-6.20f, 4.40f), new Vector2(-3.64f, 4.40f), new Vector2(-3.64f, 2.25f), new Vector2(-6.20f, 2.25f) }
        };

        public static Corridor[] Corridors { get; } =
        {
            new Corridor { From = "WA", To = "OR", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "OR", To = "CA", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "CA", To = "NV", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "NV", To = "AZ", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "AZ", To = "NM", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "NM", To = "TX", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "TX", To = "OK", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "OK", To = "KS", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "KS", To = "MO", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "MO", To = "IL", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "IL", To = "IN", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "IN", To = "OH", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "OH", To = "PA", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "PA", To = "NY", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "NY", To = "MA", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "PA", To = "NJ", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "VA", To = "NC", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "NC", To = "SC", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "SC", To = "GA", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "GA", To = "FL", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "TN", To = "AL", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "AL", To = "MS", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "MS", To = "LA", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "LA", To = "TX", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "MT", To = "ND", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "ND", To = "MN", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "MN", To = "WI", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "WI", To = "MI", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "CO", To = "WY", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "WY", To = "MT", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "ID", To = "UT", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "UT", To = "CO", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "CO", To = "NE", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "NE", To = "IA", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "IA", To = "IL", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "KY", To = "WV", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "WV", To = "VA", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "AR", To = "TN", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "OK", To = "AR", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "CA", To = "AZ", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "IL", To = "KY", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
            new Corridor { From = "TX", To = "NM", Color = new Color(0.78f, 0.7f, 0.38f, 0.75f) },
        };

        public static int StateCount => States.Length;

        public static StatePoly Find(string code)
        {
            if (string.IsNullOrEmpty(code)) return States[0];
            for (int i = 0; i < States.Length; i++)
            {
                if (States[i].Code == code) return States[i];
            }

            return States[0];
        }

        public static StatePoly Find(RegionId id)
        {
            string home = HomeState(id);
            for (int i = 0; i < States.Length; i++)
            {
                if (States[i].Code == home) return States[i];
            }

            for (int i = 0; i < States.Length; i++)
            {
                if (States[i].Region == id) return States[i];
            }

            return States[0];
        }

        /// <summary>Nth state belonging to a sim region (for plant scatter).</summary>
        public static StatePoly StateInRegion(RegionId id, int index)
        {
            int match = 0;
            StatePoly first = Find(id);
            for (int i = 0; i < States.Length; i++)
            {
                if (States[i].Region != id) continue;
                if (match == index) return States[i];
                match++;
            }

            if (match == 0) return first;
            int wrapped = ((index % match) + match) % match;
            match = 0;
            for (int i = 0; i < States.Length; i++)
            {
                if (States[i].Region != id) continue;
                if (match == wrapped) return States[i];
                match++;
            }

            return first;
        }

        public static int CountInRegion(RegionId id)
        {
            int n = 0;
            for (int i = 0; i < States.Length; i++)
                if (States[i].Region == id) n++;
            return n;
        }

        public static Vector3 Centroid(string code)
        {
            StatePoly poly = Find(code);
            Vector2 c = Centroid2(poly.Ring);
            return new Vector3(c.x, RegionHeight + 0.02f, c.y);
        }

        public static Vector3 Centroid(RegionId id) => Centroid(HomeState(id));

        public static Vector3 PlantAnchor(RegionId id)
        {
            Vector3 c = Centroid(id);
            return new Vector3(c.x, RegionHeight + 0.35f, c.z);
        }

        public static Vector3 PlantAnchor(RegionId id, int plantIndexInRegion)
        {
            StatePoly st = StateInRegion(id, plantIndexInRegion);
            Vector2 c = Centroid2(st.Ring);
            return new Vector3(c.x, RegionHeight + 0.35f, c.y);
        }

        public static Vector2 Centroid2(Vector2[] ring)
        {
            float area = 0f;
            float cx = 0f;
            float cy = 0f;
            int n = ring.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = ring[i];
                Vector2 b = ring[(i + 1) % n];
                float cross = a.x * b.y - b.x * a.y;
                area += cross;
                cx += (a.x + b.x) * cross;
                cy += (a.y + b.y) * cross;
            }

            area *= 0.5f;
            if (Mathf.Abs(area) < 1e-5f)
            {
                Vector2 sum = Vector2.zero;
                for (int i = 0; i < n; i++) sum += ring[i];
                return sum / n;
            }

            cx /= (6f * area);
            cy /= (6f * area);
            return new Vector2(cx, cy);
        }

        public static Color OceanFill(bool sunRichTint)
        {
            return sunRichTint
                ? new Color(0.22f, 0.32f, 0.38f, 1f)
                : new Color(0.18f, 0.24f, 0.32f, 1f);
        }

        public static Color StateFill(string code, bool sunRichTint)
        {
            Color c = Find(code).Fill;
            if (!sunRichTint) return c;
            return Color.Lerp(c, new Color(0.62f, 0.48f, 0.32f, 1f), 0.22f);
        }

        public static Color RegionFill(RegionId id, bool sunRichTint)
            => StateFill(HomeState(id), sunRichTint);
    }
}
