using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// <see cref="CarDrive.Diagnostics.MegaProfileProbe"/> 를 실을 <b>개발 빌드</b>를 냅니다.
///
/// 에디터 프로파일은 <c>EditorLoop</c> 가 지배해서 정상 상태의 병목을 못 봅니다.
/// 그래서 이 프로젝트의 성능 판단은 <b>스탠드얼론에서만</b> 유효합니다.
///
/// <b>프레임 타이밍을 켭니다.</b> <c>FrameTimingManager</c> 는 플레이어 설정의
/// Frame Timing Stats 가 꺼져 있으면 <b>0 을 돌려주고 아무 말도 하지 않습니다</b> -
/// 그러면 CPU/GPU 바운드를 가르는 유일한 근거가 조용히 사라집니다.
///
/// 원래 설정은 빌드가 끝나면 되돌립니다. 진단하려고 만진 값이 저장소에 남으면
/// 다음 사람이 왜 켜져 있는지 모릅니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod MegaProfileBuild.Run
/// </code>
/// </summary>
public static class MegaProfileBuild
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string OutputDirectory = "Build/Profile";
    private const string Executable = "CarDriveProfile.exe";

    public static void Run()
    {
        int errors = 0;

        bool wasTiming = PlayerSettings.enableFrameTimingStats;

        try
        {
            Directory.CreateDirectory(OutputDirectory);

            PlayerSettings.enableFrameTimingStats = true;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputDirectory + "/" + Executable,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,

                // <b>개발 빌드여야 합니다.</b> ProfilerRecorder 는 개발 빌드가 아니면
                // 이름은 받아 주면서 값은 0 을 돌려줍니다.
                options = BuildOptions.Development | BuildOptions.ConnectWithProfiler,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);

            Debug.Log($"MegaProfileBuild: {report.summary.result} · " +
                      $"{report.summary.totalTime.TotalSeconds:F0} 초 · " +
                      $"{report.summary.totalSize / 1048576} MB · " +
                      $"오류 {report.summary.totalErrors} · 경고 {report.summary.totalWarnings}");

            if (report.summary.result != BuildResult.Succeeded) errors++;
        }
        catch (Exception e)
        {
            Debug.LogError("MegaProfileBuild: " + e);
            errors++;
        }
        finally
        {
            PlayerSettings.enableFrameTimingStats = wasTiming;
        }

        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }
}
