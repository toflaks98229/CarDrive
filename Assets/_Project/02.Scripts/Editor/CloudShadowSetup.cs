using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 구름 그림자에 필요한 것을 만들어 배선합니다.
    ///
    ///  1. 이어 붙는 구름 무늬 텍스처를 굽습니다.
    ///  2. 씬에 <see cref="CloudShadows"/> 를 놓고 그 무늬를 연결합니다.
    ///
    /// <b>무늬는 반드시 이어 붙어야 합니다.</b> 구름은 계속 흘러가는데 무늬가 끊기면
    /// 일정 거리마다 하늘에 금이 간 것처럼 직선 경계가 지나갑니다.
    /// 그래서 격자를 <b>주기적으로</b> 감싸는 값 노이즈를 씁니다. 좌표가 격자 폭을 넘으면
    /// 처음 격자점으로 되돌아오므로 좌우·상하가 저절로 맞물립니다.
    /// </summary>
    public static class CloudShadowSetup
    {
        /// <summary>메인 씬 경로입니다.</summary>
        private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

        /// <summary>구름 무늬를 저장할 경로입니다.</summary>
        private const string CloudMapPath = "Assets/_Project/04.Art/03.Shaders/Toon/Ramps/CloudShadowMap.png";

        /// <summary>무늬 해상도입니다. 픽셀 룩이라 크게 만들 이유가 없습니다.</summary>
        private const int MapSize = 256;

        /// <summary>가장 굵은 격자의 칸 수입니다. 이 수의 배수로 옥타브가 쌓입니다.</summary>
        private const int BasePeriod = 4;

        /// <summary>겹칠 옥타브 수입니다. 많을수록 가장자리가 잘게 부서집니다.</summary>
        private const int Octaves = 4;

        // --- Public Methods ---

        /// <summary>에디터 메뉴에서 실행합니다.</summary>
        [MenuItem("CarDrive/Look/구름 그림자 설정")]
        public static void Setup()
        {
            List<string> report = new List<string>();

            Texture2D cloudMap = BakeCloudMap(report);
            PlaceComponent(cloudMap, report);

            AssetDatabase.SaveAssets();

            Debug.Log("CloudShadowSetup:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 명령줄에서 씬을 열고 배선한 뒤 저장합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.CloudShadowSetup.SetupFromCommandLine</c>
        /// </summary>
        public static void SetupFromCommandLine()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Setup();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // --- Private Methods ---

        /// <summary>
        /// 이어 붙는 구름 무늬를 굽습니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>구운 텍스처</returns>
        private static Texture2D BakeCloudMap(List<string> report)
        {
            EnsureFolder(Path.GetDirectoryName(CloudMapPath).Replace('\\', '/'));

            Texture2D texture = new Texture2D(MapSize, MapSize, TextureFormat.RGBA32, false, true);

            float min = float.MaxValue;
            float max = float.MinValue;
            float[] values = new float[MapSize * MapSize];

            for (int y = 0; y < MapSize; y++)
            {
                for (int x = 0; x < MapSize; x++)
                {
                    float u = (float)x / MapSize;
                    float v = (float)y / MapSize;

                    float value = 0f;
                    float amplitude = 1f;
                    float total = 0f;
                    int period = BasePeriod;

                    for (int o = 0; o < Octaves; o++)
                    {
                        value += TilingNoise(u, v, period) * amplitude;
                        total += amplitude;

                        amplitude *= 0.5f;
                        period *= 2;
                    }

                    value /= total;

                    values[y * MapSize + x] = value;
                    if (value < min) min = value;
                    if (value > max) max = value;
                }
            }

            // 0~1 을 꽉 채웁니다. 그러지 않으면 셰이더의 0.5 경계가 무늬 한가운데를
            // 지나지 않아 구름이 전부 끼거나 전부 개어 버립니다.
            float span = Mathf.Max(1e-4f, max - min);

            for (int y = 0; y < MapSize; y++)
            {
                for (int x = 0; x < MapSize; x++)
                {
                    float n = (values[y * MapSize + x] - min) / span;
                    texture.SetPixel(x, y, new Color(n, n, n, 1f));
                }
            }

            texture.Apply();
            File.WriteAllBytes(CloudMapPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(CloudMapPath, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(CloudMapPath);

            report.Add("· 구름 무늬를 구웠습니다: " + CloudMapPath + " (" + MapSize + "px, 옥타브 " + Octaves + ")");

            return AssetDatabase.LoadAssetAtPath<Texture2D>(CloudMapPath);
        }

        /// <summary>
        /// 주기적으로 이어 붙는 값 노이즈입니다.
        ///
        /// 격자점의 난수를 이중 보간하되, 좌표가 격자 폭을 넘으면 <b>처음 격자점으로
        /// 되돌아오게</b> 합니다. 그래서 오른쪽 끝과 왼쪽 끝, 위쪽 끝과 아래쪽 끝이 맞물립니다.
        /// </summary>
        /// <param name="u">가로 좌표 (0~1)</param>
        /// <param name="v">세로 좌표 (0~1)</param>
        /// <param name="period">격자 칸 수</param>
        private static float TilingNoise(float u, float v, int period)
        {
            float x = u * period;
            float y = v * period;

            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);

            float fx = x - x0;
            float fy = y - y0;

            // 부드럽게 이어지도록 보간 곡선을 씁니다. 선형이면 격자가 눈에 보입니다.
            float sx = fx * fx * (3f - 2f * fx);
            float sy = fy * fy * (3f - 2f * fy);

            // 여기서 되감기 때문에 무늬가 이어 붙습니다.
            int x1 = (x0 + 1) % period;
            int y1 = (y0 + 1) % period;
            x0 %= period;
            y0 %= period;

            float a = LatticeValue(x0, y0, period);
            float b = LatticeValue(x1, y0, period);
            float c = LatticeValue(x0, y1, period);
            float d = LatticeValue(x1, y1, period);

            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        /// <summary>
        /// 격자점 하나의 난수입니다. 같은 좌표는 항상 같은 값을 냅니다.
        /// </summary>
        /// <param name="x">격자 X</param>
        /// <param name="y">격자 Y</param>
        /// <param name="period">격자 칸 수. 옥타브마다 달라야 무늬가 겹치지 않습니다.</param>
        private static float LatticeValue(int x, int y, int period)
        {
            int seed = x * 73856093 ^ y * 19349663 ^ period * 83492791;

            // 정수를 섞어 0~1 로 폅니다.
            seed = (seed << 13) ^ seed;
            int n = seed * (seed * seed * 15731 + 789221) + 1376312589;

            return ((n & 0x7fffffff) / (float)0x7fffffff);
        }

        /// <summary>
        /// 구름 무늬에 맞는 임포트 설정을 겁니다.
        ///
        /// 램프와 달리 <b>반복(Repeat)</b>과 <b>보간(Bilinear)</b>을 씁니다.
        /// 구름은 계속 이어 붙어야 하고, 무늬 자체는 부드러워야 셰이더의 밴드가
        /// 깨끗한 경계를 만들 수 있기 때문입니다. 밉맵은 켭니다 — 먼 지면에서
        /// 무늬가 지글거리면 화면 전체가 어른거립니다.
        /// </summary>
        /// <param name="path">임포트할 텍스처 경로</param>
        private static void ConfigureImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.CompressedLQ;

            importer.SaveAndReimport();
        }

        /// <summary>
        /// 씬에 구름 그림자 컴포넌트를 놓습니다. 이미 있으면 무늬만 다시 연결합니다.
        /// </summary>
        /// <param name="cloudMap">연결할 무늬</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void PlaceComponent(Texture2D cloudMap, List<string> report)
        {
            CloudShadows clouds = Object.FindAnyObjectByType<CloudShadows>(FindObjectsInactive.Include);

            if (clouds == null)
            {
                // 날씨 시스템 옆에 둡니다. 값을 거기서 가져오므로 한자리에 모이는 편이 읽기 좋습니다.
                WeatherSystem weather = Object.FindAnyObjectByType<WeatherSystem>();
                GameObject host = weather != null ? weather.gameObject : new GameObject("CloudShadows");

                clouds = host.AddComponent<CloudShadows>();
                report.Add("· 구름 그림자를 놓았습니다: " + host.name);
            }
            else
            {
                report.Add("· 구름 그림자가 이미 있습니다: " + clouds.gameObject.name);
            }

            clouds.cloudMap = cloudMap;
            EditorUtility.SetDirty(clouds);

            report.Add("  구름량은 WeatherSystem 이, 흐르는 속도는 바람이 정합니다. " +
                       "맑은 날에는 그림자가 없습니다.");
        }

        /// <summary>폴더가 없으면 만듭니다.</summary>
        /// <param name="path">"Assets/A/B" 형태의 폴더 경로</param>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
