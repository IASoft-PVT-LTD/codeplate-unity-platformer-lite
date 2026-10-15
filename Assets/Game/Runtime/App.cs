using Game.Core;
using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// The game's services, created once by Boot: content (config, levels), saved progress,
    /// art, audio, input and the screens. Everything else reaches them through here.
    /// </summary>
    public static class App
    {
        public static GameContent Content { get; private set; }
        public static GameConfig Config => Content.Config;
        public static Progress Progress { get; private set; }
        public static Art Art { get; private set; }
        public static AudioDirector Audio { get; private set; }
        public static InputSource Input { get; private set; }
        public static ScreenRouter Screens { get; private set; }
        /// <summary>The level being played (null on menus).</summary>
        public static LevelScreen Level { get; internal set; }
        public static bool Ready { get; private set; }

        /// <summary>Loads content and progress. Called by Boot before anything else.</summary>
        internal static void Init(Art art, AudioDirector audio, InputSource input, ScreenRouter screens)
        {
            Content = GameContent.Load(path => Resources.Load<TextAsset>(path)?.text);
            foreach (var e in Content.Errors) Debug.LogError($"[CodePlate] content: {e}");
            Progress = SaveStore.Load();
            // The saved language, or English when the game doesn't ship it (any more).
            Progress.Settings.Language = Loc.Use(Progress.Settings.Language);
            Art = art;
            Audio = audio;
            Input = input;
            Screens = screens;
            Ready = true;
        }

        /// <summary>Writes progress and settings to disk.</summary>
        public static void Save() => SaveStore.Save(Progress);

        /// <summary>Starts a level by number (1-based).</summary>
        public static void PlayLevel(int number) => Screens.ShowLevel(number);

        internal static void Shutdown()
        {
            Ready = false;
            Level = null;
        }
    }
}
