using System.IO;
using UnityEditor;
using UnityEditor.Compilation;

namespace PartyPrototype.Editor
{
    public static class PartyRefresh
    {
        [MenuItem("Party Prototype/Repair stale scripts (stop Play first)")]
        public static void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Stop Play mode", "Stop Play mode, then run Repair stale scripts again.", "OK");
                return;
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            foreach (var path in Directory.GetFiles("Assets/PartyPrototype", "*.cs", SearchOption.AllDirectories))
                AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
            UnityEngine.Debug.Log("Requested a clean script compilation. Wait for Unity to finish, then press Play. The home screen should show HUD 0.8.1 and both solo buttons.");
        }
    }
}
