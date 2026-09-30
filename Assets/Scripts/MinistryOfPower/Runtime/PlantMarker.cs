using System;
using UnityEngine;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.Runtime
{
    /// <summary>Clickable plant icon on the grey-box map.</summary>
    public sealed class PlantMarker : MonoBehaviour
    {
        public static event Action<string> PlantSelected;

        [SerializeField] private string plantId;

        public string PlantId => plantId;

        public void Configure(string id, Color color)
        {
            plantId = id;
            var r = GetComponent<Renderer>();
            if (r != null)
            {
                if (r.material == null)
                    r.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                r.material.color = color;
            }

            // Ensure raycasts hit
            if (GetComponent<Collider>() == null)
                gameObject.AddComponent<CapsuleCollider>();
        }

        private void OnMouseDown()
        {
            if (!string.IsNullOrEmpty(plantId))
                PlantSelected?.Invoke(plantId);
        }

        public static void Select(string id) => PlantSelected?.Invoke(id);
    }
}
