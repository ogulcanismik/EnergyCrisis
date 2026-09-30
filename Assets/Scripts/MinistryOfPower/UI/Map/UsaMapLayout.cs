using UnityEngine;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.UI.Map
{
    /// <summary>
    /// Stylized continental-USA grey-box layout: three playable regions + interconnects.
    /// Coordinates are XZ map space (Y up). Tunable without touching mesh builders.
    /// </summary>
    public static class UsaMapLayout
    {
        public const float RegionHeight = 0.05f;
        public const float OceanHeight = -0.02f;

        /// <summary>Default orthographic framing for Shell B center canvas.</summary>
        public static readonly Vector3 DefaultCameraPosition = new Vector3(0f, 22f, -0.5f);
        public const float DefaultOrthoSize = 11.5f;
        public const float MinOrthoSize = 6.5f;
        public const float MaxOrthoSize = 16f;

        public static readonly Vector2 OceanSize = new Vector2(36f, 24f);

        public struct RegionPoly
        {
            public RegionId Id;
            public string MapLabel;
            public Color Fill;
            public Vector2[] Ring;
        }

        public struct Corridor
        {
            public RegionId From;
            public RegionId To;
            public Color Color;
        }

        /// <summary>
        /// Three political-map regions approximating Midwest/North, Atlantic Coast, Southwest Interior.
        /// Rings are closed (last != first); builder closes the loop.
        /// </summary>
        public static RegionPoly[] Regions { get; } =
        {
            new RegionPoly
            {
                Id = RegionId.Desert,
                MapLabel = "SOUTHWEST",
                Fill = new Color(0.52f, 0.44f, 0.34f, 1f),
                Ring = new[]
                {
                    new Vector2(-11.4f, 5.4f),
                    new Vector2(-7.6f, 5.9f),
                    new Vector2(-4.8f, 4.1f),
                    new Vector2(-3.6f, 1.1f),
                    new Vector2(-2.6f, -2.1f),
                    new Vector2(-4.2f, -4.3f),
                    new Vector2(-7.2f, -3.9f),
                    new Vector2(-10.1f, -2.3f),
                    new Vector2(-11.6f, 0.4f),
                    new Vector2(-11.9f, 2.9f)
                }
            },
            new RegionPoly
            {
                Id = RegionId.North,
                MapLabel = "MIDWEST / NORTH",
                Fill = new Color(0.42f, 0.48f, 0.52f, 1f),
                Ring = new[]
                {
                    new Vector2(-7.6f, 5.9f),
                    new Vector2(-4.1f, 7.3f),
                    new Vector2(0.4f, 7.1f),
                    new Vector2(3.4f, 6.3f),
                    new Vector2(4.1f, 3.4f),
                    new Vector2(1.4f, 2.0f),
                    new Vector2(-1.6f, 2.4f),
                    new Vector2(-3.6f, 1.1f),
                    new Vector2(-4.8f, 4.1f)
                }
            },
            new RegionPoly
            {
                Id = RegionId.Coast,
                MapLabel = "ATLANTIC COAST",
                Fill = new Color(0.36f, 0.44f, 0.55f, 1f),
                Ring = new[]
                {
                    new Vector2(3.4f, 6.3f),
                    new Vector2(7.1f, 6.6f),
                    new Vector2(10.1f, 5.3f),
                    new Vector2(11.3f, 3.0f),
                    new Vector2(11.1f, 0.4f),
                    new Vector2(9.6f, -2.1f),
                    new Vector2(8.1f, -4.6f),
                    new Vector2(6.4f, -6.9f),
                    new Vector2(4.4f, -7.3f),
                    new Vector2(2.4f, -5.6f),
                    new Vector2(0.4f, -4.1f),
                    new Vector2(-2.6f, -2.1f),
                    new Vector2(-3.6f, 1.1f),
                    new Vector2(-1.6f, 2.4f),
                    new Vector2(1.4f, 2.0f),
                    new Vector2(4.1f, 3.4f)
                }
            }
        };

        public static Corridor[] Corridors { get; } =
        {
            new Corridor { From = RegionId.North, To = RegionId.Coast, Color = new Color(0.85f, 0.75f, 0.35f, 0.9f) },
            new Corridor { From = RegionId.North, To = RegionId.Desert, Color = new Color(0.75f, 0.7f, 0.4f, 0.85f) },
            new Corridor { From = RegionId.Coast, To = RegionId.Desert, Color = new Color(0.7f, 0.65f, 0.45f, 0.8f) }
        };

        public static Vector3 Centroid(RegionId id)
        {
            RegionPoly poly = Find(id);
            Vector2 c = Centroid2(poly.Ring);
            return new Vector3(c.x, RegionHeight + 0.02f, c.y);
        }

        public static Vector3 PlantAnchor(RegionId id)
        {
            Vector3 c = Centroid(id);
            return new Vector3(c.x, RegionHeight + 0.35f, c.z);
        }

        public static RegionPoly Find(RegionId id)
        {
            for (int i = 0; i < Regions.Length; i++)
            {
                if (Regions[i].Id == id) return Regions[i];
            }

            return Regions[0];
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

        public static Color RegionFill(RegionId id, bool sunRichTint)
        {
            Color c = Find(id).Fill;
            if (!sunRichTint) return c;
            // Same USA grey-box; warmer wash for Sun-Rich until a dedicated layout exists.
            return Color.Lerp(c, new Color(0.62f, 0.48f, 0.32f, 1f), 0.22f);
        }
    }
}
