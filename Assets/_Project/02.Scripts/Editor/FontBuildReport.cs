using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 빌드를 한 번 돌리고 <b>폰트가 실제로 얼마를 차지했는지</b>만 뽑아 봅니다.
    ///
    /// <b>왜 필요한가.</b> 폰트 아틀라스를 Static 에서 Dynamic 으로 바꾸면
    /// 에디터의 <c>.asset</c> 파일은 눈에 띄게 줄지만, 그것이 곧 빌드 크기는 아닙니다.
    /// Dynamic 은 아틀라스를 버리는 대신 <b>원본 TTF 를 빌드에 넣습니다.</b>
    /// 그 맞바꿈이 실제로 일어났는지는 <see cref="BuildReport.packedAssets"/> 로만 확인됩니다.
    /// (<c>BuildRunner</c> 는 총합만 찍으므로 여기서 따로 뜯어봅니다.)
    ///
    /// 결과물은 <c>Logs/BuildCheck</c> 에 떨어지고 이 폴더는 .gitignore 대상입니다.
    /// </summary>
    public static class FontBuildReport
    {
        /// <summary>
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.FontBuildReport.BuildAndReportFonts</c>
        /// </summary>
        [MenuItem("CarDrive/UI/빌드해서 폰트 차지 용량 재기")]
        public static void BuildAndReportFonts()
        {
            BuildPlayerOptions options = new BuildPlayerOptions();
            options.scenes = new[] { "Assets/_Project/01.Scenes/SampleScene.unity" };
            options.locationPathName = "Logs/BuildCheck/CarDrive.exe";
            options.target = BuildTarget.StandaloneWindows64;
            options.options = BuildOptions.None;

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;

            StringBuilder sb = new StringBuilder();
            sb.Append("[빌드 결과] ").Append(s.result)
              .Append(" | 오류 ").Append(s.totalErrors)
              .Append(" | 경고 ").Append(s.totalWarnings)
              .Append(" | 총 크기 ").Append((s.totalSize / 1048576f).ToString("F1")).Append(" MB")
              .Append(" | 시간 ").Append(s.totalTime.TotalSeconds.ToString("F1")).Append("s\n");

            if (s.result != BuildResult.Succeeded)
            {
                Debug.LogError(sb.ToString());
                EditorApplication.Exit(1);
                return;
            }

            // 폰트와 관련된 항목만 골라 큰 것부터 나열합니다.
            List<PackedAssetInfo> fonts = new List<PackedAssetInfo>();
            ulong grandTotal = 0;

            PackedAssets[] packs = report.packedAssets;
            for (int i = 0; i < packs.Length; i++)
            {
                PackedAssetInfo[] contents = packs[i].contents;
                for (int j = 0; j < contents.Length; j++)
                {
                    PackedAssetInfo info = contents[j];
                    grandTotal += info.packedSize;

                    string p = info.sourceAssetPath;
                    if (string.IsNullOrEmpty(p)) continue;

                    if (p.EndsWith(".ttf") || p.EndsWith(".otf")
                        || p.Contains("TextMesh Pro") || p.Contains("SDF"))
                    {
                        fonts.Add(info);
                    }
                }
            }

            fonts.Sort((a, b) => b.packedSize.CompareTo(a.packedSize));

            sb.Append("빌드에 실린 모든 오브젝트 합계 : ")
              .Append((grandTotal / 1048576f).ToString("F1")).Append(" MB\n");
            sb.Append("폰트 관련 항목 (큰 것부터, 상위 20개)\n");

            ulong fontTotal = 0;
            for (int i = 0; i < fonts.Count; i++)
            {
                fontTotal += fonts[i].packedSize;
                if (i >= 20) continue;

                sb.Append("  ").Append((fonts[i].packedSize / 1024f).ToString("F0").PadLeft(9))
                  .Append(" KB  ").Append(fonts[i].type.Name.PadRight(14))
                  .Append(fonts[i].sourceAssetPath).Append('\n');
            }

            sb.Append("폰트 관련 합계 : ").Append(fonts.Count).Append("개, ")
              .Append((fontTotal / 1048576f).ToString("F2")).Append(" MB");

            Debug.Log(sb.ToString());
        }
    }
}
