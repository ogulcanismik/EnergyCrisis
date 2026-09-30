using UnityEngine;

namespace MinistryOfPower.Runtime
{
    /// <summary>
    /// Stub audio hub — empty OK. Exists so the Managers hierarchy is complete and designers
    /// have a known place to hang SFX / music later.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private bool muteMaster;
        [SerializeField] [Range(0f, 1f)] private float masterVolume = 1f;

        public bool MuteMaster => muteMaster;
        public float MasterVolume => muteMaster ? 0f : masterVolume;
    }
}
