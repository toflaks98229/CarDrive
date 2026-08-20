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

        /// <summary>타일 한 장의 디테일 격자 해상도입니다. 타일이 100m이므로 한 칸이 약 0.39m입니다.</summary>
        private const int DetailResolution = 256;

        /// <summary>
        /// 한 조각(patch)이 담을 격자 수입니다. 이 단위로 잘라 컬링하고, <b>조각 하나가 그리기 한 번</b>입니다.
        /// 64면 타일 한 장이 4x4=16 조각입니다. 32로 두면 64 조각이 되어 그리기 명령이 4배로 늡니다.
        /// </summary>
        private const int DetailPerPatch = 64;

        /// <summary>
        /// LOD가 바뀔 때 지형이 최대 몇 픽셀까지 튈 수 있는지입니다.
        /// 기본값 5에서도 능선이 계단처럼 각져 보입니다. 2로 낮춰 실루엣을 매끄럽게 합니다.
        /// </summary>
        private const float HeightmapPixelError = 2f;

        /// <summary>
        /// 이 거리 너머의 지면을 통짜 텍스처로 대체하는 거리입니다.
        /// 크게 잡아 사실상 끕니다. 대체되면 우리 셰이더가 아니라 흐릿한 이미지가 보입니다.
        /// </summary>
        private const float BasemapDistance = 20000f;

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

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null) continue;

                Undo.RecordObject(terrain, "지면 단장");
                Undo.RecordObject(terrain.gameObject, "지면 단장");

                if (material != null) terrain.materialTemplate = material;

                terrain.basemapDistance = BasemapDistance;
                terrain.heightmapPixelError = HeightmapPixelError;

                // 병 같은 Prop 이 지면을 뚫고 떨어지지 않게 Ground 레이어에 올립니다.
                // 충돌 행렬에서 Prop 은 Default 와 부딪히지 않도록 꺼져 있고,
                // 부딪히도록 켜져 있는 지면 레이어는 Ground 뿐입니다.
                if (groundLayer >= 0) terrain.gameObject.layer = groundLayer;

                terrain.detailObjectDistance = Settings.detailDistance;
                terrain.detailObjectDensity = Settings.detailDensity;

                if (vegetationPrefabs != null) planted += VegetationPainter.Paint(
                    terrain, vegetation, vegetationPrefabs, DetailResolution, DetailPerPatch, instances);

                EditorUtility.SetDirty(terrain);
            }

            report.Add("· 터레인 " + terrains.Length + "장에 옷을 입혔습니다. " +
                       "(화면 오차 " + HeightmapPixelError + ", 베이스맵 " + BasemapDistance + "m)");

            report.Add(groundLayer >= 0
                ? "· 지면 레이어: Ground(" + groundLayer + ") — 소품이 지면을 뚫지 않습니다."
                : "! Ground 레이어가 없습니다. 소품이 지면을 뚫고 떨어집니다.");

            if (grassPrefab != null)
            {
                report.Add("· 풀 심은 칸 " + planted + "개. 그리는 거리 " + Settings.detailDistance +
                           "m / 밀도 배율 " + Settings.detailDensity);

                ReportVegetationCost(vegetation, instances, report);
            }
        }

        /// <summary>
        /// 종별 포기 수와 그로부터 나오는 <b>그리기 비용</b>을 적습니다.
        ///
        /// <b>포기 수가 곧 드로우 콜입니다.</b> 유니티 터레인 디테일은 드로우 콜 하나에
        /// 약 500 포기까지만 담으므로, 잎을 아무리 늘려도 그리기 횟수는 포기 수로만 정해집니다.
        /// 그래서 <b>잎이 많은 큰 포기</b>가 같은 밀도를 훨씬 적은 그리기로 냅니다.
        ///
        /// 심고 나서 바로 확인할 수 있어야 조절이 됩니다. 숫자 없이 눈으로만 보면
        /// 무엇이 비싼지 알 수 없습니다.
        /// </summary>
        /// <param name="species">심은 종 목록</param>
        /// <param name="instances">종별 포기 수</param>
        /// <param name="report">결과를 적을 목록</param>
        private static void ReportVegetationCost(List<VegetationSpecies> species, long[] instances,
                                                 List<string> report)
        {
            if (species == null || instances == null) return;

            // 드로우 콜 하나에 담기는 포기 수입니다. 실측(92,916 포기 / 193 콜)에서 나온 값입니다.
            const int InstancesPerDrawCall = 500;

            long totalInstances = 0;
            long totalBlades = 0;

            for (int i = 0; i < species.Count && i < instances.Length; i++)
            {
                if (instances[i] <= 0) continue;

                long blades = instances[i] * species[i].bladesPerTuft;
                totalInstances += instances[i];
                totalBlades += blades;

                report.Add("  · " + species[i].id + " — 포기 " + instances[i].ToString("N0") +
                           " / 잎 " + blades.ToString("N0") +
                           " (포기당 " + species[i].bladesPerTuft + ")");
            }

            if (totalInstances == 0) return;

            long estimatedCalls = (totalInstances + InstancesPerDrawCall - 1) / InstancesPerDrawCall;

            report.Add("· 식생 합계 — 포기 " + totalInstances.ToString("N0") +
                       " / 잎(삼각형) " + totalBlades.ToString("N0"));
            report.Add("· 예상 드로우 콜 " + estimatedCalls +
                       "  (포기 " + InstancesPerDrawCall + "개당 1회. 잎 수는 영향을 주지 않습니다)");
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
