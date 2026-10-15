namespace Game.Core
{
    /// <summary>
    /// Where the project's files are, relative to the Unity project folder (the one holding
    /// Assets/ and ProjectSettings/). Every path the code builds to a project file starts here,
    /// so a move needs one edit. Keep checks/layout.mjs in step: a project rule compares them.
    /// Only constants: the simulation itself never reads files.
    /// </summary>
    public static class ProjectPaths
    {
        /// <summary>The folder holding Game/, Resources/, Scenes/, Settings/ and Tests/.</summary>
        public const string Game = "Assets";
        public const string Resources = Game + "/Resources";
        public const string Art = Resources + "/Art";
        public const string Audio = Resources + "/Audio";
        public const string UI = Resources + "/UI";
        public const string Levels = Resources + "/Levels";
        public const string Settings = Game + "/Settings";
        public const string Scenes = Game + "/Scenes";
        public const string BootScene = Scenes + "/Boot.unity";
        /// <summary>Recorded runs, one per level (see Replay).</summary>
        public const string Replays = "replays";
        /// <summary>The docs (architecture, levels, movement, recipes).</summary>
        public const string Docs = "docs";
        /// <summary>Screenshots from npm run shots (not part of the project).</summary>
        public const string Shots = "shots";

        /// <summary>The replay file of level n, e.g. replays/level_03.replay.</summary>
        public static string Replay(int level) => $"{Replays}/level_{level:00}.replay";
    }
}
