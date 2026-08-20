using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;

namespace CarDrive.EditorTools.ProcGen
{
    /// <summary>
    /// 이미 구워진 터레인 위에 산을 올립니다.
    ///
    /// ── 왜 굽는 도구를 고치지 않는가 ──
    ///
    /// <see cref="WorldTerrainBaker"/> 는 도로를 깎고 마을을 평탄하게 만드는 일까지
    /// 한 덩어리로 하고 있습니다. 거기에 산을 끼워 넣으면 산 하나 고칠 때마다
    /// 도로가 함께 흔들립니다. 그래서 <b>다 구운 뒤에 더하는</b> 방식을 씁니다.
    /// 산이 마음에 안 들면 산만 빼고 다시 올리면 됩니다.
    ///
    /// ── 쓰는 알고리즘: 능선 다중프랙탈 (Musgrave) ──
    ///
    /// 보통 지형에 쓰는 fBm 은 <b>둥근 언덕</b>을 만듭니다. 굽는 도구가 이미 그걸 씁니다.
    /// 여기 필요한 건 <b>날카로운 능선</b>입니다.
    ///
    /// 방법은 간단합니다. 노이즈를 절댓값으로 접습니다.
    ///   <c>1 - |n * 2 - 1|</c>
    /// 골짜기였던 자리가 접히면서 <b>뾰족한 마루</b>가 됩니다. 제곱해서 날을 더 세웁니다.
    ///
    /// 그다음이 "다중프랙탈"의 핵심입니다. 다음 겹의 세기를 <b>앞 겹의 높이로 곱합니다.</b>
    /// 그러면 잔주름이 능선 위에만 생기고 골짜기는 매끈하게 남습니다.
    /// 실제 산이 그렇게 생겼습니다. 침식은 골짜기를 깎아 내지 봉우리를 깎지 않습니다.
    ///
    /// ── 두 가지 마스크 ──
    ///
    /// <b>산맥 마스크</b>: 낮은 주기 노이즈를 문턱값으로 잘라 산이 <b>줄기로 뭉치게</b> 합니다.
    ///   이게 없으면 온 맵이 고르게 우글거려 산이 아니라 자갈밭이 됩니다.
    ///
    /// <b>비움 마스크</b>: 도로와 마을 근처를 0으로 눌러 둡니다.
    ///   길 위에 산이 솟으면 주행이 불가능해집니다.
    ///
    /// ── 타일 이음매 ──
    ///
    /// 올리는 높이는 <b>월드 좌표만의 함수</b>입니다. 이웃 타일의 맞닿은 줄은
    /// 정확히 같은 월드 좌표를 갖고, 타일 크기 100m 를 128칸으로 나눈 간격은
    /// 2의 거듭제곱이라 부동소수점 오차도 없습니다. 그래서 이음매가 저절로 맞습니다.
    /// 확인은 CarDrive > World > 터레인 검증 으로 합니다.
    /// </summary>
    public static class MountainTool
    {
        /// <summary>메인 씬 경로입니다.</summary>
        private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

        /// <summary>무엇을 올렸는지 적어 두는 쪽지의 경로입니다.</summary>
        private const string StampPath = "Assets/_Project/03.DataAssets/Terrain/Generated/MountainStamp.asset";

        // --- Public Methods ---

        /// <summary>산을 올립니다. 이미 올라가 있으면 걷어 내고 다시 올립니다.</summary>
        [MenuItem("CarDrive/World/0. 산 만들기")]
        public static void Raise()
        {
            List<string> report = new List<string>();

            MountainStamp stamp = LoadOrCreateStamp();

            // 이미 올라가 있으면 먼저 걷어 냅니다.
            // 값을 바꿔 다시 실행할 때 산이 겹쳐 쌓이지 않게 하는 유일한 방법입니다.
            //
            // 높이맵이 16비트라 걷어 내고 다시 올릴 때마다 2.1mm 쯤 어긋납니다.
            // 여러 번 조정해도 문제될 크기는 아니지만, 완전히 되돌리려면 터레인을 다시 구우세요.
            if (stamp.applied) Apply(stamp, -1f, report);

            stamp.applied = false;
            Apply(stamp, 1f, report);
            stamp.applied = true;

            EditorUtility.SetDirty(stamp);
            AssetDatabase.SaveAssets();

            Log("산 만들기", report);
        }

