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
        public string CityName;
        public string SavedAt;
        public int Population;
        public int Buildings;
        public double Money;

        public float CameraX, CameraZ, CameraYaw, CameraDistance;
        public int SpeedLevel = 1;

        public CitySnapshot City;
    }

    /// <summary>Reads and writes the save file (one slot) in Unity's per-user data folder.</summary>
    public static class SaveStore
    {
        private const string FileName = "urban-genesis-save.json";

        public static string Path { get { return System.IO.Path.Combine(Application.persistentDataPath, FileName); } }

        public static bool Exists { get { return File.Exists(Path); } }

        /// <summary>Writes the save. Returns null on success, or a message saying what went wrong.</summary>
        public static string Write(SaveGame save)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                string temp = Path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(save));
                if (File.Exists(Path))
                {
                    File.Delete(Path);
                }

                File.Move(temp, Path);
                return null;
            }
            catch (Exception e)
            {
                return "Could not save: " + e.Message;
            }
        }

        /// <summary>Reads the save. Returns null and sets <paramref name="error"/> if there is none or it is unreadable.</summary>
        public static SaveGame Read(out string error)
        {
            error = null;
            try
            {
                if (!File.Exists(Path))
                {
                    error = "There is no saved city yet.";
                    return null;
                }

                SaveGame save = JsonUtility.FromJson<SaveGame>(File.ReadAllText(Path));
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
