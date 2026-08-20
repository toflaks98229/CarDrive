using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CarDrive.Systems;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 화면 룩을 로우폴리 · 코지 방식으로 바꿉니다.
    ///
    /// 하는 일:
    ///  1. 지면을 CarDrive/LowPoly Terrain 머티리얼로 갈아 끼웁니다. (PSX 뒤틀림이 사라집니다)
    ///  2. 풀 포기 메시와 프리팹을 만들어 터레인 디테일로 빽빽하게 심습니다.
    ///  3. 조명을 아늑한 값으로 잡습니다.
    ///
    /// <b>풀은 셰이더만으로 완성되지 않습니다.</b> 포기 사이가 벌어지면 그 틈으로 지면이
    /// 비쳐 포기 하나하나가 눈에 띕니다. 그래서 여기서 잡는 <b>밀도와 자리 흩뜨림</b>이
    /// 셰이더만큼 중요합니다. 특히 자리를 흩뜨리지 않으면 격자에 줄 맞춰 심겨
    /// 바둑판 무늬가 그대로 보입니다.
    ///
    /// PSX 셰이더와 픽셀라이즈 후처리는 지우지 않았습니다. 되돌리고 싶으면 그대로 있습니다.
    /// </summary>
    public static class LowPolyLookSetup
    {
        // --- Constants ---

        /// <summary>지면 셰이더의 이름입니다.</summary>
        private const string TerrainShaderName = "CarDrive/LowPoly Terrain";

        /// <summary>풀 셰이더의 이름입니다.</summary>
        private const string GrassShaderName = "CarDrive/LowPoly Grass";

        /// <summary>셰이더와 머티리얼을 두는 폴더입니다.</summary>
        private const string ShaderFolder = "Assets/_Project/04.Art/03.Shaders/LowPoly";

        /// <summary>지면 머티리얼 경로입니다.</summary>
        private const string TerrainMaterialPath = ShaderFolder + "/LowPolyTerrain.mat";

        /// <summary>풀 머티리얼 경로입니다.</summary>
        private const string GrassMaterialPath = ShaderFolder + "/LowPolyGrass.mat";

        /// <summary>만들어 낸 메시와 프리팹을 두는 폴더입니다.</summary>
        private const string GeneratedFolder = "Assets/_Project/04.Art/02.Models/Generated";

        /// <summary>풀 포기 메시 경로입니다.</summary>
        private const string GrassMeshPath = GeneratedFolder + "/GrassTuft.asset";

        /// <summary>풀 포기 프리팹 경로입니다.</summary>
        private const string GrassPrefabPath = GeneratedFolder + "/GrassTuft.prefab";

        /// <summary>메인 씬 경로입니다.</summary>
        private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

        // --- 풀 심기 설정 ---

        /// <summary>
        /// 만질 만한 값들은 설정 에셋에 있습니다. (CarDrive 메뉴의 월드 창)
        ///
        /// 예전에는 여기 상수로 박혀 있었습니다. 룩을 이것저것 시험하는 동안에는 그래도 됐지만,
        /// 방향이 정해진 지금은 값 하나 바꾸려고 코드를 열고 다시 컴파일하는 것이 번거로울 뿐입니다.
        /// </summary>
        private static CarDriveWorldSettings Settings { get { return CarDriveWorldSettings.Instance; } }

        private static float DetailDistance { get { return Settings.detailDistance; } }
        // --- 코지 팔레트 ---
        // 지면과 풀이 같은 색을 써야 경계가 생기지 않으므로 한자리에 모아 둡니다.

        /// <summary>
        /// 잔디 색입니다. 어두운 쪽과 밝은 쪽이 <b>조금 다릅니다.</b>
        ///
        /// 두 색이 만드는 얼룩은 주기가 17~45m라 잎(3cm) 하나 안에서는 변화가 없고,
        /// 옆 잎과도 사실상 같은 색입니다. 이 얼룩이 없으면 들판이 통짜 색 벽이 되어
        /// 깊이가 사라집니다. 지면과 풀이 <b>같은 두 색</b>을 쓰므로 둘 사이에는 경계가 없습니다.
        ///
        /// 한때 이 값들을 파스텔(채도 34~36 / 명도 70~80)로 올렸다가 되돌렸습니다.
        /// 화면 전체가 흰빛으로 떠서 마른 억새의 느낌이 사라졌습니다.
        /// </summary>
        private static readonly Color GrassDark  = new Color(0.694f, 0.494f, 0.180f);

        /// <summary>잔디의 밝은 쪽입니다. 볕이 드는 마른 풀빛입니다.</summary>
        private static readonly Color GrassLight = new Color(0.855f, 0.667f, 0.290f);

        /// <summary>잎 끝 색입니다. 가까운 풀에서만 쓰입니다.</summary>
        private static readonly Color GrassTip   = GrassLight;

        private static readonly Color DirtDark   = new Color(0.510f, 0.396f, 0.263f);
        private static readonly Color DirtLight  = new Color(0.616f, 0.494f, 0.341f);
        private static readonly Color RoadDark   = new Color(0.400f, 0.373f, 0.361f);
        private static readonly Color RoadLight  = new Color(0.482f, 0.451f, 0.435f);

        /// <summary>
        /// 그늘 색입니다. 파란 그늘은 서늘한 화면에 어울립니다.
        /// 따뜻한 화면에서는 그늘도 따뜻해야 색이 갈라지지 않습니다.
        /// </summary>
        private static readonly Color ShadowTint = new Color(0.596f, 0.514f, 0.494f);

        /// <summary>
        /// 하늘 지평선 색입니다. <b>안개 색도 반드시 이 값이어야 합니다.</b>
        /// 먼 것이 하늘에 녹아드는 것이 대기 원근이고, 그 인상이 이 룩의 핵심입니다.
        /// </summary>
        private static readonly Color SkyHorizon = new Color(0.878f, 0.812f, 0.765f);

        /// <summary>하늘 천정 색입니다. 지평선보다 조금 어둡고 덜 따뜻합니다.</summary>
        private static readonly Color SkyTop = new Color(0.722f, 0.667f, 0.663f);

        // --- Public Methods ---

        /// <summary>에디터 메뉴에서 실행합니다.</summary>
        [MenuItem("CarDrive/World/로우폴리 코지 룩으로 전환")]
        public static void Apply()
        {
            List<string> report = new List<string>();

            EnsureFolder(GeneratedFolder);

            Material terrainMat = BuildTerrainMaterial(report);
            Material grassMat = BuildGrassMaterial(report);
            GameObject grassPrefab = BuildGrassPrefab(grassMat, report);

            ApplyToTerrains(terrainMat, grassPrefab, report);
            ApplyCozyLighting(report);
            ApplySkyColors(report);
            ApplyColorGrading(report);

            AssetDatabase.SaveAssets();

            Debug.Log("LowPolyLookSetup:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 명령줄에서 씬을 열고 적용한 뒤 저장합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod LowPolyLookSetup.ApplyFromCommandLine</c>
        /// </summary>
        public static void ApplyFromCommandLine()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Apply();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 심어진 결과를 재어 봅니다.
        ///
        /// 특히 <b>도로 위에 풀이 심겼는지</b>를 봅니다.
        /// 디테일 격자와 알파맵은 축 순서가 같다고 보고 썼는데, 만약 뒤집혀 있다면
        /// 풀이 엉뚱한 곳에 심깁니다. 도로 위 밀도를 재면 그것이 바로 드러납니다.
        /// </summary>
        [MenuItem("CarDrive/World/심긴 풀 검사 (도로에 심기지 않았는지)")]
        public static void VerifyFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

            long grassCells = 0, grassTotal = 0;
            long roadCells = 0, roadPlanted = 0;
            long allCells = 0, allPlanted = 0;

            for (int i = 0; i < terrains.Length; i++)
            {
                TerrainData data = terrains[i].terrainData;
                if (data == null || data.detailPrototypes.Length == 0) continue;

                int res = data.detailResolution;
                int ares = data.alphamapResolution;

                int[,] detail = data.GetDetailLayer(0, 0, res, res, 0);
                float[,,] alpha = data.GetAlphamaps(0, 0, ares, ares);
                int layers = alpha.GetLength(2);

                for (int z = 0; z < res; z++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        int az = Mathf.Clamp(Mathf.FloorToInt((z + 0.5f) / res * ares), 0, ares - 1);
                        int ax = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) / res * ares), 0, ares - 1);

                        int planted = detail[z, x];
                        allCells++;
                        if (planted > 0) allPlanted++;

                        float grass = alpha[az, ax, 0];
                        float road = layers > 2 ? alpha[az, ax, 2] : 0f;

                        if (road > 0.7f)
                        {
                            roadCells++;
                            if (planted > 0) roadPlanted++;
                        }
                        else if (grass > 0.9f)
                        {
                            grassCells++;
                            if (planted > 0) grassTotal++;
                        }
                    }
                }
            }

            string Pct(long a, long b) { return b == 0 ? "-" : (100.0 * a / b).ToString("F1") + "%"; }

            Debug.Log(
                "VERIFY 터레인 " + terrains.Length + "장" + System.Environment.NewLine +
                "VERIFY 전체 격자 " + allCells + " 중 심긴 칸 " + allPlanted + " (" + Pct(allPlanted, allCells) + ")" + System.Environment.NewLine +
                "VERIFY 잔디 칸 심긴 비율 " + Pct(grassTotal, grassCells) + "  (높을수록 좋음)" + System.Environment.NewLine +
                "VERIFY 도로 칸 심긴 비율 " + Pct(roadPlanted, roadCells) + "  (0%가 정상. 높으면 축이 뒤집힌 것)");
        }

        /// <summary>여러 번 잰 값 중 가장 작은 값을 고릅니다.</summary>
        /// <param name="samples">경우 x 회차 표</param>
        /// <param name="row">볼 경우</param>
        /// <param name="count">회차 수</param>
        /// <returns>최솟값</returns>
        private static double Min(double[,] samples, int row, int count)
        {
            double best = double.MaxValue;
            for (int i = 0; i < count; i++) best = System.Math.Min(best, samples[row, i]);

            return best;
        }

        /// <summary>
        /// 지형 타일 하나를 켜는 데 걸리는 시간을 잽니다.
        ///
        /// 화면이 끊기는 원인을 짐작만 하지 않기 위한 것입니다.
        /// 이 값이 크면 여러 장을 한꺼번에 켜는 것이 실제로 프레임을 잡아먹는다는 뜻이고,
        /// 나눠서 켜는 것이 옳은 처방이 됩니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod LowPolyLookSetup.MeasureTileActivationFromCommandLine</c>
        /// </summary>
        public static void MeasureTileActivationFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Vector3 grassSpot, roadSpot;
            if (!FindSpots(out grassSpot, out roadSpot))
            {
                Debug.LogError("TILE: 풀밭을 찾지 못했습니다.");
                EditorApplication.Exit(1);
                return;
            }

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            System.Array.Sort(terrains, (a, b) => string.CompareOrdinal(a.name, b.name));

            GameObject camGo = new GameObject("TileCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 60f;
            cam.farClipPlane = 1200f;
            cam.transform.position = grassSpot + Vector3.up * 1.7f;
            cam.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            RenderTexture rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            Texture2D probe = new Texture2D(1, 1, TextureFormat.RGB24, false);
            Rect one = new Rect(0, 0, 1, 1);

            // 자원이 준비될 때까지 몇 장 그려 둡니다.
            for (int i = 0; i < 12; i++)
            {
                cam.Render();
                RenderTexture.active = rt;
                probe.ReadPixels(one, 0, 0);
            }

            double total = 0.0;
            int counted = 0;

            // 가까운 타일 몇 장을 껐다 켜며 잽니다.
            for (int i = 0; i < terrains.Length && counted < 5; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null) continue;

                GameObject tile = terrain.gameObject;
                tile.SetActive(false);

                cam.Render();
                RenderTexture.active = rt;
                probe.ReadPixels(one, 0, 0);

                System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

                tile.SetActive(true);

                // 켜는 비용은 SetActive 자체가 아니라 <b>그 뒤 첫 그림</b>에서 나옵니다.
                // 지형이 그때 풀 조각을 다시 짜기 때문입니다.
                cam.Render();
                RenderTexture.active = rt;
                probe.ReadPixels(one, 0, 0);

                watch.Stop();

                double ms = watch.Elapsed.TotalMilliseconds;
                total += ms;
                counted++;

                Debug.Log("TILE " + terrain.name + " 켜는 데 " + ms.ToString("F2") + " ms");
            }

            RenderTexture.active = null;

            if (counted > 0)
            {
                double average = total / counted;

                Debug.Log("TILE 평균 " + average.ToString("F2") + " ms/장" + System.Environment.NewLine +
                          "TILE 한 프레임에 4장을 켜면 약 " + (average * 4).ToString("F0") +
                          " ms — 60프레임 기준 한 장(16.7ms)을 " +
                          (average * 4 / 16.7).ToString("F1") + "배 넘깁니다.");
            }

            cam.targetTexture = null;
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(probe);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        /// <summary>
        /// 설정을 바꿔 가며 <b>드로우 콜과 삼각형 수</b>를 셉니다.
        ///
        /// 시간을 재는 것은 그만두었습니다. 배치 렌더링으로 잰 밀리초는 같은 조건으로 두 번 돌려도
        /// 크게 달라져, 무엇이 나아졌는지 판단할 수 없었습니다.
        /// 드로우 콜은 <b>세는 값</b>이라 흔들리지 않습니다. 그리고 지금 이 게임은 CPU 바운드
        /// (CPU 70% / GPU 30%)라, 드로우 콜이 곧 프레임입니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod LowPolyLookSetup.CountFromCommandLine</c>
        /// </summary>
        [MenuItem("CarDrive/World/그리기 비용 세기 (드로우 콜 · 삼각형)")]
        public static void CountFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Vector3 grassSpot, roadSpot;
            if (!FindSpots(out grassSpot, out roadSpot))
            {
                Debug.LogError("COUNT: 풀밭을 찾지 못했습니다.");
                EditorApplication.Exit(1);
                return;
            }

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            System.Array.Sort(terrains, (a, b) => string.CompareOrdinal(a.name, b.name));

            for (int i = 0; i < terrains.Length; i++)
            {
                terrains[i].drawTreesAndFoliage = true;
                terrains[i].Flush();
            }

            GameObject camGo = new GameObject("CountCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 60f;
            cam.farClipPlane = 1200f;
            cam.transform.position = grassSpot + Vector3.up * 1.7f;
            cam.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            RenderTexture rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            Count(cam, "풀 그대로 / 컬링 없음");

            int shown = TerrainChunkCuller.ApplyNow(cam);
            Count(cam, "청크 컬링 켬 (타일 " + shown + "장)");
            TerrainChunkCuller.RestoreAll();

            // 풀만 껐을 때와 견주면 풀이 차지하는 몫이 그대로 드러납니다.
            for (int i = 0; i < terrains.Length; i++)
            {
                terrains[i].drawTreesAndFoliage = false;
                terrains[i].Flush();
            }

            Count(cam, "풀 끔 (지면만)");

            for (int i = 0; i < terrains.Length; i++)
            {
                terrains[i].drawTreesAndFoliage = true;
                terrains[i].Flush();
            }

            cam.targetTexture = null;
            Object.DestroyImmediate(camGo);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        /// <summary>한 장 그리고 그때의 드로우 콜과 삼각형 수를 적습니다.</summary>
        /// <param name="cam">쓸 카메라</param>
        /// <param name="label">무엇을 잰 것인지 적을 이름</param>
        private static void Count(Camera cam, string label)
        {
            // 첫 장은 자원이 준비되는 중이라 수가 다릅니다. 몇 장 그린 뒤에 읽습니다.
            for (int i = 0; i < 4; i++) cam.Render();

            Debug.Log("COUNT " + label.PadRight(28) +
                      " 드로우콜 " + UnityStats.drawCalls.ToString().PadLeft(5) +
                      " | SetPass " + UnityStats.setPassCalls.ToString().PadLeft(4) +
                      " | 삼각형 " + (UnityStats.triangles / 1000).ToString().PadLeft(5) + "k" +
                      " | 정점 " + (UnityStats.vertices / 1000).ToString().PadLeft(5) + "k");
        }

        // --- Private Methods ---

        /// <summary>
        /// 찍을 만한 풀밭 자리와 도로 자리를 한 타일 안에서 찾습니다.
        /// </summary>
        /// <param name="grassSpot">찾은 풀밭 자리</param>
        /// <param name="roadSpot">찾은 도로 자리</param>
        /// <returns>둘 다 찾았으면 true입니다.</returns>
        private static bool FindSpots(out Vector3 grassSpot, out Vector3 roadSpot)
        {
            grassSpot = Vector3.zero;
            roadSpot = Vector3.zero;

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

            // <b>이름순으로 줄을 세웁니다.</b>
            // FindObjectsByType 은 순서를 보장하지 않아, 실행할 때마다 다른 타일이 먼저 걸립니다.
            // 그러면 잰 시간을 실행끼리 견줄 수 없습니다. 실제로 같은 설정인데 1.58ms 와 6.65ms 가
            // 번갈아 나왔고, 그 값으로는 무엇이 나아졌는지 판단할 수 없었습니다.
            System.Array.Sort(terrains, (a, b) => string.CompareOrdinal(a.name, b.name));

            for (int i = 0; i < terrains.Length; i++)
            {
                TerrainData data = terrains[i].terrainData;
                if (data == null) continue;

                int ares = data.alphamapResolution;
                float[,,] alpha = data.GetAlphamaps(0, 0, ares, ares);
                if (alpha.GetLength(2) < 3) continue;

                Vector3 origin = terrains[i].transform.position;
                bool foundRoad = false, foundGrass = false;
                Vector3 road = Vector3.zero, grass = Vector3.zero;

                for (int z = 0; z < ares && !(foundRoad && foundGrass); z++)
                {
                    for (int x = 0; x < ares; x++)
                    {
                        Vector3 world = new Vector3(
                            origin.x + (x + 0.5f) / ares * data.size.x,
                            0f,
                            origin.z + (z + 0.5f) / ares * data.size.z);

                        if (!foundRoad && alpha[z, x, 2] > 0.85f)
                        {
                            road = world;
                            foundRoad = true;
                        }
                        else if (!foundGrass && alpha[z, x, 0] > 0.97f)
                        {
                            grass = world;
                            foundGrass = true;
                        }

                        if (foundRoad && foundGrass) break;
                    }
                }

                if (!foundRoad || !foundGrass) continue;

                road.y = SampleGround(road);
                grass.y = SampleGround(grass);

                grassSpot = grass;
                roadSpot = road;
                return true;
            }

            return false;
        }

        /// <summary>어느 터레인 위든 그 자리의 지면 높이를 찾습니다.</summary>
        /// <param name="world">월드 좌표. y는 무시합니다.</param>
        /// <returns>지면 높이</returns>
        private static float SampleGround(Vector3 world)
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

            for (int i = 0; i < terrains.Length; i++)
            {
                TerrainData data = terrains[i].terrainData;
                if (data == null) continue;

                Vector3 origin = terrains[i].transform.position;
                if (world.x < origin.x || world.x > origin.x + data.size.x) continue;
                if (world.z < origin.z || world.z > origin.z + data.size.z) continue;

                return terrains[i].SampleHeight(world) + origin.y;
            }

            return 0f;
        }

        /// <summary>지면 머티리얼을 만들거나 갱신합니다.</summary>
        /// <param name="report">결과를 적을 목록</param>
        /// <returns>준비된 머티리얼. 셰이더를 못 찾으면 null입니다.</returns>
        private static Material BuildTerrainMaterial(List<string> report)
        {
            Shader shader = Shader.Find(TerrainShaderName);
            if (shader == null)
            {
                report.Add("  [실패] " + TerrainShaderName + " 셰이더를 찾지 못했습니다.");
                return null;
            }

            Material mat = LoadOrCreate(TerrainMaterialPath, shader);

            mat.SetColor("_GrassColorA", GrassDark);
            mat.SetColor("_GrassColorB", GrassLight);
            mat.SetColor("_DirtColorA", DirtDark);
            mat.SetColor("_DirtColorB", DirtLight);
            mat.SetColor("_RoadColorA", RoadDark);
            mat.SetColor("_RoadColorB", RoadLight);
            mat.SetColor("_ShadowColor", ShadowTint);
            mat.SetFloat("_ColorNoiseScale", 0.022f);
            // 면 법선(_FlatShading)은 <b>화면 미분</b>으로 구하기 때문에 터레인 삼각형이
            // 그대로 드러납니다. 게다가 터레인 삼각형은 거리에 따라 LOD로 바뀌므로
            // 각진 얼룩이 카메라를 따라 움직여 조잡해 보입니다. 그래서 꺼 둡니다.
            //
            // 밝기 단계(_ShadeSteps)도 1로 둡니다. 완만한 지형에 단계를 주면
            // 등고선 같은 띠가 생기는데, 그 띠 자체가 또 하나의 경계입니다.
            // 로우폴리·코지의 인상은 각진 음영이 아니라 <b>텍스처 없는 평평한 색</b>에서 나옵니다.
            mat.SetFloat("_FlatShading", 0f);
            mat.SetFloat("_ShadeSteps", 1f);
            mat.SetFloat("_AmbientBoost", 1f);
            mat.SetFloat("_TextureBlend", 0f);

            EditorUtility.SetDirty(mat);
            report.Add("  지면 머티리얼 준비 완료. (텍스처 섞기 0 — 색만 씁니다)");
            return mat;
        }

        /// <summary>풀 머티리얼을 만들거나 갱신합니다.</summary>
        /// <param name="report">결과를 적을 목록</param>
        /// <returns>준비된 머티리얼. 셰이더를 못 찾으면 null입니다.</returns>
        private static Material BuildGrassMaterial(List<string> report)
        {
            Shader shader = Shader.Find(GrassShaderName);
            if (shader == null)
            {
                report.Add("  [실패] " + GrassShaderName + " 셰이더를 찾지 못했습니다.");
                return null;
            }

            Material mat = LoadOrCreate(GrassMaterialPath, shader);

            // 지면과 <b>똑같은</b> 잔디 색을 넣습니다. 여기가 어긋나면 풀밭 경계에 선이 보입니다.
            mat.SetColor("_GrassColorA", GrassDark);
            mat.SetColor("_GrassColorB", GrassLight);
            mat.SetColor("_TipColor", GrassTip);
            mat.SetColor("_ShadowColor", ShadowTint);
            mat.SetFloat("_ColorNoiseScale", 0.022f);
            // 그라데이션의 세기입니다. <b>가까운 풀에만</b> 걸립니다.
            // 멀리서는 잎 하나가 몇 픽셀이라, 명암이 남으면 부피가 아니라 잡음으로 보입니다.
            mat.SetFloat("_RootTint", 0.26f);
            mat.SetFloat("_TipBlend", 0.35f);
            mat.SetFloat("_GradientNear", Settings.gradientNear);

            // 밟힘. 차·사람·유령이 지나가면 그 자리의 풀이 눕습니다. (GrassPusher)
            // 눕는 정도를 1로 두는 것이 중요합니다. 덜 눕히면 차 밑에 남은 풀이
            // 차 바닥을 뚫고 실내로 올라옵니다.
            mat.SetFloat("_PushLay", Settings.pushLay);
            mat.SetFloat("_PushSpread", Settings.pushSpread);
            mat.SetFloat("_PushHeightReach", Settings.pushHeightReach);
            mat.SetFloat("_GradientFar", Settings.gradientFar);
            // 완전히 눕힙니다. 조금이라도 잎의 진짜 법선이 섞이면 잎마다 밝기가 달라져
            // 그 차이가 윤곽선으로 보입니다.
            mat.SetFloat("_NormalUp", 1f);

            // 색 그라디언트의 기준 높이입니다. 잎이 가장 클 때의 키(약 0.83m)에 맞춥니다.
            // 잎 자신의 비율이 아니라 이 높이로 색을 정해야 키가 다른 잎끼리 색이 이어집니다.
            mat.SetFloat("_CanopyHeight", 0.75f);
            mat.SetFloat("_WindStrength", 0.18f);
            mat.SetFloat("_WindSpeed", 1.1f);
            mat.SetFloat("_WindScale", 0.08f);
            mat.SetFloat("_ShadeSteps", 1f);
            mat.SetFloat("_AmbientBoost", 1f);

            // 그리기를 멈추는 거리보다 <b>앞에서</b> 눕기 시작해야 잘린 자국이 안 보입니다.
            // 눕는 구간을 <b>길게</b> 잡습니다.
            // 짧게 잡으면 그 구간이 통째로 띠처럼 보여, 풀밭이 원형으로 잘린 자국이 남습니다.
            // 35m 부터 69m 까지 34m 에 걸쳐 낮아지면 어디서 끊겼는지 짚을 수 없습니다.
            // 더 일찍 시작하면 경계는 확실히 사라지지만 중간 거리가 휑해집니다.
            mat.SetFloat("_FadeStart", DetailDistance * 0.5f);
            mat.SetFloat("_FadeEnd", DetailDistance * 0.98f);

            // 포기가 수십만 개라 인스턴싱 없이는 그릴 수 없습니다.
            mat.enableInstancing = true;

            EditorUtility.SetDirty(mat);
            report.Add("  풀 머티리얼 준비 완료. (인스턴싱 켬, " +
                       (DetailDistance * 0.5f).ToString("F0") + "m부터 서서히 눕기 시작)");
            report.Add("    그라데이션: " + Settings.gradientNear + "m까지 유지 -> " + Settings.gradientFar + "m에서 단색");
            return mat;
        }

        /// <summary>
        /// 풀 포기 하나의 메시와 프리팹을 만듭니다.
        ///
        /// 잎 하나는 밑동이 넓고 끝이 좁은 사각형 두 장입니다. 포기 하나에 네 장을 세워
        /// 사방을 채웁니다. 잎을 삼각형 한 장으로 하면 더 싸지만, 끝이 뾰족해
        /// 바람에 휠 때 각져 보입니다.
        /// </summary>
        /// <param name="grassMat">잎에 씌울 머티리얼</param>
        /// <param name="report">결과를 적을 목록</param>
        /// <returns>만들어진 프리팹</returns>
        private static GameObject BuildGrassPrefab(Material grassMat, List<string> report)
        {
            if (grassMat == null) return null;

            int BladeCount = Settings.bladesPerTuft;
            float BladeHeight = Settings.bladeHeight;
            float TuftRadius = Settings.tuftRadius;

            const float BladeWidth = 0.034f;

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            // 언제 다시 구워도 같은 모양이 나오도록 씨앗을 고정합니다.
            Random.State prev = Random.state;
            Random.InitState(20260818);

            for (int i = 0; i < BladeCount; i++)
            {
                float angle = (i / (float)BladeCount) * Mathf.PI * 2f + Random.Range(-0.5f, 0.5f);
                float dist = TuftRadius * Mathf.Sqrt(Random.value);
                Vector3 root = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                float yaw = Random.Range(0f, Mathf.PI * 2f);
                Vector3 side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * BladeWidth;
                Vector3 face = Vector3.Cross(Vector3.up, side).normalized;

                float height = BladeHeight * Random.Range(0.7f, 1.3f);
                Vector3 lean = face * (height * Random.Range(0.1f, 0.3f));
                Vector3 tip = root + Vector3.up * height + lean;

                // 잎 하나가 <b>삼각형 한 장</b>입니다.
                //
                // 사각형으로 하면 정점 넷에 삼각형 둘이 듭니다. 잎 끝은 어차피 폭이
                // 거의 0으로 좁아지니, 끝을 한 점으로 모으면 정점 하나와 삼각형 하나가 그대로 빠집니다.
                // 포기가 26만 개라 이 차이가 정점 630만 개와 470만 개의 차이가 됩니다.
                int b = verts.Count;

                verts.Add(root - side);
                verts.Add(root + side);
                verts.Add(tip);

                for (int n = 0; n < 3; n++) normals.Add(face);

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(0.5f, 1f));

                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            }

            Random.state = prev;

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GrassMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, GrassMeshPath);
            }

            mesh.Clear();
            mesh.name = "GrassTuft";
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            EditorUtility.SetDirty(mesh);

            // 프리팹으로 감쌉니다. 터레인 디테일은 메시가 아니라 게임오브젝트를 받습니다.
            GameObject temp = new GameObject("GrassTuft");
            temp.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = temp.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = grassMat;

            // 포기가 수십만 개입니다. 그림자를 드리우게 두면 그것만으로 프레임이 무너집니다.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, GrassPrefabPath);
            Object.DestroyImmediate(temp);

            report.Add("  풀 포기 메시: 잎 " + BladeCount + "장 / 삼각형 " + (tris.Count / 3) +
                       "개 / 키 " + BladeHeight.ToString("F2") + "m / 반경 " + TuftRadius.ToString("F2") + "m");
            return prefab;
        }

        /// <summary>
        /// 모든 터레인에 지면 머티리얼을 씌우고 풀을 심습니다.
        ///
        /// 실제 일은 <see cref="TerrainDressing"/> 이 합니다.
        /// 터레인 굽기도 같은 코드를 부르기 때문에, 여기 두면 두 벌이 되어 언젠가 어긋납니다.
        /// </summary>
        /// <param name="terrainMat">지면 머티리얼</param>
        /// <param name="grassPrefab">풀 포기 프리팹</param>
        /// <param name="report">결과를 적을 목록</param>
        private static void ApplyToTerrains(Material terrainMat, GameObject grassPrefab, List<string> report)
        {
            if (terrainMat == null) return;

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            TerrainDressing.Apply(terrains, terrainMat, grassPrefab, report);
        }

        /// <summary>
        /// 조명을 아늑한 값으로 잡습니다.
        ///
        /// 코지 룩에서 가장 중요한 것은 <b>그늘이 검지 않은 것</b>입니다.
        /// 주변광을 하늘/중간/땅 세 색으로 나눠 주면 그늘에 하늘빛과 풀빛이 스며들어
        /// 부드러워집니다.
        /// </summary>
        /// <param name="report">결과를 적을 목록</param>
        private static void ApplyCozyLighting(List<string> report)
        {
            // 조명의 주인은 SkyController 입니다.
            //
            // 여기서 RenderSettings 에 직접 값을 써 봤자, SkyController 가 매 프레임
            // LateUpdate 에서 시각에 맞춰 다시 덮어씁니다. 그래서 값을 직접 쓰지 않고
            // <b>SkyController 가 쓸 색을</b> 코지 톤으로 바꿔 둡니다.
            //
            // 이렇게 해야 해가 뜨고 지는 것과 구름이 끼는 것이 그대로 살아 있으면서
            // 색조만 아늑해집니다.
            SkyController sky = Object.FindAnyObjectByType<SkyController>();

            if (sky != null)
            {
                Undo.RecordObject(sky, "로우폴리 코지 룩으로 전환");

                sky.enabled = true;
                sky.driveAmbient = true;
                sky.driveFogColor = true;

                // 주변광 — 하늘에서 내려오는 빛이므로 하늘과 같은 계열이어야 합니다.
                sky.dayAmbientSky = new Color(0.639f, 0.588f, 0.565f);
                sky.nightAmbientSky = new Color(0.055f, 0.070f, 0.105f);
                sky.groundAmbientScale = 0.45f;

                // 볕 — 살짝 노란기가 돌아야 따뜻해 보입니다.
                sky.dayLightColor = new Color(1f, 0.937f, 0.831f);
                sky.duskLightColor = new Color(1f, 0.690f, 0.420f);
                sky.moonLightColor = new Color(0.545f, 0.647f, 0.918f);
                sky.moonIntensity = 0.10f;

                // 구름 — 짙게 끼면 주변광은 55%, 햇빛은 45%까지 떨어집니다.
                sky.weatherDarkFloor = 0.55f;
                sky.weatherSunFloor = 0.45f;

                // <b>안개 색 = 하늘 지평선 색.</b> 이 룩의 핵심입니다.
                //
                // 멀리 있는 것이 점점 옅어지다 하늘에 녹아 사라지는 것을 대기 원근이라 합니다.
                // 두 색이 어긋나면 산이 하늘에 녹지 않고 <b>회색 띠를 두른 채</b> 떠 보입니다.
                sky.dayFogColor = SkyHorizon;
                sky.nightFogColor = new Color(0.045f, 0.055f, 0.090f);

                EditorUtility.SetDirty(sky);

                report.Add("  SkyController: 켰습니다. 해의 고도·구름에 따라 광량이 바뀝니다.");
                report.Add("    낮/밤 주변광, 볕 색, 달빛 하한 0.10, 구름 어둡기(주변광 55% / 햇빛 45%)");
            }
            else
            {
                report.Add("  [경고] SkyController 를 찾지 못했습니다. 시간대 광량이 적용되지 않습니다.");
            }

            WeatherRig rig = Object.FindAnyObjectByType<WeatherRig>();
            if (rig != null)
            {
                Undo.RecordObject(rig, "로우폴리 코지 룩으로 전환");

                // 주변광은 SkyController 가 이미 구름까지 반영해 다룹니다.
                // 둘 다 켜면 매 프레임 서로의 값을 덮어써 실행 순서에 따라 결과가 달라집니다.
                rig.controlAmbient = false;

                // 켜면 안개를 지수형으로 덮어써, 아래에서 잡는 선형 안개가 사라집니다.
                // 맑은 날에는 안개를 아예 꺼 버려서 멀리 있는 맵 가장자리가 드러납니다.
                rig.controlRenderFog = false;

                // 켜면 카메라 파클립을 줄여 지형이 잘려 보일 수 있습니다.
                rig.controlVisibility = false;

                EditorUtility.SetDirty(rig);
                report.Add("  WeatherRig: 조명·안개는 SkyController 에 맡기고 비 파티클만 담당합니다.");
            }

            TimeSystem time = Object.FindAnyObjectByType<TimeSystem>();
            if (time != null)
            {
                Undo.RecordObject(time, "로우폴리 코지 룩으로 전환");
                time.sunMaxIntensity = 1.25f;
                EditorUtility.SetDirty(time);

                report.Add("  TimeSystem.sunMaxIntensity: 1.25 (한낮 볕의 세기)");
            }

            // 그림자만 여기서 잡습니다. SkyController 는 그림자 설정을 건드리지 않습니다.
            Light sun = RenderSettings.sun;
            if (sun == null && sky != null) sun = sky.sun;

            if (sun != null)
            {
                Undo.RecordObject(sun, "로우폴리 코지 룩으로 전환");

                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.55f;   // 진하게 두면 아늑함이 사라집니다.

                EditorUtility.SetDirty(sun);
                report.Add("  그림자: 부드럽게 / 진하기 55%");
            }

            RenderSettings.reflectionIntensity = 0.6f;

            // 안개 — 색은 SkyController 가 매 프레임 시각에 맞춰 덮어씁니다.
            // 여기서는 <b>거리만</b> 정합니다.
            // 풀은 70m 에서 끝납니다. 안개가 그보다 뒤에서 시작하면 풀이 없는 맨땅이
            // 또렷하게 보여 경계가 됩니다. 안개를 앞으로 당겨 그 구간을 덮습니다.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            // 대기 원근을 살리려면 안개가 <b>충분히 가까이서</b> 시작해야 합니다.
            // 멀리서 시작하면 먼 산이 또렷하게 남아 그림이 납작해집니다.
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 340f;

            // 하늘 색을 18단계로 끊고 있었습니다. PSX 룩에서는 일부러 넣은 것이지만
            // 지금은 하늘에 <b>가로 띠</b>로 보입니다. 이것도 경계입니다.
            Material skybox = RenderSettings.skybox;
            if (skybox != null && skybox.HasProperty("_Bands"))
            {
                skybox.SetFloat("_Bands", 256f);
                EditorUtility.SetDirty(skybox);

                report.Add("  하늘 색 계단: 256단계로 올려 껐습니다. (18단계라 가로 띠가 보였습니다)");
            }

            report.Add("  안개: 선형 70~340m, 색은 하늘 지평선과 같은 값 (대기 원근)");
        }

        /// <summary>
        /// 하늘 그라디언트를 따뜻한 톤으로 바꿉니다.
        ///
        /// 레퍼런스의 인상은 <b>하늘이 파랗지 않다는 것</b>에서 절반이 나옵니다.
        /// 지평선은 따뜻한 살구빛, 위로 갈수록 조금 어둡고 덜 따뜻해집니다.
        /// 파란 하늘을 두고 지면만 노랗게 바꾸면 두 색이 서로 밀어내 촌스러워집니다.
        /// </summary>
        /// <param name="report">결과를 적을 목록</param>
        private static void ApplySkyColors(List<string> report)
        {
            Material skybox = RenderSettings.skybox;
            if (skybox == null)
            {
                report.Add("  [경고] 스카이박스가 없어 하늘 색을 바꾸지 못했습니다.");
                return;
            }

            if (skybox.HasProperty("_DayHorizon")) skybox.SetColor("_DayHorizon", SkyHorizon);
            if (skybox.HasProperty("_DayTop")) skybox.SetColor("_DayTop", SkyTop);

            // 해 질 무렵의 붉은 기도 조금 눅여 둡니다. 낮이 이미 따뜻해서 그대로 두면 과합니다.
            if (skybox.HasProperty("_DuskColor")) skybox.SetColor("_DuskColor", new Color(0.851f, 0.502f, 0.290f));

            EditorUtility.SetDirty(skybox);
            report.Add("  하늘: 지평선 살구빛 / 천정 따뜻한 회색 (파란 하늘을 걷어냈습니다)");
        }

        /// <summary>
        /// 컬러 그레이딩으로 화면 전체의 색감을 잡습니다.
        ///
        /// 팔레트만 바꿔서는 레퍼런스의 느낌이 나오지 않습니다.
        /// 그 그림의 특징은 <b>낮은 채도, 낮은 대비, 따뜻한 치우침</b>인데
        /// 이것은 개별 물체의 색이 아니라 <b>다 그린 화면 전체</b>에 거는 것이기 때문입니다.
        ///
        /// 씬에 이미 있는 볼륨 프로파일에 얹습니다. 원래 있던 Bloom·Vignette 같은 것은
        /// 건드리지 않고 색 관련 항목만 다룹니다.
        /// </summary>
        /// <param name="report">결과를 적을 목록</param>
        private static void ApplyColorGrading(List<string> report)
        {
            Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include);
            VolumeProfile profile = null;

            for (int i = 0; i < volumes.Length; i++)
            {
                // 씬 전체에 거는 볼륨을 찾습니다. 좁은 구역에만 거는 것은 건너뜁니다.
                if (!volumes[i].isGlobal || volumes[i].sharedProfile == null) continue;

                profile = volumes[i].sharedProfile;
                break;
            }

            if (profile == null)
            {
                report.Add("  [경고] 전역 볼륨을 찾지 못해 컬러 그레이딩을 걸지 못했습니다.");
                return;
            }

            // 톤 매핑 — 밝은 쪽이 흰색으로 뭉개지지 않게 눌러 줍니다.
            // 이것이 없으면 노란 풀밭의 밝은 부분이 하얗게 타 버립니다.
            Tonemapping tone;
            if (!profile.TryGet(out tone)) tone = profile.Add<Tonemapping>(true);
            tone.active = true;
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.Neutral;

            // 화이트 밸런스 — 화면 전체를 따뜻한 쪽으로 기울입니다.
            // 물체마다 색을 노랗게 칠하는 것과 다릅니다. 이쪽은 <b>빛의 색</b>을 바꾸는 것이라
            // 그늘과 하늘까지 한꺼번에 같은 방향으로 물듭니다.
            WhiteBalance balance;
            if (!profile.TryGet(out balance)) balance = profile.Add<WhiteBalance>(true);
            balance.active = true;
            balance.temperature.overrideState = true;
            balance.temperature.value = 8f;
            balance.tint.overrideState = true;
            balance.tint.value = 3f;

            // 채도와 대비 — 레퍼런스는 둘 다 낮습니다.
            //
            // 여기를 더 밀면 파스텔이 됩니다. 한 번 그렇게 해 봤다가(대비 -16 / 채도 -14)
            // 화면이 흰빛으로 떠서 되돌렸습니다. 색을 빼는 것과 <b>흰색을 섞는 것</b>은 다릅니다.
            // 대비를 낮추면 어두운 곳이 들려 올라와, 그늘이 검게 꺼지지 않고 뿌옇게 남습니다.
            // 대비를 낮추면 어두운 곳이 들려 올라와, 그늘이 검게 꺼지지 않고 뿌옇게 남습니다.
            // 그 뿌연 그늘이 아늑한 인상을 만듭니다.
            ColorAdjustments grade;
            if (!profile.TryGet(out grade)) grade = profile.Add<ColorAdjustments>(true);
            grade.active = true;
            grade.contrast.overrideState = true;
            grade.contrast.value = -9f;
            grade.saturation.overrideState = true;
            grade.saturation.value = -6f;
            grade.postExposure.overrideState = true;
            grade.postExposure.value = 0f;

            EditorUtility.SetDirty(profile);

            report.Add("  컬러 그레이딩: 톤매핑 Neutral / 색온도 +8 / 대비 -9 / 채도 -6");
            report.Add("    (" + profile.name + " 에 얹었습니다. Bloom·Vignette 는 그대로 뒀습니다)");
        }

        /// <summary>머티리얼을 불러오고 없으면 만듭니다.</summary>
        /// <param name="path">머티리얼 경로</param>
        /// <param name="shader">쓸 셰이더</param>
        /// <returns>준비된 머티리얼</returns>
        private static Material LoadOrCreate(string path, Shader shader)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            return mat;
        }

        /// <summary>폴더가 없으면 만듭니다.</summary>
        /// <param name="folder">Assets 로 시작하는 폴더 경로</param>
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
    }
}
