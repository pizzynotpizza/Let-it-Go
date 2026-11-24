using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

// After a Windows build, copies a portable Python with MediaPipe and
// "Start Hand Tracker.bat" next to the game, so the build plays with the hand
// on a computer without Python. The work is done by Tools/bundle_hand_tracker.py.
public class HandTrackerBuildBundle : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        var target = report.summary.platform;
        if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneWindows) return;

        string project = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
        string script = Path.Combine(project, "Tools", "bundle_hand_tracker.py");
        string python = Path.Combine(project, "HandTrackerEnv", "Scripts", "python.exe");
        string buildDir = Path.GetDirectoryName(report.summary.outputPath);
        if (!File.Exists(python))
        {
            UnityEngine.Debug.LogError("[HandTrackerBuildBundle] HandTrackerEnv missing. Run 'Start Hand Tracker.bat' in the project once, then build again.");
            return;
        }

        var psi = new ProcessStartInfo(python, $"\"{script}\" \"{buildDir}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = project,
        };
        using (var p = Process.Start(psi))
        {
            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0)
                UnityEngine.Debug.LogError("[HandTrackerBuildBundle] Bundle failed:\n" + error + output);
            else
                UnityEngine.Debug.Log(output.Trim());
        }
    }
}
