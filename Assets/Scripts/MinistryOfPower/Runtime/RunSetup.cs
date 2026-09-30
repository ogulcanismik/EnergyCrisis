using UnityEngine;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.Runtime
{
    /// <summary>
    /// Carries New Game / Load choices from Main Menu into MinistryDesk.
    /// </summary>
    public sealed class RunSetup : MonoBehaviour
    {
        public static RunSetup Instance { get; private set; }

        public string ScenarioId = "usa_like";
        public DifficultyId Difficulty = DifficultyId.Normal;
        public int LoadSlot = -1; // -1 = new game
        public int PreferredSaveSlot = 0;
        public bool BootFromMenu = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void ConfigureNewGame(string scenarioId, DifficultyId difficulty)
        {
            ScenarioId = scenarioId;
            Difficulty = difficulty;
            LoadSlot = -1;
        }

        public void ConfigureLoad(int slot)
        {
            LoadSlot = slot;
        }

        public static RunSetup Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("RunSetup");
            return go.AddComponent<RunSetup>();
        }
    }
}
