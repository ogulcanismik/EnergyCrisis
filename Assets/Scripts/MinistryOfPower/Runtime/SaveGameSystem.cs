using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.Runtime
{
    [Serializable]
    public sealed class SaveSlotMeta
    {
        public int Slot;
        public string ScenarioId;
        public string ScenarioName;
        public string DifficultyName;
        public string DateLabel;
        public int AbsoluteDay;
        public float Confidence;
        public float Budget;
        public long SavedUtcTicks;
        public bool Occupied;
    }

    [Serializable]
    public sealed class GameSaveData
    {
        public int Version = 1;
        public int Slot;
        public string ScenarioId;
        public string ScenarioName;
        public int Difficulty;
        public int Seed;
        public int Year;
        public int AbsoluteDay;
        public int DayIndex;
        public int QuarterIndex;
        public float DayFraction;
        public int Speed;
        public float Budget;
        public float FuelPriceIndex;
        public float Adequacy;
        public float Affordability;
        public float Transition;
        public float Confidence;
        public float Coal;
        public float Gas;
        public float Oil;
        public float Uranium;
        public float ImportBaseline;
        public float OilShockMul;
        public int OilShockDays;
        public float HydroMul;
        public int DroughtDays;
        public float DemandMul;
        public int HeatwaveDays;
        public float ImportAvailable;
        public int ImportDays;
        public float PrivateReserveMw;
        public float PrivateReserveQuarterlyCost;
        public float EmergencyImportMw;
        public int EmergencyImportDaysRemaining;
        public int DaysSinceMajorEvent;
        public int DaysSinceMeterCrash;
        public float FuelCoal;
        public float FuelGas;
        public float FuelOil;
        public float FuelUranium;
        public int AdequacyStreak;
        public int AffordStreak;
        public int BestAdequacyStreak;
        public int BestAffordStreak;
        public int SelectedRegion;
        public List<PlantSave> Plants = new List<PlantSave>();
        public List<BuildSave> Builds = new List<BuildSave>();
        public List<string> BuildCatalogIds = new List<string>();
    }

    [Serializable]
    public sealed class PlantSave
    {
        public string Id;
        public string DisplayName;
        public int Fuel;
        public float CapacityMw;
        public float Availability;
        public float VariableCost;
        public float OilExposure;
        public string DefinitionId;
        public bool Retired;
        public float QuarterlyUpkeep;
        public float DailyFuelUse;
        public int Region;
    }

    [Serializable]
    public sealed class BuildSave
    {
        public string Id;
        public string DisplayName;
        public string DefinitionId;
        public int Fuel;
        public float CapacityMw;
        public float Availability;
        public float VariableCost;
        public float OilExposure;
        public int PaymentMode;
        public float Upfront;
        public float Quarterly;
        public int TotalQuarters;
        public int QuartersRemaining;
        public float Upkeep;
        public float DailyFuelUse;
        public bool Cancelled;
        public int Region;
    }

    /// <summary>JSON save slots under persistentDataPath/saves/slot_N.json</summary>
    public static class SaveGameSystem
    {
        public const int SlotCount = 5;

        public static string SaveRoot => Path.Combine(Application.persistentDataPath, "saves");

        public static string SlotPath(int slot) => Path.Combine(SaveRoot, $"slot_{slot}.json");

        public static SaveSlotMeta[] ListSlots()
        {
            var metas = new SaveSlotMeta[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                metas[i] = PeekSlot(i);
            }

            return metas;
        }

        public static SaveSlotMeta PeekSlot(int slot)
        {
            var meta = new SaveSlotMeta { Slot = slot, Occupied = false };
            string path = SlotPath(slot);
            if (!File.Exists(path))
            {
                return meta;
            }

            try
            {
                GameSaveData data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
                if (data == null) return meta;
                meta.Occupied = true;
                meta.ScenarioId = data.ScenarioId;
                meta.ScenarioName = data.ScenarioName;
                meta.DifficultyName = DifficultyConfig.Create((DifficultyId)data.Difficulty).DisplayName;
                meta.DateLabel = $"{data.Year:D4} day {data.AbsoluteDay}";
                meta.AbsoluteDay = data.AbsoluteDay;
                meta.Confidence = data.Confidence;
                meta.Budget = data.Budget;
                meta.SavedUtcTicks = File.GetLastWriteTimeUtc(path).Ticks;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Save peek failed slot {slot}: {e.Message}");
            }

            return meta;
        }

        public static bool TrySave(int slot, GameSaveData data, out string message)
        {
            if (slot < 0 || slot >= SlotCount)
            {
                message = "Invalid slot.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(SaveRoot);
                data.Slot = slot;
                File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(data, true));
                message = $"Saved to slot {slot + 1}.";
                return true;
            }
            catch (Exception e)
            {
                message = e.Message;
                return false;
            }
        }

        public static bool TryLoad(int slot, out GameSaveData data, out string message)
        {
            data = null;
            string path = SlotPath(slot);
            if (!File.Exists(path))
            {
                message = "Empty slot.";
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
                if (data == null)
                {
                    message = "Corrupt save.";
                    return false;
                }

                message = $"Loaded slot {slot + 1}.";
                return true;
            }
            catch (Exception e)
            {
                message = e.Message;
                return false;
            }
        }

        public static bool AnySaveExists()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (File.Exists(SlotPath(i))) return true;
            }

            return false;
        }

        /// <summary>Newest occupied slot by file write time, or -1.</summary>
        public static int FindNewestSlot()
        {
            int best = -1;
            long bestTicks = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                SaveSlotMeta m = PeekSlot(i);
                if (!m.Occupied) continue;
                if (m.SavedUtcTicks >= bestTicks)
                {
                    bestTicks = m.SavedUtcTicks;
                    best = i;
                }
            }

            return best;
        }
    }
}
