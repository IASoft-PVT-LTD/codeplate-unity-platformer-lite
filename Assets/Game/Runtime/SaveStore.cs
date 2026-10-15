using System;
using System.IO;
using Game.Core;
using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// Reads and writes progress.json in Application.persistentDataPath (on the web that's the
    /// browser's IndexedDB). The format and its migrations live in Game.Core.Progress.
    /// </summary>
    public static class SaveStore
    {
        /// <summary>Tests and tools point this at a scratch file so they never touch the player's save.</summary>
        public static string PathOverride;

        public static string FilePath => PathOverride ?? Path.Combine(Application.persistentDataPath, "progress.json");

        public static Progress Load()
        {
            string text = null;
            try
            {
                if (File.Exists(FilePath)) text = File.ReadAllText(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CodePlate] couldn't read {FilePath}: {e.Message}");
            }
            var p = Progress.FromJson(text, out string problem);
            if (problem != null) Debug.LogWarning($"[CodePlate] {problem}");
            return p;
        }

        public static void Save(Progress p)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, p.ToJson());
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CodePlate] couldn't save progress: {e.Message}");
            }
        }
    }
}
