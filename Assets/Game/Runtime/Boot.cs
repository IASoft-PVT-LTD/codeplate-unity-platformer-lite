using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Runtime
{
    /// <summary>
    /// The only component in the only scene (Assets/Scenes/Boot.unity). It builds everything
    /// else from code: the camera (pixel-perfect, 480x270 of game pixels scaled up crisply), a
    /// 2D light, audio, input, the UI, then shows the title. Don't add objects to the scene: add
    /// code here or in the screens (AGENTS.md rule 3).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class Boot : MonoBehaviour
    {
        public static Boot Instance { get; private set; }

        public Camera Camera { get; private set; }
        public PixelPerfectCamera PixelPerfect { get; private set; }

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // The simulation steps once per FixedUpdate: exactly 60 times a second.
            Time.fixedDeltaTime = 1f / 60f;
            Time.maximumDeltaTime = 0.1f;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;

            var content = Resources.Load<TextAsset>(Game.Core.GameContent.ConfigPath);
            int ppu = 16, w = 480, h = 270;
            if (content != null)
            {
                var cfg = Game.Core.GameConfig.FromJson(content.text, new System.Collections.Generic.List<string>());
                ppu = cfg.tilePx;
                w = cfg.viewWidth;
                h = cfg.viewHeight;
            }
            BuildCamera(ppu, w, h);
            BuildLight();

            var audio = new GameObject("Audio").AddComponent<AudioDirector>();
            audio.transform.SetParent(transform);
            var ui = new GameObject("UI").AddComponent<ScreenRouter>();
            ui.transform.SetParent(transform);
            App.Init(new Art(ppu), audio, new InputSource(), ui);
            ui.Build(w, h);
            ApplyDisplaySettings();

            if (!ShotDirector.TryStart(this)) ui.ShowTitle();
        }

        void BuildCamera(int ppu, int w, int h)
        {
            var go = new GameObject("Camera");
            go.transform.SetParent(transform);
            go.transform.position = new Vector3(0, 0, -10);
            Camera = go.AddComponent<Camera>();
            Camera.orthographic = true;
            Camera.orthographicSize = h / 2f / ppu;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.08f, 0.06f, 0.05f);
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 100f;
            go.AddComponent<AudioListener>();
            PixelPerfect = go.AddComponent<PixelPerfectCamera>();
            PixelPerfect.assetsPPU = ppu;
            PixelPerfect.refResolutionX = w;
            PixelPerfect.refResolutionY = h;
            PixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            PixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.None;
        }

        void BuildLight()
        {
            var go = new GameObject("Light");
            go.transform.SetParent(transform);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = Color.white;
        }

        /// <summary>Applies the saved display settings (fullscreen).</summary>
        public static void ApplyDisplaySettings()
        {
            if (Application.isEditor || Application.platform == RuntimePlatform.WebGLPlayer) return;
            Screen.fullScreen = App.Progress.Settings.Fullscreen;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            App.Shutdown();
        }
    }
}
