using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Runtime
{
    /// <summary>
    /// The world map (UI/WorldMap.uxml): one page per world from worlds.json, its levels as nodes
    /// along a winding path over the world's backdrop. Locked levels are greyed out; finished ones
    /// show a check and the gems found. Arrow keys / gamepad move between nodes.
    /// </summary>
    public sealed class WorldMapScreen : IScreen
    {
        public VisualElement Root { get; }
        /// <summary>The world shown (1-based).</summary>
        public int World { get; private set; }
        public readonly List<Button> Nodes = new List<Button>();
        readonly VisualElement _area;
        readonly Label _levelName, _levelBest, _worldName, _worldSub, _gemTotal;
        readonly Button _prev, _next;

        public WorldMapScreen(int world)
        {
            Root = Ui.Load("WorldMap");
            _area = Ui.Q<VisualElement>(Root, "map-area");
            _levelName = Ui.Q<Label>(Root, "level-name");
            _levelBest = Ui.Q<Label>(Root, "level-best");
            _worldName = Ui.Q<Label>(Root, "world-name");
            _worldSub = Ui.Q<Label>(Root, "world-subtitle");
            _gemTotal = Ui.Q<Label>(Root, "gem-total");
            Ui.Button(Root, "back", () => Back());
            _prev = Ui.Button(Root, "prev-world", () => Page(-1));
            _next = Ui.Button(Root, "next-world", () => Page(1));
            World = world > 0 ? world : NewestWorld();
            Build();
        }

        /// <summary>The last world with an unlocked level.</summary>
        static int NewestWorld()
        {
            int best = 1;
            foreach (var w in App.Content.Worlds)
            {
                var levels = App.Content.LevelsOf(w.Id);
                if (levels.Count > 0 && App.Progress.IsUnlocked(levels[0].Number)) best = w.Id;
            }
            return best;
        }

        void Page(int delta)
        {
            int i = App.Content.Worlds.FindIndex(w => w.Id == World) + delta;
            if (i < 0 || i >= App.Content.Worlds.Count) return;
            App.Screens.ShowMap(App.Content.Worlds[i].Id);
        }

        void Build()
        {
            var info = App.Content.World(World) ?? new WorldInfo { Id = World, Name = $"World {World}" };
            _worldName.text = info.Name;
            _worldSub.text = info.Subtitle;
            int wi = App.Content.Worlds.FindIndex(w => w.Id == World);
            _prev.SetEnabled(wi > 0);
            _next.SetEnabled(wi >= 0 && wi < App.Content.Worlds.Count - 1);
            if (ColorUtility.TryParseHtmlString(info.Accent, out var accent)) _worldName.style.color = accent;

            var levels = App.Content.LevelsOf(World);
            int gems = 0;
            Button focus = null;
            var points = new List<Vector2>();
            for (int i = 0; i < levels.Count; i++)
            {
                // A gentle zigzag across the middle of the screen.
                float t = levels.Count == 1 ? 0.5f : i / (float)(levels.Count - 1);
                points.Add(new Vector2(Mathf.Lerp(70, 410, t), 136 + (i % 2 == 0 ? 14 : -18) + (i == levels.Count - 1 ? -6 : 0)));
            }
            for (int i = 0; i + 1 < points.Count; i++)
            {
                bool open = App.Progress.IsUnlocked(levels[i + 1].Number);
                float len = Vector2.Distance(points[i], points[i + 1]);
                for (float d = 14; d < len - 12; d += 7)
                {
                    var p = Vector2.Lerp(points[i], points[i + 1], d / len);
                    var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                    dot.AddToClassList("map-path");
                    if (!open) dot.AddToClassList("locked");
                    dot.style.left = Mathf.Round(p.x - 1.5f);
                    dot.style.top = Mathf.Round(p.y - 1.5f);
                    _area.Add(dot);
                }
            }
            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                int n = level.Number;
                bool unlocked = App.Progress.IsUnlocked(n);
                var rec = App.Progress.Levels.TryGetValue(n, out var r) ? r : null;
                var node = new Button { name = $"level-{n}", text = level.HasBoss ? "" : (i + 1).ToString() };
                node.AddToClassList("node");
                if (level.HasBoss) node.AddToClassList("boss");
                if (rec != null && rec.Completed) node.AddToClassList("done");
                float size = level.HasBoss ? 32 : 24;
                node.style.left = points[i].x - size / 2;
                node.style.top = points[i].y - size / 2;
                node.SetEnabled(unlocked);
                node.clicked += () =>
                {
                    App.Audio.Play("click");
                    App.PlayLevel(n);
                };
                node.RegisterCallback<FocusInEvent>(_ => ShowInfo(level));
                node.RegisterCallback<PointerEnterEvent>(_ => ShowInfo(level));
                _area.Add(node);
                Nodes.Add(node);

                var gemRow = new VisualElement { pickingMode = PickingMode.Ignore };
                gemRow.AddToClassList("node-gems");
                gemRow.style.left = points[i].x - 12;
                gemRow.style.top = points[i].y + size / 2 + 2;
                for (int g = 0; g < LevelData.GemsPerLevel; g++)
                {
                    var gem = new VisualElement { pickingMode = PickingMode.Ignore };
                    gem.AddToClassList("node-gem");
                    if (rec != null && rec.Gems[g])
                    {
                        gem.AddToClassList("found");
                        gems++;
                    }
                    gemRow.Add(gem);
                }
                Ui.Show(gemRow, unlocked);
                _area.Add(gemRow);
                if (unlocked) focus = node;
            }
            _gemTotal.text = $"Gems {gems}/{levels.Count * LevelData.GemsPerLevel}";
            if (focus != null)
            {
                var f = focus;
                Root.schedule.Execute(() => f.Focus());
                ShowInfo(levels[Nodes.IndexOf(focus)]);
            }
        }

        void ShowInfo(LevelData level)
        {
            int indexInWorld = App.Content.LevelsOf(level.World).IndexOf(level) + 1;
            _levelName.text = $"{level.World}-{indexInWorld}  {level.Title}";
            var best = App.Progress.Levels.TryGetValue(level.Number, out var r) ? r.BestTime : 0;
            _levelBest.text = !App.Progress.IsUnlocked(level.Number) ? "Locked: finish the level before it"
                : best > 0 ? $"Best {Ui.Time(best)}   Par {Ui.Time(level.ParTime)}" : $"Par {Ui.Time(level.ParTime)}";
        }

        public void Tick()
        {
            // Page through worlds with the shoulder buttons or Q/E.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if ((kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame)) Page(-1);
            if ((kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame)) Page(1);
        }

        public bool Back()
        {
            App.Screens.ShowTitle();
            return true;
        }
    }
}
