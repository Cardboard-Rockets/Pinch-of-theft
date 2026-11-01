using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;
using UnityEngine;

[InitializeOnLoad]
public static class CodexDeliveryInspect
{
    // Подключает однократное извлечение диагностических сообщений после сборки.
    static CodexDeliveryInspect() { EditorApplication.update += Inspect; }

    // Сохраняет сообщения двух отчётов без вывода полного журнала редактора.
    private static void Inspect()
    {
        const string root = "Builds/MyGame/";
        if (!File.Exists(root + "Status.txt") || !File.ReadAllText(root + "Status.txt").StartsWith("Succeeded")) return;
        EditorApplication.update -= Inspect;
        var web = InternalEditorUtility.LoadSerializedFileAndForget("Temp/DeliveryReports/Web.buildreport").OfType<BuildReport>().FirstOrDefault();
        var desktop = BuildReport.GetLatestReport();
        Save(web, root + "Web-Diagnostics.txt");
        Save(desktop, root + "Desktop-Diagnostics.txt");
    }

    // Записывает только предупреждения и ошибки указанного отчёта.
    private static void Save(BuildReport report, string path)
    {
        if (report == null) { File.WriteAllText(path, "Report unavailable"); return; }
        File.WriteAllLines(path, new[] { report.summary.platform + ": " + report.summary.result }.Concat(
            report.steps.SelectMany(step => step.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception || m.type == LogType.Warning)
                .Select(m => m.type + ": " + m.content)));
    }
}
