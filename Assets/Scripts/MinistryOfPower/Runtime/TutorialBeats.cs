using UnityEngine;

namespace MinistryOfPower.Runtime
{
    /// <summary>One-shot tutorial tips beyond the first-run help brief.</summary>
    public static class TutorialBeats
    {
        private const string KeyBuild = "mop_tip_build";
        private const string KeyEvent = "mop_tip_event";
        private const string KeyWinter = "mop_tip_winter";
        private const string KeySave = "mop_tip_save";

        public static bool SeenBuild
        {
            get => PlayerPrefs.GetInt(KeyBuild, 0) != 0;
            set { PlayerPrefs.SetInt(KeyBuild, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool SeenEvent
        {
            get => PlayerPrefs.GetInt(KeyEvent, 0) != 0;
            set { PlayerPrefs.SetInt(KeyEvent, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool SeenWinter
        {
            get => PlayerPrefs.GetInt(KeyWinter, 0) != 0;
            set { PlayerPrefs.SetInt(KeyWinter, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool SeenSave
        {
            get => PlayerPrefs.GetInt(KeySave, 0) != 0;
            set { PlayerPrefs.SetInt(KeySave, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void ResetAll()
        {
            SeenBuild = false;
            SeenEvent = false;
            SeenWinter = false;
            SeenSave = false;
        }

        public const string TipBuild =
            "TIP — First order placed. Watch Construction queue: ETA, /q drain, cancel rows. Treasury stress marks !.";
        public const string TipEvent =
            "TIP — Crisis card. Adequacy crises let you load-shed who goes dark. Others: absorb / fossil / tariff.";
        public const string TipWinter =
            "TIP — Winter. Thin firm/storage raises peak risk. Check Resources projections and fuel cover.";
        public const string TipSave =
            "TIP — Saved. Slot 1 is Quicksave (F5) and autosaves every 30 days. Pause for other slots.";
    }
}
