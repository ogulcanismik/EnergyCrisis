using System;
using UnityEditor;
using UnityEngine;

namespace MinistryOfPower.EditorTools
{
    /// <summary>
    /// After script compile, wait until Editor is not compiling/updating before agents hammer MCP.
    /// Menu: Ministry of Power / Wait Until Editor Idle.
    /// </summary>
    public static class McpRetryHygiene
    {
        private static Action<bool> _pending;

        [MenuItem("Ministry of Power/Wait Until Editor Idle")]
        public static void WaitMenu()
        {
            WaitUntilIdle(ok => Debug.Log(ok ? "Editor idle — MCP safe." : "Editor idle wait timed out."));
        }

        public static void WaitUntilIdle(Action<bool> done, float timeoutSeconds = 90f)
        {
            _pending = done;
            double start = EditorApplication.timeSinceStartup;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                bool busy = EditorApplication.isCompiling
                            || EditorApplication.isUpdating
                            || (EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying);
                if (!busy)
                {
                    EditorApplication.update -= tick;
                    Action<bool> cb = _pending;
                    _pending = null;
                    cb?.Invoke(true);
                    return;
                }

                if (EditorApplication.timeSinceStartup - start > timeoutSeconds)
                {
                    EditorApplication.update -= tick;
                    Action<bool> cb = _pending;
                    _pending = null;
                    cb?.Invoke(false);
                }
            };
            EditorApplication.update += tick;
        }
    }
}
