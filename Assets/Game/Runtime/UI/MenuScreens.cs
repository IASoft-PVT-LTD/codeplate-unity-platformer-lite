using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Runtime
{
    /// <summary>The title screen (UI/Title.uxml): Play, Settings, Credits, Quit.</summary>
    public sealed class TitleScreen : IScreen
    {
        public VisualElement Root { get; }

        public TitleScreen()
        {
            Root = Ui.Load("Title");
            Ui.Button(Root, "play", () => App.Screens.ShowMap());
            Ui.Button(Root, "settings", () => App.Screens.OpenSettings());
            Ui.Button(Root, "credits", () => App.Screens.OpenCredits());
            var quit = Ui.Button(Root, "quit", Application.Quit);
            // Quitting makes no sense in a browser tab or on phones.
            Ui.Show(quit, !Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer);
            var version = Ui.Q<Label>(Root, "version");
            if (version != null) version.text = $"v{Application.version}";
        }

        public void Tick() { }
        public bool Back() => false;
    }

    /// <summary>Settings (UI/Settings.uxml): volumes, shake, fullscreen, touch buttons, reset progress.</summary>
    public sealed class SettingsScreen : IScreen
    {
        public VisualElement Root { get; }
        readonly VisualElement _confirm;
        readonly Button _touch;
        static readonly string[] TouchModes = { "auto", "on", "off" };

        public SettingsScreen()
        {
            Root = Ui.Load("Settings");
            var s = App.Progress.Settings;
            Bind(Ui.Q<Slider>(Root, "music"), (float)s.MusicVolume, v => s.MusicVolume = v);
            Bind(Ui.Q<Slider>(Root, "sfx"), (float)s.SfxVolume, v =>
            {
                s.SfxVolume = v;
                App.Audio.Play("click");
            });
            Bind(Ui.Q<Slider>(Root, "touch-size"), (float)s.TouchScale, v => s.TouchScale = v);
            Bind(Ui.Q<Slider>(Root, "touch-opacity"), (float)s.TouchOpacity, v => s.TouchOpacity = v);
            var shake = Ui.Q<Toggle>(Root, "shake");
            shake.value = s.ScreenShake;
            shake.RegisterValueChangedCallback(e => s.ScreenShake = e.newValue);
            var full = Ui.Q<Toggle>(Root, "fullscreen");
            full.value = s.Fullscreen;
            full.RegisterValueChangedCallback(e =>
            {
                s.Fullscreen = e.newValue;
                Boot.ApplyDisplaySettings();
            });
            Ui.Show(full.parent, Application.platform != RuntimePlatform.WebGLPlayer && !Application.isMobilePlatform);
            _touch = Ui.Button(Root, "touch", () =>
            {
                int i = System.Array.IndexOf(TouchModes, s.TouchControls);
                s.TouchControls = TouchModes[(i + 1) % TouchModes.Length];
                ShowTouchMode();
            });
            ShowTouchMode();
            var input = App.Input;
            var controls = Ui.Q<Label>(Root, "controls");
            controls.text =
                $"Keys: arrows or WASD move, {First(input.Label("Jump"))} jumps, {First(input.Label("Dash"))} dashes, {First(input.Label("Pause"))} pauses.\nGamepad: stick moves, A jumps, X dashes, Start pauses.";
            _confirm = Ui.Q<VisualElement>(Root, "confirm");
            Ui.Button(Root, "reset", () =>
            {
                Ui.Show(_confirm, true);
                Ui.FocusFirst(_confirm);
            });
            Ui.Button(Root, "confirm-yes", () =>
            {
                App.Progress.ResetProgress();
                App.Save();
                Ui.Show(_confirm, false);
                Ui.FocusFirst(Root);
            });
            Ui.Button(Root, "confirm-no", () =>
            {
                Ui.Show(_confirm, false);
                Ui.FocusFirst(Root);
            });
            Ui.Button(Root, "back", () => Back());
        }

        /// <summary>The first key of a binding list ("Space | Z | K" -> "Space").</summary>
        static string First(string keys) => keys.Split('|')[0].Trim();

        void ShowTouchMode() => _touch.text = char.ToUpper(App.Progress.Settings.TouchControls[0]) + App.Progress.Settings.TouchControls.Substring(1);

        static void Bind(Slider slider, float value, System.Action<double> set)
        {
            slider.value = value;
            slider.RegisterValueChangedCallback(e => set(e.newValue));
        }

        public bool ConfirmOpen => _confirm.resolvedStyle.display != DisplayStyle.None;

        public void Tick() { }

        public bool Back()
        {
            if (_confirm.style.display == DisplayStyle.Flex)
            {
                Ui.Show(_confirm, false);
                Ui.FocusFirst(Root);
                return true;
            }
            App.Save();
            App.Screens.Close(this);
            return true;
        }
    }

    /// <summary>Credits (UI/Credits.uxml), from Assets/Resources/Content/credits.txt.</summary>
    public sealed class CreditsScreen : IScreen
    {
        public VisualElement Root { get; }

        public CreditsScreen()
        {
            Root = Ui.Load("Credits");
            Ui.Q<Label>(Root, "credits-text").text = Resources.Load<TextAsset>("Content/credits")?.text ?? "";
            Ui.Button(Root, "back", () => Back());
        }

        public void Tick() { }

        public bool Back()
        {
            App.Screens.Close(this);
            return true;
        }
    }
}
