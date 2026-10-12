using System;
using System.IO;
using UnityEngine;
using Uvc.Core;

namespace Uvc.Prototype
{
    public enum LoadStatus
    {
        NoSave,
        Loaded,
        RestoredFromBackup,
        Corrupted,
        NewerSchema
    }

    /// <summary>
    /// One slot + backup + start-of-day snapshot. Writes go through a temp file,
    /// then replace the main file, keeping the previous one as the backup.
    /// </summary>
    public static class SaveSystem
    {
        private static string Folder => Application.persistentDataPath;
        private static string MainPath => Path.Combine(Folder, "uvc_save.json");
        private static string BackupPath => Path.Combine(Folder, "uvc_save.bak");
        private static string TempPath => Path.Combine(Folder, "uvc_save.tmp");
        private static string DayStartPath => Path.Combine(Folder, "uvc_daystart.json");

        public static bool HasSave => File.Exists(MainPath) || File.Exists(BackupPath);

        public static void Save(DayState state)
        {
            string json = JsonUtility.ToJson(state);
            File.WriteAllText(TempPath, json);
            if (File.Exists(MainPath))
            {
                try
                {
                    File.Replace(TempPath, MainPath, BackupPath);
                    return;
                }
                catch (Exception exception) when (exception is IOException or PlatformNotSupportedException or UnauthorizedAccessException)
                {
                    File.Copy(MainPath, BackupPath, true);
                    File.Delete(MainPath);
                }
            }

            File.Move(TempPath, MainPath);
        }

        public static void SaveDayStart(DayState state) => File.WriteAllText(DayStartPath, JsonUtility.ToJson(state));

        public static DayState LoadDayStart()
        {
            return TryRead(DayStartPath, out DayState state) == LoadStatus.Loaded ? state : null;
        }

        public static LoadStatus Load(out DayState state)
        {
            LoadStatus main = TryRead(MainPath, out state);
            if (main is LoadStatus.Loaded or LoadStatus.NewerSchema)
            {
                return main;
            }

            LoadStatus backup = TryRead(BackupPath, out state);
            if (backup == LoadStatus.Loaded)
            {
                return main == LoadStatus.NoSave ? LoadStatus.Loaded : LoadStatus.RestoredFromBackup;
            }

            state = null;
            return main == LoadStatus.NoSave && backup == LoadStatus.NoSave ? LoadStatus.NoSave : backup == LoadStatus.NewerSchema ? LoadStatus.NewerSchema : LoadStatus.Corrupted;
        }

        public static void Delete()
        {
            foreach (string path in new[] { MainPath, BackupPath, TempPath })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static LoadStatus TryRead(string path, out DayState state)
        {
            state = null;
            if (!File.Exists(path))
            {
                return LoadStatus.NoSave;
            }

            try
            {
                DayState loaded = JsonUtility.FromJson<DayState>(File.ReadAllText(path));
                if (loaded == null || loaded.schemaVersion <= 0 || loaded.clock == null || loaded.inventory == null || loaded.quests.Count == 0)
                {
                    return LoadStatus.Corrupted;
                }

                if (loaded.schemaVersion > DayState.CurrentSchemaVersion)
                {
                    return LoadStatus.NewerSchema;
                }

                state = loaded;
                return LoadStatus.Loaded;
            }
            catch (Exception exception) when (exception is ArgumentException or IOException)
            {
                return LoadStatus.Corrupted;
            }
        }
    }

    [Serializable]
    public sealed class Settings
    {
        private const string Key = "uvc.settings";

        public float uiScale = 1f;

        public static Settings Load()
        {
            try
            {
                return JsonUtility.FromJson<Settings>(PlayerPrefs.GetString(Key, "{}")) ?? new Settings();
            }
            catch (ArgumentException)
            {
                return new Settings();
            }
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }
    }
}
