using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 프로젝트의 <b>모든 메시가 UV 를 갖고 있는지</b> 한 번에 셉니다.
///
/// <b>왜 필요한가.</b> UV 가 없는 메시는 조용히 망가집니다 — 셰이더가
/// <c>TEXCOORD0</c> 을 읽으면 0 이 들어와 텍스처의 한 픽셀이 면 전체에 늘어나고,
/// 라이트맵을 구우려면 <c>uv2</c> 가 있어야 하는데 없으면 그냥 안 구워집니다.
/// 어느 쪽도 오류를 내지 않아서 "왜 이 벽만 다르지" 로만 드러납니다.
///
/// FBX 는 임포터가 채워 주지만 <b>코드로 만든 메시(.asset)</b> 는 만든 쪽이
/// 넣지 않으면 없습니다. 이 감사는 둘을 갈라서 셉니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod ProjectUvAudit.Run
/// </code>
/// </summary>
public static class ProjectUvAudit
{
    private static readonly string[] Roots = { "Assets/_Project", "Packages" };

    public static void Run()
    {
        List<string> paths = AssetDatabase.FindAssets("t:Mesh t:Model", Roots)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        int meshes = 0;
        int noUv0 = 0;
        int noUv2 = 0;

        List<string> missingUv0 = new List<string>();
        List<string> missingUv2 = new List<string>();

        foreach (string path in paths)
        {
            foreach (Mesh mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
            {
                meshes++;

                bool hasUv0 = mesh.uv != null && mesh.uv.Length == mesh.vertexCount && mesh.vertexCount > 0;
                bool hasUv2 = mesh.uv2 != null && mesh.uv2.Length == mesh.vertexCount && mesh.vertexCount > 0;

                string label = $"{System.IO.Path.GetFileName(path)}/{mesh.name} ({mesh.vertexCount}v)";

                if (!hasUv0)
                {
                    noUv0++;
                    missingUv0.Add(label);
                }

                if (!hasUv2)
                {
                    noUv2++;
                    missingUv2.Add(label);
                }
            }
        }

        Debug.Log($"ProjectUvAudit: 자산 {paths.Count} 개 · 메시 {meshes} 개 · " +
                  $"UV0 없음 {noUv0} · UV2(라이트맵) 없음 {noUv2}");

        foreach (string s in missingUv0.Take(40)) Debug.Log("  UV0 없음: " + s);
        if (missingUv0.Count > 40) Debug.Log($"  ... 외 {missingUv0.Count - 40} 개");

        foreach (string s in missingUv2.Take(15)) Debug.Log("  UV2 없음: " + s);
        if (missingUv2.Count > 15) Debug.Log($"  ... 외 {missingUv2.Count - 15} 개");

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
