using UnityEngine;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.Runtime
{
    /// <summary>Persistent player preferences for grey-box UX.</summary>
    public static class GameSettings
    {
        private const string KeyTooltips = "mop_tooltips";
        private const string KeyVolume = "mop_volume";
        private const string KeyDefaultSpeed = "mop_default_speed";
        private const string KeyHelpSeen = "mop_help_seen";

        public static bool ShowTooltips
        {
            get => PlayerPrefs.GetInt(KeyTooltips, 1) != 0;
            set { PlayerPrefs.SetInt(KeyTooltips, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(KeyVolume, 0.7f);
            set { PlayerPrefs.SetFloat(KeyVolume, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        public static GameSpeed DefaultSpeed
        {
            get => (GameSpeed)PlayerPrefs.GetInt(KeyDefaultSpeed, (int)GameSpeed.Normal);
            set { PlayerPrefs.SetInt(KeyDefaultSpeed, (int)value); PlayerPrefs.Save(); }
        }

        public static bool HelpSeen
        {
            get => PlayerPrefs.GetInt(KeyHelpSeen, 0) != 0;
            set { PlayerPrefs.SetInt(KeyHelpSeen, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
