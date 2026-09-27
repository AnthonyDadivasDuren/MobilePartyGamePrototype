using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace PartyPrototype.Editor
{
    public sealed class PartyApkCheck : IPostprocessBuildWithReport
    {
        public int callbackOrder => 1000;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android || !report.summary.outputPath.EndsWith(".apk")) return;
            bool verified = false;
            using (var zip = ZipFile.OpenRead(report.summary.outputPath))
                foreach (var entry in zip.Entries)
                    if (entry.FullName.EndsWith("global-metadata.dat") || entry.FullName.EndsWith("Assembly-CSharp.dll"))
                        using (var stream = entry.Open())
                        using (var memory = new MemoryStream())
                        {
                            stream.CopyTo(memory); var bytes = memory.ToArray();
                            verified |= Encoding.UTF8.GetString(bytes).Contains("HUD 0.8.1") || Encoding.Unicode.GetString(bytes).Contains("HUD 0.8.1");
                        }
            if (!verified) throw new BuildFailedException("APK contains stale or missing HUD code. Switch the active platform to Android in Build Profiles, wait for compilation, then use Party Prototype > Build Android APK again. Do not install this APK.");
            UnityEngine.Debug.Log("APK contents verified: HUD 0.8.1 is present in the compiled game code.");
        }
    }
}
