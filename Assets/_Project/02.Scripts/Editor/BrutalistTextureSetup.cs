using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 보행 로봇의 콘크리트 머티리얼에 <b>브루탈리즘 콘크리트 맵</b>을 물립니다.
///
/// <b>왜 타일링을 1 로 두는가.</b> 두 기계의 UV 는 이미 <b>월드 스케일</b>입니다
/// (<c>Art/Blender/uv_worldscale.py</c>) — UV 1.0 이 실제 표면 1 m 입니다. 그래서 여기서
/// 타일링을 건드리면 그 규약이 깨집니다. 결의 크기를 바꾸고 싶으면 <c>UV_TILE</c> 을 고쳐
/// 모델을 다시 내보내십시오. 그래야 <b>부품마다 결이 달라지는 일</b>이 생기지 않습니다.
///
/// 맵은 <c>Art/Textures/make_surfaces.py</c> 가 <b>이 게임의 값에서</b> 만들어 냅니다.
/// 받아온 사진이 아니라 처음부터 이 용도로 지은 것입니다 — 고유색 5개,
/// 이 프로젝트의 4x4 Bayer 로 디더, 1 m 로 이어붙고 방향성이 없습니다.
///
/// <c>_BaseMapGain</c> 은 맵의 채널별 평균을 1 로 맞춰 곱셈을 상쇄하고,
/// <c>_BaseMapStrength</c> 가 결의 세기를 정합니다. 이득은 이 스크립트가 텍스처를
/// <b>실제로 읽어</b> 계산하므로 맵을 다시 만들어도 그대로 맞습니다.
///
/// <b>⚠ <c>_BaseColor</c> 는 설계한 명도 그대로 둡니다.</b> 색판 텍스처였다면 흰색으로
/// 덮는 것이 맞지만(<c>art-import-conventions</c>), 이 맵은 <b>결</b>만 싣습니다. 흰색으로
/// 두면 콘크리트·강철·암철이 같은 회색이 되어 명도 대비가 통째로 사라집니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod BrutalistTextureSetup.Run
/// </code>
/// </summary>
public static class BrutalistTextureSetup
{
    // --- Constants ---

    /// <summary>
    /// 결 지도가 있는 곳입니다. 로봇 폴더에 있는 것은 <b>거기서 처음 만들었기 때문</b>이고,
    /// 지금은 건물도 같은 지도를 씁니다 — 같은 콘크리트여야 같은 세계로 보입니다.
    /// </summary>
    private const string MapDir = "Assets/_Project/04.Art/02.Models/Robot";
    private const string MaterialDir = "Assets/_Project/04.Art/00.Materials";

    /// <summary>콘크리트 결입니다. 넓은 얼룩과 거친 골재, 기공.</summary>
    private const string ConcreteMap = "Concrete_Brutalist.png";

    /// <summary>강철 결입니다. 거의 평평하고 고운 결에 드문 부식 자국.</summary>
    private const string SteelMap = "Steel_Plate.png";

    /// <summary>
    /// 결의 세기입니다. 1 이면 사진의 계조를 그대로 씁니다.
    ///
    /// 툰 램프가 명암을 계단으로 끊으므로 1 에서는 밝은 절반이 하얗게 타 <b>대리석</b>으로
    /// 읽힙니다. 인스펙터에서 실시간으로 돌려 보고 정하면 됩니다.
    /// </summary>
    private const float Strength = 0.45f;

    /// <summary>
    /// 머티리얼 이름 → <b>설계한 최종 명도</b>와 그 재질의 결입니다.
    ///
    /// 명도만 다르고 결이 같으면 세 재질이 <b>같은 물질의 밝기 차이</b>로 읽힙니다.
    /// 콘크리트는 부어 만든 것이고 강철은 압연한 판이라, 결이 갈려야 재질이 갈립니다.
    /// </summary>
    private static readonly (string material, Color value, string texture)[] Wiring =
    {
        ("RobotConcrete", new Color(0.60f, 0.59f, 0.55f), ConcreteMap),
        ("RobotSteel", new Color(0.26f, 0.28f, 0.31f), SteelMap),
        ("RobotDark", new Color(0.11f, 0.12f, 0.13f), SteelMap),

        // 메가스트럭처. 로봇과 <b>같은 지도</b>를 씁니다. 콘크리트가 로봇의 엉덩이일 때와
        // 100 m 벽일 때 달라 보이면 두 물건이 같은 세계에 있는 것으로 보이지 않습니다.
        ("MegaConcrete", new Color(0.58f, 0.57f, 0.53f), ConcreteMap),
        ("MegaSteel", new Color(0.26f, 0.28f, 0.31f), SteelMap),
        ("MegaDark", new Color(0.09f, 0.10f, 0.11f), SteelMap),
    };