        /// <summary>올려 둔 산을 걷어 냅니다.</summary>
        [MenuItem("CarDrive/World/0. 산 지우기")]
        public static void Lower()
        {
            List<string> report = new List<string>();

            MountainStamp stamp = AssetDatabase.LoadAssetAtPath<MountainStamp>(StampPath);
            if (stamp == null || !stamp.applied)
            {
                report.Add("· 올려 둔 산이 없습니다.");
                Log("산 지우기", report);
                return;
            }

            Apply(stamp, -1f, report);

            stamp.applied = false;
            EditorUtility.SetDirty(stamp);
            AssetDatabase.SaveAssets();

            Log("산 지우기", report);
        }

        /// <summary>
        /// 올려 둔 산을 <b>기억에서만</b> 지웁니다. 터레인은 건드리지 않습니다.
        ///
        /// 터레인을 다시 구우면 높이맵이 처음부터 다시 만들어져 산이 함께 사라집니다.
        /// 그런데 쪽지에는 "산이 올라가 있다"고 남아 있어서, 다음 '산 만들기'가
        /// <b>있지도 않은 산을 걷어 내며 땅을 파냅니다.</b> 그것을 막습니다.
        /// </summary>
        public static void ForgetStamp()
        {
            MountainStamp stamp = AssetDatabase.LoadAssetAtPath<MountainStamp>(StampPath);
            if (stamp == null || !stamp.applied) return;

            stamp.applied = false;
            EditorUtility.SetDirty(stamp);
            AssetDatabase.SaveAssets();

            Debug.Log("MountainTool: 터레인을 다시 구웠으므로 올려 둔 산 기록을 지웠습니다.");
        }

        /// <summary>
        /// 명령줄에서 씬을 열고 산을 올린 뒤 저장합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.ProcGen.MountainTool.RaiseFromCommandLine</c>
        /// </summary>
        public static void RaiseFromCommandLine()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Raise();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // --- Private Methods : 적용 ---

        /// <summary>
        /// 모든 터레인의 높이맵에 산을 더하거나 뺍니다.
        /// </summary>
        /// <param name="stamp">쓸 설정</param>
        /// <param name="sign">1이면 올리고, -1이면 걷어 냅니다.</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void Apply(MountainStamp stamp, float sign, List<string> report)
        {
            WorldStreamer world = Object.FindAnyObjectByType<WorldStreamer>(FindObjectsInactive.Include);
            if (world == null)
            {
                report.Add("! WorldStreamer 를 찾지 못했습니다. 도로 자리를 알 수 없어 중단합니다.");
                return;
            }

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            if (terrains.Length == 0)
            {
                report.Add("! 터레인이 없습니다. CarDrive > World > 터레인 월드 굽기 를 먼저 실행하세요.");
                return;
            }

            WorldLayout layout = WorldLayout.From(world);

            // 씨앗마다 노이즈를 읽는 자리를 옮깁니다.
            // Mathf.PerlinNoise 는 씨앗을 받지 않으므로 이렇게 흉내 냅니다.
            System.Random random = new System.Random(stamp.seed);
            Vector2 offset = new Vector2(
                (float)random.NextDouble() * 4000f,
                (float)random.NextDouble() * 4000f);

            float highest = 0f;
            float heightScale = 0f;
            int clamped = 0;
            int raisedSamples = 0;

            for (int t = 0; t < terrains.Length; t++)
            {
                Terrain terrain = terrains[t];
                if (terrain == null || terrain.terrainData == null) continue;

                TerrainData data = terrain.terrainData;
                int res = data.heightmapResolution;

                Vector3 origin = terrain.transform.position;
                Vector3 size = data.size;

                heightScale = size.y;

                // 칸 간격입니다. 타일 100m 를 128칸으로 나누므로 오차 없이 떨어집니다.
                float stepX = size.x / (res - 1);
                float stepZ = size.z / (res - 1);

                float[,] heights = data.GetHeights(0, 0, res, res);

                // GetHeights 는 [z, x] 순서입니다. 첫 축이 세로(Z)입니다.
                for (int z = 0; z < res; z++)
                {
                    float wz = origin.z + z * stepZ;

                    for (int x = 0; x < res; x++)
                    {
                        float wx = origin.x + x * stepX;

                        float delta = MountainHeight(wx, wz, stamp, offset, layout);
                        if (delta <= 0f) continue;

                        float next = heights[z, x] + delta * sign;

                        if (next > 1f)
                        {
                            next = 1f;
                            clamped++;
                        }
                        else if (next < 0f)
                        {
                            next = 0f;
                            clamped++;
                        }

                        heights[z, x] = next;

                        if (sign > 0f)
                        {
                            highest = Mathf.Max(highest, delta);
                            raisedSamples++;
                        }
                    }
                }

                data.SetHeights(0, 0, heights);
                EditorUtility.SetDirty(data);
            }

            if (sign > 0f)
            {
                report.Add("· 터레인 " + terrains.Length + "장에 산을 올렸습니다.");
                report.Add("· 가장 높은 봉우리: 주변 지면보다 " + (highest * heightScale).ToString("F1") + "m " +
                           "(터레인 높이 범위 " + heightScale.ToString("F0") + "m 기준)");
                report.Add("· 솟은 지점 " + raisedSamples + "곳 — 도로 " + stamp.roadClearance +
                           "m 안쪽과 마을은 그대로 둡니다. (길 " + layout.RoadCount + "개)");
            }
            else
            {
                report.Add("· 터레인 " + terrains.Length + "장에서 산을 걷어 냈습니다.");
            }

            if (clamped > 0)
            {
                report.Add("! 높이맵 천장에 " + clamped + "곳이 닿아 봉우리가 잘렸습니다.");
                report.Add("  MountainStamp 의 amplitude 를 낮추세요. 잘린 곳은 되돌려도 완전히 복구되지 않습니다.");
            }
        }

