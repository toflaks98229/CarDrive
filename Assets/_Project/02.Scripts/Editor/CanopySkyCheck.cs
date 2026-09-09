using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 구운 파노라마와 <b>유니티가 실제로 들고 있는 큐브맵</b>을 견줍니다.
///
/// <b>왜 필요했는가.</b> 블렌더가 쓴 .hdr 의 천장은 선형 0.005 인데 게임 화면에서
/// 같은 자리가 0.498 로 나왔습니다 — 96 배입니다. 임포트 설정은 이미 잘 도는
/// 하늘 사진(<c>ClearBlueSky_Kloofendal.hdr</c>)과 한 바이트도 다르지 않았고,
/// 셰이더의 <c>unity_ColorSpaceDouble</c> 는 2 배뿐이라 설명이 안 됩니다.
///
/// 파일과 화면 사이에는 <b>임포트</b>와 <b>렌더</b> 두 단계가 있습니다. 텍셀을 직접
/// 읽으면 둘 중 어디서 벌어진 일인지 한 번에 갈립니다. 추측을 하나 더 쌓는 대신
/// 물어보는 편이 빠릅니다.
///
/// <code>
/// Unity.exe -batchmode -projectPath . -executeMethod CanopySkyCheck.Run -quit
/// </code>
/// </summary>
public static class CanopySkyCheck
{
    private const string Baked =
        "Assets/_Project/04.Art/01.Images/Skybox/CanopySky_Baked.hdr";

    private const string Known =
        "Assets/_Project/04.Art/01.Images/Skybox/ClearBlueSky_Kloofendal.hdr";

    public static void Run()
    {
        int errors = 0;

        try
        {
            Report(Baked);
            Report(Known);
        }
        catch (Exception e)
        {
            Debug.LogError("CanopySkyCheck: " + e);
            errors++;
        }

        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }

    /// <summary>
    /// 큐브맵 한 장의 위쪽 면(+Y, 곧 <b>머리 바로 위</b>)을 재서 남깁니다.
    ///
    /// <c>GetPixels</c> 는 읽기 가능해야 돌아가므로 잠시 켰다가 되돌립니다.
    /// 진단하려고 만진 값이 저장소에 남으면 다음 사람이 왜 켜져 있는지 모릅니다.
    /// </summary>
    private static void Report(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null)
        {
            Debug.LogError("CanopySkyCheck: 임포터 없음 — " + path);
            return;
        }

        bool wasReadable = importer.isReadable;

        try
        {
            importer.isReadable = true;
            importer.SaveAndReimport();

            Cubemap cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);

            if (cube == null)
            {
                Debug.LogError("CanopySkyCheck: 큐브맵이 아닙니다 — " + path);
                return;
            }

            float[] up = cube.GetPixels(CubemapFace.PositiveY)
                .Select(c => c.r)
                .OrderBy(v => v)
                .ToArray();

            int n = up.Length;

            Debug.Log(string.Format(
                "CanopySkyCheck: {0}\n" +
                "  형식 {1} · {2}x{2} · 밉 {3} · sRGB플래그 {4}\n" +
                "  위쪽 면 R 채널 — 최저 {5:F4} · 25% {6:F4} · 중앙 {7:F4} · " +
                "75% {8:F4} · 최고 {9:F4}",
                path.Substring(path.LastIndexOf('/') + 1),
                cube.format, cube.width, cube.mipmapCount, importer.sRGBTexture,
                up[0], up[n / 4], up[n / 2], up[3 * n / 4], up[n - 1]));
        }
        finally
        {
            importer.isReadable = wasReadable;
            importer.SaveAndReimport();
        }
    }
}
