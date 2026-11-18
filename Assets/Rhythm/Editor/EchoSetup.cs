using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Echo.EditorTools
{
    /// <summary>One-click project configuration for the portrait build.</summary>
    public static class EchoSetup
    {
        public const string ScenePath = "Assets/Rhythm/Scenes/Rhythm.unity";
        public const string SceneFolder = "Assets/Rhythm/Scenes";

        [MenuItem("Tools/Echo/Setup Project", priority = 0)]
        public static void Setup()
        {
            ConfigurePlayer();
            CreateScene();
            Debug.Log("[Echo] Setup complete — scene at " + ScenePath);
        }

        [MenuItem("Tools/Echo/Open Game Scene", priority = 1)]
        public static void OpenScene()
        {
            if (File.Exists(ScenePath))
                EditorSceneManager.OpenScene(ScenePath);
            else
                Debug.LogWarning("[Echo] Scene missing. Run Tools/Echo/Setup Project first.");
        }

        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Echo Games";
            PlayerSettings.productName = "Echo";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.defaultScreenWidth = 1080;
            PlayerSettings.defaultScreenHeight = 1920;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.echogames.echo");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            PlayerSettings.colorSpace = ColorSpace.Linear;

            AssetDatabase.SaveAssets();
        }

        public static void CreateScene()
        {
            if (!Directory.Exists(SceneFolder)) Directory.CreateDirectory(SceneFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject go = new GameObject("Echo");
            go.AddComponent<Echo.EchoGame>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Echo/Build Android APK", priority = 20)]
        public static void BuildAndroid()
        {
            if (!File.Exists(ScenePath)) CreateScene();

            Directory.CreateDirectory("Builds");
            BuildPlayerOptions options = new BuildPlayerOptions();
            options.scenes = new string[] { ScenePath };
            options.locationPathName = "Builds/Echo.apk";
            options.target = BuildTarget.Android;
            options.options = BuildOptions.None;

            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[Echo] Android build: " + report.summary.result + " → Builds/Echo.apk");
        }
    }
}
