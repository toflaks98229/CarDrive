using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 지형에 식생을 <b>여러 종으로</b> 심습니다.
    ///
    /// <b>예전에는 종이 하나였고 규칙도 하나였습니다.</b> 스플랫의 잔디 가중치가 문턱을 넘으면
    /// 심고, 아니면 안 심는 것이 전부였습니다. 그래서 두 가지 문제가 있었습니다.
    ///  1. 월드 어디를 가도 같은 풀이라 장소가 구분되지 않았습니다.
    ///  2. 밀도가 스플랫 하나로만 정해져 풀밭이 <b>고르게 덮인 카펫</b>처럼 보였습니다.
    ///
    /// 여기서는 종마다 <b>경사·고도·군집</b> 규칙을 따로 봅니다.
    ///
    /// <b>총 포기 수는 늘지 않습니다.</b> 칸당 최대치를 종의 비중대로 나누므로,
    /// 종을 셋으로 늘려도 심기는 양은 그대로입니다. 오히려 잎이 적은 종이 섞이면
    /// 삼각형은 줄어듭니다. 성능을 지키면서 눈에 보이는 다양성만 늘리는 것이 목적입니다.
    /// </summary>
    public static class VegetationPainter
    {
        // --- Public Methods ---

        /// <summary>
        /// 지형 하나에 식생을 심습니다.
        /// </summary>
        /// <param name="terrain">심을 지형</param>
        /// <param name="species">심을 종 목록</param>
        /// <param name="prefabs">종 순서에 맞춘 프리팹. null인 자리는 건너뜁니다.</param>
        /// <param name="detailResolution">디테일 격자 해상도</param>
        /// <param name="detailPerPatch">패치당 격자 수</param>
        /// <param name="instancesPerSpecies">
        /// 종별로 심은 <b>포기 수</b>를 더해 넣을 곳입니다. 길이는 <paramref name="species"/> 와 같아야 합니다.
        /// 드로우 콜이 포기 수로 정해지므로, 이 값이 곧 그리기 비용입니다. null 이어도 됩니다.
        /// </param>
        /// <returns>심은 칸의 수</returns>
        public static long Paint(Terrain terrain, List<VegetationSpecies> species, GameObject[] prefabs,
                                 int detailResolution, int detailPerPatch, long[] instancesPerSpecies)
        {
            TerrainData data = terrain != null ? terrain.terrainData : null;
            if (data == null || species == null || prefabs == null) return 0;

            List<DetailPrototype> protos = new List<DetailPrototype>();
            List<int> speciesIndex = new List<int>();

            for (int i = 0; i < species.Count && i < prefabs.Length; i++)
            {
                if (prefabs[i] == null || species[i].weight <= 0f) continue;

                protos.Add(CreatePrototype(species[i], prefabs[i]));
                speciesIndex.Add(i);
            }

            if (protos.Count == 0) return 0;

            // SetDetailResolution 은 심어 둔 것을 지우므로 반드시 칠하기 전에 부릅니다.
            data.SetDetailResolution(detailResolution, detailPerPatch);

            // 밀도값의 <b>의미</b>를 정합니다. 이것을 안 맞추면 심어도 안 보입니다.
            // 덮개 모드에서는 값이 0~255의 덮인 비율이라, 포기 수로 넣은 1~3은 거의 0이 됩니다.
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            data.detailPrototypes = protos.ToArray();
            data.RefreshPrototypes();

            return PaintLayers(terrain, data, species, speciesIndex, detailResolution, instancesPerSpecies);
        }

        // --- Private Methods ---

        /// <summary>
        /// 종 하나에 해당하는 터레인 디테일 프로토타입을 만듭니다.
        /// </summary>
        /// <param name="spec">종</param>
        /// <param name="prefab">그 종의 프리팹</param>
        /// <returns>터레인에 넘길 프로토타입</returns>
        private static DetailPrototype CreatePrototype(VegetationSpecies spec, GameObject prefab)
        {
            DetailPrototype proto = new DetailPrototype();

            proto.prototype = prefab;
            proto.usePrototypeMesh = true;
            proto.useInstancing = true;
            proto.renderMode = DetailRenderMode.VertexLit;

            proto.minWidth = 0.85f;
            proto.maxWidth = 1.45f;
            proto.minHeight = 0.75f;
            proto.maxHeight = 1.5f;
            proto.noiseSpread = 0.4f;

            // 종마다 색조를 달리하면 같은 머티리얼을 쓰면서도 서로 구분됩니다.
            proto.healthyColor = spec.tint;
            proto.dryColor = spec.tint;

            // 자리를 흩뜨리지 않으면 격자에 줄 맞춰 심겨 <b>바둑판 무늬</b>가 그대로 보입니다.
            proto.positionJitter = 1f;

            // 비탈에서는 조금 눕혀야 땅에 붙어 보입니다. 다 눕히면 누워 버립니다.
            proto.alignToGround = 0.35f;

            return proto;
        }

        /// <summary>
        /// 격자를 훑으며 종별 밀도 지도를 만들어 심습니다.
        /// </summary>
        /// <param name="terrain">심을 지형</param>
        /// <param name="data">그 지형의 데이터</param>
        /// <param name="species">종 목록</param>
        /// <param name="speciesIndex">디테일 레이어 순서 → 종 색인</param>
        /// <param name="resolution">디테일 격자 해상도</param>
        /// <param name="instancesPerSpecies">종별 포기 수를 더해 넣을 곳. null 이어도 됩니다.</param>
        /// <returns>무언가 심긴 칸의 수</returns>
        private static long PaintLayers(Terrain terrain, TerrainData data, List<VegetationSpecies> species,
                                        List<int> speciesIndex, int resolution, long[] instancesPerSpecies)
        {
            CarDriveWorldSettings settings = CarDriveWorldSettings.Instance;

            int alphaRes = data.alphamapResolution;
            float[,,] alpha = data.GetAlphamaps(0, 0, alphaRes, alphaRes);

            int layerCount = speciesIndex.Count;
            int[][,] maps = new int[layerCount][,];
            for (int i = 0; i < layerCount; i++) maps[i] = new int[resolution, resolution];

            // 비중의 합으로 칸당 최대치를 나눕니다. 종이 늘어도 총량이 그대로인 이유입니다.
            float weightSum = 0f;
            for (int i = 0; i < layerCount; i++) weightSum += species[speciesIndex[i]].weight;
            if (weightSum <= 0f) return 0;

            Vector3 terrainPosition = terrain.transform.position;
            float terrainHeight = data.size.y;

            float threshold = settings.grassThreshold;
            int maxPerCell = settings.maxPerCell;

            long planted = 0;

            for (int z = 0; z < resolution; z++)
            {
                float z01 = (z + 0.5f) / resolution;
                int az = Mathf.Clamp(Mathf.FloorToInt(z01 * alphaRes), 0, alphaRes - 1);

                for (int x = 0; x < resolution; x++)
                {
                    float x01 = (x + 0.5f) / resolution;
                    int ax = Mathf.Clamp(Mathf.FloorToInt(x01 * alphaRes), 0, alphaRes - 1);

                    float grass = alpha[az, ax, 0];
                    if (grass < threshold) continue;

                    // 잔디가 옅어지는 가장자리에서는 포기 수도 함께 줄여, 풀밭이 끝나는 자리에
                    // 선이 생기지 않고 성글게 흩어지며 사라지게 합니다.
                    //
                    // 0 에서 시작해 올려야 가장자리가 <b>정말로</b> 성글어집니다.
                    float edge = Mathf.InverseLerp(threshold, 1f, grass);
                    float budget = maxPerCell * edge * edge;
                    if (budget <= 0f) continue;

                    // 경사와 높이는 칸마다 한 번만 구해 모든 종이 나눠 씁니다.
                    float steepness = data.GetSteepness(x01, z01);
                    float worldY = terrainPosition.y + data.GetInterpolatedHeight(x01, z01);

                    bool any = false;

                    for (int layer = 0; layer < layerCount; layer++)
                    {
                        VegetationSpecies spec = species[speciesIndex[layer]];

                        int count = ResolveCount(spec, budget / weightSum, steepness, worldY,
                                                 terrainPosition, x01, z01, data.size, x, z);
                        if (count <= 0) continue;

                        maps[layer][z, x] = count;
                        any = true;

                        // 드로우 콜이 포기 수로 정해지므로 여기서 세어 둡니다.
                        if (instancesPerSpecies != null)
                        {
                            int index = speciesIndex[layer];
                            if (index < instancesPerSpecies.Length) instancesPerSpecies[index] += count;
                        }
                    }

                    if (any) planted++;
                }
            }

            for (int layer = 0; layer < layerCount; layer++)
            {
                data.SetDetailLayer(0, 0, layer, maps[layer]);
            }

            EditorUtility.SetDirty(data);
            return planted;
        }

        /// <summary>
        /// 이 칸에 이 종을 몇 포기 심을지 정합니다.
        ///
        /// 경사·고도에 맞지 않거나 군집 노이즈가 문턱에 못 미치면 0입니다.
        /// </summary>
        /// <param name="spec">종</param>
        /// <param name="share">비중으로 나눈 이 종의 몫</param>
        /// <param name="steepness">이 칸의 경사(도)</param>
        /// <param name="worldY">이 칸의 월드 높이</param>
        /// <param name="terrainPosition">지형의 월드 위치</param>
        /// <param name="x01">지형 안에서의 가로 비율</param>
        /// <param name="z01">지형 안에서의 세로 비율</param>
        /// <param name="size">지형의 크기</param>
        /// <param name="cellX">격자 가로 색인. 소수점 밀도를 흩뿌리는 데 씁니다.</param>
        /// <param name="cellZ">격자 세로 색인</param>
        /// <returns>심을 포기 수</returns>
        private static int ResolveCount(VegetationSpecies spec, float share, float steepness, float worldY,
                                        Vector3 terrainPosition, float x01, float z01, Vector3 size,
                                        int cellX, int cellZ)
        {
            if (steepness > spec.maxSlope || steepness < spec.minSlope) return 0;
            if (worldY > spec.maxHeight || worldY < spec.minHeight) return 0;

            float density = share * spec.weight;

            // 군집 — 노이즈가 문턱을 넘는 자리에만 심고, 넘는 정도만큼 짙어집니다.
            //
            // 타일 경계에서 무늬가 끊기지 않도록 <b>월드 좌표</b>로 노이즈를 봅니다.
            // 타일 안의 비율(x01)로 보면 타일마다 같은 무늬가 반복돼 격자가 드러납니다.
            if (spec.patchThreshold > 0f)
            {
                float worldX = terrainPosition.x + x01 * size.x;
                float worldZ = terrainPosition.z + z01 * size.z;

                float noise = Mathf.PerlinNoise(
                    (worldX + spec.patchOffset) * spec.patchScale,
                    (worldZ + spec.patchOffset) * spec.patchScale);

                if (noise < spec.patchThreshold) return 0;

                density *= Mathf.InverseLerp(spec.patchThreshold, 1f, noise);
            }

            // <b>소수점 아래를 반올림하면 비중이 낮은 종이 통째로 사라집니다.</b>
            //
            // 밀도 0.35 를 반올림하면 0 이라, 잔풀처럼 비중을 낮춰 둔 종은
            // 어느 칸에도 심기지 않습니다. 반대로 0.5 를 넘으면 <b>모든 칸에</b> 심겨
            // 0.5 를 경계로 없거나 가득 차거나 둘 중 하나가 됩니다.
            //
            // 그래서 소수점은 자리마다 흩뿌립니다. 0.35 면 칸의 35% 에 한 포기가 들어갑니다.
            // 좌표로 만든 해시라 다시 구워도 같은 자리에 같은 결과가 나옵니다.
            int whole = Mathf.FloorToInt(density);
            float fraction = density - whole;

            if (fraction > 0.001f && Hash01(cellX, cellZ, spec.seed) < fraction) whole++;

            return whole;
        }

        /// <summary>
        /// 좌표와 씨앗으로 0~1 사이의 값을 만듭니다. 같은 입력이면 언제나 같은 값입니다.
        /// </summary>
        /// <param name="x">격자 가로 색인</param>
        /// <param name="z">격자 세로 색인</param>
        /// <param name="seed">종마다 다른 씨앗. 종끼리 같은 자리에 몰리지 않게 합니다.</param>
        /// <returns>0 이상 1 미만의 값</returns>
        private static float Hash01(int x, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ (uint)(seed * 83492791);

                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;

                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
