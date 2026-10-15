using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CodePlate
{
    /// <summary>
    /// Player builds from the command line: `npm run build:web` (Builds/Web), `build:windows`
    /// (Builds/Windows/Sprocket.exe), `build:macos` (Builds/macOS/Sprocket.app). The target's
    /// module must be installed in Unity Hub (Web, Windows Mono, Mac Mono). See docs/recipes/builds.md.
    /// </summary>
    public static class Builds
    {
        static readonly string[] Scenes = { Game.Core.ProjectPaths.BootScene };

        /// <summary>The web page around the game: loading bar, full-window canvas, "turn your phone sideways".</summary>
        const string WebTemplate = "Assets/WebGLTemplates/Game";

        public static void Web()
        {
            if (Directory.Exists(WebTemplate)) PlayerSettings.WebGL.template = "PROJECT:" + Path.GetFileName(WebTemplate);
            Build(BuildTarget.WebGL, "Builds/Web");
        }
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, $"Builds/Windows/{PlayerSettings.productName}.exe");
        public static void MacOS() => Build(BuildTarget.StandaloneOSX, $"Builds/macOS/{PlayerSettings.productName}.app");

        static void Build(BuildTarget target, string path)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
            {
                Debug.LogError($"[CodePlate] the {target} build support module isn't installed: add it in Unity Hub (Installs > ... > Add modules) or `unity install-modules`.");
                EditorApplication.Exit(2);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[CodePlate] build {s.result}: {Path.GetFullPath(path)} ({s.totalSize / (1024 * 1024.0):0.0} MB, {s.totalTime.TotalSeconds:0} s, {s.totalErrors} errors)");
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
