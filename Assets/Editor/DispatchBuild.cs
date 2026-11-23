using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frostline
{
    public static class DispatchBuild
    {
        private const string ScenePath = "Assets/Scenes/Dispatch.unity";
        private const string PanelPath = "Assets/Resources/UI/DispatchPanel.asset";

        public static void BuildWindows()
        {
            PrepareProject();
            const string player = "Builds/Windows/FrostlineDispatch.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(player));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = player,
                target = BuildTarget.StandaloneWindows64
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Windows build failed: {report.summary.result}");
            File.Copy("Assets/Resources/Fonts/OFL.txt", "Builds/Windows/FONT-LICENSE.txt", true);
            Debug.Log($"FROSTLINE_BUILD_OK {report.summary.totalSize} bytes");
        }

        private static void PrepareProject()
        {
            if (!File.Exists(ScenePath))
            {
                var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<PanelSettings>();
                    settings.name = "DispatchPanel";
                    settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                    settings.referenceResolution = new Vector2Int(1600, 900);
                    settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                    settings.match = 0.55f;
                    settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                        "Assets/Resources/UI/DispatchTheme.tss");
                    AssetDatabase.CreateAsset(settings, PanelPath);
                }

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.05f, 0.08f, 0.12f);
                camera.cullingMask = 0;
                var terminal = new GameObject("Dispatch");
                terminal.AddComponent<UIDocument>();
                terminal.AddComponent<DispatchBoard>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            var configuredScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var document = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
            var boardSettings = new SerializedObject(document.GetComponent<DispatchBoard>());
            var campaign = boardSettings.FindProperty("configuration").objectReferenceValue as CampaignConfig
                ?? AssetDatabase.LoadAssetAtPath<CampaignConfig>($"Assets/Resources/{CampaignConfig.ResourcePath}.asset");
            if (campaign == null)
                throw new InvalidOperationException("The dispatch scene requires a campaign configuration.");
            campaign.CreateRoute(new Dictionary<string, Texture2D>());
            var serializedDocument = new SerializedObject(document);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            SerializedProperty targetPanel = serializedDocument.FindProperty("m_PanelSettings");
            if (targetPanel.objectReferenceValue != panel)
            {
                targetPanel.objectReferenceValue = panel;
                serializedDocument.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(configuredScene);
                EditorSceneManager.SaveScene(configuredScene);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "Frostline";
            PlayerSettings.productName = "霜线信使";
            PlayerSettings.bundleVersion = "0.4.0";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 810;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
        }
    }
}