        // --- Private Methods : 노이즈 ---

        /// <summary>
        /// 한 지점에서 산이 얼마나 솟을지 구합니다. 0~amplitude 범위입니다.
        ///
        /// 월드 좌표만 넣으면 값이 정해지므로 타일이 달라도 이음매가 맞습니다.
        /// </summary>
        /// <param name="wx">월드 X</param>
        /// <param name="wz">월드 Z</param>
        /// <param name="stamp">쓸 설정</param>
        /// <param name="offset">씨앗에서 나온 노이즈 읽는 자리</param>
        /// <param name="layout">비워 둘 길과 마을</param>
        /// <returns>정규화 높이 증가분(0~1)</returns>
        private static float MountainHeight(float wx, float wz, MountainStamp stamp,
                                            Vector2 offset, WorldLayout layout)
        {
            // 길과 마을 근처는 아예 계산하지 않습니다. 대부분의 지점이 여기서 걸러집니다.
            float clearance = Clearance(wx, wz, stamp, layout);
            if (clearance <= 0f) return 0f;

            // 산맥이 설 자리인지 먼저 봅니다. 아니면 능선을 계산할 이유가 없습니다.
            float range = RangeMask(wx, wz, stamp, offset);
            if (range <= 0f) return 0f;

            float ridge = Ridge(wx, wz, stamp, offset);

            return stamp.amplitude * range * ridge * clearance;
        }

        /// <summary>
        /// 산맥 마스크입니다. 낮은 주기 노이즈를 문턱값으로 잘라 산을 줄기로 뭉칩니다.
        /// </summary>
        /// <param name="wx">월드 X</param>
        /// <param name="wz">월드 Z</param>
        /// <param name="stamp">쓸 설정</param>
        /// <param name="offset">노이즈 읽는 자리</param>
        /// <returns>0~1. 0이면 평지입니다.</returns>
        private static float RangeMask(float wx, float wz, MountainStamp stamp, Vector2 offset)
        {
            float n = Mathf.PerlinNoise(
                wx * stamp.rangeFrequency + offset.x + 500f,
                wz * stamp.rangeFrequency + offset.y + 500f);

            return SmoothStep(stamp.rangeThreshold, stamp.rangeThreshold + stamp.rangeFade, n);
        }