    // --- Public Methods ---

    public static void Run()
    {
        int errors = 0;
        int wired = 0;

        foreach ((string material, Color value, string texture) in Wiring)
        {
            string texturePath = MapDir + "/" + texture;
            string materialPath = MaterialDir + "/" + material + ".mat";

            Texture2D map = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            Material target = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (map == null)
            {
                Debug.LogError("BrutalistTextureSetup: 텍스처가 없습니다: " + texturePath);
                errors++;
                continue;
            }

            if (target == null)
            {
                Debug.LogError("BrutalistTextureSetup: 머티리얼이 없습니다: " + materialPath);
                errors++;
                continue;
            }

            Configure(texturePath);

            Color gain = MeasureGain(texturePath);

            target.SetTexture("_BaseMap", map);
            target.SetTextureScale("_BaseMap", Vector2.one);
            target.SetTextureOffset("_BaseMap", Vector2.zero);
            target.SetColor("_BaseMapGain", gain);
            target.SetFloat("_BaseMapStrength", Strength);

            // 결은 <b>옵트인</b>입니다. 켜지 않은 머티리얼은 그 코드를 컴파일하지도 않습니다.
            target.SetFloat("_UseGrain", 1f);
            target.EnableKeyword("_GRAIN_ON");

            // 설계한 명도 그대로. 이득이 맵의 평균을 1 로 만들어 두므로 나눌 것이 없습니다.
            target.SetColor("_BaseColor", value);
            target.SetColor("_Color", value);

            EditorUtility.SetDirty(target);
            wired++;

            Debug.Log($"BrutalistTextureSetup: {material} ← {texture} · 이득 {gain.r:F2},{gain.g:F2},{gain.b:F2} · 세기 {Strength:F2}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"BrutalistTextureSetup: {wired} 개 연결, 실패 {errors} 개");

        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }

    // --- Private Methods ---

    /// <summary>
    /// 맵의 <b>채널별 선형 평균의 역수</b>를 냅니다. 이것을 곱하면 맵의 평균이 1 이 되어
    /// 사진의 색조와 어둠이 상쇄되고, 남는 것은 결뿐입니다.
    ///
    /// 임포트 설정을 건드리지 않으려고 파일을 직접 디코드합니다. 임포터의 <c>isReadable</c>
    /// 을 켜면 런타임 메모리가 두 배가 되고, 껐다 켜느라 재임포트를 두 번 하게 됩니다.
    /// </summary>
    private static Color MeasureGain(string path)
    {
        Texture2D probe = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);

        if (!probe.LoadImage(System.IO.File.ReadAllBytes(path)))
        {
            UnityEngine.Object.DestroyImmediate(probe);
            Debug.LogWarning("BrutalistTextureSetup: 텍스처를 읽지 못해 이득을 1 로 둡니다: " + path);
            return Color.white;
        }

        Color[] pixels = probe.GetPixels();
        double r = 0.0, g = 0.0, b = 0.0;

        foreach (Color p in pixels)
        {
            r += ToLinear(p.r);
            g += ToLinear(p.g);
            b += ToLinear(p.b);
        }

        UnityEngine.Object.DestroyImmediate(probe);

        int n = Mathf.Max(pixels.Length, 1);
        return new Color((float)(n / System.Math.Max(r, 1e-6)),
                         (float)(n / System.Math.Max(g, 1e-6)),
                         (float)(n / System.Math.Max(b, 1e-6)), 1f);
    }

    /// <summary>sRGB 한 채널을 선형으로 옮깁니다. 평균은 선형에서 내야 뜻이 있습니다.</summary>
    private static double ToLinear(float c)
    {
        return c <= 0.04045f ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    /// 임포트 설정입니다. 이 맵은 <b>색판 규약 쪽</b>입니다 — 고유색 8개짜리라
    /// <c>Point</c> 필터에 무압축이어야 계단이 뭉개지지 않습니다.
    ///
    /// <b>다만 밉맵은 켭니다.</b> 색판은 정해진 크기로 한 번 붙지만 이 맵은 월드 스케일로
    /// <b>멀리까지 이어붙으므로</b>, 밉이 없으면 거리에서 디더 무늬가 지글거립니다.
    /// 가까이서는 Point 의 각이 살고 멀리서는 밉이 받아 줍니다.
    /// </summary>
    private static void Configure(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Repeat;   // 월드 스케일 UV 는 1.0 을 넘어갑니다
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = true;
        importer.streamingMipmaps = false;
        importer.maxTextureSize = 256;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = false;

        importer.SaveAndReimport();
    }
}
