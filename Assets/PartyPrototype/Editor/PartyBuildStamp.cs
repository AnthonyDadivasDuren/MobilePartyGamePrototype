using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace PartyPrototype.Editor
{
    public sealed class PartyBuildStamp : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            PlayerSettings.bundleVersion = "0.8.1";
            const string materialPath = "Assets/PartyPrototype/Resources/PartyTiltMaterial.mat";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(materialPath) == null)
            {
                var shader = UnityEngine.Shader.Find("Universal Render Pipeline/Unlit") ?? UnityEngine.Shader.Find("Unlit/Color");
                AssetDatabase.CreateAsset(new UnityEngine.Material(shader), materialPath);
                AssetDatabase.SaveAssets();
            }
            if (report.summary.platform == BuildTarget.Android)
            {
                PlayerSettings.Android.bundleVersionCode = Math.Max(3, PlayerSettings.Android.bundleVersionCode + 1);
                PlayerSettings.Android.forceInternetPermission = true;
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            }
            const string path = "Assets/PartyPrototype/Resources/PartyBuildInfo.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "Build " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}
