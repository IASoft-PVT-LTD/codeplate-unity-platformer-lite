using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Runtime
{
    /// <summary>
    /// Owns the UI Toolkit panel and moves between screens: title → world map → level → results.
    /// Menus sit on a stack of overlays; the top one gets Esc/back and Tick(). The panel is scaled
    /// by whole numbers so UI pixels match the game's pixels.
    /// </summary>
    public sealed class ScreenRouter : MonoBehaviour
    {
        public UIDocument Document { get; private set; }
        public PanelSettings Panel { get; private set; }
        public VisualElement Root { get; private set; }
        /// <summary>Full-screen menus (title, map) go here; the HUD and touch buttons above; overlays on top.</summary>
        public VisualElement MenuLayer { get; private set; }
        public VisualElement HudLayer { get; private set; }
        public VisualElement TouchLayer { get; private set; }
        public VisualElement OverlayLayer { get; private set; }

        readonly List<IScreen> _stack = new List<IScreen>();
        IScreen _menu;
        MenuBackdrop _menuBackdrop;
        int _viewW = 480, _viewH = 270;

        /// <summary>The top overlay or menu screen (tests read it).</summary>
        public IScreen Current => _stack.Count > 0 ? _stack[_stack.Count - 1] : _menu;
        /// <summary>True while an overlay (pause, results, settings...) is open over the game.</summary>
        public bool OverlayOpen => _stack.Count > 0;

        internal void Build(int viewW, int viewH)
        {
            _viewW = viewW;
            _viewH = viewH;
            Panel = ScriptableObject.CreateInstance<PanelSettings>();
            Panel.name = "Game UI";
            Panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/Theme");
            Panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            Panel.scale = Zoom();
            Panel.clearColor = false;
            // UI images are sprites at 16 px per unit (ImportSettings.cs): draw them 1:1 in game pixels.
            Panel.referenceSpritePixelsPerUnit = App.Art.PixelsPerTile;
            Document = gameObject.AddComponent<UIDocument>();
            Document.panelSettings = Panel;
            Root = Document.rootVisualElement;
            Root.AddToClassList("layer");
            Root.pickingMode = PickingMode.Ignore;
            MenuLayer = Layer("menus");
            HudLayer = Layer("hud");
            TouchLayer = Layer("touch");
            OverlayLayer = Layer("overlays");
            Ui.ApplyFont(Root);
        }

        VisualElement Layer(string name)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            e.AddToClassList("layer");
            Root.Add(e);
            return e;
        }

        /// <summary>Screenshots fix the scale (ShotDirector); 0 = fit the screen.</summary>
        public static int ZoomOverride;

        /// <summary>The whole-number scale that fits the reference resolution on screen.</summary>
        public int Zoom() => ZoomOverride > 0 ? ZoomOverride : Game.Core.TouchLayout.Zoom(Screen.width, Screen.height, _viewW, _viewH);

        void Update()
        {
            if (Panel != null && Panel.scale != Zoom()) Panel.scale = Zoom();
            var top = Current;
            if (top == null) return;
            top.Tick();
            if ((App.Level == null || OverlayOpen) && Time.frameCount != OpenedFrame && App.Input.BackPressed()) top.Back();
        }

        // ------------------------------------------------------------------ menus

        void SetMenu(IScreen screen, int backdropWorld)
        {
            CloseAll();
            if (_menu != null) _menu.Root.RemoveFromHierarchy();
            _menu = screen;
            if (screen != null)
            {
                MenuLayer.Add(screen.Root);
                Ui.FocusFirst(screen.Root);
            }
            if (backdropWorld > 0)
            {
                if (_menuBackdrop == null) _menuBackdrop = MenuBackdrop.Create(transform);
                _menuBackdrop.Show(backdropWorld);
            }
            else if (_menuBackdrop != null)
            {
                Destroy(_menuBackdrop.gameObject);
                _menuBackdrop = null;
            }
        }

        public void ShowTitle()
        {
            LeaveLevel();
            SetMenu(new TitleScreen(), 1);
            App.Audio.PlayMusic("music_title");
        }

        /// <summary>The world map, on the given world's page (0 = the newest unlocked world).</summary>
        public void ShowMap(int world = 0)
        {
            LeaveLevel();
            var map = new WorldMapScreen(world);
            SetMenu(map, map.World);
            App.Audio.PlayMusic("music_title");
        }

        public void ShowLevel(int number)
        {
            LeaveLevel();
            SetMenu(null, 0);
            var go = new GameObject("Level");
            go.transform.SetParent(transform.parent);
            var level = go.AddComponent<LevelScreen>();
            level.Begin(number);
        }

        void LeaveLevel()
        {
            if (App.Level == null) return;
            var go = App.Level.gameObject;
            App.Level.End();
            Destroy(go);
            App.Level = null;
            HudLayer.Clear();
            TouchLayer.Clear();
        }

        // ------------------------------------------------------------------ overlays

        /// <summary>The frame the last overlay opened on: the key press that opened it isn't also a "back".</summary>
        public int OpenedFrame { get; private set; } = -1;

        public void Open(IScreen overlay)
        {
            OpenedFrame = Time.frameCount;
            _stack.Add(overlay);
            OverlayLayer.Add(overlay.Root);
            Ui.FocusFirst(overlay.Root);
        }

        public void Close(IScreen overlay)
        {
            _stack.Remove(overlay);
            overlay.Root.RemoveFromHierarchy();
            if (Current != null) Ui.FocusFirst(Current.Root);
        }

        public void CloseAll()
        {
            foreach (var s in _stack) s.Root.RemoveFromHierarchy();
            _stack.Clear();
        }

        public void OpenSettings() => Open(new SettingsScreen());

        /// <summary>
        /// Switches the language (Settings' Language button), saves it, and builds the screens on
        /// show again in it: the title or map underneath, then each open overlay. A level's HUD
        /// keeps its labels until the next level starts; its messages switch at once.
        /// </summary>
        public void ChangeLanguage(string code)
        {
            App.Progress.Settings.Language = Game.Core.Loc.Use(code);
            App.Save();
            var open = new List<IScreen>(_stack);
            if (_menu is TitleScreen) SetMenu(new TitleScreen(), 1);
            else if (_menu is WorldMapScreen map) SetMenu(new WorldMapScreen(map.World), map.World);
            else CloseAll();
            foreach (var s in open)
            {
                if (s is SettingsScreen) OpenSettings();
                else if (s is CreditsScreen) OpenCredits();
                else if (s is PauseMenu && App.Level != null) Open(new PauseMenu(App.Level));
                else Open(s);
            }
        }
        public void OpenCredits() => Open(new CreditsScreen());
    }
}
