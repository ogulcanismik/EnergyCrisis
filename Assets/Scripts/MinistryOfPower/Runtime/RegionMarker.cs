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
            switch (id)
            {
                case RegionId.North: return new Color(0.45f, 0.5f, 0.55f);
                case RegionId.Coast: return new Color(0.35f, 0.45f, 0.6f);
                case RegionId.Desert: return new Color(0.55f, 0.45f, 0.3f);
                default: return Color.gray;
            }
        }
    }
}
