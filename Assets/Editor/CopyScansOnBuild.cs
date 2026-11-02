using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Liefert die Kopf-Scans mit dem Spiel aus:
// Kopiert nach jedem Build den Ordner "Scans" aus dem Projektordner (neben "Assets") neben die .exe.
// Im Spiel zeigt "scansRoot": "Scans" (Standard in HumanTetris_config.json) dann auf genau diesen Ordner.
//
// Vom Spiel erzeugte Dateien (Highscores, Bestenliste, Aufnahmen aus Tests im Editor) werden NICHT mitkopiert,
// damit jede ausgelieferte Version sauber startet.
public class CopyScansOnBuild : IPostprocessBuildWithReport
{
    public const string FolderName = "Scans";

    private static readonly string[] SkipFiles =
    {
        "highscore.json", "leaderboard.json", "sample.wav"
    };

    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        string projectFolder = Directory.GetParent(Application.dataPath).FullName;
        string source = Path.Combine(projectFolder, FolderName);
        if (!Directory.Exists(source))
        {
            Debug.LogWarning("CopyScansOnBuild: Kein Ordner '" + FolderName + "' im Projekt gefunden, es werden keine Scans mitgeliefert.");
            return;
        }

        // Bei Windows-Builds ist outputPath die .exe, bei manchen Plattformen ein Ordner
        string outputPath = report.summary.outputPath;
        string buildFolder = Directory.Exists(outputPath) ? outputPath : Path.GetDirectoryName(outputPath);
        string target = Path.Combine(buildFolder, FolderName);

        int copied = CopyDirectory(source, target);
        Debug.Log("CopyScansOnBuild: " + copied + " Dateien nach " + target + " kopiert.");
    }

    private static int CopyDirectory(string source, string target)
    {
        int count = 0;
        Directory.CreateDirectory(target);

        foreach (string file in Directory.GetFiles(source))
        {
            string name = Path.GetFileName(file);
            if (name.EndsWith(".tmp") || System.Array.IndexOf(SkipFiles, name) >= 0) continue;

            string dest = Path.Combine(target, name);
            // Nur kopieren, wenn neu oder geändert (spart Zeit bei großen GLBs)
            if (File.Exists(dest) && new FileInfo(dest).Length == new FileInfo(file).Length
                && File.GetLastWriteTimeUtc(dest) >= File.GetLastWriteTimeUtc(file))
                continue;

            File.Copy(file, dest, true);
            count++;
        }

        foreach (string dir in Directory.GetDirectories(source))
            count += CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));

        return count;
    }
}
