using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 명령줄에서 Windows 빌드를 돌려 결과를 로그로 남깁니다.
/// 에디터를 열지 않고도 "빌드가 실제로 되는지"를 확인하려고 둡니다.
/// </summary>
public static class BuildRunner
{
    /// <summary>
    /// <c>Unity.exe -batchmode -quit -executeMethod BuildRunner.BuildWindows</c>
    /// </summary>
    public static void BuildWindows()
    {
        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = new[] { "Assets/_Project/01.Scenes/SampleScene.unity" };
        options.locationPathName = "Logs/BuildCheck/CarDrive.exe";
        options.target = BuildTarget.StandaloneWindows64;
        options.options = BuildOptions.None;

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;

        Debug.Log("BUILD RESULT: " + s.result +
                  " | 오류 " + s.totalErrors +
                  " | 경고 " + s.totalWarnings +
                  " | 크기 " + (s.totalSize / 1024 / 1024) + " MB" +
                  " | 시간 " + s.totalTime.TotalSeconds.ToString("F1") + "s");

        if (s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
