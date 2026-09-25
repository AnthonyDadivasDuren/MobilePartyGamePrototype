using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PartyPrototype.Editor
{
    public static class PartySetup
    {
        const string ScenePath = "Assets/PartyPrototype/Scenes/PartyPrototype.unity";
        [MenuItem("Party Prototype/Create and open prototype")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateScene();
        }
        static void CreateScene()
        {
            Directory.CreateDirectory("Assets/PartyPrototype/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Party Prototype", typeof(PartyApp));
            EditorSceneManager.SaveScene(scene, ScenePath);
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.defaultScreenWidth = 540; PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            AssetDatabase.SaveAssets();
            Debug.Log("Party prototype ready. Press Play to host or join a quiz.");
        }
        [MenuItem("Party Prototype/Build Windows player")]
        public static void BuildWindows()
        {
            string folder = EditorUtility.OpenFolderPanel("Choose Windows build folder", "", "");
            if (string.IsNullOrEmpty(folder)) return;
            if (!EnsureScene()) return;
            Build(Path.Combine(folder, "PartyPrototype.exe"), BuildTarget.StandaloneWindows64);
        }
        [MenuItem("Party Prototype/Build Android APK")]
        public static void BuildAndroid()
        { BuildAndroid(false); }
        [MenuItem("Party Prototype/Build and Run Android (USB)")]
        public static void BuildAndRunAndroid()
        { BuildAndroid(true); }
        static void BuildAndroid(bool install)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorUtility.DisplayDialog("Wait for Unity", "Let script compilation and asset importing finish, then build again.", "OK"); return; }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                EditorUtility.DisplayDialog("Android platform selected", "Wait for Unity to finish recompiling for Android, then select this build menu again. This keeps the build from using assemblies from before the platform switch.", "OK");
                return;
            }
            string path = EditorUtility.SaveFilePanel("Save Android APK", "", "PartyPrototype-0.4.1", "apk");
            if (string.IsNullOrEmpty(path)) return;
            if (!EnsureScene()) return;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.prototype.partynight");
            EditorUserBuildSettings.buildAppBundle = false;
            Build(path, BuildTarget.Android, install);
        }
        static bool EnsureScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            if (!File.Exists(ScenePath)) CreateScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return true;
        }
        static void Build(string path, BuildTarget target, bool install = false)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = path,
                target = target, options = BuildOptions.Development | BuildOptions.CleanBuildCache | (install ? BuildOptions.AutoRunPlayer : BuildOptions.None)
            });
            if (report.summary.result != BuildResult.Succeeded)
                Debug.LogError("Build failed. Check the first error in the Console.");
            else
            {
                Debug.Log("Built party prototype HUD 0.4.1: " + path);
                if (target == BuildTarget.Android && !install)
                    EditorUtility.DisplayDialog("APK built — installation still needed", "Install this newly built APK on your phone:\n" + path + "\n\nBuilding a file does not update the installed app. The start screen must show HUD 0.4.1 and the new build stamp. Alternatively use Build and Run Android (USB).", "OK");
                EditorUtility.RevealInFinder(path);
            }
        }
    }
}
