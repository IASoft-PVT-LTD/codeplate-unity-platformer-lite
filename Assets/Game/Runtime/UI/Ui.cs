using System;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Game.Runtime
{
    /// <summary>Helpers for the UI Toolkit screens: loading UXML (in the player's language), wiring buttons, focus, the pixel font.</summary>
    public static class Ui
    {
        /// <summary>
        /// Insets a full-screen layer by the screen's safe area (a phone's notch, rounded corners and
        /// gesture bar), in panel units. Call it every frame or after a rotation; desktops have no insets.
        /// </summary>
        public static Vector4 FitSafeArea(VisualElement layer)
        {
            var inset = SafeInsets();
            layer.style.left = inset.x;
            layer.style.right = inset.y;
            layer.style.top = inset.z;
            layer.style.bottom = inset.w;
            return inset;
        }

        /// <summary>Screenshots can pretend the screen has a cutout or a gesture bar: insets in UI units (left, right, top, bottom).</summary>
        public static Vector4? SafeInsetsOverride;

        /// <summary>How far the safe area (Screen.safeArea) sits in from each screen edge, in UI units: left, right, top, bottom.</summary>
        public static Vector4 SafeInsets()
        {
            if (SafeInsetsOverride.HasValue) return SafeInsetsOverride.Value;
            var sa = Screen.safeArea;
            if (sa.width <= 0 || sa.height <= 0 || App.Screens == null) return Vector4.zero;
            float z = Mathf.Max(1, App.Screens.Zoom());
            return new Vector4(sa.xMin / z, (Screen.width - sa.xMax) / z, (Screen.height - sa.yMax) / z, sa.yMin / z);
        }

        /// <summary>
        /// Instantiates Assets/Resources/UI/&lt;name&gt;.uxml as a full-screen element, with every
        /// label and button text in the player's language (Game.Core Loc: the English text in
        /// the UXML is the key).
        /// </summary>
        public static VisualElement Load(string name)
        {
            var asset = Resources.Load<VisualTreeAsset>("UI/" + name);
            if (asset == null)
            {
                Debug.LogError($"[CodePlate] missing UI: {Game.Core.ProjectPaths.UI}/{name}.uxml");
                return new VisualElement();
            }
            var tree = asset.Instantiate();
            tree.AddToClassList("layer");
            tree.pickingMode = PickingMode.Ignore;
            tree.name = name;
            Translate(tree);
            return tree;
        }

        /// <summary>Replaces every label and button text under root with its text in the language in use.</summary>
        public static void Translate(VisualElement root)
        {
            if (Game.Core.Loc.Language == Game.Core.Loc.English) return;
            root.Query<UnityEngine.UIElements.TextElement>().ForEach(t =>
            {
                if (!string.IsNullOrEmpty(t.text)) t.text = Game.Core.Loc.T(t.text);
            });
        }

        /// <summary>The element called <paramref name="name"/> (an error naming the UXML if it's missing).</summary>
        public static T Q<T>(VisualElement root, string name) where T : VisualElement
        {
            var e = root.Q<T>(name);
            if (e == null) Debug.LogError($"[CodePlate] UI element #{name} ({typeof(T).Name}) not found in {root.name}");
            return e;
        }

        /// <summary>Wires a button by name: plays the click sound, then runs <paramref name="action"/>.</summary>
        public static Button Button(VisualElement root, string name, Action action)
        {
            var b = Q<Button>(root, name);
            if (b == null) return null;
            b.clicked += () =>
            {
                App.Audio?.Play("click");
                action();
            };
            return b;
        }

        /// <summary>Focuses the first enabled, visible button under root (so keyboard and gamepad work at once).</summary>
        public static void FocusFirst(VisualElement root)
        {
            root.schedule.Execute(() =>
            {
                var b = root.Query<Button>().Where(x => x.enabledInHierarchy && x.resolvedStyle.display != DisplayStyle.None).First();
                b?.Focus();
            });
        }

        public static void Show(VisualElement e, bool show)
        {
            if (e != null) e.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static FontAsset _font;

        /// <summary>
        /// Uses the game's pixel font (Assets/Resources/Art/ui/font.ttf) for every label under root.
        /// Swap the .ttf to change the font everywhere (docs/recipes/reskin-and-rebrand.md).
        /// </summary>
        public static void ApplyFont(VisualElement root)
        {
            if (_font == null)
            {
                var ttf = Resources.Load<Font>("Art/ui/font");
                if (ttf == null) return;
                _font = FontAsset.CreateFontAsset(ttf, 64, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 512, 512);
                if (_font == null) return;
                _font.name = "Pixel font";
            }
            root.style.unityFontDefinition = FontDefinition.FromSDFFont(_font);
        }

        /// <summary>"1:05.3" from seconds.</summary>
        public static string Time(double seconds)
        {
            if (seconds <= 0) return "--";
            int m = (int)(seconds / 60);
            return $"{m}:{seconds - m * 60:00.0}";
        }
    }

    /// <summary>A full-screen menu or overlay the router can show.</summary>
    public interface IScreen
    {
        VisualElement Root { get; }
        /// <summary>Called every frame while it's the top screen.</summary>
        void Tick();
        /// <summary>Esc / gamepad B. Return true if handled.</summary>
        bool Back();
    }
}
