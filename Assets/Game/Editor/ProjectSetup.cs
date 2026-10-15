using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CodePlate
{
    // One-time project setup, kept so you can see (and redo) how the settings were made:
    // the URP asset with the 2D renderer, and the Boot scene with its single Boot object.
    // Menu: Tools > CodePlate > Rebuild Project Setup. Batch: -executeMethod CodePlate.ProjectSetup.Run
    public static class ProjectSetup
    {
        const string PipelinePath = Game.Core.ProjectPaths.Settings + "/URP-2D.asset";
        const string RendererPath = Game.Core.ProjectPaths.Settings + "/URP-2D-Renderer.asset";
        const string ScenePath = Game.Core.ProjectPaths.BootScene;

        [MenuItem("Tools/CodePlate/Rebuild Project Setup")]
        public static void Run()
        {
            Directory.CreateDirectory(Game.Core.ProjectPaths.Settings);
            Directory.CreateDirectory(Game.Core.ProjectPaths.Scenes);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var menus = typeof(UniversalRenderPipelineAsset).Assembly.GetType("UnityEditor.Rendering.Universal.Renderer2DMenus")
                    ?? System.Type.GetType("UnityEditor.Rendering.Universal.Renderer2DMenus, Unity.RenderPipelines.Universal.Editor");
                var create = menus.GetMethod("CreateRendererAsset", BindingFlags.Static | BindingFlags.NonPublic);
                var data = (ScriptableRendererData)create.Invoke(null, new object[] { RendererPath, RendererType._2DRenderer, false, "Renderer" });
                pipeline = UniversalRenderPipelineAsset.Create(data);
                pipeline.supportsHDR = false;
                pipeline.msaaSampleCount = 1;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Boot").AddComponent<Game.Runtime.Boot>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[CodePlate] project setup done");
        }
    }
}
