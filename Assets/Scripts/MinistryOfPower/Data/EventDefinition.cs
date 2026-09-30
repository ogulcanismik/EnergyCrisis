using UnityEngine;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.Data
{
    [CreateAssetMenu(menuName = "Ministry of Power/Event Definition", fileName = "Event_")]
    public sealed class EventDefinition : ScriptableObject
    {
        public string Id = "event";
        public string DisplayName = "Event";
        [TextArea(2, 5)] public string Description;
        public PendingEventKind Kind = PendingEventKind.OilPriceShock;
        [Range(0f, 1f)] public float BaseWeight = 1f;
        [Tooltip("Minimum oil-linked share before this event forces a crisis decision.")]
        [Range(0f, 1f)] public float CrisisOilShareThreshold = 0.15f;
    }
}
