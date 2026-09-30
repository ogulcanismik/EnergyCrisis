using System;
using UnityEngine;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.Runtime
{
    /// <summary>Clickable grey-box region cube on the map.</summary>
    public sealed class RegionMarker : MonoBehaviour
    {
        public static event Action<RegionId> RegionSelected;

        [SerializeField] private RegionId regionId;

        public RegionId RegionId => regionId;

        public void Configure(RegionId id, Color color)
        {
            regionId = id;
            var r = GetComponent<Renderer>();
            if (r != null)
            {
                if (r.material == null || r.sharedMaterial == null)
                {
                    r.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                }

                r.material.color = color;
            }
        }

        private void OnMouseDown()
        {
            Select(regionId);
        }

        public static void Select(RegionId id)
        {
            RegionSelected?.Invoke(id);
        }

        private void OnMouseEnter()
        {
            var r = GetComponent<Renderer>();
            if (r != null) r.material.color = Color.Lerp(r.material.color, Color.white, 0.25f);
        }

        private void OnMouseExit()
        {
            // Restore via DayNightWeatherController refresh — keep slight tint by reconfigure on exit
            Color baseCol = BaseColor(regionId);
            var r = GetComponent<Renderer>();
            if (r != null) r.material.color = baseCol;
        }

        public static Color BaseColor(RegionId id)
        {
            // Match UsaMapLayout political fills so hover restore stays consistent.
            switch (id)
            {
                case RegionId.North: return new Color(0.42f, 0.48f, 0.52f, 1f);
                case RegionId.Coast: return new Color(0.36f, 0.44f, 0.55f, 1f);
                case RegionId.Desert: return new Color(0.52f, 0.44f, 0.34f, 1f);
                default: return Color.gray;
            }
        }
    }
}
