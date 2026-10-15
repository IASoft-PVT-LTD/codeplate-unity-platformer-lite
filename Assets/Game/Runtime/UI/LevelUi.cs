using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Runtime
{
    /// <summary>The HUD (UI/Hud.uxml): hearts, coins, gems, time, the boss's health, banners and toasts.</summary>
    public sealed class HudView
    {
        public readonly VisualElement Root;
        readonly VisualElement _hearts, _gems, _banner, _bossBar, _bossPips;
        readonly Label _coins, _time, _toast, _bannerSmall, _bannerBig, _bossName;
        readonly List<VisualElement> _heartIcons = new List<VisualElement>();
        readonly List<VisualElement> _gemIcons = new List<VisualElement>();
        float _bannerLeft, _toastLeft;

        public HudView(int maxHealth)
        {
            Root = Ui.Load("Hud");
            _hearts = Ui.Q<VisualElement>(Root, "hearts");
            _gems = Ui.Q<VisualElement>(Root, "gems");
            _coins = Ui.Q<Label>(Root, "coins");
            _time = Ui.Q<Label>(Root, "time");
            _toast = Ui.Q<Label>(Root, "toast");
            _banner = Ui.Q<VisualElement>(Root, "banner");
            _bannerSmall = Ui.Q<Label>(Root, "banner-small");
            _bannerBig = Ui.Q<Label>(Root, "banner-big");
            _bossBar = Ui.Q<VisualElement>(Root, "boss-bar");
            _bossName = Ui.Q<Label>(Root, "boss-name");
            _bossPips = Ui.Q<VisualElement>(Root, "boss-pips");
            for (int i = 0; i < maxHealth; i++)
            {
                var h = new VisualElement { pickingMode = PickingMode.Ignore };
                h.AddToClassList("icon");
                h.AddToClassList("heart");
                _hearts.Add(h);
                _heartIcons.Add(h);
            }
            for (int i = 0; i < LevelData.GemsPerLevel; i++)
            {
                var g = new VisualElement { pickingMode = PickingMode.Ignore };
                g.AddToClassList("icon");
                g.AddToClassList("gem-slot");
                _gems.Add(g);
                _gemIcons.Add(g);
            }
            _toast.style.opacity = 0;
            _banner.style.opacity = 0;
            Ui.Show(_bossBar, false);
        }

        /// <summary>Shows the level's name for a couple of seconds.</summary>
        public void Banner(string small, string big, float seconds = 2.2f)
        {
            _bannerSmall.text = small;
            _bannerBig.text = big;
            _banner.style.opacity = 1;
            _bannerLeft = seconds;
        }

        public void Toast(string text, float seconds = 1.4f)
        {
            _toast.text = text;
            _toast.style.opacity = 1;
            _toastLeft = seconds;
        }

        public string ToastText => _toast.text;

        /// <summary>Hides the banner and toast at once (the results panel opens over them).</summary>
        public void ClearMessages()
        {
            _bannerLeft = _toastLeft = 0;
            _banner.style.opacity = 0;
            _toast.style.opacity = 0;
        }

        public void Update(World w, float dt)
        {
            for (int i = 0; i < _heartIcons.Count; i++) _heartIcons[i].EnableInClassList("empty", i >= w.Player.Health);
            for (int i = 0; i < _gemIcons.Count; i++) _gemIcons[i].EnableInClassList("found", w.Gems[i]);
            _coins.text = w.Coins.ToString();
            _time.text = Ui.Time(w.Seconds <= 0 ? 0.0001 : w.Seconds);
            var boss = w.ActiveBoss;
            Ui.Show(_bossBar, boss != null);
            if (boss != null)
            {
                _bossName.text = boss.Name;
                while (_bossPips.childCount < boss.MaxHealth)
                {
                    var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                    pip.AddToClassList("boss-pip");
                    _bossPips.Add(pip);
                }
                for (int i = 0; i < _bossPips.childCount; i++) _bossPips[i].EnableInClassList("lost", i >= boss.Health);
            }
            if (_bannerLeft > 0 && (_bannerLeft -= dt) <= 0) _banner.style.opacity = 0;
            if (_toastLeft > 0 && (_toastLeft -= dt) <= 0) _toast.style.opacity = 0;
        }
    }

    /// <summary>The pause menu (UI/Pause.uxml).</summary>
    public sealed class PauseMenu : IScreen
    {
        public VisualElement Root { get; }
        readonly LevelScreen _level;

        public PauseMenu(LevelScreen level)
        {
            _level = level;
            Root = Ui.Load("Pause");
            Ui.Q<Label>(Root, "pause-level").text = level.DisplayName;
            Ui.Button(Root, "resume", () => Back());
            Ui.Button(Root, "restart", () => App.PlayLevel(_level.Number));
            Ui.Button(Root, "settings", () => App.Screens.OpenSettings());
            Ui.Button(Root, "map", () => App.Screens.ShowMap(_level.Level.World));
        }

        public void Tick()
        {
            // The pause key toggles; ignore the press that opened the menu.
            if (Time.frameCount != App.Screens.OpenedFrame && App.Input.PausePressed()) Back();
        }

        public bool Back()
        {
            if (!_level.Paused) return true;
            App.Screens.Close(this);
            _level.Paused = false;
            _level.ResumedFrame = Time.frameCount;
            return true;
        }
    }

    /// <summary>The results panel (UI/Results.uxml) after reaching the goal.</summary>
    public sealed class ResultsPanel : IScreen
    {
        public VisualElement Root { get; }
        readonly LevelScreen _level;

        public ResultsPanel(LevelScreen level, bool newBest, double bestBefore)
        {
            _level = level;
            var w = level.World;
            Root = Ui.Load("Results");
            Ui.Q<Label>(Root, "results-title").text = level.Level.HasBoss ? "Boss defeated!" : "Level clear!";
            Ui.Q<Label>(Root, "time").text = Ui.Time(w.Seconds);
            Ui.Q<Label>(Root, "par").text = Ui.Time(level.Level.ParTime);
            Ui.Q<Label>(Root, "best").text = Ui.Time(newBest ? w.Seconds : bestBefore);
            Ui.Q<Label>(Root, "coins").text = w.Coins.ToString();
            Ui.Q<Label>(Root, "deaths").text = w.Deaths.ToString();
            Ui.Show(Ui.Q<Label>(Root, "new-best"), newBest && bestBefore > 0);
            var gems = Ui.Q<VisualElement>(Root, "results-gems");
            for (int i = 0; i < LevelData.GemsPerLevel; i++)
            {
                var g = new VisualElement();
                g.AddToClassList("icon");
                g.AddToClassList("gem-slot");
                g.EnableInClassList("found", w.Gems[i]);
                gems.Add(g);
            }
            int next = App.Content.NextLevel(level.Number);
            var nextBtn = Ui.Button(Root, "next", () => App.PlayLevel(next));
            Ui.Show(nextBtn, next != 0);
            Ui.Button(Root, "retry", () => App.PlayLevel(_level.Number));
            Ui.Button(Root, "map", () => App.Screens.ShowMap(App.Content.Level(next)?.World ?? level.Level.World));
        }

        public void Tick() { }

        public bool Back()
        {
            App.Screens.ShowMap(_level.Level.World);
            return true;
        }
    }

    /// <summary>
    /// On-screen buttons (UI/Touch.uxml): left, right, jump, dash, pause. Each button tracks the
    /// pointers pressing it, so several fingers work at once. Shown on phones and tablets, or after the first touch ("auto"), or
    /// always/never from Settings. Sizes and opacity come from Settings too.
    /// </summary>
    public sealed class TouchControls
    {
        public readonly VisualElement Root;
        readonly Dictionary<VisualElement, (int bits, HashSet<int> pointers)> _buttons = new Dictionary<VisualElement, (int, HashSet<int>)>();
        bool _sawTouch;

        public TouchControls()
        {
            Root = Ui.Load("Touch");
            Add("touch-left", InputFrame.Left);
            Add("touch-right", InputFrame.Right);
            Add("touch-jump", InputFrame.Jump);
            Add("touch-dash", InputFrame.Dash);
            var pause = Ui.Q<VisualElement>(Root, "touch-pause");
            pause.RegisterCallback<PointerDownEvent>(e =>
            {
                App.Input.TouchPause = true;
                e.StopPropagation();
            });
            Apply();
        }

        void Add(string name, int bits)
        {
            var b = Ui.Q<VisualElement>(Root, name);
            var pointers = new HashSet<int>();
            _buttons[b] = (bits, pointers);
            b.RegisterCallback<PointerDownEvent>(e =>
            {
                pointers.Add(e.pointerId);
                b.CapturePointer(e.pointerId);
                b.AddToClassList("held");
                _sawTouch |= e.pointerType == "touch";
                e.StopPropagation();
            });
            void Release(int id)
            {
                pointers.Remove(id);
                if (b.HasPointerCapture(id)) b.ReleasePointer(id);
                if (pointers.Count == 0) b.RemoveFromClassList("held");
            }
            b.RegisterCallback<PointerUpEvent>(e => Release(e.pointerId));
            b.RegisterCallback<PointerCancelEvent>(e => Release(e.pointerId));
            b.RegisterCallback<PointerCaptureOutEvent>(e => Release(e.pointerId));
        }

        /// <summary>The element for a button: "left", "right", "jump", "dash" or "pause".</summary>
        public VisualElement Button(string name) => Root.Q<VisualElement>("touch-" + name);

        /// <summary>Whether the buttons show, from Settings and the device.</summary>
        public bool Visible
        {
            get
            {
                string mode = App.Progress.Settings.TouchControls;
                if (mode == "auto" && App.Config.touchControls != "auto") mode = App.Config.touchControls == "always" ? "on" : "off";
                if (mode == "on") return true;
                if (mode == "off") return false;
                // Desktop browsers often report a touchscreen they don't have, so "auto" waits for a
                // real touch (or a phone/tablet) before showing the buttons.
                var ts = UnityEngine.InputSystem.Touchscreen.current;
                if (ts != null && ts.primaryTouch.press.isPressed) _sawTouch = true;
                return _sawTouch || Application.isMobilePlatform;
            }
        }

        /// <summary>Applies Settings (size, opacity, visibility). Called every frame by the level.</summary>
        public void Apply()
        {
            var s = App.Progress.Settings;
            bool show = Visible;
            Ui.Show(Root, show);
            foreach (var b in _buttons.Keys)
            {
                b.style.opacity = (float)s.TouchOpacity;
                b.style.scale = new Scale(new Vector3((float)s.TouchScale, (float)s.TouchScale, 1));
            }
            Ui.Q<VisualElement>(Root, "touch-pause").style.opacity = (float)s.TouchOpacity;
        }

        /// <summary>The InputFrame bits the held buttons add this tick.</summary>
        public int Bits
        {
            get
            {
                int bits = 0;
                foreach (var kv in _buttons)
                    if (kv.Value.pointers.Count > 0) bits |= kv.Value.bits;
                return bits;
            }
        }

        /// <summary>Presses or releases a button from code, exactly as a finger would (tests use it).</summary>
        public void Simulate(string name, bool down, int pointerId = 1)
        {
            var b = Button(name);
            if (name == "pause")
            {
                if (down) App.Input.TouchPause = true;
                return;
            }
            var (_, pointers) = _buttons[b];
            if (down) pointers.Add(pointerId);
            else pointers.Remove(pointerId);
            b.EnableInClassList("held", pointers.Count > 0);
        }
    }
}
