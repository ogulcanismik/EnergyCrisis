using UnityEngine;
using MinistryOfPower.Data;
using MinistryOfPower.UI;

namespace MinistryOfPower.Runtime
{
    /// <summary>
    /// Owns HUD / pause-menu references for the desk scene. Paradox chrome is gated by
    /// <see cref="ParadoxChromeHud.UseLegacyHud"/> (default off for UI Toolkit prep);
    /// ESC pause still builds at runtime via <see cref="PauseMenuOverlay"/>.
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        [SerializeField] private ParadoxChromeHud paradoxHud;
        [SerializeField] private PauseMenuOverlay pauseMenu;
        [SerializeField] private GameTuning tuning;

        public ParadoxChromeHud ParadoxHud => paradoxHud;
        public PauseMenuOverlay PauseMenu => pauseMenu;

        public void BindTuning(GameTuning gameTuning)
        {
            tuning = gameTuning;
            if (paradoxHud != null)
                paradoxHud.ApplyTuning(gameTuning);
        }

        public ParadoxChromeHud EnsureHud()
        {
            if (paradoxHud == null)
                paradoxHud = GetComponent<ParadoxChromeHud>();
            if (paradoxHud == null)
                paradoxHud = FindFirstObjectByType<ParadoxChromeHud>();
            if (paradoxHud == null)
                paradoxHud = gameObject.AddComponent<ParadoxChromeHud>();
            if (tuning != null)
                paradoxHud.ApplyTuning(tuning);
            return paradoxHud;
        }

        public PauseMenuOverlay EnsurePauseMenu(Transform host)
        {
            pauseMenu = PauseMenuOverlay.Ensure(host != null ? host : transform);
            return pauseMenu;
        }

#if UNITY_EDITOR
        public void EditorAssign(ParadoxChromeHud hud, PauseMenuOverlay pause, GameTuning gameTuning)
        {
            paradoxHud = hud;
            pauseMenu = pause;
            tuning = gameTuning;
        }
#endif
    }
}
