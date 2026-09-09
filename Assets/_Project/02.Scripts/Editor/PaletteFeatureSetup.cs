using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 화면 전체 색 수 제한을 <b>붙였다 뗐다</b> 합니다.
///
/// URP 가 들고 있는 <c>FullScreenPassRendererFeature</c> 를 씁니다 — 렌더 패스를 직접
/// 쓰지 않아도 되고, 이 프로젝트의 첫 렌더 피처라 <b>되돌리기 쉬운 쪽</b>을 골랐습니다.
/// <see cref="Remove"/> 한 번이면 흔적이 남지 않습니다.
///
/// 세 등급(Balanced · HighFidelity · Performant)의 렌더러에 모두 답니다. 하나만 달면
/// 품질을 바꾸는 순간 룩이 달라져서, 그것이 버그로 보입니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod PaletteFeatureSetup.Add
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod PaletteFeatureSetup.Remove
/// </code>
/// </summary>
public static class PaletteFeatureSetup
{
    // --- Constants ---

    private const string FeatureName = "CarDrive Palette";
    private const string ShaderPath = "Assets/_Project/04.Art/03.Shaders/Post/CarDrivePalette.shader";
    private const string MaterialPath = "Assets/_Project/04.Art/00.Materials/PostPalette.mat";

    private static readonly string[] Renderers =
    {
        "Assets/_Project/07.Settings/URP-Balanced-Renderer.asset",
        "Assets/_Project/07.Settings/URP-HighFidelity-Renderer.asset",
        "Assets/_Project/07.Settings/URP-Performant-Renderer.asset",
    };

    // --- Public Methods ---

    public static void Add()
    {
        Material material = EnsureMaterial();
        if (material == null)
        {
            Finish(1);
            return;
        }

        int added = 0;

        foreach (string path in Renderers)
        {
            UniversalRendererData data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null)
            {
                Debug.LogError("PaletteFeatureSetup: 렌더러를 찾지 못했습니다: " + path);
                continue;
            }

            if (Find(data) != null)
            {
                Debug.Log("PaletteFeatureSetup: 이미 붙어 있습니다: " + path);
                continue;
            }

            FullScreenPassRendererFeature feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = FeatureName;
            feature.passMaterial = material;

            // 그레이딩·블룸이 끝난 <b>뒤</b>에 자릅니다. 앞에서 자르면 톤매핑이 계단을 도로
            // 뭉개 색 수를 줄인 뜻이 사라집니다.
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.requirements = ScriptableRenderPassInput.None;

            data.rendererFeatures.Add(feature);
            AssetDatabase.AddObjectToAsset(feature, data);
            data.SetDirty();
            EditorUtility.SetDirty(data);
            added++;

            Debug.Log("PaletteFeatureSetup: 붙임 — " + path);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"PaletteFeatureSetup: 렌더러 {added} 개에 붙였습니다");

        Finish(0);
    }

    public static void Remove()
    {
        int removed = 0;

        foreach (string path in Renderers)
        {
            UniversalRendererData data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null) continue;

            ScriptableRendererFeature feature = Find(data);
            if (feature == null) continue;

            data.rendererFeatures.Remove(feature);
            AssetDatabase.RemoveObjectFromAsset(feature);
            Object.DestroyImmediate(feature, true);
            data.SetDirty();
            EditorUtility.SetDirty(data);
            removed++;

            Debug.Log("PaletteFeatureSetup: 뗌 — " + path);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"PaletteFeatureSetup: 렌더러 {removed} 개에서 뗐습니다");

        Finish(0);
    }

    /// <summary>세기를 환경변수로 돌려 봅니다. 값을 정하기 전에 여러 벌 찍어 보려고 둡니다.</summary>
    public static void Tune()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Debug.LogError("PaletteFeatureSetup: 머티리얼이 없습니다: " + MaterialPath);
            Finish(2);
            return;
        }

        // ⚠ <b>셰이더부터 확인합니다.</b> 재질이 어느 시점에 서드파티
        // <c>PixelizePalette</c> 로 바뀌어 있었는데, 그쪽은 <c>multi_compile</c> 의
        // 방법 키워드가 재질에 안 걸려 있어 <b>디더 분기가 아예 컴파일되지
        // 않았습니다</b> — <c>_DitherStrength 0.8</c> 이 적혀 있는데 그 값을 읽는
        // 코드가 없는 상태였습니다. 게다가 자기 베이어 행렬을 따로 써서, 켜졌더라도
        // 화면의 무늬가 메시들의 <c>CarDriveDither.hlsl</c> 과 두 종류로 갈립니다.
        Shader want = Shader.Find("CarDrive/Post/Palette");

        if (want != null && material.shader != want)
        {
            Debug.Log("PaletteFeatureSetup: 셰이더를 되돌립니다 — " +
                      material.shader.name + " → " + want.name);
            material.shader = want;
        }

        material.SetFloat("_Levels", Knob("PALETTE_LEVELS", material.GetFloat("_Levels")));
        material.SetFloat("_Strength", Knob("PALETTE_STRENGTH", material.GetFloat("_Strength")));
        material.SetFloat("_DitherStrength", Knob("PALETTE_DITHER", material.GetFloat("_DitherStrength")));
        material.SetFloat("_Desaturate", Knob("PALETTE_DESAT", material.GetFloat("_Desaturate")));
        material.SetFloat("_DitherPixel", Knob("PALETTE_PIXEL", material.GetFloat("_DitherPixel")));

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        Debug.Log($"PaletteFeatureSetup: 단계 {material.GetFloat("_Levels"):F0} · 세기 {material.GetFloat("_Strength"):F2} · " +
                  $"디더 {material.GetFloat("_DitherStrength"):F2} · 채도빼기 {material.GetFloat("_Desaturate"):F2} · " +
                  $"디더칸 {material.GetFloat("_DitherPixel"):F0} px · 셰이더 {material.shader.name}");

        Finish(0);
    }

    private static float Knob(string name, float fallback)
    {
        string raw = System.Environment.GetEnvironmentVariable(name);
        return float.TryParse(raw, out float value) ? value : fallback;
    }

    // --- Private Methods ---

    private static ScriptableRendererFeature Find(UniversalRendererData data)
    {
        return data.rendererFeatures.FirstOrDefault(f => f != null && f.name == FeatureName);
    }

    private static Material EnsureMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null) return material;

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            Debug.LogError("PaletteFeatureSetup: 셰이더를 찾지 못했습니다: " + ShaderPath);
            return null;
        }

        material = new Material(shader);
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();

        Debug.Log("PaletteFeatureSetup: 머티리얼을 만들었습니다: " + MaterialPath);
        return material;
    }

    private static void Finish(int code)
    {
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }
}
