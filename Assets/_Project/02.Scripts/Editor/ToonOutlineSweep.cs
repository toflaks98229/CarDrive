using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 두께가 0 인 외곽선 패스를 <b>제출에서 뺍니다.</b>
///
/// <see cref="CarDriveToonLit"/> 의 외곽선은 <c>LightMode = SRPDefaultUnlit</c> 인
/// <b>별도 패스</b>입니다. 셰이더는 두께가 0 이면 꼭짓점을 한 점으로 뭉개
/// 아무것도 안 그리는데, 거기 붙은 주석은 그것을 "이 패스를 건너뜁니다" 라고
/// 적고 있었습니다. <b>건너뛰는 것은 래스터화뿐입니다.</b> 드로우 콜과 SetPass 는
/// 그대로 나가고 정점 셰이더도 모든 꼭짓점에서 돕니다.
///
/// 그리고 이 프로젝트의 머티리얼 가운데 외곽선을 <b>켠 것이 하나도 없습니다.</b>
/// 전부 0 이거나 부모(0)를 물려받습니다. 곧 이 셰이더를 쓰는 모든 물체가
/// <b>아무것도 안 그리는 드로우를 한 벌씩 더</b> 내고 있었습니다.
///
/// 실측(메가스트럭처, 데크 한가운데): 드로우 <b>72 → 144</b>. 정확히 두 배입니다.
/// 이 프로젝트의 병목이 드로우 제출이라는 점에서 순손실입니다.
///
/// <b>⚠ 외곽선을 다시 켜려면 이것을 다시 돌려야 합니다.</b> 패스를 껐다는 사실은
/// 머티리얼에 저장되므로(<c>m_DisabledShaderPasses</c>), 인스펙터에서 두께만
/// 올리면 여전히 안 그려집니다. 두께를 값으로 두고 여기서 켜고 끕니다 - 그래야
/// "두께가 0 인데 패스는 켜져 있다" 는 어긋난 상태가 생기지 않습니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod ToonOutlineSweep.Run
/// </code>
/// </summary>
public static class ToonOutlineSweep
{
    /// <summary>외곽선 패스를 가진 셰이더입니다.</summary>
    private const string OutlineShader = "CarDrive/Toon Lit";

    /// <summary>URP 에서 패스를 켜고 끄는 이름은 <b>LightMode 태그</b>입니다.</summary>
    private const string OutlinePass = "SRPDefaultUnlit";

    private const string WidthProperty = "_OutlineWidth";

    public static void Run()
    {
        int errors = 0;

        try
        {
            Material[] materials = AssetDatabase.FindAssets("t:Material")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Material>)
                .Where(m => m != null && m.shader != null && m.shader.name == OutlineShader)
                .OrderBy(m => m.name)
                .ToArray();

            int off = 0;
            int on = 0;

            foreach (Material material in materials)
            {
                // <b>두께가 값의 주인입니다.</b> 패스를 켤지는 여기서 유도합니다.
                bool wanted = material.HasProperty(WidthProperty)
                              && material.GetFloat(WidthProperty) > 0.0001f;

                if (material.GetShaderPassEnabled(OutlinePass) == wanted)
                {
                    if (wanted) on++;
                    continue;
                }

                material.SetShaderPassEnabled(OutlinePass, wanted);
                EditorUtility.SetDirty(material);

                if (wanted) on++;
                else off++;
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"ToonOutlineSweep: {OutlineShader} 를 쓰는 머티리얼 {materials.Length} 개 · " +
                      $"이번에 끈 것 {off} 개 · 외곽선을 실제로 쓰는 것 {on} 개");
        }
        catch (Exception e)
        {
            Debug.LogError("ToonOutlineSweep: " + e);
            errors++;
        }

        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }
}