        /// <summary>
        /// 능선 다중프랙탈입니다. 노이즈를 접어 마루를 만들고, 잔주름을 능선 위에만 얹습니다.
        /// </summary>
        /// <param name="wx">월드 X</param>
        /// <param name="wz">월드 Z</param>
        /// <param name="stamp">쓸 설정</param>
        /// <param name="offset">노이즈 읽는 자리</param>
        /// <returns>0~1. 1이 마루입니다.</returns>
        private static float Ridge(float wx, float wz, MountainStamp stamp, Vector2 offset)
        {
            // 좌표를 먼저 휩니다. 이걸 안 하면 능선이 격자를 따라 곧게 뻗습니다.
            float warpX = (Mathf.PerlinNoise(
                wx * stamp.warpFrequency + offset.x,
                wz * stamp.warpFrequency + offset.y) - 0.5f) * 2f * stamp.warpStrength;

            float warpZ = (Mathf.PerlinNoise(
                wx * stamp.warpFrequency + offset.x + 137.7f,
                wz * stamp.warpFrequency + offset.y + 191.3f) - 0.5f) * 2f * stamp.warpStrength;

            float x = wx + warpX;
            float z = wz + warpZ;

            float sum = 0f;
            float norm = 0f;
            float amp = 1f;
            float freq = stamp.ridgeFrequency;

            // 다음 겹의 세기입니다. 앞 겹이 낮았던 곳에서는 잔주름도 죽습니다.
            float weight = 1f;

            for (int o = 0; o < stamp.octaves; o++)
            {
                float n = Mathf.PerlinNoise(x * freq + offset.x, z * freq + offset.y);

                // 접어서 마루를 만들고, 제곱해서 날을 세웁니다.
                n = 1f - Mathf.Abs(n * 2f - 1f);
                n *= n;

                n *= weight;
                weight = Mathf.Clamp01(n * 2f);

                sum += n * amp;
                norm += amp;

                amp *= stamp.gain;
                freq *= stamp.lacunarity;
            }

            return norm > 0f ? Mathf.Clamp01(sum / norm) : 0f;
        }

        /// <summary>
        /// 비움 마스크입니다. 길과 마을 근처를 0으로 눌러 둡니다.
        /// </summary>
        /// <param name="wx">월드 X</param>
        /// <param name="wz">월드 Z</param>
        /// <param name="stamp">쓸 설정</param>
        /// <param name="layout">길과 마을</param>
        /// <returns>0~1. 0이면 손대지 않습니다.</returns>
        private static float Clearance(float wx, float wz, MountainStamp stamp, WorldLayout layout)
        {
            float road = SmoothStep(
                stamp.roadClearance,
                stamp.roadClearance + stamp.roadFade,
                layout.DistanceToRoad(wx, wz));

            if (road <= 0f) return 0f;

            float village = SmoothStep(
                layout.VillageRadius,
                layout.VillageRadius + stamp.villageFade,
                layout.DistanceToVillage(wx, wz));

            return road * village;
        }

        /// <summary>가장자리가 매끄러운 0~1 보간입니다. 선형으로 하면 경계에 각이 보입니다.</summary>
        /// <param name="from">0이 되는 값</param>
        /// <param name="to">1이 되는 값</param>
        /// <param name="value">알고 싶은 값</param>
        /// <returns>0~1</returns>
        private static float SmoothStep(float from, float to, float value)
        {
            if (to - from < 0.0001f) return value >= to ? 1f : 0f;

            float t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        // --- Private Methods : 공용 ---

        /// <summary>쪽지를 읽습니다. 없으면 기본값으로 만듭니다.</summary>
        /// <returns>쪽지</returns>
        private static MountainStamp LoadOrCreateStamp()
        {
            MountainStamp stamp = AssetDatabase.LoadAssetAtPath<MountainStamp>(StampPath);
            if (stamp != null) return stamp;

            stamp = ScriptableObject.CreateInstance<MountainStamp>();
            AssetDatabase.CreateAsset(stamp, StampPath);

            return stamp;
        }

        /// <summary>진행 내용을 한 번에 찍습니다.</summary>
        /// <param name="title">머리말</param>
        /// <param name="report">적어 둔 줄들</param>
        private static void Log(string title, List<string> report)
        {
            Debug.Log("MountainTool(" + title + "):" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }
    }
}
