using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 지면과 도로에 쓸 픽셀 텍스처와 터레인 레이어를 만들어 냅니다.
    ///
    /// 원본은 <b>Kenney Voxel Pack(CC0)</b>의 타일입니다.
    /// (Ground/Kenney/ATTRIBUTION.md 참고. 개인·상업 사용과 개작이 모두 허용됩니다)
    ///
    /// 왜 원본을 그대로 쓰지 않는가:
    /// 이 게임은 화면을 <b>세로 215픽셀</b>로 줄여 출력하고(PixelizeFeature) 밤 주행이 기본입니다.
    /// 원본은 밝고 채도가 높은 카툰 팔레트라(grass_top 평균색 rgb(45,202,112)) 그대로 깔면
    /// 지면이 화면에서 튀어 헤드라이트와 귀신이 묻힙니다.
    /// 그래서 <b>픽셀 구조는 손대지 않고 밝기와 채도만 낮춰</b> 씁니다.
    ///
    /// 저장소에 있던 TerrainSampleAssets(1.9GB 4K PBR)를 쓰지 않는 이유도 같습니다.
    /// 215픽셀로 줄이면 그 디테일과 노멀맵 요철은 전부 사라집니다.
    ///
    /// 원본을 찾지 못하면 절차적으로 만들어 대신합니다. 도구가 멈추지는 않습니다.
    /// </summary>
    public static class PixelGroundTextures
    {
        // --- Constants ---

        /// <summary>만들어 낸 텍스처와 레이어를 둘 폴더입니다.</summary>
        public const string Folder = "Assets/_Project/04.Art/01.Images/Ground";

        /// <summary>CC0 원본 타일이 있는 폴더입니다.</summary>
        private const string SourceFolder = Folder + "/Kenney";

        /// <summary>원본이 없을 때 절차적으로 만들 텍스처의 한 변 픽셀 수입니다.</summary>
        private const int FallbackSize = 64;

        /// <summary>
        /// 최종 텍스처의 한 변 픽셀 수입니다.
        /// PSX의 텍스처는 대개 64x64였습니다. 텍스처 페이지가 256x256뿐이라
        /// 그 안에 여러 장을 욱여넣어야 했기 때문입니다.
        /// </summary>
        private const int PsxSize = 64;

        /// <summary>
        /// 채널당 색 단계 수입니다. PSX는 15비트 색(채널당 5비트 = 32단계)이었습니다.
        /// </summary>
        private const int PsxLevels = 32;

        /// <summary>
        /// 4x4 오더드 디더 행렬(베이어)입니다.
        ///
        /// 색이 32단계뿐이면 완만한 그라데이션에 굵은 띠가 생깁니다. 그 시절에는 이 행렬로
        /// 픽셀을 번갈아 흩뿌려 눈속임했고, <b>그 자글자글한 무늬가 PSX 텍스처의 인상</b>입니다.
        /// </summary>
        private static readonly int[,] Bayer4 =
        {
            {  0,  8,  2, 10 },
            { 12,  4, 14,  6 },
            {  3, 11,  1,  9 },
            { 15,  7, 13,  5 }
        };

        // --- Layer specs ---

        /// <summary>
        /// 만들 레이어 하나의 설정입니다.
        /// </summary>
        private struct Spec
        {
            /// <summary>만들어질 에셋 이름입니다.</summary>
            public string name;

            /// <summary>Kenney 폴더에서 찾을 원본 파일 이름입니다.</summary>
            public string source;

            /// <summary>밝기 배율입니다. 낮출수록 어두워집니다.</summary>
            public float brightness;

            /// <summary>채도 배율입니다. 0이면 완전한 흑백입니다.</summary>
            public float saturation;

            /// <summary>월드에서 한 장이 덮는 크기(m)입니다.</summary>
            public float tileMeters;

            /// <summary>덧씌울 잡티의 세기입니다. 깔끔한 무늬를 그 시절처럼 거칠게 만듭니다.</summary>
            public float grain;

            /// <summary>원본이 없을 때 쓸 대체 팔레트입니다.</summary>
            public Color32[] fallback;

            public Spec(string name, string source, float brightness, float saturation,
                        float tileMeters, float grain, Color32[] fallback)
            {
                this.name = name;
                this.source = source;
                this.brightness = brightness;
                this.saturation = saturation;
                this.tileMeters = tileMeters;
                this.grain = grain;
                this.fallback = fallback;
            }
        }

        /// <summary>
        /// 만들 레이어 목록입니다. 순서가 곧 알파맵의 레이어 번호입니다. (풀 0 · 흙 1 · 도로 2)
        /// 색감을 바꾸고 싶으면 여기 밝기·채도를 조정하고 "지면 텍스처 다시 만들기"로 다시 구우세요.
        ///
        /// <b>밝기는 '밤에 어둡게 보이려고' 낮추는 값이 아닙니다.</b>
        /// 알베도는 재질이 빛을 얼마나 되쏘는지를 나타내는 성질이고, 시간대와 무관합니다.
        /// 처음에는 밤 분위기를 내려고 0.42까지 낮췄는데, 그러면 <b>한낮에도 지면이 어둡습니다.</b>
        /// (실측: 잔디 휘도 26.5%. 조명을 2.5배로 올려도 화면 밝기가 41%에 그쳤습니다)
        /// 밤의 어둠은 SkyController가 주변광과 태양광을 낮춰서 만듭니다.
        /// 그래서 여기서는 실제 알베도에 가까운 값을 씁니다. (잔디 약 48%, 아스팔트 약 28%)
        /// </summary>
        private static readonly Spec[] Sources =
        {
            new Spec("Ground_Grass", "grass_top", 0.76f, 0.55f, 6f, 0.16f, new[]
            {
                new Color32(0x2F, 0x3D, 0x26, 255), new Color32(0x3A, 0x4A, 0x2E, 255),
                new Color32(0x45, 0x57, 0x3A, 255), new Color32(0x50, 0x66, 0x49, 255)
            }),
            new Spec("Ground_Dirt", "dirt", 0.72f, 0.50f, 6f, 0.20f, new[]
            {
                new Color32(0x3D, 0x32, 0x26, 255), new Color32(0x4A, 0x3D, 0x2E, 255),
                new Color32(0x57, 0x49, 0x3A, 255), new Color32(0x66, 0x58, 0x49, 255)
            }),
            new Spec("Ground_Road", "greystone", 0.62f, 0.35f, 4f, 0.13f, new[]
            {
                new Color32(0x1C, 0x1F, 0x22, 255), new Color32(0x23, 0x26, 0x29, 255),
                new Color32(0x2B, 0x2F, 0x33, 255), new Color32(0x36, 0x3B, 0x40, 255)
            })
        };

        // --- Public Methods ---

        /// <summary>지면 텍스처만 다시 만듭니다. 지형은 다시 굽지 않습니다.</summary>
        [MenuItem("CarDrive/World/지면 텍스처 다시 만들기")]
        public static void RebuildTextures()
        {
            TerrainLayer[] layers = CreateOrLoad(true);
            AssetDatabase.SaveAssets();

            Debug.Log("PixelGroundTextures: 레이어 " + layers.Length + "개를 다시 만들었습니다.");
        }

        /// <summary>
        /// 지면 레이어 세 가지(풀·흙·도로)를 만들어 돌려줍니다.
        /// 이미 있으면 다시 만들지 않고 그대로 씁니다.
        /// </summary>
        /// <param name="rebuild">true면 이미 있어도 새로 만듭니다.</param>
        /// <returns>풀·흙·도로 순서의 터레인 레이어</returns>
        public static TerrainLayer[] CreateOrLoad(bool rebuild)
        {
            EnsureFolder();

            TerrainLayer[] layers = new TerrainLayer[Sources.Length];
            for (int i = 0; i < Sources.Length; i++) layers[i] = BuildLayer(Sources[i], rebuild);

            return layers;
        }

        // --- Private Methods ---

        /// <summary>
        /// 텍스처와 터레인 레이어를 한 쌍으로 만듭니다.
        /// </summary>
        /// <param name="spec">만들 레이어의 설정</param>
        /// <param name="rebuild">이미 있어도 다시 만들지 여부</param>
        /// <returns>만들어졌거나 이미 있던 터레인 레이어</returns>
        private static TerrainLayer BuildLayer(Spec spec, bool rebuild)
        {
            string texPath = Folder + "/" + spec.name + ".png";
            string layerPath = Folder + "/" + spec.name + ".terrainlayer";

            if (!rebuild)
            {
                TerrainLayer cached = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
                if (cached != null) return cached;
            }

            // 1. 원본을 밤 팔레트로 보정합니다. 원본이 없으면 절차적으로 만듭니다.
            Texture2D built = LoadSource(spec.source);
            if (built != null)
            {
                Grade(built, spec.brightness, spec.saturation);

                // 여기부터가 PSX 처리입니다. 원본의 깔끔한 무늬를 그 시절 규격으로 깎습니다.
                Texture2D shrunk = Downsample(built, PsxSize);
                Object.DestroyImmediate(built);
                built = shrunk;

                ApplyPsxLook(built, spec.grain, spec.name.GetHashCode());
            }
            else
            {
                Debug.LogWarning("PixelGroundTextures: " + spec.source + " 원본을 찾지 못해 " +
                                 "절차적 텍스처로 대신합니다. (" + SourceFolder + ")");
                built = GenerateFallback(spec.fallback, spec.name.GetHashCode());
            }

            File.WriteAllBytes(texPath, built.EncodeToPNG());
            Object.DestroyImmediate(built);

            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            ApplyImportSettings(texPath);

            // 2. 그 텍스처를 쓰는 터레인 레이어를 만듭니다.
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            bool isNew = layer == null;
            if (isNew) layer = new TerrainLayer();

            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            layer.tileSize = new Vector2(spec.tileMeters, spec.tileMeters);
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
        /// CC0 원본 PNG를 읽어 옵니다.
        ///
        /// 임포트 설정과 무관하게 읽으려고 파일 바이트에서 직접 만듭니다.
        /// (에셋으로 읽으면 Read/Write가 꺼져 있을 때 픽셀을 못 가져옵니다)
        /// </summary>
        /// <param name="fileName">확장자를 뺀 파일 이름</param>
        /// <returns>읽어 온 텍스처. 파일이 없으면 null입니다.</returns>
        private static Texture2D LoadSource(string fileName)
        {
            string path = SourceFolder + "/" + fileName + ".png";
            if (!File.Exists(path)) return null;

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(File.ReadAllBytes(path))) return tex;

            Object.DestroyImmediate(tex);
            return null;
        }

        /// <summary>
        /// 밝기와 채도를 낮춰 밤에 어울리게 만듭니다.
        ///
        /// 픽셀마다 같은 변환을 적용하므로 <b>원본의 픽셀 구조와 무늬는 그대로 남습니다.</b>
        /// 색의 가짓수도 거의 그대로라 픽셀 아트의 성격을 잃지 않습니다.
        /// </summary>
        /// <param name="tex">보정할 텍스처</param>
        /// <param name="brightness">밝기 배율</param>
        /// <param name="saturation">채도 배율</param>
        private static void Grade(Texture2D tex, float brightness, float saturation)
        {
            Color32[] pixels = tex.GetPixels32();

            for (int i = 0; i < pixels.Length; i++)
            {
                float r = pixels[i].r / 255f;
                float g = pixels[i].g / 255f;
                float b = pixels[i].b / 255f;

                float lum = 0.2126f * r + 0.7152f * g + 0.0722f * b;

                r = Mathf.Clamp01((lum + (r - lum) * saturation) * brightness);
                g = Mathf.Clamp01((lum + (g - lum) * saturation) * brightness);
                b = Mathf.Clamp01((lum + (b - lum) * saturation) * brightness);

                pixels[i] = new Color32((byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f), 255);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
        }

        /// <summary>
        /// 텍스처를 정해진 크기로 줄입니다.
        ///
        /// 부드럽게 섞지 않고 <b>점 추출</b>로 줄입니다. 평균을 내면 무늬가 뭉개져
        /// 흐릿해지는데, 그 시절 텍스처는 오히려 알갱이가 또렷했습니다.
        /// </summary>
        /// <param name="source">줄일 텍스처</param>
        /// <param name="size">결과 한 변의 픽셀 수</param>
        /// <returns>줄여서 만든 새 텍스처</returns>
        private static Texture2D Downsample(Texture2D source, int size)
        {
            Texture2D result = new Texture2D(size, size, TextureFormat.RGBA32, true);
            Color32[] src = source.GetPixels32();
            Color32[] dst = new Color32[size * size];

            int sw = source.width;
            int sh = source.height;

            for (int y = 0; y < size; y++)
            {
                int sy = y * sh / size;
                for (int x = 0; x < size; x++)
                {
                    int sx = x * sw / size;
                    dst[y * size + x] = src[sy * sw + sx];
                }
            }

            result.SetPixels32(dst);
            result.Apply();
            return result;
        }

        /// <summary>
        /// 그 시절 규격으로 깎습니다. 잡티를 얹고, 오더드 디더를 섞고, 15비트 색으로 줄입니다.
        ///
        /// 원본은 색면이 넓고 경계가 깔끔한 카툰 타일입니다. 그대로 두면 아무리 작게 줄여도
        /// 요즘 그림처럼 보입니다. PSX 텍스처의 인상은 <b>거친 알갱이와 디더 무늬</b>에서 나옵니다.
        /// </summary>
        /// <param name="tex">깎을 텍스처</param>
        /// <param name="grain">덧씌울 잡티의 세기(0~1)</param>
        /// <param name="seed">난수 씨앗</param>
        private static void ApplyPsxLook(Texture2D tex, float grain, int seed)
        {
            Color32[] pixels = tex.GetPixels32();
            int w = tex.width;

            float steps = PsxLevels - 1;
            float step = 1f / steps;

            for (int i = 0; i < pixels.Length; i++)
            {
                int x = i % w;
                int y = i / w;

                float r = pixels[i].r / 255f;
                float g = pixels[i].g / 255f;
                float b = pixels[i].b / 255f;

                // 1. 잡티. 색면이 넓은 곳을 거칠게 만듭니다.
                if (grain > 0f)
                {
                    float n = (Hash(x, y, seed) - 0.5f) * 2f * grain;
                    r = Mathf.Clamp01(r + n);
                    g = Mathf.Clamp01(g + n);
                    b = Mathf.Clamp01(b + n);
                }

                // 2. 오더드 디더. 양자화 직전에 격자 무늬만큼 밀어 두면
                //    단계 사이가 점으로 흩어져 띠가 보이지 않습니다.
                float dither = ((Bayer4[y & 3, x & 3] + 0.5f) / 16f - 0.5f) * step;

                // 3. 15비트 색으로 줄입니다.
                r = Mathf.Round(Mathf.Clamp01(r + dither) * steps) / steps;
                g = Mathf.Round(Mathf.Clamp01(g + dither) * steps) / steps;
                b = Mathf.Round(Mathf.Clamp01(b + dither) * steps) / steps;

                pixels[i] = new Color32((byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f), 255);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
        }

        /// <summary>
        /// 원본을 찾지 못했을 때 쓸 텍스처를 절차적으로 만듭니다.
        /// 격자 좌표를 감싸며 보간하므로 상하좌우로 이어 붙습니다.
        /// </summary>
        /// <param name="palette">쓸 색 목록</param>
        /// <param name="seed">난수 씨앗</param>
        /// <returns>만들어진 텍스처</returns>
        private static Texture2D GenerateFallback(Color32[] palette, int seed)
        {
            Texture2D tex = new Texture2D(FallbackSize, FallbackSize, TextureFormat.RGBA32, true);
            Color32[] pixels = new Color32[FallbackSize * FallbackSize];

            for (int y = 0; y < FallbackSize; y++)
            {
                for (int x = 0; x < FallbackSize; x++)
                {
                    float blotch = TileNoise(x, y, 6, seed);
                    float speck = Hash(x * 7 + 13, y * 11 + 5, seed + 991);
                    float v = Mathf.Clamp01(blotch * 0.6f + speck * 0.4f);

                    int index = Mathf.Clamp(Mathf.FloorToInt(v * palette.Length), 0, palette.Length - 1);
                    pixels[y * FallbackSize + x] = palette[index];
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>격자를 감싸며 보간해 이어 붙는 값 노이즈를 만듭니다.</summary>
        /// <param name="x">텍셀 X</param>
        /// <param name="y">텍셀 Y</param>
        /// <param name="cells">격자 칸 수</param>
        /// <param name="seed">난수 씨앗</param>
        /// <returns>0~1 값</returns>
        private static float TileNoise(int x, int y, int cells, int seed)
        {
            float fx = (float)x / FallbackSize * cells;
            float fy = (float)y / FallbackSize * cells;

            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;

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
            importer.textureCompression = TextureImporterCompression.Uncompressed; // 디더 무늬는 압축하면 뭉개집니다.
            importer.maxTextureSize = 64;   // 그 시절 텍스처 크기를 넘지 않게 못 박아 둡니다.

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
}
