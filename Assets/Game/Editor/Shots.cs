using System;
using Game.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodePlate
{
    /// <summary>
    /// `npm run shots -- screen=level level=3 x=40` runs the editor in batch mode (with graphics)
    /// with -executeMethod CodePlate.Shots.Run -codeplateShot "...". This opens the Boot scene
    /// and enters play mode; ShotDirector (runtime) does the rest, and the watcher below quits
    /// the editor when it's done.
    /// </summary>
    public static class Shots
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene(Game.Core.ProjectPaths.BootScene);
            EditorApplication.EnterPlaymode();
        }
    }

    [InitializeOnLoad]
    static class ShotWatcher
    {
        static readonly double Started;

        static ShotWatcher()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-codeplateShot") < 0) return;
            Started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Watch;
        }

        static void Watch()
        {
            if (ShotDirector.Finished)
            {
                EditorApplication.update -= Watch;
                EditorApplication.Exit(ShotDirector.ExitCode);
            }
            else if (EditorApplication.timeSinceStartup - Started > 240)
            {
                Debug.LogError("[CodePlate] shot timed out after 240 s");
                EditorApplication.Exit(1);
            }
        }
    }
}
