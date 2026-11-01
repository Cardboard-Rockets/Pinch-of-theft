using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class CodexDeliveryBuild
{
    private const string Key = "CodexDelivery.Stage";
    private const string Output = "Builds/MyGame";
    private static bool busy;

    // Продолжает очередь сборок после переключения платформы и перезагрузки домена.
    static CodexDeliveryBuild() { EditorApplication.update += Tick; }

    // Запускает сборку Web и Windows из текущего списка включённых сцен.
    [MenuItem("Tools/Build Delivery/Build Web and Windows %&b")]
    public static void Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory(Output);
        var profile = BuildProfile.GetActiveBuildProfile();
        SessionState.SetString("CodexDelivery.Profile", profile == null ? "" : AssetDatabase.GetAssetPath(profile));
        SessionState.SetInt("CodexDelivery.Target", (int)EditorUserBuildSettings.activeBuildTarget);
        SessionState.SetInt(Key, 1);
        File.WriteAllText(Output + "/Status.txt", "Queued Web and Windows");
    }

    // Выполняет следующий шаг только после завершения импорта и компиляции.
    private static void Tick()
    {
        int stage = SessionState.GetInt(Key, 0);
        if (stage == 0 || busy || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        busy = true;
        try
        {
            if (stage == 1 || stage == 2)
            {
                BuildTarget target = stage == 1 ? BuildTarget.WebGL : BuildTarget.StandaloneWindows64;
                if (EditorUserBuildSettings.activeBuildTarget != target)
                {
                    BuildProfile.SetActiveBuildProfile(null);
                    File.WriteAllText(Output + "/Status.txt", "Switching to " + target);
                    if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target))
                        throw new Exception("Cannot switch to " + target);
                    return;
                }
                string name = stage == 1 ? "Web" : "Desktop";
                string location = stage == 1 ? Output + "/Web" : Output + "/Desktop/Pinch of theft.exe";
                Directory.CreateDirectory(Output + "/" + name);
                File.WriteAllText(Output + "/Status.txt", "Building " + name);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    target = target,
                    locationPathName = location,
                    options = BuildOptions.None
                });
                string result = $"{name}: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nTime: {report.summary.totalTime}\n";
                File.WriteAllText(Output + "/" + name + "-BuildReport.txt", result);
                if (report.summary.result != BuildResult.Succeeded) throw new Exception(result);
                SessionState.SetInt(Key, stage + 1);
            }
            else
            {
                SessionState.SetInt(Key, 0);
                string profilePath = SessionState.GetString("CodexDelivery.Profile", "");
                File.WriteAllText(Output + "/Status.txt", "Succeeded: Web and Desktop");
                if (!string.IsNullOrEmpty(profilePath)) BuildProfile.SetActiveBuildProfile(AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath));
                else
                {
                    var original = (BuildTarget)SessionState.GetInt("CodexDelivery.Target", (int)BuildTarget.WebGL);
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(original), original);
                }
                Debug.Log("MYGAME_DELIVERY_SUCCEEDED");
            }
        }
        catch (Exception error)
        {
            SessionState.SetInt(Key, 0);
            File.WriteAllText(Output + "/Status.txt", "FAILED: " + error);
            Debug.LogException(error);
        }
        finally { busy = false; }
    }
}
