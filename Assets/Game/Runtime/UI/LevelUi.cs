using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Runtime
{
    /// <summary>
    /// The HUD (UI/Hud.uxml): hearts, coins and gems along the top, the level clock big in the
    /// top middle (a countdown when the level has a time limit: amber, then red and pulsing near
    /// time up; otherwise the time taken), the boss's health, banners, toasts and the "Out of
    /// hearts" card.
    /// </summary>
    public sealed class HudView
    {
        public readonly VisualElement Root;
        readonly VisualElement _hearts, _gems, _banner, _bossBar, _bossPips, _clock, _outOfHearts;
        readonly Label _coins, _time, _toast, _bannerSmall, _bannerBig, _bossName, _outOfHeartsNote;
        readonly List<VisualElement> _heartIcons = new List<VisualElement>();
        readonly List<VisualElement> _gemIcons = new List<VisualElement>();
        float _bannerLeft, _toastLeft, _pulse;
        int _lastShown = -1;

        /// <summary>The clock's warning stage as shown (tests read it).</summary>
        public LevelClock.Stage ClockStage { get; private set; }
        /// <summary>The clock element (tests check where it is and its colour).</summary>
        public VisualElement Clock => _clock;
        public Label ClockLabel => _time;
        /// <summary>True while the "Out of hearts" card shows.</summary>
        public bool OutOfHeartsShowing => _outOfHearts.ClassListContains("on");

        public HudView(int maxHealth)
        {
            Root = Ui.Load("Hud");
            _hearts = Ui.Q<VisualElement>(Root, "hearts");
            _gems = Ui.Q<VisualElement>(Root, "gems");
            _coins = Ui.Q<Label>(Root, "coins");
            _time = Ui.Q<Label>(Root, "time");
            _clock = Ui.Q<VisualElement>(Root, "clock");
            _outOfHearts = Ui.Q<VisualElement>(Root, "out-of-hearts");
            _outOfHeartsNote = Ui.Q<Label>(Root, "out-of-hearts-note");
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

        /// <summary>Shows the "Out of hearts" card (the level then starts again; LevelScreen times it), with an optional note ("The ad was closed early").</summary>
        public void ShowOutOfHearts(string note = null)
        {
            ClearMessages();
            _outOfHeartsNote.text = note ?? "";
            Ui.Show(_outOfHeartsNote, !string.IsNullOrEmpty(note));
            _outOfHearts.AddToClassList("on");
        }

        public string OutOfHeartsNote => _outOfHeartsNote.text;

        /// <summary>Hides the banner and toast at once (the results panel opens over them).</summary>
        public void ClearMessages()
        {
            _bannerLeft = _toastLeft = 0;
            _banner.style.opacity = 0;
            _toast.style.opacity = 0;
        }

        public void Update(World w, float dt)
        {
            Ui.FitSafeArea(Ui.Q<VisualElement>(Root, "hud"));
            for (int i = 0; i < _heartIcons.Count; i++) _heartIcons[i].EnableInClassList("empty", i >= w.Player.Health);
            for (int i = 0; i < _gemIcons.Count; i++) _gemIcons[i].EnableInClassList("found", w.Gems[i]);
            _coins.text = w.Coins.ToString();
            if (w.TimerOn)
            {
                // Time left in whole seconds: amber from timeWarning, red from timeDanger, and in the
                // red each new second gives the clock a little pulse.
                int shown = LevelClock.Shown(w.TimeLeft);
                _time.text = LevelClock.Text(w.TimeLeft);
                ClockStage = LevelClock.StageFor(w.TimeLeft, w.Config);
                if (ClockStage == LevelClock.Stage.Danger && shown != _lastShown && w.Player.Current != Player.State.Dead) _pulse = 1;
                _lastShown = shown;
            }
            else
            {
                _time.text = Ui.Time(w.Seconds <= 0 ? 0.0001 : w.Seconds);
                ClockStage = LevelClock.Stage.Calm;
            }
            _clock.EnableInClassList("warn", ClockStage == LevelClock.Stage.Warning);
            _clock.EnableInClassList("danger", ClockStage == LevelClock.Stage.Danger);
            _pulse = Mathf.Max(0, _pulse - dt / 0.35f);
            float grow = 1 + 0.22f * _pulse * _pulse;
            _clock.style.scale = new Scale(new Vector3(grow, grow, 1));
            var boss = w.ActiveBoss;
            Ui.Show(_bossBar, boss != null);
            if (boss != null)
            {
                _bossName.text = Loc.T(boss.Name);
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
            Ui.Q<Label>(Root, "results-title").text = level.Level.HasBoss ? Loc.T("Boss defeated!") : Loc.T("Level clear!");
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
    /// On-screen controls (UI/Touch.uxml): a d-pad, jump, dash and pause. The d-pad is one
    /// touch surface: the finger's position relative to its centre picks the
    /// direction (Game.Core Dpad, with a dead zone in the middle), sliding the thumb changes
    /// direction without lifting, and it follows that one finger by its pointer id, so another
    /// finger can hold jump at the same time. The arm being held lights up; arms the game doesn't
    /// use (config.json "dpadAxes") are dimmed. Jump and dash each track the pointers pressing them. Where they sit
    /// comes from Game.Core TouchLayout: every bottom edge at least 15% of the screen height up,
    /// inside the safe area; the d-pad about a quarter of the screen's width, taking touches a
    /// little beyond its art. Shown on phones and tablets, or after the first touch ("auto"), or
    /// always/never from Settings. Size and opacity come from Settings too.
    /// </summary>
    public sealed class TouchControls
    {
        /// <summary>The d-pad's directions, from config.json "dpadAxes" (default "x_down": left, right and down; down + jump drops through a ledge, down falls faster).</summary>
        public readonly DpadAxes Axes;

        public readonly VisualElement Root;
        readonly Dictionary<VisualElement, (int bits, HashSet<int> pointers)> _buttons = new Dictionary<VisualElement, (int, HashSet<int>)>();
        readonly VisualElement _dpad, _dpadArt;
        readonly List<VisualElement> _pieces;
        readonly Dictionary<int, VisualElement> _arms = new Dictionary<int, VisualElement>();
        readonly (int bits, VisualElement el)[] _corners;
        bool _sawTouch;
        int _dpadPointer = -1;

        /// <summary>The direction bits the d-pad holds now.</summary>
        public int DpadBits { get; private set; }
        /// <summary>Where the controls are, in UI units from the screen's top-left (tests check it).</summary>
        public TouchLayout Layout { get; private set; }

        public TouchControls()
        {
            Root = Ui.Load("Touch");
            Axes = Dpad.Parse(App.Config.dpadAxes, out _);
            Add("touch-jump", InputFrame.Jump);
            Add("touch-dash", InputFrame.Dash);
            _dpad = Ui.Q<VisualElement>(Root, "touch-dpad");
            _dpadArt = Ui.Q<VisualElement>(Root, "dpad-art");
            _arms[InputFrame.Up] = Ui.Q<VisualElement>(Root, "dpad-up");
            _arms[InputFrame.Left] = Ui.Q<VisualElement>(Root, "dpad-left");
            _arms[InputFrame.Right] = Ui.Q<VisualElement>(Root, "dpad-right");
            _arms[InputFrame.Down] = Ui.Q<VisualElement>(Root, "dpad-down");
            _corners = new[]
            {
                (InputFrame.Up | InputFrame.Left, Ui.Q<VisualElement>(Root, "dpad-ul")),
                (InputFrame.Up | InputFrame.Right, Ui.Q<VisualElement>(Root, "dpad-ur")),
                (InputFrame.Down | InputFrame.Left, Ui.Q<VisualElement>(Root, "dpad-dl")),
                (InputFrame.Down | InputFrame.Right, Ui.Q<VisualElement>(Root, "dpad-dr")),
            };
            _pieces = _dpad.Query<VisualElement>(className: "dpad-piece").ToList();
            int gives = Dpad.Gives(Axes);
            foreach (var kv in _arms) kv.Value.EnableInClassList("unused", (gives & kv.Key) == 0);
            foreach (var (bits, el) in _corners) el.EnableInClassList("unused", (gives & bits) != bits);

            _dpad.RegisterCallback<PointerDownEvent>(e =>
            {
                _sawTouch |= e.pointerType == "touch";
                DpadDown(e.pointerId, e.position);
                e.StopPropagation();
            });
            _dpad.RegisterCallback<PointerMoveEvent>(e => DpadMove(e.pointerId, e.position));
            _dpad.RegisterCallback<PointerUpEvent>(e => DpadUp(e.pointerId));
            _dpad.RegisterCallback<PointerCancelEvent>(e => DpadUp(e.pointerId));
            _dpad.RegisterCallback<PointerCaptureOutEvent>(e => DpadUp(e.pointerId));

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

        // ------------------------------------------------------------------ the d-pad

        /// <summary>A finger lands on the d-pad (panel position). Another finger landing on it while one steers is ignored.</summary>
        public void DpadDown(int pointerId, Vector2 panelPos)
        {
            if (_dpadPointer >= 0) return;
            _dpadPointer = pointerId;
            if (_dpad.panel != null && !_dpad.HasPointerCapture(pointerId)) _dpad.CapturePointer(pointerId);
            Aim(panelPos);
        }

        /// <summary>The steering finger moved (it stays captured even when it slides off the pad).</summary>
        public void DpadMove(int pointerId, Vector2 panelPos)
        {
            if (pointerId == _dpadPointer) Aim(panelPos);
        }

        /// <summary>The steering finger lifted (or the system took it away).</summary>
        public void DpadUp(int pointerId)
        {
            if (pointerId != _dpadPointer) return;
            _dpadPointer = -1;
            if (_dpad.HasPointerCapture(pointerId)) _dpad.ReleasePointer(pointerId);
            SetDirections(0);
        }

        void Aim(Vector2 panelPos)
        {
            // Directions are measured from the drawn pad's centre, in halves of the drawn pad, so a
            // finger on the touch margin around the art reads as the arm next to it.
            var box = _dpadArt.worldBound;
            float half = box.width / 2;
            if (half <= 0) return;
            double dx = (panelPos.x - box.center.x) / half;
            double dy = (panelPos.y - box.center.y) / half;
            SetDirections(Dpad.Directions(dx, dy, Axes));
        }

        void SetDirections(int bits)
        {
            DpadBits = bits;
            foreach (var kv in _arms) kv.Value.EnableInClassList("lit", (bits & kv.Key) != 0);
            foreach (var (b, el) in _corners) el.EnableInClassList("lit", (bits & b) == b);
        }

        /// <summary>The element for a control: "dpad" (its touch area), "jump", "dash" or "pause".</summary>
        public VisualElement Button(string name) => Root.Q<VisualElement>("touch-" + name);

        /// <summary>The d-pad as drawn (inside its slightly larger touch area).</summary>
        public VisualElement DpadArt => _dpadArt;

        /// <summary>A d-pad piece by name: "up", "left", "right", "down", "hub", "ul", "ur", "dl", "dr".</summary>
        public VisualElement DpadPiece(string name) => Root.Q<VisualElement>("dpad-" + name);

        /// <summary>Whether the controls show, from Settings and the device.</summary>
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

        /// <summary>Applies Settings (size, opacity, visibility) and places the controls. Called every frame by the level.</summary>
        public void Apply()
        {
            var s = App.Progress.Settings;
            bool show = Visible;
            Ui.Show(Root, show);
            var inset = Ui.FitSafeArea(Ui.Q<VisualElement>(Root, "touch"));
            var screen = App.Screens != null ? App.Screens.Root.layout : Rect.zero;
            if (screen.width > 0 && screen.height > 0)
            {
                Layout = TouchLayout.For(screen.width, screen.height, inset.x, inset.y, inset.z, inset.w, s.TouchScale);
                Place(_dpad, Layout.DpadTouch, screen, inset, fromLeft: true);
                _dpadArt.style.left = (float)(Layout.Dpad.Left - Layout.DpadTouch.Left);
                _dpadArt.style.top = (float)(Layout.Dpad.Top - Layout.DpadTouch.Top);
                _dpadArt.style.width = (float)Layout.Dpad.Width;
                _dpadArt.style.height = (float)Layout.Dpad.Height;
                Place(Button("dash"), Layout.Dash, screen, inset, fromLeft: false);
                Place(Button("jump"), Layout.Jump, screen, inset, fromLeft: false);
                // A third of the pad each, set here rather than in percent so the cells land on whole units.
                float cell = (float)Layout.Dpad.Width / 3f;
                foreach (var p in _pieces)
                {
                    p.style.width = cell;
                    p.style.height = cell;
                }
                SetCell("up", 1, 0, cell);
                SetCell("left", 0, 1, cell);
                SetCell("right", 2, 1, cell);
                SetCell("down", 1, 2, cell);
                SetCell("hub", 1, 1, cell);
                SetCell("ul", 0, 0, cell);
                SetCell("ur", 2, 0, cell);
                SetCell("dl", 0, 2, cell);
                SetCell("dr", 2, 2, cell);
            }
            _dpadArt.style.opacity = (float)s.TouchOpacity;
            foreach (var b in _buttons.Keys) b.style.opacity = (float)s.TouchOpacity;
            Ui.Q<VisualElement>(Root, "touch-pause").style.opacity = (float)s.TouchOpacity;
            if (!show && _dpadPointer >= 0) DpadUp(_dpadPointer);
        }

        void SetCell(string name, int col, int row, float cell)
        {
            var p = DpadPiece(name);
            p.style.left = col * cell;
            p.style.top = row * cell;
        }

        /// <summary>Puts a control at a TouchLayout box (screen units) inside the safe-area layer (insets: left, right, top, bottom).</summary>
        static void Place(VisualElement e, UiBox box, Rect screen, Vector4 inset, bool fromLeft)
        {
            e.style.width = (float)box.Width;
            e.style.height = (float)box.Height;
            e.style.bottom = (float)(screen.height - box.Bottom - inset.w);
            e.style.top = StyleKeyword.Auto;
            if (fromLeft)
            {
                e.style.left = (float)(box.Left - inset.x);
                e.style.right = StyleKeyword.Auto;
            }
            else
            {
                e.style.right = (float)(screen.width - box.Right - inset.y);
                e.style.left = StyleKeyword.Auto;
            }
        }

        /// <summary>The InputFrame bits the controls add this tick: the d-pad's directions plus held buttons.</summary>
        public int Bits
        {
            get
            {
                int bits = DpadBits;
                foreach (var kv in _buttons)
                    if (kv.Value.pointers.Count > 0) bits |= kv.Value.bits;
                return bits;
            }
        }

        /// <summary>
        /// Presses or releases a control from code, exactly as a finger would (tests use it):
        /// "jump", "dash", "pause"; or "left", "right", "down", "up" as a finger on the middle of
        /// that d-pad arm (it goes through the same direction maths as a real finger).
        /// </summary>
        public void Simulate(string name, bool down, int pointerId = 1)
        {
            if (name == "pause")
            {
                if (down) App.Input.TouchPause = true;
                return;
            }
            if (name == "left" || name == "right" || name == "up" || name == "down")
            {
                if (down) DpadDown(pointerId, DpadPiece(name).worldBound.center);
                else DpadUp(pointerId);
                return;
            }
            var b = Button(name);
            var (_, pointers) = _buttons[b];
            if (down) pointers.Add(pointerId);
            else pointers.Remove(pointerId);
            b.EnableInClassList("held", pointers.Count > 0);
        }
    }
}
