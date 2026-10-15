using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests
{
    /// <summary>
    /// The real game: the Boot scene, its screens, keyboard and touch input, pause, finishing a
    /// level through the scene, settings and saving. Uses a scratch save file, never yours.
    /// InputTestFixture gives each test fresh virtual devices.
    /// </summary>
    public partial class GameTests : InputTestFixture
    {
        Keyboard _kb;
        string _save;

        public override void Setup()
        {
            base.Setup();
            _kb = InputSystem.AddDevice<Keyboard>();
            _save = Path.Combine(Application.temporaryCachePath, "playmode-test-progress.json");
            if (File.Exists(_save)) File.Delete(_save);
            SaveStore.PathOverride = _save;
        }

        public override void TearDown()
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            SaveStore.PathOverride = null;
            TearDownParts();
            base.TearDown();
        }

        /// <summary>Other parts of this partial class (optional features' tests) undo what they set up here.</summary>
        partial void TearDownParts();

        static IEnumerator Boot()
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            Assert.IsTrue(App.Ready, "Boot should set everything up");
            Assert.IsEmpty(App.Content.Errors, string.Join("\n", App.Content.Errors));
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Seconds(float s)
        {
            float end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>Clicks a UI Toolkit button the way keyboard/gamepad "submit" does.</summary>
        static void Click(Button b)
        {
            Assert.IsNotNull(b);
            Assert.IsTrue(b.enabledInHierarchy, $"button {b.name} is disabled");
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = b;
                b.SendEvent(e);
            }
        }

        static Button FindButton(string name) => App.Screens.Root.Q<Button>(name);

        static IEnumerator StartLevel(int n)
        {
            App.PlayLevel(n);
            yield return Frames(3);
            Assert.IsNotNull(App.Level);
            Assert.AreEqual(n, App.Level.Number);
        }

        /// <summary>
        /// Starts level n driven by replays/level_NN.replay from its very first tick. The replay is
        /// hooked up before any frame passes: an idle tick before it starts would shift the timing
        /// of everything after it (enemies and bosses don't wait).
        /// </summary>
        static List<int> StartReplay(int n)
        {
            var inputs = Replay.Parse(File.ReadAllText(ProjectPaths.Replay(n)), new List<string>());
            App.PlayLevel(n); // builds the level at once; FixedUpdate hasn't ticked it yet
            Assert.IsNotNull(App.Level);
            Assert.AreEqual(n, App.Level.Number);
            Assert.AreEqual(0, App.Level.World.Tick, "the replay must drive the first tick");
            App.Level.InputOverride = t => t < inputs.Count ? inputs[t] : 0;
            return inputs;
        }

        [UnityTest]
        public IEnumerator BootShowsTheTitleWithMusic()
        {
            yield return Boot();
            Assert.IsInstanceOf<TitleScreen>(App.Screens.Current);
            Assert.AreEqual("music_title", App.Audio.CurrentMusic);
            Assert.IsNotNull(FindButton("play"));
            Assert.IsNotNull(Resources.Load<Font>("Art/ui/font"), "the pixel font loads");
        }

        [UnityTest]
        public IEnumerator PlayOpensTheMapAndANodeStartsTheLevel()
        {
            yield return Boot();
            Click(FindButton("play"));
            yield return Frames(2);
            var map = App.Screens.Current as WorldMapScreen;
            Assert.IsNotNull(map, "Play opens the world map");
            Assert.IsTrue(map.Nodes[0].enabledSelf, "level 1 is open");
            Assert.IsFalse(map.Nodes[1].enabledSelf, "level 2 is locked on a fresh save");
            Click(map.Nodes[0]);
            yield return Frames(3);
            Assert.IsNotNull(App.Level);
            Assert.AreEqual(1, App.Level.Number);
            Assert.AreEqual(App.Content.World(1).Music, App.Audio.CurrentMusic);
        }

        [UnityTest]
        public IEnumerator KeyboardRunsAndJumps()
        {
            yield return Boot();
            yield return StartLevel(1);
            var p = App.Level.World.Player;
            double x0 = p.Body.Pos.X;
            Press(_kb.rightArrowKey);
            yield return Seconds(0.4f);
            Assert.Greater(p.Body.Pos.X, x0 + 1.0, "right arrow runs right");
            Release(_kb.rightArrowKey);
            yield return Seconds(0.3f);
            double y0 = p.Body.Pos.Y;
            Press(_kb.spaceKey);
            double top = y0;
            float end = Time.realtimeSinceStartup + 0.35f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                top = Math.Min(top, p.Body.Pos.Y);
            }
            Release(_kb.spaceKey);
            Assert.Less(top, y0 - 0.5, "space jumps");
        }

        [UnityTest]
        public IEnumerator EscapePausesAndResumes()
        {
            yield return Boot();
            yield return StartLevel(1);
            yield return Frames(5);
            PressAndRelease(_kb.escapeKey);
            yield return Frames(3);
            Assert.IsInstanceOf<PauseMenu>(App.Screens.Current, "Esc opens the pause menu");
            int tick = App.Level.World.Tick;
            yield return Seconds(0.3f);
            Assert.AreEqual(tick, App.Level.World.Tick, "the simulation stops while paused");
            PressAndRelease(_kb.escapeKey);
            yield return Frames(3);
            Assert.IsFalse(App.Screens.OverlayOpen, "Esc again resumes");
            yield return Seconds(0.2f);
            Assert.Greater(App.Level.World.Tick, tick, "and the simulation runs again");
        }

        [UnityTest]
        public IEnumerator FinishingLevelOneShowsResultsSavesAndUnlocksLevelTwo()
        {
            yield return Boot();
            var inputs = StartReplay(1);
            App.Level.StepTicks(inputs.Count + 60);
            Assert.IsTrue(App.Level.World.Finished, "the replay finishes the level through the scene");
            yield return Seconds(1.0f);
            Assert.IsInstanceOf<ResultsPanel>(App.Screens.Current);
            Assert.IsTrue(App.Progress.IsUnlocked(2));
            Assert.IsTrue(File.Exists(_save), "progress was saved");
            var saved = Progress.FromJson(File.ReadAllText(_save), out _);
            Assert.IsTrue(saved.Level(1).Completed);
            Click(FindButton("next"));
            yield return Frames(3);
            Assert.AreEqual(2, App.Level.Number, "Next starts level 2");
        }

        [UnityTest]
        public IEnumerator TouchButtonsDriveThePlayer()
        {
            yield return Boot();
            App.Progress.Settings.TouchControls = "on";
            yield return StartLevel(1);
            yield return Frames(2);
            var touch = App.Level.Touch;
            Assert.IsTrue(touch.Visible);
            Assert.AreEqual(DisplayStyle.Flex, touch.Root.resolvedStyle.display);
            var screen = App.Screens.Root.worldBound;
            foreach (var name in new[] { "dpad", "jump", "dash", "pause" })
            {
                var r = touch.Button(name).worldBound;
                Assert.IsTrue(r.width > 10 && screen.Contains(r.center), $"touch control {name} is on screen ({r})");
            }
            Assert.IsNull(touch.Button("left"), "the left/right buttons are gone: the d-pad replaced them");
            var p = App.Level.World.Player;
            double x0 = p.Body.Pos.X;
            touch.Simulate("right", true);
            yield return Seconds(0.4f);
            Assert.Greater(p.Body.Pos.X, x0 + 1.0, "holding the d-pad's right arm runs");
            touch.Simulate("jump", true, 2);
            yield return Seconds(0.15f);
            Assert.AreNotEqual(Player.State.Ground, p.Current, "a second finger jumps while running");
            touch.Simulate("jump", false, 2);
            touch.Simulate("right", false);
            touch.Simulate("pause", true);
            yield return Frames(3);
            Assert.IsInstanceOf<PauseMenu>(App.Screens.Current, "the pause button pauses");
            App.Progress.Settings.TouchControls = "off";
            yield return Frames(2);
            Assert.IsFalse(touch.Visible);
        }

        [UnityTest]
        public IEnumerator TheDpadIsOneSurfaceThatFollowsItsFinger()
        {
            yield return Boot();
            App.Progress.Settings.TouchControls = "on";
            yield return StartLevel(1);
            yield return Frames(2);
            var touch = App.Level.Touch;
            Vector2 At(string piece) => touch.DpadPiece(piece).worldBound.center;
            var pad = touch.DpadArt.worldBound;
            var area = touch.Button("dpad").worldBound;
            Assert.IsTrue(area.Contains(pad.min) && area.Contains(pad.max - new Vector2(0.01f, 0.01f)), $"the touch area {area} holds the drawn pad {pad}");
            Assert.Greater(area.width * area.height, 1.2f * pad.width * pad.height, "and is a bit bigger than it");
            Assert.GreaterOrEqual(pad.width, 0.2f * App.Screens.Root.worldBound.width - 3, "the d-pad is about a quarter of the screen's width");

            // One finger: down on the left arm, then slide right without lifting.
            touch.DpadDown(1, At("left"));
            Assert.AreEqual(InputFrame.Left, touch.DpadBits);
            Assert.IsTrue(touch.DpadPiece("left").ClassListContains("lit"), "the held arm lights up");
            touch.DpadMove(1, pad.center);
            Assert.AreEqual(0, touch.DpadBits, "the middle is a dead zone");
            touch.DpadMove(1, At("right"));
            Assert.AreEqual(InputFrame.Right, touch.DpadBits, "sliding across changes direction");
            Assert.IsFalse(touch.DpadPiece("left").ClassListContains("lit"));
            touch.DpadMove(1, At("dr"));
            Assert.AreEqual(InputFrame.Right | InputFrame.Down, touch.DpadBits, "the corner is the diagonal");
            Assert.IsTrue(touch.DpadPiece("dr").ClassListContains("lit"));
            touch.DpadMove(1, new Vector2(pad.xMax + 60, pad.center.y));
            Assert.AreEqual(InputFrame.Right, touch.DpadBits, "a thumb slid off the pad still steers");
            touch.DpadUp(1);
            touch.DpadDown(1, new Vector2(area.xMin + 1, pad.center.y));
            Assert.AreEqual(InputFrame.Left, touch.DpadBits, "a finger on the margin left of the art is left");
            touch.DpadMove(1, new Vector2(pad.xMax + 60, pad.center.y));

            // Another finger holds jump at the same time; a third finger on the pad doesn't take over.
            touch.Simulate("jump", true, 2);
            touch.DpadDown(3, At("left"));
            Assert.AreEqual(InputFrame.Right, touch.DpadBits, "the pad keeps following its own finger");
            Assert.AreEqual(InputFrame.Right | InputFrame.Jump, touch.Bits);
            touch.DpadMove(3, At("left"));
            Assert.AreEqual(InputFrame.Right, touch.DpadBits);
            touch.DpadUp(3);
            Assert.AreEqual(InputFrame.Right, touch.DpadBits, "lifting another finger changes nothing");
            touch.DpadUp(1);
            Assert.AreEqual(0, touch.DpadBits);
            Assert.AreEqual(InputFrame.Jump, touch.Bits, "jump is still held");
            touch.Simulate("jump", false, 2);

            // The default axes ("x_down"): up has no use in this game, dimmed and never pressed;
            // down is wired (drops through ledges).
            touch.DpadDown(4, At("up"));
            Assert.AreEqual(0, touch.DpadBits & InputFrame.Up);
            touch.DpadUp(4);
            touch.DpadDown(4, At("down"));
            Assert.AreEqual(InputFrame.Down, touch.DpadBits);
            touch.DpadUp(4);
            yield return Frames(2);
            Assert.AreEqual(0.3f, touch.DpadPiece("up").resolvedStyle.opacity, 0.01f, "the up arm is dimmed");
            Assert.AreEqual(1f, touch.DpadPiece("down").resolvedStyle.opacity, 0.01f);
            Assert.AreEqual(DisplayStyle.None, touch.DpadPiece("ul").resolvedStyle.display, "no up diagonals");
            Assert.AreEqual(DisplayStyle.Flex, touch.DpadPiece("dl").resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator TouchControlsSitAtLeastFifteenPercentUp()
        {
            yield return Boot();
            App.Progress.Settings.TouchControls = "on";
            yield return StartLevel(1);
            foreach (double size in new[] { 1.0, 1.5, 0.75 })
            {
                App.Progress.Settings.TouchScale = size;
                yield return Frames(3);
                var screen = App.Screens.Root.worldBound;
                foreach (var name in new[] { "dpad", "dpad-art", "jump", "dash" })
                {
                    var r = name == "dpad-art" ? App.Level.Touch.DpadArt.worldBound : App.Level.Touch.Button(name).worldBound;
                    float above = screen.yMax - r.yMax;
                    Assert.GreaterOrEqual(above, 0.15f * screen.height - 0.5f, $"{name} at size {size}: bottom edge {above} units up of {screen.height}");
                    Assert.GreaterOrEqual(r.xMin, screen.xMin, name);
                    Assert.LessOrEqual(r.xMax, screen.xMax, name);
                }
                App.Level.Touch.Simulate("jump", true, 9);
                yield return Frames(2);
                Assert.GreaterOrEqual(screen.yMax - App.Level.Touch.Button("jump").worldBound.yMax, 0.15f * screen.height - 0.5f, "a pressed button shrinks, never drops");
                App.Level.Touch.Simulate("jump", false, 9);
            }
        }

        [UnityTest]
        public IEnumerator TheClockIsBigInTheTopMiddleAndTurnsAmberThenRed()
        {
            yield return Boot();
            yield return StartLevel(1);
            yield return Frames(3);
            var hud = App.Level.Hud;
            var w = App.Level.World;
            var screen = App.Screens.Root.worldBound;
            var clock = hud.Clock.worldBound;
            Assert.AreEqual(screen.center.x, clock.center.x, 2f, "centred");
            Assert.Less(clock.yMin, 8f, "at the top");
            Assert.Greater(hud.ClockLabel.resolvedStyle.fontSize, App.Screens.Root.Q<Label>("coins").resolvedStyle.fontSize, "bigger than the HUD's other text");
            Assert.Greater(w.Level.TimeLimit, 0, "level 1 has a time limit");
            Assert.AreEqual(w.Level.TimeLimit * 60, w.AllowanceTicks, "the level's own limit");
            Assert.AreEqual(LevelClock.Stage.Calm, hud.ClockStage);

            w.TicksLeft = 25 * 60;
            yield return Frames(2);
            Assert.AreEqual(LevelClock.Stage.Warning, hud.ClockStage);
            Assert.IsTrue(hud.Clock.ClassListContains("warn"));
            var amber = hud.ClockLabel.resolvedStyle.color;
            Assert.Greater(amber.r, 0.9f);
            Assert.Greater(amber.g, 0.55f, "amber, not red");
            Assert.Less(amber.b, 0.4f);

            w.TicksLeft = 8 * 60;
            yield return Frames(2);
            Assert.AreEqual(LevelClock.Stage.Danger, hud.ClockStage);
            Assert.AreEqual("0:08", hud.ClockLabel.text);
            var red = hud.ClockLabel.resolvedStyle.color;
            Assert.Greater(red.r, 0.9f);
            Assert.Less(red.g, 0.4f, "red");
            Assert.IsTrue(hud.Clock.ClassListContains("danger"));
        }

        [UnityTest]
        public IEnumerator AFallCostsAHeartAndRunningOutRestartsTheLevel()
        {
            yield return Boot();
            // A finished run from before: its best time and gems must survive a failed attempt.
            App.Progress.Record(1, 30, new[] { true, true, false }, 5, 0);
            yield return StartLevel(1);
            var level = App.Level;
            var w = level.World;
            level.StepTicks(30);

            // A fall with hearts left: one heart less, back at the checkpoint (here the start), no refill.
            w.Player.Body.Pos = new V2d(w.Player.Body.Pos.X, w.Grid.Height + 3);
            level.StepTicks(2);
            Assert.AreEqual(Player.State.Dead, w.Player.Current);
            Assert.AreEqual(w.Config.maxHealth - 1, w.Player.Health, "the fall cost a heart");
            level.StepTicks(GameConfig.Ticks(w.Config.respawnDelay) + 2);
            Assert.AreNotEqual(Player.State.Dead, w.Player.Current, "respawned");
            Assert.AreEqual(w.Config.maxHealth - 1, w.Player.Health, "and kept the hearts left");

            // Losing the last heart: the card, then the level from the very beginning.
            w.Checkpoint = new V2d(30.5, w.Checkpoint.Y); // as if a checkpoint were reached
            w.Player.Health = 1;
            w.Player.Body.Pos = new V2d(w.Player.Body.Pos.X, w.Grid.Height + 3);
            level.StepTicks(2);
            Assert.IsTrue(w.OutOfHearts);
            yield return Frames(2);
            Assert.IsTrue(level.Hud.OutOfHeartsShowing, "the Out of hearts card shows");
            Assert.Greater(level.RestartIn, 0);
            yield return Seconds((float)w.Config.outOfHeartsTime + 0.5f);
            Assert.AreNotSame(level, App.Level, "a new attempt");
            var again = App.Level.World;
            Assert.AreEqual(1, App.Level.Number);
            Assert.AreEqual(again.Config.maxHealth, again.Player.Health, "full hearts");
            Assert.AreEqual(again.Level.Start.X, again.Checkpoint.X, 1e-9, "checkpoints cleared");
            Assert.Less(again.Tick, 120, "the clock started again");
            Assert.Greater(again.TicksLeft, again.AllowanceTicks - 120);
            Assert.AreEqual(0, again.Coins);
            Assert.IsFalse(App.Level.Hud.OutOfHeartsShowing);
            Assert.AreEqual(30, App.Progress.Level(1).BestTime, 1e-9, "the saved best time stays");
            Assert.IsTrue(App.Progress.Level(1).Gems[1], "and the saved gems");
        }

        [UnityTest]
        public IEnumerator AutoTouchButtonsWaitForARealTouch()
        {
            // A desktop browser: keyboard and mouse, plus the touchscreen many browsers report even
            // when the PC has none. "auto" must keep the buttons hidden until a finger touches.
            InputSystem.AddDevice<Mouse>();
            var screenDevice = InputSystem.AddDevice<Touchscreen>();
            yield return Boot();
            App.Config.touchControls = "auto"; // config.json can force the buttons on or off; this tests "auto"
            App.Progress.Settings.TouchControls = "auto";
            yield return StartLevel(1);
            yield return Frames(3);
            var touch = App.Level.Touch;
            Assert.IsFalse(Application.isMobilePlatform);
            Assert.IsFalse(touch.Visible, "auto hides the buttons on a desktop without a touch");
            Assert.AreEqual(DisplayStyle.None, touch.Root.resolvedStyle.display);

            BeginTouch(1, new Vector2(100, 100), screen: screenDevice);
            yield return Frames(3);
            EndTouch(1, new Vector2(100, 100), screen: screenDevice);
            yield return Frames(3);
            Assert.IsTrue(touch.Visible, "the first real touch shows the buttons");
            Assert.AreEqual(DisplayStyle.Flex, touch.Root.resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator SettingsSaveAndResetAsksFirst()
        {
            yield return Boot();
            App.Progress.Record(1, 30, new[] { true, false, false }, 5, 0);
            Click(FindButton("settings"));
            yield return Frames(2);
            var settings = App.Screens.Current as SettingsScreen;
            Assert.IsNotNull(settings);
            settings.Root.Q<Slider>("music").value = 0.2f;
            settings.Root.Q<Toggle>("shake").value = false;
            Click(FindButton("touch"));
            Click(FindButton("reset"));
            yield return Frames(2);
            Assert.IsTrue(settings.ConfirmOpen, "reset asks first");
            Click(FindButton("confirm-no"));
            yield return Frames(1);
            Assert.IsTrue(App.Progress.Level(1).Completed, "Keep keeps the progress");
            Click(FindButton("back"));
            yield return Frames(2);
            Assert.IsInstanceOf<TitleScreen>(App.Screens.Current);
            var saved = Progress.FromJson(File.ReadAllText(_save), out _);
            Assert.AreEqual(0.2, saved.Settings.MusicVolume, 0.01);
            Assert.IsFalse(saved.Settings.ScreenShake);
            Assert.AreEqual("on", saved.Settings.TouchControls);
            Click(FindButton("settings"));
            yield return Frames(2);
            Click(FindButton("reset"));
            Click(FindButton("confirm-yes"));
            yield return Frames(1);
            Assert.IsFalse(App.Progress.Level(1).Completed, "Erase erases");
        }

        [UnityTest]
        public IEnumerator TheLanguageButtonSwitchesTheScreensAndIsSaved()
        {
            yield return Boot();
            Click(FindButton("settings"));
            yield return Frames(2);
            Assert.AreEqual(DisplayStyle.None, App.Screens.Root.Q<VisualElement>("language-row").resolvedStyle.display, "one language: no Language row");
            Click(FindButton("back"));
            yield return Frames(2);
            try
            {
                Loc.Load("{\"languages\": {\"en\": \"English\", \"xx\": \"Xish\"}, \"texts\": {\"xx\": {\"Play\": \"Plxx\", \"Settings\": \"Setxx\"}}}", null);
                Click(FindButton("settings"));
                yield return Frames(2);
                Assert.AreEqual(DisplayStyle.Flex, App.Screens.Root.Q<VisualElement>("language-row").resolvedStyle.display);
                Assert.AreEqual("English", FindButton("language").text);
                Click(FindButton("language"));
                yield return Frames(2);
                Assert.AreEqual("xx", Loc.Language);
                Assert.IsInstanceOf<SettingsScreen>(App.Screens.Current, "Settings stays open, rebuilt");
                Assert.AreEqual("Xish", FindButton("language").text);
                Assert.IsNotNull(App.Screens.Current.Root.Q<Label>(className: "heading"));
                Assert.AreEqual("Setxx", App.Screens.Current.Root.Q<Label>(className: "heading").text, "the UXML texts are translated");
                Assert.AreEqual("Plxx", FindButton("play").text, "the title underneath is rebuilt too");
                Assert.AreEqual("xx", Progress.FromJson(File.ReadAllText(_save), out _).Settings.Language, "the choice is saved");
            }
            finally
            {
                Loc.Load(Resources.Load<TextAsset>(Loc.Path)?.text, null);
                Loc.Use(Loc.English);
            }
        }

        [UnityTest]
        public IEnumerator CreditsShowTheCreditsText()
        {
            yield return Boot();
            Click(FindButton("credits"));
            yield return Frames(2);
            Assert.IsInstanceOf<CreditsScreen>(App.Screens.Current);
            StringAssert.Contains("CodePlate", App.Screens.Root.Q<Label>("credits-text").text);
            // The text wraps and the list scrolls vertically only: no sideways scroll bar.
            var scroll = App.Screens.Root.Q<ScrollView>("scroll");
            Assert.AreEqual(ScrollerVisibility.Hidden, scroll.horizontalScrollerVisibility);
            Assert.AreEqual(ScrollViewMode.Vertical, scroll.mode);
        }

        [UnityTest]
        public IEnumerator ThePrivacyPolicyButtonShowsOnlyWithAnAddress()
        {
            yield return Boot();
            string before = App.Config.privacyPolicyUrl;
            try
            {
                App.Config.privacyPolicyUrl = "";
                Click(FindButton("credits"));
                yield return Frames(2);
                Assert.AreEqual(DisplayStyle.None, FindButton("privacy").resolvedStyle.display, "no address in config.json: no button");
                Click(FindButton("back"));
                yield return Frames(2);

                App.Config.privacyPolicyUrl = "https://example.com/privacy/";
                Assert.AreEqual("https://example.com/privacy/", Links.PrivacyPolicy);
                Click(FindButton("credits"));
                yield return Frames(2);
                var credits = FindButton("privacy");
                Assert.AreEqual(DisplayStyle.Flex, credits.resolvedStyle.display, "Credits has a Privacy policy button");
                Assert.AreEqual("Privacy policy", credits.text);
                Assert.IsTrue(credits.focusable);
                Click(FindButton("back"));
                yield return Frames(2);
                Click(FindButton("settings"));
                yield return Frames(2);
                Assert.AreEqual(DisplayStyle.Flex, FindButton("privacy").resolvedStyle.display, "Settings has one too");
            }
            finally
            {
                App.Config.privacyPolicyUrl = before;
            }
        }

        [UnityTest]
        public IEnumerator BossFightPlaysThroughTheScene()
        {
            yield return Boot();
            int n = App.Content.Levels.Find(l => l.HasBoss).Number;
            var inputs = StartReplay(n);
            bool bossMusic = false, bossBar = false;
            for (int i = 0; i < inputs.Count + 60 && !App.Level.World.Finished; i += 30)
            {
                App.Level.StepTicks(30);
                yield return null;
                bossMusic |= App.Audio.CurrentMusic == App.Content.World(App.Level.Level.World).BossMusic;
                bossBar |= App.Screens.Root.Q("boss-bar").resolvedStyle.display == DisplayStyle.Flex;
            }
            var bw = App.Level.World;
            Assert.IsTrue(bw.Finished, $"the boss level's replay should finish in the scene: tick {bw.Tick}, player at {bw.Player.Body.Pos} ({bw.Player.Current}), {bw.Deaths} deaths");
            Assert.IsTrue(bossMusic, "boss music plays during the fight");
            Assert.IsTrue(bossBar, "the HUD shows the boss's health");
        }

        [UnityTest]
        public IEnumerator EveryArtSlotAndSoundLoads()
        {
            yield return Boot();
            foreach (var slot in ArtSlots.All)
            {
                var t = Resources.Load<Texture2D>("Art/" + slot.Path);
                Assert.IsNotNull(t, $"Art/{slot.Path}");
                Assert.AreEqual(0, t.width % slot.W, $"{slot.Path}: width {t.width} isn't a whole number of {slot.W} px frames (Art.cs / assets.json)");
                Assert.AreEqual(slot.H, t.height, $"{slot.Path}: height");
                Assert.AreEqual(FilterMode.Point, t.filterMode, $"{slot.Path}: pixel art should import with Point filtering");
            }
            foreach (var name in new[] { "jump", "coin", "gem", "stomp", "hurt", "die", "goal", "click", "music_title" })
                Assert.IsNotNull(Resources.Load<AudioClip>("Audio/" + name), $"Audio/{name}");
            // The art table in Art.cs agrees with assets.json (frame sizes).
            var manifest = (JsonObject)Json.Parse(File.ReadAllText(ProjectPaths.AssetsManifest), "assets.json");
            var sizes = new Dictionary<string, (int, int)>();
            foreach (var o in manifest.Arr("assets"))
            {
                var a = (JsonObject)o;
                sizes[a.Str("path")] = (a.Int("width"), a.Int("height"));
            }
            foreach (var slot in ArtSlots.All)
            {
                var path = $"{ProjectPaths.Art}/{slot.Path}.png";
                Assert.IsTrue(sizes.TryGetValue(path, out var wh), $"{path} isn't in assets.json");
                Assert.AreEqual((slot.W, slot.H), wh, $"{path}: Art.cs says {slot.W}x{slot.H} per frame, assets.json says {wh.Item1}x{wh.Item2}");
            }
        }
    }
}
