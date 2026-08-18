using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 지면과 도로에 쓸 픽셀 텍스처와 터레인 레이어를 만들어 냅니다.
///
/// 왜 만들어 쓰는가:
/// 이 게임은 화면을 <b>세로 215픽셀</b>로 줄여 출력합니다(PixelizeFeature).
/// 그 해상도에서는 4K 포토리얼 텍스처의 디테일이 전부 뭉개져 사라지고,
/// 노멀맵의 요철도 보이지 않습니다. 저장소에 있는 TerrainSampleAssets는
/// 1.9GB짜리 4K PBR 세트라 이 화면에는 낭비이자 어울리지 않습니다.
///
/// 그래서 기존 아트와 같은 규칙으로 맞춘 작은 텍스처를 만듭니다.
/// (프로젝트의 스프라이트는 전부 8~128px에 Point 필터를 씁니다)
///  - 64x64, 색 네 가지로 제한한 팔레트
///  - 상하좌우로 이어 붙는 타일링
///  - Point 필터라 확대해도 픽셀이 뭉개지지 않음
///  - 밉맵은 켬. 달리는 중 먼 지면이 지글거리는 것을 막아야 합니다.
///
/// 도로 텍스처는 <b>방향성이 없어야</b> 합니다. 북쪽 길(+Z)과 동쪽 길(+X)이
/// 같은 레이어를 함께 쓰기 때문입니다.
/// </summary>
public static class PixelGroundTextures
{
    // --- Constants ---

    /// <summary>만들어 낸 텍스처와 레이어를 둘 폴더입니다.</summary>
    public const string Folder = "Assets/_Project/04.Art/01.Images/Ground";

    /// <summary>텍스처 한 변의 픽셀 수입니다.</summary>
    private const int Size = 64;

    // --- Palettes ---
    // 밤 주행이 기본이라 전체적으로 어둡게 잡았습니다.
    // 헤드라이트가 닿는 곳만 밝아져야 조명이 읽힙니다.

    /// <summary>풀밭 — 채도를 낮춘 초록 네 단계입니다.</summary>
    private static readonly Color32[] GrassPalette =
    {
        new Color32(0x2F, 0x3D, 0x26, 255),
        new Color32(0x3A, 0x4A, 0x2E, 255),
        new Color32(0x45, 0x57, 0x3A, 255),
        new Color32(0x50, 0x66, 0x49, 255)
    };

    /// <summary>흙 — 비탈과 마을 부지에 씁니다.</summary>
    private static readonly Color32[] DirtPalette =
    {
        new Color32(0x3D, 0x32, 0x26, 255),
        new Color32(0x4A, 0x3D, 0x2E, 255),
        new Color32(0x57, 0x49, 0x3A, 255),
        new Color32(0x66, 0x58, 0x49, 255)
    };

    /// <summary>도로 — 푸른 기가 도는 어두운 회색입니다. 방향성이 없어야 합니다.</summary>
    private static readonly Color32[] RoadPalette =
    {
        new Color32(0x1C, 0x1F, 0x22, 255),
        new Color32(0x23, 0x26, 0x29, 255),
        new Color32(0x2B, 0x2F, 0x33, 255),
        new Color32(0x36, 0x3B, 0x40, 255)
    };

    // --- Public Methods ---

    /// <summary>
    /// 세 가지 지면 레이어(풀·흙·도로)를 만들어 돌려줍니다.
    /// 이미 있으면 다시 만들지 않고 그대로 씁니다.
    /// </summary>
    /// <param name="rebuild">true면 이미 있어도 새로 만듭니다.</param>
    /// <returns>풀·흙·도로 순서의 터레인 레이어</returns>
    public static TerrainLayer[] CreateOrLoad(bool rebuild)
    {
        EnsureFolder();

        TerrainLayer grass = BuildLayer("Ground_Grass", GrassPalette, 6f, 5, 0.35f, rebuild);
        TerrainLayer dirt = BuildLayer("Ground_Dirt", DirtPalette, 6f, 6, 0.40f, rebuild);
        TerrainLayer road = BuildLayer("Ground_Road", RoadPalette, 4f, 9, 0.55f, rebuild);

        return new[] { grass, dirt, road };
    }

    // --- Private Methods ---

