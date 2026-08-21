using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 갓 구운 터레인에 <b>옷을 입힙니다.</b> 지면 머티리얼 · 지면 레이어 · 화면 오차 · 풀입니다.
    ///
    /// ── 왜 따로 떼어 두었는가 ──
    ///
    /// <see cref="WorldTerrainBaker"/> 는 구울 때 <c>TerrainData</c> 를 새로 만듭니다.
    /// 높이와 지면 텍스처는 다시 계산하지만 <b>풀과 지면 설정은 알지 못합니다.</b>
    /// 그것들은 룩 도구가 나중에 따로 입혀 준 것이기 때문입니다.
    ///
    /// 그래서 예전에는 한 번 구울 때마다 다음이 조용히 사라졌습니다.
    ///
    ///  · <b>풀</b> — 디테일 프로토타입과 심어 둔 밀도 지도가 통째로 없어집니다.
    ///  · <b>지면 레이어</b> — Ground 에서 Default 로 돌아갑니다.
    ///    충돌 행렬에서 Prop 은 Default 와 부딪히지 않으므로,
    ///    <b>병 같은 소품이 지면을 뚫고 떨어집니다.</b>
    ///  · <b>지면 머티리얼</b> — 지도 프리팹에 박힌 옛 참조로 되돌아갑니다.
    ///  · 화면 오차와 베이스맵 거리 — 기본값으로 돌아가 능선이 각지고 먼 지면이 흐려집니다.
    ///
    /// 아는 사람만 아는 절차였고, 잊으면 위 상태로 남았습니다.
    /// 이제 굽기가 마지막에 이것을 부릅니다. <b>한 번 구우면 끝입니다.</b>
    /// </summary>
    public static class TerrainDressing
    {
        // --- Constants ---

        /// <summary>풀 포기 프리팹 경로입니다.</summary>
        public const string GrassPrefabPath = "Assets/_Project/04.Art/02.Models/Generated/GrassTuft.prefab";

        /// <summary>우리 에셋인지 가리는 기준 경로입니다.</summary>
        private const string ProjectRoot = "Assets/_Project/";

        /// <summary>지도 프리팹입니다. 지면 머티리얼을 찾는 마지막 수단입니다.</summary>
        private const string MapPrefabFolder = "Assets/_Project/05.Prefabs/Map";

        /// <summary>
        /// 디테일 격자 한 칸이 덮을 <b>목표 거리(m)</b>입니다.
        ///
        /// <b>왜 해상도가 아니라 칸 크기인가.</b> 예전에는 <c>DetailResolution = 256</c> 이라는
        /// 고정 해상도였습니다. 타일이 100m 일 때는 0.39m/칸으로 맞았지만,
        /// <b>타일을 키우면 풀이 조용히 성겨집니다.</b> 600m 타일에 256을 그대로 쓰면
        /// 한 칸이 2.34m 가 되어 풀밭이 텅 빕니다. 굽고 나서야 눈으로 알아채는 종류의 실수입니다.
        ///
        /// 칸 크기를 고정하면 타일 크기가 바뀌어도 <b>단위 면적당 풀의 양이 같습니다.</b>
        /// 100m 타일에서는 256 이 그대로 나와 지금 동작이 바뀌지 않습니다.
        /// </summary>
        private const float TargetDetailCellSize = 0.39f;

        /// <summary>
        /// 유니티가 허용하는 디테일 격자 해상도의 상한입니다.
        ///
        /// 이 값이 곧 <b>월드 크기의 상한</b>이기도 합니다. 목표 칸 크기 0.39m 를 유지하면
        /// 타일 한 변은 최대 약 1,578m 까지입니다.
        /// </summary>
        private const int MaxDetailResolution = 4048;

        /// <summary>
        /// 한 조각(patch)이 담을 격자 수입니다. 이 단위로 잘라 컬링하고, <b>조각 하나가 그리기 한 번</b>입니다.
        /// 64면 타일 한 장이 4x4=16 조각입니다. 32로 두면 64 조각이 되어 그리기 명령이 4배로 늡니다.
        /// </summary>
        private const int DetailPerPatch = 64;

        // 지형 LOD 수치(화면 오차·베이스맵 거리)는 <b>여기서 정하지 않습니다.</b>
        //
        // 예전에는 이 파일이 HeightmapPixelError = 2 를, TerrainPerformanceSetup 이 10 을
        // 각각 <c>private const</c> 로 들고 같은 속성에 썼습니다. <b>나중에 실행한 도구가
        // 이기는 구조</b>라, 도구를 어떤 순서로 눌렀는지가 지형 밀도를 정하고 있었습니다.
        //
        // 지금은 CarDriveWorldSettings 가 주인입니다. 두 도구가 같은 값을 읽습니다.

        /// <summary>
        /// 알려진 지면 머티리얼들입니다. 깔려 있던 것을 알아내지 못했을 때 앞에서부터 씁니다.
        ///
        /// 순서는 룩이 바뀌어 온 순서를 거꾸로 둔 것입니다. 가장 최근 룩이 앞입니다.
        /// 이 목록에 기대는 것은 <b>비상 수단</b>입니다. 보통은 깔려 있던 것을 그대로 물려받습니다.
        /// </summary>
        private static readonly string[] KnownTerrainMaterials =
        {
            "Assets/_Project/04.Art/03.Shaders/Toon/CarDriveToonTerrain.mat",
            "Assets/_Project/04.Art/03.Shaders/PSX/PSXTerrain.mat",
            "Assets/_Project/04.Art/03.Shaders/LowPoly/LowPolyTerrain.mat",
        };

        /// <summary>만질 만한 값들은 설정 에셋에 있습니다. (CarDrive 메뉴의 월드 창)</summary>
        private static CarDriveWorldSettings Settings { get { return CarDriveWorldSettings.Instance; } }

        // --- Public Methods ---

        /// <summary>
        /// 이미 깔려 있는 터레인에 옷을 다시 입힙니다. 굽지 않고 단장만 합니다.
        ///
        /// 굽기가 알아서 부르므로 보통은 쓸 일이 없습니다.
        /// 풀이 사라졌거나 소품이 지면을 뚫을 때, 굽지 않고 되돌리는 용도입니다.
        /// </summary>
        [MenuItem("CarDrive/World/지면 단장 다시 입히기")]
        public static void ApplyToScene()
        {
            List<string> report = new List<string>();

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

            Material material = ResolveMaterial(terrains, report);
            GameObject grass = ResolveGrassPrefab(terrains, report);

            Apply(terrains, material, grass, report);

            AssetDatabase.SaveAssets();

            Debug.Log("TerrainDressing:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 명령줄에서 씬을 열고 단장한 뒤 저장합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.TerrainDressing.ApplyFromCommandLine</c>
        /// </summary>
        public static void ApplyFromCommandLine()
        {
            UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/01.Scenes/SampleScene.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            ApplyToScene();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 지금 씬에 깔려 있는 지면 머티리얼을 알아냅니다.
        ///
        /// <b>깔려 있던 것을 그대로 물려받는 것이 첫 번째입니다.</b> 그래야 어떤 룩으로 맞춰
        /// 두었든 다시 구워도 그대로 남습니다. 어느 룩이 켜져 있는지 코드가 알 필요가 없습니다.
        /// </summary>
        /// <param name="existing">굽기 전에 깔려 있던 터레인들</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>쓸 지면 머티리얼. 하나도 못 찾으면 null 입니다.</returns>
        public static Material ResolveMaterial(Terrain[] existing, List<string> report)
        {
            // 1) 이미 깔려 있던 것.
            if (existing != null)
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    if (existing[i] == null) continue;

                    Material mat = existing[i].materialTemplate;
                    if (!IsOurs(mat)) continue;

                    report.Add("· 지면 머티리얼: 깔려 있던 것을 물려받습니다 — " + mat.name);
                    return mat;
                }
            }

            // 2) 알려진 룩 머티리얼.
            for (int i = 0; i < KnownTerrainMaterials.Length; i++)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(KnownTerrainMaterials[i]);
                if (mat == null) continue;

                report.Add("! 지면 머티리얼: 깔려 있던 것을 알아내지 못해 " + mat.name + " 을 씁니다.");
                report.Add("  다른 룩을 쓰고 있었다면 해당 룩 적용을 다시 실행하세요.");
                return mat;
            }

            // 3) 지도 프리팹에 박힌 것. 옛 참조일 수 있어 마지막에 둡니다.
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { MapPrefabFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (prefab == null) continue;

                Terrain t = prefab.GetComponentInChildren<Terrain>(true);
                if (t == null || t.materialTemplate == null) continue;

                report.Add("! 지면 머티리얼: 지도 프리팹의 " + t.materialTemplate.name + " 을 씁니다.");
                return t.materialTemplate;
            }

            report.Add("! 지면 머티리얼을 찾지 못했습니다. 유니티 기본값이 그대로 보입니다.");
            return null;
        }

        /// <summary>
        /// 지금 심겨 있는 풀 프리팹을 알아냅니다.
        /// </summary>
        /// <param name="existing">굽기 전에 깔려 있던 터레인들</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>쓸 풀 프리팹. 없으면 null 입니다.</returns>
        public static GameObject ResolveGrassPrefab(Terrain[] existing, List<string> report)
        {
            if (existing != null)
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    if (existing[i] == null || existing[i].terrainData == null) continue;

                    DetailPrototype[] protos = existing[i].terrainData.detailPrototypes;
                    for (int p = 0; p < protos.Length; p++)
                    {
                        if (protos[p] == null || protos[p].prototype == null) continue;

                        report.Add("· 풀: 심겨 있던 " + protos[p].prototype.name + " 을 물려받습니다.");
                        return protos[p].prototype;
                    }
                }
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrassPrefabPath);

            report.Add(prefab != null
                ? "· 풀: " + GrassPrefabPath + " 를 씁니다."
                : "! 풀 프리팹이 없습니다. 로우폴리 코지 룩으로 전환을 한 번 실행하면 만들어집니다.");

            return prefab;
        }

        /// <summary>
        /// 터레인들에 옷을 입힙니다.
        /// </summary>
        /// <param name="terrains">입힐 터레인들</param>
        /// <param name="material">지면 머티리얼. null 이면 건드리지 않습니다.</param>
        /// <param name="grassPrefab">
        /// 풀 프리팹. null 이면 식생을 심지 않습니다.
        ///
        /// <b>이 프리팹의 머티리얼로 종을 다시 굽습니다.</b> 예전에는 이 프리팹 하나를
        /// 그대로 심었지만, 지금은 설정에 적힌 종 목록대로 여러 벌을 만들어 심습니다.
        /// (호출부를 고치지 않아도 되도록 인자는 그대로 두었습니다)
        /// </param>
        /// <param name="report">진행 내용을 적을 목록</param>
        public static void Apply(Terrain[] terrains, Material material, GameObject grassPrefab,
                                 List<string> report)
        {
            if (terrains == null || terrains.Length == 0)
            {
                report.Add("! 입힐 터레인이 없습니다.");
                return;
            }

            int groundLayer = LayerMask.NameToLayer("Ground");
            long planted = 0;

            // 심을 종을 정하고 메시를 굽습니다. 설정이 비어 있으면 예전 값으로 기본 3종을 만듭니다.
            List<VegetationSpecies> vegetation = ResolveSpecies();
            GameObject[] vegetationPrefabs = grassPrefab != null
                ? VegetationBuilder.BuildAll(vegetation, ResolveGrassMaterial(grassPrefab), report)
                : null;

            // 종별 포기 수를 세어 둡니다. 드로우 콜이 이 값으로 정해지므로 곧 그리기 비용입니다.
            long[] instances = new long[vegetation.Count];

            // 종별 <b>실제 그리기 횟수</b>입니다. 패치마다 세므로 추정이 아닙니다.
            long[] drawCalls = new long[vegetation.Count];

            // 그려지는 몫을 재려면 패치 수와 타일 크기가 필요합니다.
            long patchCount = 0;

            // 그려지는 양을 추정하려면 월드의 격자 칸 수와 타일 크기가 필요합니다.
            long cellCount = 0;
            float tileSize = 0f;

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null) continue;

                Undo.RecordObject(terrain, "지면 단장");
                Undo.RecordObject(terrain.gameObject, "지면 단장");

                if (material != null) terrain.materialTemplate = material;

                // LOD 는 설정이 주인입니다. 예전에는 이 도구가 화면 오차 2 · 베이스맵 20000 을 쓰고
                // TerrainPerformanceSetup 이 10 · 120 을 써서, 어느 도구를 나중에 눌렀는지가
                // 지형 밀도를 정하고 있었습니다.
                terrain.basemapDistance = Settings.basemapDistance;
                terrain.heightmapPixelError = Settings.heightmapPixelError;

                // 병 같은 Prop 이 지면을 뚫고 떨어지지 않게 Ground 레이어에 올립니다.
                // 충돌 행렬에서 Prop 은 Default 와 부딪히지 않도록 꺼져 있고,
                // 부딪히도록 켜져 있는 지면 레이어는 Ground 뿐입니다.
                if (groundLayer >= 0) terrain.gameObject.layer = groundLayer;

                terrain.detailObjectDistance = Settings.detailDistance;
                terrain.detailObjectDensity = Settings.detailDensity;

                // <b>디테일 해상도는 타일마다 그 크기에서 구합니다.</b>
                // 고정값을 쓰면 타일을 키웠을 때 풀이 조용히 성겨집니다.
                int detailResolution = ResolveDetailResolution(terrain);

                // 그려지는 양을 재려면 격자 칸 수와 타일 크기가 필요합니다. (아래 ReportVegetationCost)
                if (terrain.terrainData != null)
                {
                    cellCount += (long)detailResolution * detailResolution;
                    patchCount += (detailResolution / DetailPerPatch) * (detailResolution / DetailPerPatch);
                    tileSize = terrain.terrainData.size.x;
                }

                if (vegetationPrefabs != null) planted += VegetationPainter.Paint(
                    terrain, vegetation, vegetationPrefabs, detailResolution, DetailPerPatch,
                    instances, drawCalls);

                EditorUtility.SetDirty(terrain);
            }

            report.Add("· 터레인 " + terrains.Length + "장에 옷을 입혔습니다. " +
                       "(화면 오차 " + Settings.heightmapPixelError +
                       ", 베이스맵 " + Settings.basemapDistance + "m)");

            report.Add(groundLayer >= 0
                ? "· 지면 레이어: Ground(" + groundLayer + ") — 소품이 지면을 뚫지 않습니다."
                : "! Ground 레이어가 없습니다. 소품이 지면을 뚫고 떨어집니다.");

            if (grassPrefab != null)
            {
                report.Add("· 풀 심은 칸 " + planted + "개. 그리는 거리 " + Settings.detailDistance +
                           "m / 밀도 배율 " + Settings.detailDensity);

                ReportVegetationCost(vegetation, instances, drawCalls, patchCount, tileSize, report);
            }
        }

        /// <summary>
        /// 종별 포기 수와 <b>실제 그리기 횟수</b>를 적습니다.
        ///
        /// <b>포기 수를 500으로 나누면 안 됩니다.</b> 유니티는 디테일을 <c>(패치 x 종)</c>
        /// 단위로 그리므로, 패치 하나에 어떤 종이 한 포기라도 있으면 그 종 몫으로 한 번이 나갑니다.
        /// 종이 넷이면 <b>패치당 최소 네 번</b>이 바닥값입니다. 총 포기 수를 절반으로 줄여도
        /// 종을 넷으로 늘리면 그리기 횟수가 그대로일 수 있습니다.
        /// 그래서 <see cref="VegetationPainter"/> 가 패치마다 직접 세어 온 값을 씁니다.
        ///
        /// 여기서 한 번 더 잘못을 저지를 뻔했습니다. 한때 이 함수가 <b>월드 전체</b> 포기 수를
        /// 500으로 나눠 "드로우 콜 5,653" 이라고 적었습니다. 프로파일러가 재던 것은
        /// <b>화면 안</b>의 92,916 포기였으니 30배 부풀린 값이었습니다.
        /// 한 번에 그려지는 것은 <c>detailDistance</c> 안에 걸친 패치의 몫뿐입니다.
        /// </summary>
        /// <param name="species">심은 종 목록</param>
        /// <param name="instances">종별 포기 수</param>
        /// <param name="drawCalls">종별 그리기 횟수 (월드 전체)</param>
        /// <param name="patchCount">월드 전체의 패치 수</param>
        /// <summary>
        /// 이 타일에 쓸 디테일 격자 해상도를 구합니다.
        ///
        /// <b>칸 크기를 고정하고 해상도를 따라오게 합니다.</b> 그래야 타일을 키워도
        /// 단위 면적당 풀의 양이 같습니다. 반대로 하면(해상도 고정) 타일을 키운 만큼
        /// 풀밭이 성겨지는데, 그 사실이 굽기 전에는 드러나지 않습니다.
        ///
        /// 결과는 <see cref="DetailPerPatch"/> 의 배수로 맞춥니다. 조각이 딱 떨어져야
        /// 컬링 단위가 어긋나지 않고, 조각 하나의 크기도 타일 크기와 무관하게 일정해집니다.
        /// (0.39m × 64 = 약 25m)
        /// </summary>
        /// <param name="terrain">해상도를 구할 지형</param>
        /// <returns>이 타일에 쓸 디테일 격자 해상도</returns>
        private static int ResolveDetailResolution(Terrain terrain)
        {
            float size = terrain != null && terrain.terrainData != null ? terrain.terrainData.size.x : 100f;
            if (size <= 0f) size = 100f;

            int wanted = Mathf.RoundToInt(size / TargetDetailCellSize);

            // 조각 크기가 일정하도록 조각 수 단위로 반올림합니다.
            int patches = Mathf.Max(1, Mathf.RoundToInt(wanted / (float)DetailPerPatch));
            int resolution = patches * DetailPerPatch;

            if (resolution <= MaxDetailResolution) return resolution;

            // 상한에 걸리면 <b>조용히 넘어가지 않습니다.</b> 풀 밀도가 의도보다 낮아지는데,
            // 그 사실을 모르면 "왜 풀이 성긴지"를 엉뚱한 곳에서 찾게 됩니다.
            int capped = (MaxDetailResolution / DetailPerPatch) * DetailPerPatch;
            Debug.LogWarning("TerrainDressing: 타일 한 변이 " + size.ToString("0") + "m 라 " +
                             "목표 칸 크기(" + TargetDetailCellSize + "m)를 지키려면 해상도 " + resolution +
                             " 이 필요하지만 상한은 " + MaxDetailResolution + " 입니다. " +
                             capped + " 로 낮춥니다 — 한 칸이 " +
                             (size / capped).ToString("0.00") + "m 가 되어 풀이 성겨집니다. " +
                             "타일을 " + (MaxDetailResolution * TargetDetailCellSize).ToString("0") +
                             "m 이하로 줄이세요.");

            return capped;
        }

        /// <param name="tileSize">타일 한 변의 길이(m)</param>
        /// <param name="report">결과를 적을 목록</param>
        private static void ReportVegetationCost(List<VegetationSpecies> species, long[] instances,
                                                 long[] drawCalls, long patchCount, float tileSize,
                                                 List<string> report)
        {
            if (species == null || instances == null || drawCalls == null) return;
            if (patchCount <= 0 || tileSize <= 0f) return;

            long totalInstances = 0;
            long totalBlades = 0;
            long totalCalls = 0;

            for (int i = 0; i < species.Count && i < instances.Length; i++)
            {
                if (instances[i] <= 0) continue;

                long blades = instances[i] * species[i].bladesPerTuft;
                totalInstances += instances[i];
                totalBlades += blades;
                totalCalls += drawCalls[i];

                report.Add("  · " + species[i].id +
                           " — 포기 " + instances[i].ToString("N0") +
                           " / 잎 " + blades.ToString("N0") +
                           " / 그리기 " + drawCalls[i].ToString("N0") +
                           " (포기당 잎 " + species[i].bladesPerTuft + ")");
            }

            if (totalInstances == 0) return;

            report.Add("· 월드 전체 — 포기 " + totalInstances.ToString("N0") +
                       " / 잎 " + totalBlades.ToString("N0") +
                       " / 그리기 " + totalCalls.ToString("N0"));

            // 화면 안에 걸치는 패치가 전체의 몇 분의 일인지로 줄여 봅니다.
            //
            // 패치는 통째로 들어가고 빠지므로, 거리 안에 조금이라도 걸치면 다 그려집니다.
            // 그래서 반경에 패치 반대각선을 더해 잡습니다.
            float patchSize = TargetDetailCellSize * DetailPerPatch;
            float reach = Settings.detailDistance + patchSize * 0.7071f;

            double patchesInView = (Mathf.PI * reach * reach) / (patchSize * patchSize);
            double share = System.Math.Min(1.0, patchesInView / patchCount);

            report.Add("· 한 번에 그려지는 몫 — 패치 " + patchesInView.ToString("N0") +
                       " / " + patchCount.ToString("N0") +
                       " → 그리기 약 " + System.Math.Ceiling(totalCalls * share).ToString("N0") + "회");
            report.Add("  (패치 " + patchSize.ToString("F0") + "m, 그리는 거리 " + Settings.detailDistance + "m." +
                       " 종 하나가 패치 하나에서 최소 한 번이라, 종을 늘리면 바닥값이 함께 올라갑니다)");
        }


        // --- Private Methods ---

        /// <summary>
        /// 심을 식생 종을 정합니다.
        ///
        /// 설정에 적어 둔 것이 있으면 그것을, 비어 있으면 <b>예전의 단일 풀 값으로</b>
        /// 기본 세 종을 만듭니다. 그래서 설정을 손대지 않은 프로젝트도 그대로 돌아가고,
        /// 주된 종이 예전 모습을 그대로 물려받습니다.
        /// </summary>
        /// <returns>심을 종 목록</returns>
        private static List<VegetationSpecies> ResolveSpecies()
        {
            CarDriveWorldSettings settings = Settings;

            bool authored = settings.vegetation != null && settings.vegetation.Count > 0;
            if (authored) return settings.vegetation;

            return VegetationDefaults.Create(
                settings.bladesPerTuft, settings.tuftRadius, settings.bladeHeight);
        }

        /// <summary>
        /// 풀 프리팹에서 잎 머티리얼을 꺼냅니다.
        ///
        /// 종을 새로 구울 때 <b>지금 쓰고 있는 잎 머티리얼을 그대로</b> 물려받기 위한 것입니다.
        /// 룩을 바꿔 두었다면 그 룩의 머티리얼이 그대로 이어집니다.
        /// </summary>
        /// <param name="grassPrefab">지금 심겨 있는 풀 프리팹</param>
        /// <returns>잎 머티리얼. 찾지 못하면 null입니다.</returns>
        private static Material ResolveGrassMaterial(GameObject grassPrefab)
        {
            if (grassPrefab == null) return null;

            MeshRenderer renderer = grassPrefab.GetComponentInChildren<MeshRenderer>(true);
            return renderer != null ? renderer.sharedMaterial : null;
        }

        /// <summary>
        /// 우리가 만든 에셋인지 봅니다.
        ///
        /// 유니티가 기본으로 물려 주는 터레인 머티리얼은 패키지 안에 있어 이 경로 밖입니다.
        /// 그것을 물려받으면 룩이 통째로 사라지므로 걸러야 합니다.
        /// </summary>
        /// <param name="mat">확인할 머티리얼</param>
        /// <returns>프로젝트 안의 에셋이면 true 입니다.</returns>
        private static bool IsOurs(Material mat)
        {
            if (mat == null) return false;

            string path = AssetDatabase.GetAssetPath(mat);
            return !string.IsNullOrEmpty(path) && path.StartsWith(ProjectRoot);
        }
    }
}
