using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 지형 타일의 <b>렌더링 비용</b>에 직접 영향을 주는 설정을 한꺼번에 적용합니다.
    ///
    /// <b>왜 필요한가.</b> 이 월드는 타일이 103장인데, 타일 하나의 설정이 잘못되어 있으면
    /// 그 비용이 103배로 돌아옵니다. 그런데 인스펙터로는 한 장씩만 만질 수 있습니다.
    ///
    /// 여기서 만지는 것은 <b>보이는 모습이 아니라 그리는 방법</b>입니다.
    /// 룩을 바꾸는 것은 <c>PsxLookSetup</c>·<c>LowPolyLookSetup</c> 쪽입니다.
    /// </summary>
    public static class TerrainPerformanceSetup
    {
        // --- Constants ---

        /// <summary>
        /// 지형 메시의 단순화 정도입니다. 낮을수록 촘촘하고 비쌉니다.
        ///
        /// 기본 씬은 <b>2</b>로 되어 있는데, 이 프로젝트의 <c>PsxLookSetup</c>은 이미
        /// <b>12</b>를 의도하고 있습니다. 픽셀 룩에서는 지면 실루엣이 뭉개져도 거의 보이지 않으므로
        /// 삼각형을 크게 줄일 수 있습니다.
        /// </summary>
        private const float HeightmapPixelError = 10f;

        /// <summary>
        /// 지면 텍스처를 합성 텍스처로 바꾸는 거리(m)입니다.
        /// 이 거리를 넘으면 스플랫 여러 장을 섞지 않고 한 장으로 그립니다.
        /// </summary>
        private const float BasemapDistance = 120f;

        // <b>나무 그리기 거리(treeDistance)는 건드리지 않습니다.</b>
        //
        // 나무 셰이더에 240~330m 디더 페이드가 걸려 있어서, 그리기 거리를 그보다 짧게 잡으면
        // <b>페이드가 시작되기도 전에 나무가 타일 단위로 잘립니다.</b>
        // 이 프로젝트는 이미 그 문제를 한 번 겪었고, 그 기록이 TerrainChunkCuller 주석에 남아 있습니다.
        // 나무를 줄이려면 거리가 아니라 그 셰이더의 페이드 구간을 함께 옮겨야 합니다.

        /// <summary>
        /// 나무가 메시에서 빌보드로 바뀌는 거리(m)입니다.
        ///
        /// <b>지금 이 프로젝트에서는 아무 효과가 없습니다.</b> 유니티의 터레인 빌보드는
        /// 프로토타입 프리팹이 <c>Nature/Soft Occlusion *</c> 셰이더를 쓸 때만 만들어집니다.
        /// 우리 나무는 <c>CarDriveToonLit</c> 을 쓰므로 빌보드가 생성되지 않고, 거리와 무관하게
        /// 언제나 메시로 그려집니다. 콘솔의 "must use the Nature/Soft Occlusion shader" 경고가 그것입니다.
        ///
        /// <b>그런데도 그 셰이더로 바꾸지 않는 이유.</b> 툰 룩과, 나무가 멀어질 때 쓰는
        /// 디더 페이드(<see cref="ViewDistanceSetup"/> 가 맞춰 둔 것)를 통째로 잃기 때문입니다.
        /// 빌보드 한 단계를 얻자고 룩을 버리는 거래라 하지 않습니다.
        ///
        /// 값은 남겨 둡니다. 나중에 Soft Occlusion 나무를 섞어 심으면 그때부터 이 값이 삽니다.
        /// </summary>
        private const float BillboardStart = 35f;

        // --- Public Methods ---

        /// <summary>
        /// 성능 위주 설정을 모든 타일에 적용합니다.
        /// </summary>
        [MenuItem("CarDrive/World/터레인 렌더링 최적화 적용")]
        public static void ApplyPerformanceSettings()
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            if (terrains.Length == 0)
            {
                Debug.LogWarning("TerrainPerformanceSetup: 씬에서 터레인을 찾지 못했습니다.");
                return;
            }

            Undo.RecordObjects(terrains, "터레인 렌더링 최적화");

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null) continue;

                // <b>drawInstanced 는 여기서 켜지 않습니다.</b>
                //
                // CPU 제출 비용을 크게 줄여 주는 항목이 맞지만, <b>셰이더가 인스턴싱 경로를
                // 구현해야만</b> 합니다. 인스턴싱 모드에서 유니티는 평평한 패치 메시 하나를 그리고
                // <b>버텍스 셰이더에서 하이트맵을 샘플링해 변형</b>합니다.
                // 그 코드가 없는 셰이더로 켜면 지형이 평평하게 그려지고 UV가 어긋나
                // <b>텍스처가 증발한 것처럼 보입니다.</b>
                //
                // 이 프로젝트의 터레인 셰이더 셋(PSXTerrain · LowPolyTerrain · CarDriveToonTerrain)은
                // 셋 다 그 경로가 없습니다. 실제로 한 번 켰다가 지형이 깨졌습니다.
                //
                // 켜고 싶다면 셰이더에 다음이 먼저 들어가야 합니다.
                //   - #pragma multi_compile_instancing
                //   - #pragma instancing_options ... 및 터레인 인스턴싱 매크로
                //   - 버텍스에서 _TerrainHeightmapTexture 를 읽어 높이를 적용하는 코드
                // (URP 의 TerrainLit 셰이더가 그 구현의 참고가 됩니다)

                // 지형 메시를 성기게 만듭니다. 삼각형 수에 직접 듭니다.
                terrain.heightmapPixelError = HeightmapPixelError;

                // 먼 지면은 스플랫을 섞지 않고 합성 텍스처 한 장으로 그립니다.
                terrain.basemapDistance = BasemapDistance;

                // 나무 그리기 거리(treeDistance)는 건드리지 않습니다.
                // (줄이면 셰이더의 디더 페이드보다 먼저 잘려 눈앞에서 튀어나옵니다)
                //
                // 빌보드 전환 거리는 지금 우리 나무에 효과가 없습니다. 위 BillboardStart 참고.
                terrain.treeBillboardDistance = BillboardStart;

                // 반사 프로브를 쓰지 않으므로 오브젝트당 셋업을 없앱니다.
                terrain.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                EditorUtility.SetDirty(terrain);
            }

            Debug.Log("TerrainPerformanceSetup: 터레인 " + terrains.Length + "장에 적용했습니다. " +
                      "(인스턴싱은 셰이더 미지원으로 건드리지 않습니다. " +
                      "지형 단순화 " + HeightmapPixelError + ", 빌보드 전환 " + BillboardStart + "m)");

            LogFoliageDistanceHint();
        }

        /// <summary>
        /// 지형 그림자를 양면에서 단면으로 바꿉니다.
        ///
        /// 지형은 하이트맵이라 뒷면을 볼 일이 없는데 양면으로 두면 그림자 지오메트리가 두 배가 됩니다.
        /// <b>다만 빛 각도에 따라 그림자가 달라 보일 수 있어 따로 두었습니다.</b>
        /// 적용한 뒤 새벽·석양에서 한 번 확인하시고, 이상하면 되돌리기(Ctrl+Z)로 돌아가면 됩니다.
        /// </summary>
        [MenuItem("CarDrive/World/터레인 그림자 단면으로 (확인 필요)")]
        public static void ApplySingleSidedShadows()
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            if (terrains.Length == 0) return;

            Undo.RecordObjects(terrains, "터레인 그림자 단면");

            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] == null) continue;

                terrains[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                EditorUtility.SetDirty(terrains[i]);
            }

            Debug.Log("TerrainPerformanceSetup: 터레인 " + terrains.Length + "장의 그림자를 단면으로 바꿨습니다. " +
                      "새벽·석양에서 그림자가 이상하지 않은지 확인하세요.");
        }

        /// <summary>
        /// 지금 설정을 표로 찍어 봅니다. 무엇이 비싼지 눈으로 확인할 때 씁니다.
        /// </summary>
        [MenuItem("CarDrive/World/터레인 렌더링 설정 점검")]
        public static void Inspect()
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            if (terrains.Length == 0)
            {
                Debug.LogWarning("TerrainPerformanceSetup: 씬에서 터레인을 찾지 못했습니다.");
                return;
            }

            int instanced = 0;
            int twoSided = 0;
            float minPixelError = float.MaxValue;
            float maxTreeDistance = 0f;

            // 빌보드로 넘어갈 수 있는 나무를 셉니다. CountBillboardableTrees 의 설명을 보세요.
            List<string> meshOnlyTrees = new List<string>();
            int treePrototypes = CountBillboardableTrees(terrains, meshOnlyTrees);

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null) continue;

                if (terrain.drawInstanced) instanced++;
                if (terrain.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.TwoSided) twoSided++;

                minPixelError = Mathf.Min(minPixelError, terrain.heightmapPixelError);
                maxTreeDistance = Mathf.Max(maxTreeDistance, terrain.treeDistance);
            }

            Debug.Log("=== 터레인 렌더링 설정 (" + terrains.Length + "장) ===\n"
                      + "Draw Instanced 켜진 타일 : " + instanced + " / " + terrains.Length
                      + (instanced > 0
                          ? "   ← <b>꺼야 합니다.</b> 이 프로젝트의 터레인 셰이더는 인스턴싱 경로가 없어 "
                            + "켜면 지형 텍스처가 사라집니다"
                          : "   ✓ (셰이더가 지원하지 않으므로 꺼진 것이 맞습니다)")
                      + "\n그림자 양면(TwoSided) : " + twoSided + " / " + terrains.Length
                      + (twoSided > 0 ? "   ← 단면으로 바꾸면 그림자 지오메트리가 절반" : "   ✓")
                      + "\n지형 단순화(가장 촘촘한 값) : " + minPixelError
                      + (minPixelError < 5f ? "   ← 높이면 삼각형이 줄어듭니다" : "   ✓")
                      + "\n나무 그리기 거리(최대) : " + maxTreeDistance + "m"
                      + "\n빌보드로 넘어가는 나무 : " + (treePrototypes - meshOnlyTrees.Count) + " / " + treePrototypes
                      + (meshOnlyTrees.Count > 0
                          ? "   ← 빌보드 전환 거리(" + BillboardStart + "m)는 이 나무들에 <b>효과가 없습니다.</b> "
                            + "Nature/Soft Occlusion 셰이더가 아니라 언제나 메시로 그려집니다: "
                            + string.Join(", ", meshOnlyTrees.ToArray())
                          : "   ✓"));

            LogFoliageDistanceHint();
        }

        // --- Private Methods ---

        /// <summary>
        /// 터레인에 심긴 나무 중 <b>빌보드로 넘어갈 수 있는 것</b>이 몇인지 셉니다.
        ///
        /// 유니티는 나무 프로토타입이 <c>Nature/Soft Occlusion *</c> 셰이더(또는 SpeedTree)일 때만
        /// 빌보드 이미지를 굽습니다. 그 외의 프리팹은 <see cref="Terrain.treeBillboardDistance"/>가
        /// 얼마든 <b>언제나 메시로</b> 그려집니다. 콘솔에 나오는
        /// "The tree ... must use the Nature/Soft Occlusion shader" 경고가 바로 이것입니다.
        ///
        /// 이 사실이 코드 주석에만 있으면 다음 사람이 빌보드 거리를 낮추고 절약했다고 믿게 됩니다.
        /// 그래서 점검 표에 직접 찍습니다.
        /// </summary>
        /// <param name="terrains">살펴볼 터레인들</param>
        /// <param name="meshOnly">빌보드가 안 되는 프로토타입 이름을 담을 목록</param>
        /// <returns>서로 다른 나무 프로토타입의 수</returns>
        private static int CountBillboardableTrees(Terrain[] terrains, List<string> meshOnly)
        {
            List<GameObject> seen = new List<GameObject>();

            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] == null || terrains[i].terrainData == null) continue;

                TreePrototype[] protos = terrains[i].terrainData.treePrototypes;
                for (int p = 0; p < protos.Length; p++)
                {
                    if (protos[p] == null || protos[p].prefab == null) continue;
                    if (seen.Contains(protos[p].prefab)) continue;

                    seen.Add(protos[p].prefab);
                    if (!CanBillboard(protos[p].prefab)) meshOnly.Add(protos[p].prefab.name);
                }
            }

            return seen.Count;
        }

        /// <summary>
        /// 이 나무 프리팹이 빌보드를 만들 수 있는지 봅니다.
        /// </summary>
        /// <param name="prefab">나무 프로토타입 프리팹</param>
        /// <returns>Nature/Soft Occlusion 계열이거나 SpeedTree 면 true</returns>
        private static bool CanBillboard(GameObject prefab)
        {
            // LODGroup 이 있다는 것만으로는 안 됩니다. 그냥 LOD 를 단 프리팹도 있습니다.
            // 빌보드를 실제로 들고 있는 것은 BillboardRenderer 쪽입니다.
            if (prefab.GetComponentInChildren<BillboardRenderer>(true) != null) return true;

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material mat = renderers[i].sharedMaterial;
                if (mat == null || mat.shader == null) continue;

                string name = mat.shader.name;
                if (name.StartsWith("Nature/Soft Occlusion")) return true;
                if (name.StartsWith("Nature/SpeedTree")) return true;
            }

            return false;
        }


        /// <summary>
        /// 나무 그리기 거리와 <see cref="CarDriveWorldSettings.foliageDistance"/>의 관계를 알려 줍니다.
        ///
        /// <see cref="TerrainChunkCuller"/>는 둘 중 <b>더 큰 쪽</b>을 기준으로 접기 때문에,
        /// 나무 거리를 줄이지 않으면 컬링 설정만 낮춰도 실제로는 덜 접힙니다.
        /// </summary>
        private static void LogFoliageDistanceHint()
        {
            CarDriveWorldSettings settings = CarDriveWorldSettings.Instance;
            if (settings == null) return;

            Debug.Log("TerrainPerformanceSetup: 컬링이 접기 시작하는 거리는 "
                      + "'나무 그리기 거리'와 월드 설정의 foliageDistance(" + settings.foliageDistance + "m) 중 "
                      + "<b>더 큰 쪽</b>입니다. 한쪽만 낮추면 실제로는 덜 접힙니다.");
        }
    }
}
