using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// Plays one level. Each FixedUpdate (60 per second) reads one InputFrame, advances the World
    /// once, then turns its events into sounds, effects, shake and HUD updates. Views draw
    /// positions interpolated between ticks and never write to the simulation.
    /// </summary>
    public sealed class LevelScreen : MonoBehaviour
    {
        public int Number { get; private set; }
        public LevelData Level { get; private set; }
        public World World { get; private set; }
        /// <summary>"1-3  Piston Run".</summary>
        public string DisplayName { get; private set; }
        /// <summary>Feeds inputs from code instead of the controls (tests, replays, screenshots): tick → InputFrame bits.</summary>
        public Func<int, int> InputOverride;
        public bool Paused;
        /// <summary>The frame the pause menu closed on (that key press mustn't reopen it).</summary>
        public int ResumedFrame = -1;
        /// <summary>Set when the results panel opened.</summary>
        public bool ShowingResults { get; private set; }

        public PlayerView PlayerView { get; private set; }
        public HudView Hud { get; private set; }
        public TouchControls Touch { get; private set; }
        public CameraRig CameraRig { get; private set; }
        public Fx Fx { get; private set; }
        Backdrop _backdrop;
        WorldView _worldView;
        Transform _actorsRoot;
        readonly Dictionary<Actor, ActorView> _views = new Dictionary<Actor, ActorView>();
        readonly Dictionary<object, Vector2> _prev = new Dictionary<object, Vector2>();
        readonly List<Actor> _gone = new List<Actor>();
        int _hitstop;
        float _finishIn = -1;
        WorldInfo _worldInfo;

        internal void Begin(int number)
        {
            App.Level = this;
            Number = number;
            Level = App.Content.Level(number);
            if (Level == null)
            {
                Debug.LogError($"[CodePlate] no level {number}: {string.Join("; ", App.Content.Errors)}");
                return;
            }
            World = new World(Level, App.Config, App.Content.Enemies);
            _worldInfo = App.Content.World(Level.World);
            int indexInWorld = App.Content.LevelsOf(Level.World).IndexOf(Level) + 1;
            DisplayName = $"{Level.World}-{indexInWorld}  {Level.Title}";

            _backdrop = Backdrop.Create(transform, Level.World);
            _worldView = new GameObject("World").AddComponent<WorldView>();
            _worldView.transform.SetParent(transform, false);
            _worldView.Setup(World, Level.World);
            _actorsRoot = new GameObject("Actors").transform;
            _actorsRoot.SetParent(transform, false);
            foreach (var a in World.Actors) _views[a] = ActorView.Create(a, _actorsRoot);
            PlayerView = PlayerView.Create(World.Player, transform, App.Config.landSquash);
            Fx = new GameObject("Fx").AddComponent<Fx>();
            Fx.transform.SetParent(transform, false);
            CameraRig = new CameraRig(Boot.Instance.Camera, App.Config, Level.Width, Level.Height);
            CameraRig.Snap(Feet(World.Player.Body.Pos), World.Player.Facing);

            Hud = new HudView(App.Config.maxHealth);
            App.Screens.HudLayer.Add(Hud.Root);
            Touch = new TouchControls();
            App.Screens.TouchLayer.Add(Touch.Root);
            Hud.Banner($"{_worldInfo?.Name ?? "World " + Level.World}  {Level.World}-{indexInWorld}", Level.Title);
            App.Audio.PlayMusic(_worldInfo?.Music ?? "music_world1");
            Remember();
            DrawViews(0);
        }

        internal void End()
        {
            App.Input.TouchBits = 0;
        }

        /// <summary>Simulation tiles (y down) to world units (y up).</summary>
        public static Vector2 Feet(V2d p) => new Vector2((float)p.X, -(float)p.Y);

        void FixedUpdate()
        {
            if (World == null || Paused || World.Finished || App.Screens.OverlayOpen) return;
            if (_hitstop > 0)
            {
                _hitstop--;
                return;
            }
            Tick(InputOverride != null ? InputOverride(World.Tick) : App.Input.Read());
        }

        void Tick(int input)
        {
            Remember();
            foreach (var e in World.Step(input)) OnEvent(e);
        }

        /// <summary>Advances the simulation by <paramref name="ticks"/> right away (tests and screenshots fast-forward with this).</summary>
        public void StepTicks(int ticks, int input = 0)
        {
            for (int i = 0; i < ticks && !World.Finished; i++)
            {
                Tick(InputOverride != null ? InputOverride(World.Tick) : input);
                Fx.Advance(1f / 60f);
            }
            Remember();
            DrawViews(0);
        }

        /// <summary>Puts the camera straight on the player (screenshots, respawns).</summary>
        public void SnapCamera()
        {
            CameraRig.Snap(Feet(World.Player.Body.Pos), World.Player.Facing);
            _backdrop.Follow(CameraRig.Position, CameraRig.ViewWidth, CameraRig.ViewHeight);
        }

        void Update()
        {
            if (World == null) return;
            App.Input.TouchBits = Touch.Bits;
            Touch.Apply();
            if (!App.Screens.OverlayOpen && !World.Finished && Time.frameCount != ResumedFrame && App.Input.PausePressed()) OpenPause();
            if (_finishIn > 0 && (_finishIn -= Time.deltaTime) <= 0) ShowResults();
            DrawViews(Time.deltaTime);
            _worldView.Sync();
            Hud.Update(World, Time.deltaTime);
        }

        public void OpenPause()
        {
            if (Paused || World.Finished) return;
            Paused = true;
            App.Audio.Play("pause");
            App.Screens.Open(new PauseMenu(this));
        }

        void Remember()
        {
            _prev[World.Player] = Feet(World.Player.Body.Pos);
            foreach (var a in World.Actors) _prev[a] = Feet(a.Pos);
        }

        void DrawViews(float dt)
        {
            // Interpolate between the last two ticks so motion is smooth at any frame rate.
            float t = Paused || App.Screens.OverlayOpen ? 1 : Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            var p = World.Player;
            var now = Feet(p.Body.Pos);
            var feet = Vector2.Lerp(_prev.TryGetValue(p, out var pp) ? pp : now, now, t);
            PlayerView.Show(feet, dt);
            foreach (var a in World.Actors)
            {
                if (!_views.TryGetValue(a, out var v)) _views[a] = v = ActorView.Create(a, _actorsRoot);
                var an = Feet(a.Pos);
                v.Show(dt, Vector2.Lerp(_prev.TryGetValue(a, out var ap) ? ap : an, an, t));
            }
            // Views of actors the world removed (spent projectiles, crumbled blocks).
            _gone.Clear();
            foreach (var kv in _views)
                if (!World.Actors.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var a in _gone)
            {
                Destroy(_views[a].gameObject);
                _views.Remove(a);
                _prev.Remove(a);
            }
            CameraRig.Follow(feet, p.Facing, dt);
            _backdrop.Follow(CameraRig.Position, CameraRig.ViewWidth, CameraRig.ViewHeight);
        }

        void OnEvent(SimEvent e)
        {
            var cfg = App.Config;
            var at = Feet(e.Pos);
            PlayerView.OnEvent(e);
            var audio = App.Audio;
            switch (e.Type)
            {
                case SimEventType.Jump:
                    audio.Play("jump");
                    Fx.Dust(at, cfg.dustParticles);
                    break;
                case SimEventType.AirJump:
                    audio.Play("air_jump");
                    Fx.Sparkle(at, 4);
                    break;
                case SimEventType.WallJump:
                    audio.Play("wall_jump");
                    Fx.Dust(at, cfg.dustParticles);
                    break;
                case SimEventType.Dash:
                    audio.Play("dash");
                    Fx.Dust(at + new Vector2(0, 0.3f), 3);
                    break;
                case SimEventType.Land:
                    if (e.Value > 6)
                    {
                        audio.Play("land", 1f, -6f);
                        Fx.Dust(at, cfg.dustParticles);
                    }
                    break;
                case SimEventType.Coin:
                    audio.Play("coin", 1f + 0.05f * ((int)e.Value % 5));
                    Fx.Sparkle(at + new Vector2(0, 0.5f), 3);
                    break;
                case SimEventType.Gem:
                    audio.Play("gem");
                    Fx.Sparkle(at + new Vector2(0, 0.5f), 8);
                    int found = Array.FindAll(World.Gems, g => g).Length;
                    Hud.Toast($"Gem {found}/{LevelData.GemsPerLevel}");
                    break;
                case SimEventType.Heart:
                    audio.Play("heart");
                    Hud.Toast("+1 heart");
                    break;
                case SimEventType.Stomp:
                    audio.Play("stomp");
                    Fx.Pop(at + new Vector2(0, 0.4f));
                    CameraRig.Shake(cfg.shakeStomp);
                    _hitstop = GameConfig.Ticks(cfg.hitstopStomp);
                    break;
                case SimEventType.Bump:
                    audio.Play("bump");
                    Fx.Sparkle(at + new Vector2(0, 1f), 3);
                    break;
                case SimEventType.Hurt:
                    audio.Play("hurt");
                    CameraRig.Shake(cfg.shakeHurt);
                    break;
                case SimEventType.Die:
                    audio.Play("die");
                    CameraRig.Shake(cfg.shakeDie);
                    Fx.Pop(at + new Vector2(0, 0.5f));
                    Fx.Debris(at + new Vector2(0, 0.5f), 5);
                    break;
                case SimEventType.Respawn:
                    CameraRig.Snap(at, World.Player.Facing);
                    Remember();
                    if (_worldInfo != null) audio.PlayMusic(_worldInfo.Music);
                    break;
                case SimEventType.Checkpoint:
                    audio.Play("checkpoint");
                    Fx.Sparkle(at + new Vector2(0, 1.5f), 6);
                    Hud.Toast("Checkpoint");
                    break;
                case SimEventType.Spring:
                    audio.Play("spring");
                    break;
                case SimEventType.Crumble:
                    audio.Play("crumble", 1f, -4f);
                    Fx.Debris(at, 3);
                    break;
                case SimEventType.WallBreak:
                    audio.Play("wall_break");
                    Fx.Debris(at + new Vector2(0, 0.5f), 6);
                    CameraRig.Shake(cfg.shakeStomp);
                    break;
                case SimEventType.Shoot:
                    audio.Play("shoot", 1f, -3f);
                    break;
                case SimEventType.BossWake:
                    audio.Play("boss_wake");
                    audio.PlayMusic(_worldInfo?.BossMusic ?? "music_boss");
                    var boss = World.ActiveBoss;
                    if (boss != null) Hud.Banner("Boss", boss.Name, 1.8f);
                    break;
                case SimEventType.BossTelegraph:
                    break;
                case SimEventType.BossSlam:
                    audio.Play("boss_slam");
                    CameraRig.Shake(cfg.shakeBoss);
                    Fx.Steam(at + new Vector2(0, 0.2f), 4);
                    break;
                case SimEventType.BossHit:
                    audio.Play("boss_hit");
                    Fx.Pop(at + new Vector2(0, 1f));
                    CameraRig.Shake(cfg.shakeBoss);
                    _hitstop = GameConfig.Ticks(cfg.hitstopStomp * 2);
                    foreach (var kv in _views)
                        if (kv.Key is Boss) kv.Value.Flash(0.6f);
                    break;
                case SimEventType.BossDefeated:
                    audio.Play("boss_defeated");
                    Fx.Sparkle(at + new Vector2(0, 1f), 12);
                    Fx.Debris(at + new Vector2(0, 1f), 10);
                    if (_worldInfo != null) audio.PlayMusic(_worldInfo.Music);
                    break;
                case SimEventType.GoalOpen:
                    audio.Play("goal_open");
                    Fx.Sparkle(at + new Vector2(0, 1.5f), 10);
                    Hud.Toast("The door is open!", 2f);
                    break;
                case SimEventType.Goal:
                    audio.Play("goal");
                    Fx.Sparkle(at + new Vector2(0, 1.5f), 12);
                    Finish();
                    break;
            }
        }

        bool _newBest;
        double _bestBefore;

        void Finish()
        {
            var rec = App.Progress.Levels.TryGetValue(Number, out var r) ? r : null;
            _bestBefore = rec?.BestTime ?? 0;
            _newBest = App.Progress.Record(Number, World.Seconds, World.Gems, World.Coins, World.Deaths);
            App.Save();
            _finishIn = 0.7f;
        }

        /// <summary>Shows the results panel now (normally 0.7 s after the goal).</summary>
        public void ShowResults()
        {
            if (ShowingResults) return;
            _finishIn = -1;
            ShowingResults = true;
            Hud.ClearMessages();
            App.Screens.Open(new ResultsPanel(this, _newBest, _bestBefore));
        }
    }
}
