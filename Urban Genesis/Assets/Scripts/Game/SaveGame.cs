using System;
using System.IO;
using UnityEngine;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Game
{
    /// <summary>What is written to disk: the city plus the bits of the Unity layer worth keeping (name, camera, speed).</summary>
    [Serializable]
    public sealed class SaveGame
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;

        /// <summary>How many population milestones have paid their reward already.</summary>
        public int MilestonesRewarded;
        public string CityName;
        public string SavedAt;

        /// <summary>The in-game start date, as ticks (0 in older saves: the date then starts from today).</summary>
        public long StartDateTicks;
        public int Population;
        public int Buildings;
        public double Money;

        public float CameraX, CameraZ, CameraYaw, CameraDistance;
        public int SpeedLevel = 1;

        public CitySnapshot City;
    }

    /// <summary>
    /// Reads and writes the save files, one per slot (1 to <see cref="SlotCount"/>), in Unity's per-user data folder.
    /// Slot 1 keeps the file name the single-slot version used, so an old save shows up there.
    /// </summary>
    public static class SaveStore
    {
        public const int SlotCount = 5;

        public static string PathOf(int slot)
        {
            string name = slot <= 1 ? "urban-genesis-save.json" : "urban-genesis-save-" + slot + ".json";
            return System.IO.Path.Combine(Application.persistentDataPath, name);
        }

        public static bool Exists(int slot) { return File.Exists(PathOf(slot)); }

        /// <summary>True if any slot holds a save.</summary>
        public static bool AnyExists
        {
            get
            {
                for (int slot = 1; slot <= SlotCount; slot++) { if (Exists(slot)) { return true; } }
                return false;
            }
        }

        /// <summary>The slot that was written most recently, or 0 if all are empty.</summary>
        public static int MostRecentSlot()
        {
            int best = 0;
            DateTime bestTime = DateTime.MinValue;
            for (int slot = 1; slot <= SlotCount; slot++)
            {
                if (!Exists(slot)) { continue; }
                DateTime time = File.GetLastWriteTimeUtc(PathOf(slot));
                if (best == 0 || time > bestTime) { best = slot; bestTime = time; }
            }

            return best;
        }

        /// <summary>The first empty slot, or 0 if they are all in use.</summary>
        public static int FirstFreeSlot()
        {
            for (int slot = 1; slot <= SlotCount; slot++) { if (!Exists(slot)) { return slot; } }
            return 0;
        }

        /// <summary>Writes the save to a slot. Returns null on success, or a message saying what went wrong.</summary>
        public static string Write(int slot, SaveGame save)
        {
            try
            {
                string path = PathOf(slot);
                Directory.CreateDirectory(Application.persistentDataPath);
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(save));
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temp, path);
                return null;
            }
            catch (Exception e)
            {
                return "Could not save: " + e.Message;
            }
        }

        /// <summary>Removes a slot's save. Returns null on success, or a message saying what went wrong.</summary>
        public static string Delete(int slot)
        {
            try
            {
                if (Exists(slot)) { File.Delete(PathOf(slot)); }
                return null;
            }
            catch (Exception e)
            {
                return "Could not delete: " + e.Message;
            }
        }

        /// <summary>Reads a slot. Returns null and sets <paramref name="error"/> if it is empty or unreadable.</summary>
        public static SaveGame Read(int slot, out string error)
        {
            error = null;
            try
            {
                string path = PathOf(slot);
                if (!File.Exists(path))
                {
                    error = "Slot " + slot + " is empty.";
                    return null;
                }

                SaveGame save = JsonUtility.FromJson<SaveGame>(File.ReadAllText(path));
                if (save == null || save.City == null)
                {
                    error = "The save file is empty or damaged.";
                    return null;
                }

                if (save.Version > SaveGame.CurrentVersion || save.City.Version > CitySnapshot.CurrentVersion)
                {
                    error = "This save was made by a newer version of the game.";
                    return null;
                }

                return save;
            }
            catch (Exception e)
            {
                error = "Could not read the save: " + e.Message;
                return null;
            }
        }
    }
}
