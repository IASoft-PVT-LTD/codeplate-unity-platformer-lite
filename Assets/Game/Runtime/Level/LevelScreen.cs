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
    public sealed partial class LevelScreen : MonoBehaviour
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
        /// <summary>Seconds until a level that ran out of hearts starts again from the beginning (-1 = not failed).</summary>
        public float RestartIn { get; private set; } = -1;

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
            DisplayName = $"{Level.World}-{indexInWorld}  {Loc.T(Level.Title)}";

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
            Hud.Banner($"{(_worldInfo != null ? Loc.T(_worldInfo.Name) : Loc.T("World {0}", Level.World))}  {Level.World}-{indexInWorld}", Loc.T(Level.Title));
            App.Audio.PlayMusic(_worldInfo?.Music ?? "music_world1");
            Remember();
            DrawViews(0);
            PrepareContinue();
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
            if (RestartIn > 0 && !Paused && !App.Screens.OverlayOpen && (RestartIn -= Time.deltaTime) <= 0) RestartLevel();
            DrawViews(Time.deltaTime);
            _worldView.Sync();
            Hud.Update(World, Time.deltaTime);
        }

        /// <summary>Leaving the app mid-level (home button, a call, another app on a phone) pauses it.</summary>
        void OnApplicationPause(bool leaving)
        {
            if (leaving && World != null && !App.Screens.OverlayOpen) OpenPause();
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
                    Hud.Toast(Loc.T("Gem {0}/{1}", found, LevelData.GemsPerLevel));
                    break;
                case SimEventType.Clock:
                    audio.Play("clock");
                    Fx.Sparkle(at + new Vector2(0, 0.5f), 5);
                    Hud.Toast("+" + (int)cfg.timeBonus + " seconds");
                    break;
                case SimEventType.TimeUp:
                    Hud.Toast(World.Player.Health > 1 ? Loc.T("Time's up! -1 heart") : Loc.T("Time's up!"));
                    break;
                case SimEventType.OutOfHearts:
                    OutOfHearts();
                    break;
                case SimEventType.Heart:
                    audio.Play("heart");
                    Hud.Toast("+1 heart");
                    break;
                case SimEventType.ExtraHeart:
                    audio.Play("heart");
                    Hud.Toast("+1 heart: keep going!", 2f);
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
                    Hud.Toast(Loc.T("Checkpoint"));
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
                    if (boss != null) Hud.Banner(Loc.T("Boss"), Loc.T(boss.Name), 1.8f);
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
                    Hud.Toast(Loc.T("The door is open!"), 2f);
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

        /// <summary>
        /// The last heart is gone: the "Out of hearts" card for a moment, then the level starts again
        /// from the very beginning (a new World: checkpoints, clock, coins, pickups and gems found this
        /// attempt all reset). Saved best times and gems from finished runs are untouched: Progress
        /// only changes when a level is finished.
        /// </summary>
        void OutOfHearts()
        {
            if (RestartIn > 0) return;
            App.Audio.StopMusic();
            bool offered = false;
            OfferContinue(ref offered);
            if (offered) return;
            Hud.ShowOutOfHearts();
            RestartIn = (float)App.Config.outOfHeartsTime;
        }

        /// <summary>
        /// A hook for a continue offer when the hearts run out (an ad, a shop...): another
        /// part of this partial class can implement it, open its own screen and set
        /// <paramref name="offered"/>; then the level waits (it doesn't step while an overlay is
        /// open) and the offer later calls World.ContinueWithExtraHeart or RestartLevel. Not
        /// implemented: the "Out of hearts" card and the restart.
        /// </summary>
        partial void OfferContinue(ref bool offered);

        /// <summary>Called once the level is set up, for whatever OfferContinue needs ready in advance (loading an ad).</summary>
        partial void PrepareContinue();

        /// <summary>Starts this level again from the beginning (normally outOfHeartsTime after running out of hearts).</summary>
        public void RestartLevel()
        {
            RestartIn = -1;
            App.PlayLevel(Number);
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
