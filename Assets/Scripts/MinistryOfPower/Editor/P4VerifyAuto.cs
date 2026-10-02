using System.IO;
using UnityEditor;
using UnityEngine;

namespace MinistryOfPower.EditorTools
{
    /// <summary>One-shot auto verify when flag file is present.</summary>
    [InitializeOnLoad]
    public static class P4VerifyAuto
    {
        private const string Flag = "Library/mop_run_p4_verify.flag";

        static P4VerifyAuto()
        {
            EditorApplication.delayCall += TryRun;
        }

        private static void TryRun()
        {
            string flagPath = Path.Combine(Directory.GetCurrentDirectory(), Flag);
            if (!File.Exists(flagPath)) return;
            try { File.Delete(flagPath); } catch { /* ignore */ }
            P4Verify.VerifyMenu();
        }
    }
}
