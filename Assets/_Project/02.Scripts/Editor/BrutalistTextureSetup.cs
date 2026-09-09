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
    // 표면 결은 로봇만의 것이 아니라 <b>이 세계 전체가 쓰는 것</b>입니다.
    // 로봇 모델 폴더에 있던 것을 옮겼습니다.
    private const string MapDir = "Assets/_Project/04.Art/01.Images/Surfaces";
    private const string MaterialDir = "Assets/_Project/04.Art/00.Materials";

    /// <summary>콘크리트 결입니다. 넓은 얼룩과 거친 골재, 기공.</summary>
    /// <summary>
    /// 손으로 만든 색판형 결입니다. 256 x 256 에 고유색 여덟 개.
    ///
    /// <b>로봇은 이것으로 맞춰져 있습니다.</b> 사진 결로 갈아 끼우면 툰 램프가 끊는
    /// 계단이 달라지므로, 이미 조율이 끝난 쪽은 건드리지 않습니다.
    /// </summary>
    private const string ConcreteMap = "Concrete_Brutalist.png";

    /// <summary>강철 결입니다. 거의 평평하고 고운 결에 드문 부식 자국.</summary>
    private const string SteelMap = "Steel_Plate.png";

    /// <summary>
    /// <b>CC0 사진 결.</b> Poly Haven 에서 받은 1K diffuse 입니다(권리 포기).
    ///
    /// 셰이더가 이 맵을 <b>결로만</b> 씁니다 - <c>_BaseMapGain</c> 이 채널마다
    /// 평균을 1 로 맞추므로 <b>사진의 색은 지워지고 무늬만 남습니다.</b> 그래서
    /// 사진을 써도 설계한 명도가 그대로 유지됩니다.
    ///
    /// 손으로 만든 것 대신 이것을 쓰는 이유는 <b>1~3 m 짜리 자</b>입니다. 256 짜리
    /// 색판 결은 가까이서 보면 같은 무늬가 반복되고, 그 반복은 크기를 재 주지
    /// 못합니다. 사진에는 되풀이되지 않는 얼룩과 이음매가 들어 있습니다.
    /// </summary>
    private const string PhotoConcrete = "CC0_Concrete_1K.jpg";

    private const string PhotoAsphalt = "CC0_Asphalt_1K.jpg";
    private const string PhotoMetal = "CC0_MetalPlate_1K.jpg";

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
    /// <summary>
    /// 이 세계의 표면 하나입니다.
    ///
    /// <b>색의 주인은 여기뿐입니다.</b> 같은 표가 세 스크립트에 흩어져 있었고
    /// (여기 · MegastructureSetup · PropMeshSetup), 셋이 전부 머티리얼에 색을 써서
    /// <b>나중에 도는 쪽이 이겼습니다.</b> PropMeshSetup 의 표만 1군 이전 값에
    /// 멈춰 있어서, 그것을 마지막에 돌린 날에는 <b>건물 안 방만 옛 콘크리트 색</b>이
    /// 되었습니다. 화면만 보면 방이 왜 다른 색인지 알 방법이 없습니다.
    ///
    /// 치수를 만든 쪽이 적고 쓰는 쪽이 읽듯이, 색도 한 곳이 적고 나머지가 읽습니다.
    /// 그러면 실행 순서가 결과를 바꾸지 못합니다.
    /// </summary>
    public readonly struct Surface
    {
        /// <summary>FBX 안의 머티리얼 이름입니다. 모델에서 안 오는 것은 <c>null</c>.</summary>
        public readonly string Fbx;

        /// <summary>프로젝트 머티리얼 이름입니다.</summary>
        public readonly string Asset;

        /// <summary>설계한 명도. <c>null</c> 이면 <b>빌려 쓰는 것</b>이라 손대지 않습니다.</summary>
        public readonly Color? Value;

        /// <summary>결 텍스처 파일 이름입니다. <c>null</c> 이면 결을 물리지 않습니다.</summary>
        public readonly string Texture;

        /// <summary>발광색. 검으면 발광하지 않습니다.</summary>
        public readonly Color Glow;

        /// <summary>안개를 먹는 정도(1 이 보통)입니다.</summary>
        public readonly float FogScale;

        /// <summary>
        /// 결 한 장이 <b>몇 미터를 덮는가</b>의 역수입니다. 1 이면 1 m 마다 한 장.
        ///
        /// UV 가 월드 스케일(1.0 = 1 m)이라 기본값 1 은 <b>1 m 마다 1024 장짜리
        /// 사진</b>을 까는 셈입니다. 그 정도로 잘면 밉맵이 전부 뭉개 평평한 회색이
        /// 되고, 결은 아무것도 재 주지 못합니다. 콘크리트 판의 얼룩과 거푸집 자국이
        /// <b>실제 크기</b>로 보여야 1~3 m 짜리 자가 됩니다.
        /// </summary>
        public readonly float Tile;

        public Surface(string fbx, string asset, Color? value, string texture,
                       Color glow = default, float fogScale = 1f, float tile = 1f)
        {
            Fbx = fbx;
            Asset = asset;
            Value = value;
            Texture = texture;
            Glow = glow;
            FogScale = fogScale;
            Tile = tile;
        }
    }

    /// <summary>
    /// <b>메가스트럭처가 안개를 먹는 정도.</b> 실측: 안개는 257 m 에서 완전히 닫히고
    /// 파클립은 482 m 라, 그 사이는 그리는데 안 보이는 구간입니다. 438 m 짜리 지표는
    /// 거기서 사라지면 안 됩니다. 안개를 전역으로 늘리면 안개가 감추던 것(231 m 의
    /// 나무 팝, 252 m 의 지형 경계)이 드러나므로, 이 구조물에만 덜 먹입니다.
    /// </summary>
    private const float MegaFog = 0.55f;

    /// <summary>이 세계의 표면 전부입니다. 로봇도 건물도 방도 같은 콘크리트를 씁니다.</summary>
    public static readonly Surface[] Palette =
    {
        // 로봇 것은 <b>FBX 이름으로 찾는 곳이 없습니다.</b> 아는 척 적어 두면
        // 나중에 그 이름으로 찾는 코드가 조용히 엉뚱한 것을 물립니다.
        new Surface(null, "RobotConcrete", new Color(0.60f, 0.59f, 0.55f), ConcreteMap),
        new Surface(null, "RobotSteel", new Color(0.26f, 0.28f, 0.31f), SteelMap),
        new Surface(null, "RobotDark", new Color(0.11f, 0.12f, 0.13f), SteelMap),

        // 메가스트럭처는 <b>사진 결</b>을 씁니다. 건물 안 방과 집도 같은 것을
        // 쓰므로(FBX 가 같은 머티리얼 이름을 냅니다) 한 번에 맞춰집니다.
        // 콘크리트 사진은 실제로 폭 2 m 남짓한 벽입니다. 0.5 로 깔면 그 크기로
        // 앉아, 거푸집 자국 하나가 곧 <b>사람 키의 절반</b>이 됩니다.
        new Surface("M_Mega_Concrete", "MegaConcrete", new Color(0.66f, 0.65f, 0.61f),
                    PhotoConcrete, default, MegaFog, 0.5f),
        new Surface("M_Mega_Steel", "MegaSteel", new Color(0.22f, 0.235f, 0.26f),
                    PhotoMetal, default, MegaFog),
        new Surface("M_Mega_Dark", "MegaDark", new Color(0.052f, 0.058f, 0.064f),
                    PhotoMetal, default, MegaFog),
        new Surface("M_Mega_Signal", "MegaSignal", new Color(0.86f, 0.36f, 0.09f),
                    PhotoMetal, new Color(0.52f, 0.19f, 0.035f), MegaFog),
        // 아스팔트도 2 m. 차선 파선이 4 m 이므로 그 절반이 결의 단위가 됩니다.
        new Surface("M_Mega_Road", "MegaRoad", new Color(0.135f, 0.140f, 0.150f),
                    PhotoAsphalt, default, MegaFog, 0.5f),

        // 빌려 쓰는 것. 색을 여기서 정하지 않습니다.
        new Surface("M_Rock", "RockLowPoly", null, null),
    };

    /// <summary>
    /// 머티리얼 하나에 <b>팔레트가 정한 전부</b>를 씁니다.
    ///
    /// 색·결·발광·안개를 여기 한 군데서 칠하므로, 누가 언제 부르든 같은 결과가
    /// 나옵니다. 만드는 쪽(MegastructureSetup·PropMeshSetup)도 이것을 부릅니다.
    /// </summary>
    public static void Paint(Material target, Surface surface, Texture2D map, Color gain)
    {
        if (surface.Value == null) return;

        if (map != null)
        {
            target.SetTexture("_BaseMap", map);
            target.SetTextureScale("_BaseMap", Vector2.one * surface.Tile);
            target.SetTextureOffset("_BaseMap", Vector2.zero);
            target.SetColor("_BaseMapGain", gain);
            target.SetFloat("_BaseMapStrength", Strength);

            // 결은 <b>옵트인</b>입니다. 켜지 않은 머티리얼은 그 코드를 컴파일하지도 않습니다.
            target.SetFloat("_UseGrain", 1f);
            target.EnableKeyword("_GRAIN_ON");
        }

        // 설계한 명도 그대로. 이득이 맵의 평균을 1 로 만들어 두므로 나눌 것이 없습니다.
        target.SetColor("_BaseColor", surface.Value.Value);
        target.SetColor("_Color", surface.Value.Value);
        target.SetFloat("_FogScale", surface.FogScale);

        // <b>발광은 키워드가 켜져야 합니다.</b> 색만 넣으면 조용히 무시됩니다.
        target.SetColor("_EmissionColor", surface.Glow);
        target.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

        if (surface.Glow.maxColorComponent > 0.001f) target.EnableKeyword("_EMISSION");
        else target.DisableKeyword("_EMISSION");

        target.enableInstancing = true;
        EditorUtility.SetDirty(target);
    }

    /// <summary>결 텍스처와 그 이득을 읽어 옵니다. 없으면 둘 다 <c>null</c>/흰색입니다.</summary>
    public static bool LoadMap(string texture, out Texture2D map, out Color gain)
    {
        map = null;
        gain = Color.white;

        if (string.IsNullOrEmpty(texture)) return true;

        string path = MapDir + "/" + texture;
        map = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        if (map == null) return false;

        Configure(path);
        gain = MeasureGain(path);
        return true;
    }

    public static void Run()
    {
        int errors = 0;
        int wired = 0;

        foreach (Surface surface in Palette)
        {
            if (surface.Value == null) continue;

            string materialPath = MaterialDir + "/" + surface.Asset + ".mat";
            Material target = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (target == null)
            {
                Debug.LogError("BrutalistTextureSetup: 머티리얼이 없습니다: " + materialPath);
                errors++;
                continue;
            }

            if (!LoadMap(surface.Texture, out Texture2D map, out Color gain))
            {
                Debug.LogError("BrutalistTextureSetup: 텍스처가 없습니다: " + surface.Texture);
                errors++;
                continue;
            }

            Paint(target, surface, map, gain);
            wired++;

            Debug.Log($"BrutalistTextureSetup: {surface.Asset} ← {surface.Texture ?? "(결 없음)"} · " +
                      $"이득 {gain.r:F2} · 세기 {Strength:F2} · 안개 {surface.FogScale:F2}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"BrutalistTextureSetup: {wired} 개 연결, 실패 {errors} 개");

        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }

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

    /// <summary>sRGB 값을 선형으로. 평균은 <b>선형에서</b> 내야 맞습니다.</summary>
    private static double ToLinear(float c)
    {
        return c <= 0.04045f ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static void Configure(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        // <b>결의 성격이 둘입니다.</b> 손으로 만든 색판형 맵은 고유색이 여덟 개뿐이라
        // Point 에 무압축이어야 계단이 뭉개지지 않습니다. 사진 결은 반대로, Point 로
        // 두면 가까이서 화소가 각지고 압축을 안 하면 1K 한 장이 4 MB 를 먹습니다.
        //
        // 확장자로 가릅니다 - 이 폴더에서 <c>.jpg</c> 는 사진(CC0), <c>.png</c> 는
        // 손으로 만든 것입니다. LICENSE.md 가 어느 쪽이 무엇인지 적어 둡니다.
        bool photo = path.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase);

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Repeat;   // 월드 스케일 UV 는 1.0 을 넘어갑니다
        importer.filterMode = photo ? FilterMode.Bilinear : FilterMode.Point;
        importer.mipmapEnabled = true;
        importer.streamingMipmaps = false;
        importer.maxTextureSize = photo ? 1024 : 256;
        importer.textureCompression = photo
            ? TextureImporterCompression.Compressed
            : TextureImporterCompression.Uncompressed;
        importer.isReadable = false;

        importer.SaveAndReimport();
    }
}
