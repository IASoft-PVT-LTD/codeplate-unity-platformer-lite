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
    public class GameTests : InputTestFixture
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
            SaveStore.PathOverride = null;
            base.TearDown();
        }

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
            foreach (var name in new[] { "left", "right", "jump", "dash", "pause" })
            {
                var r = touch.Button(name).worldBound;
                Assert.IsTrue(r.width > 10 && screen.Contains(r.center), $"touch button {name} is on screen ({r})");
            }
            var p = App.Level.World.Player;
            double x0 = p.Body.Pos.X;
            touch.Simulate("right", true);
            yield return Seconds(0.4f);
            Assert.Greater(p.Body.Pos.X, x0 + 1.0, "holding the right button runs");
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
        public IEnumerator CreditsShowTheCreditsText()
        {
            yield return Boot();
            Click(FindButton("credits"));
            yield return Frames(2);
            Assert.IsInstanceOf<CreditsScreen>(App.Screens.Current);
            StringAssert.Contains("CodePlate", App.Screens.Root.Q<Label>("credits-text").text);
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
            var manifest = (JsonObject)Json.Parse(File.ReadAllText("assets.json"), "assets.json");
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
