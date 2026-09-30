using System;
using UnityEngine;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.Runtime
{
    /// <summary>Clickable state polygon on the USA political map. Selection drives RegionId for sim.</summary>
    public sealed class RegionMarker : MonoBehaviour
    {
        public static event Action<RegionId> RegionSelected;

        /// <summary>Last clicked state postal code (empty if none).</summary>
        public static string SelectedStateCode { get; private set; } = "";

        [SerializeField] private RegionId regionId;
        [SerializeField] private string stateCode = "";
        [SerializeField] private string fullName = "";
        [SerializeField] private Color baseFill = Color.gray;

        public RegionId RegionId => regionId;
        public string StateCode => stateCode;
        public string FullName => fullName;
        public Color BaseFill => baseFill;

        public void Configure(RegionId id, Color color)
        {
            Configure(id, "", "", color);
        }

        public void Configure(RegionId id, string code, string name, Color color)
        {
            regionId = id;
            stateCode = code ?? "";
            fullName = name ?? "";
            baseFill = color;
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
            Select(regionId, stateCode);
        }

        public static void Select(RegionId id)
        {
            Select(id, HomeStateFor(id));
        }

        public static void Select(RegionId id, string stateCode)
        {
            SelectedStateCode = stateCode ?? "";
            RegionSelected?.Invoke(id);
        }

        private void OnMouseEnter()
        {
            // Invisible pick meshes (prototype PNG art mode) skip hover wash.
            if (baseFill.a < 0.05f) return;
            var r = GetComponent<Renderer>();
            if (r != null) r.material.color = Color.Lerp(baseFill, Color.white, 0.25f);
        }

        private void OnMouseExit()
        {
            if (baseFill.a < 0.05f) return;
            var r = GetComponent<Renderer>();
            if (r != null) r.material.color = baseFill;
        }

        public static Color BaseColor(RegionId id)
        {
            switch (id)
            {
                case RegionId.North: return new Color(0.42f, 0.48f, 0.52f, 1f);
                case RegionId.Coast: return new Color(0.36f, 0.44f, 0.55f, 1f);
                case RegionId.Desert: return new Color(0.52f, 0.44f, 0.34f, 1f);
                default: return Color.gray;
            }
        }

        private static string HomeStateFor(RegionId id)
        {
            switch (id)
            {
                case RegionId.North: return "IL";
                case RegionId.Coast: return "PA";
                case RegionId.Desert: return "AZ";
                default: return "PA";
            }
        }
    }
}