    /// <summary>
    /// 텍스처와 터레인 레이어를 한 쌍으로 만듭니다.
    /// </summary>
    /// <param name="name">에셋 이름</param>
    /// <param name="palette">쓸 색 목록. 어두운 것부터 밝은 순서여야 합니다.</param>
    /// <param name="tileMeters">월드에서 한 장이 덮는 크기(m)</param>
    /// <param name="cells">얼룩의 성김. 클수록 잘게 흩어집니다.</param>
    /// <param name="grain">픽셀 단위 잡티의 세기(0~1)</param>
    /// <param name="rebuild">이미 있어도 다시 만들지 여부</param>
    /// <returns>만들어졌거나 이미 있던 터레인 레이어</returns>
    private static TerrainLayer BuildLayer(string name, Color32[] palette, float tileMeters,
                                           int cells, float grain, bool rebuild)
    {
        string texPath = Folder + "/" + name + ".png";
        string layerPath = Folder + "/" + name + ".terrainlayer";

        if (!rebuild)
        {
            TerrainLayer existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (existing != null) return existing;
        }

        // 1. 픽셀 텍스처를 만들어 PNG로 씁니다.
        Texture2D generated = Generate(palette, cells, grain, name.GetHashCode());
        File.WriteAllBytes(texPath, generated.EncodeToPNG());
        Object.DestroyImmediate(generated);

        AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
        ApplyImportSettings(texPath);

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        // 2. 그 텍스처를 쓰는 터레인 레이어를 만듭니다.
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        bool isNew = layer == null;
        if (isNew) layer = new TerrainLayer();

        layer.diffuseTexture = texture;
        layer.tileSize = new Vector2(tileMeters, tileMeters);
        layer.tileOffset = Vector2.zero;

        // 픽셀 룩에서는 광택과 금속 반사가 방해만 됩니다. 전부 죽입니다.
        layer.specular = Color.black;
        layer.metallic = 0f;
        layer.smoothness = 0f;
        layer.normalMapTexture = null;

        if (isNew) AssetDatabase.CreateAsset(layer, layerPath);
        else EditorUtility.SetDirty(layer);

        return layer;
    }

    /// <summary>
    /// 이어 붙는 픽셀 텍스처를 만듭니다.
    ///
    /// 성긴 얼룩(넓은 색 덩어리) 위에 픽셀 단위 잡티를 얹어, 멀리서는 색이 뭉치고
    /// 가까이서는 픽셀이 보이게 합니다. 좌표를 격자 크기로 <b>감싸서</b> 계산하므로
    /// 좌우와 상하가 그대로 이어집니다.
    /// </summary>
    /// <param name="palette">쓸 색 목록</param>
    /// <param name="cells">얼룩 격자의 칸 수</param>
    /// <param name="grain">픽셀 잡티의 세기</param>
    /// <param name="seed">난수 씨앗</param>
    /// <returns>만들어진 텍스처</returns>
    private static Texture2D Generate(Color32[] palette, int cells, float grain, int seed)
    {
        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
        Color32[] pixels = new Color32[Size * Size];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float blotch = TileNoise(x, y, cells, seed);
                float speck = Hash(x * 7 + 13, y * 11 + 5, seed + 991);

                float v = Mathf.Clamp01(blotch * (1f - grain) + speck * grain);

                int index = Mathf.Clamp(Mathf.FloorToInt(v * palette.Length), 0, palette.Length - 1);
                pixels[y * Size + x] = palette[index];
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// 격자를 감싸며 보간해 이어 붙는 값 노이즈를 만듭니다.
    /// </summary>
    /// <param name="x">텍셀 X</param>
    /// <param name="y">텍셀 Y</param>
    /// <param name="cells">격자 칸 수. 텍스처 크기를 나눠야 깔끔하게 이어집니다.</param>
    /// <param name="seed">난수 씨앗</param>
    /// <returns>0~1 값</returns>
    private static float TileNoise(int x, int y, int cells, int seed)
    {
        float fx = (float)x / Size * cells;
        float fy = (float)y / Size * cells;

        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0, ty = fy - y0;

        // 격자 좌표를 감싸 주는 것이 이어 붙음의 전부입니다.
        int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
        x0 %= cells; y0 %= cells;

        float sx = tx * tx * (3f - 2f * tx);
        float sy = ty * ty * (3f - 2f * ty);

        float a = Mathf.Lerp(Hash(x0, y0, seed), Hash(x1, y0, seed), sx);
        float b = Mathf.Lerp(Hash(x0, y1, seed), Hash(x1, y1, seed), sx);

        return Mathf.Lerp(a, b, sy);
    }

    /// <summary>정수 좌표에서 0~1 난수를 만듭니다.</summary>
    /// <param name="x">X</param>
    /// <param name="y">Y</param>
    /// <param name="seed">난수 씨앗</param>
    /// <returns>0~1 값</returns>
    private static float Hash(int x, int y, int seed)
    {
        int h = x * 374761393 + y * 668265263 + seed * 1274126177;
        h = (h ^ (h >> 13)) * 1274126177;
        h ^= h >> 16;
        return (h & 0x7FFFFFF) / (float)0x7FFFFFF;
    }

    /// <summary>
    /// 픽셀 아트에 맞는 임포트 설정을 적용합니다.
    /// 기존 스프라이트와 같은 규칙(Point 필터)을 따르되, 지면은 밉맵을 켭니다.
    /// </summary>
    /// <param name="path">텍스처 에셋 경로</param>
    private static void ApplyImportSettings(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;      // 확대해도 픽셀이 살아 있어야 합니다.
        importer.mipmapEnabled = true;               // 먼 지면이 지글거리는 것을 막습니다.
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.anisoLevel = 4;                     // 비스듬히 보이는 노면이 뭉개지지 않게 합니다.
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed; // 64px이라 부담이 없습니다.
        importer.maxTextureSize = 64;

        importer.SaveAndReimport();
    }

    /// <summary>출력 폴더가 없으면 만듭니다.</summary>
    private static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(Folder)) return;

        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
    }
}
