using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;

namespace CarDrive.EditorTools.ProcGen
{
    /// <summary>
    /// 월드에 지형지물을 심습니다. 프리팹 만들기 · 흩뿌리기 · 마을 세우기를 따로 실행합니다.
    ///
    /// ── 쓰는 알고리즘 ──
    ///
    /// <b>배치: 푸아송 디스크 샘플링</b> (<see cref="PoissonDisk"/>)
    ///   무작위로 뿌리면 뭉치는 곳과 텅 빈 곳이 생겨 "잘못 놓았다"로 보입니다.
    ///   최소 간격을 지키면서 촘촘히 채우면 자연물이 자라난 것처럼 보입니다.
    ///
    /// <b>바위·건물: 절차적 저폴리 메시</b> (<see cref="LowPolyMeshFactory"/>)
    ///   이 프로젝트에는 바위도 건물도 없습니다. 화면이 215픽셀로 줄고 색이 뭉개지므로
    ///   정교한 모델보다 <b>실루엣</b>이 전부입니다. 그래서 만들어 씁니다.
    ///
    /// <b>마을: 도로 전면 배치</b>
    ///   길을 따라 자리를 잡고 건물을 길 쪽으로 돌려 세웁니다.
    ///   격자 도시를 짜는 알고리즘(WFC 등)은 이 게임에 맞지 않습니다.
    ///   여기 마을은 <b>길가에 늘어선 몇 채</b>이지 도시가 아닙니다.
    ///
    /// ── 심지 않는 자리 ──
    ///
    /// 셋 다 같은 규칙을 씁니다. 도로 위, 마을 안, 너무 가파른 비탈에는 놓지 않습니다.
    /// 특히 <b>도로</b>가 중요합니다. 길 한복판에 나무가 서 있으면 주행이 불가능해집니다.
    ///
    /// ── 실행 순서 ──
    ///
    /// 터레인 굽기 → 산 만들기(<see cref="MountainTool"/>) → 1 → 2 → 3 입니다.
    /// 산을 나중에 만들면 이미 심어 둔 나무가 땅속에 묻히거나 공중에 뜹니다.
    /// </summary>
    public static class WorldFeatureTool
    {
        /// <summary>메인 씬 경로입니다.</summary>
        private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

        /// <summary>흩뿌린 것들을 담아 둘 씬 루트 이름입니다. 다시 실행하면 갈아엎습니다.</summary>
        private const string ScatterRoot = "WorldScatter";

        /// <summary>마을 건물을 담아 둘 루트 이름입니다.</summary>
        private const string VillageRoot = "VillageBuildings";

        /// <summary>바위 프리팹을 둘 폴더입니다.</summary>
        private const string RockFolder = "Assets/_Project/05.Prefabs/Prop/Rock";

        /// <summary>건물 프리팹을 둘 폴더입니다.</summary>
        private const string HouseFolder = "Assets/_Project/05.Prefabs/Prop/House";

        /// <summary>나무 프리팹이 있는 폴더입니다.</summary>
        private const string TreeFolder = "Assets/_Project/05.Prefabs/Prop/Tree";

        /// <summary>절차적 메시가 쓸 재질 폴더입니다.</summary>
        private const string MaterialFolder = "Assets/_Project/04.Art/00.Materials";

        /// <summary>만들 바위 종류 수입니다.</summary>
        private const int RockVariants = 5;

        /// <summary>만들 건물 종류 수입니다.</summary>
        private const int HouseVariants = 6;

        /// <summary>
        /// 도로 중심에서 이만큼(m) 안쪽에는 아무것도 흩뿌리지 않습니다.
        ///
        /// 터레인 굽기가 도로를 반폭 9m 로 깎고 갓길을 9m 더 평탄하게 만듭니다.
        /// 그 바깥에 세워야 나무가 길가에 서 있는 것으로 보입니다.
        /// </summary>
        private const float RoadClearance = 16f;

        /// <summary>이 각도(도)보다 가파르면 놓지 않습니다.</summary>
        private const float MaxSlope = 32f;

        // --- Public Methods ---

        /// <summary>저폴리 바위와 건물 프리팹을 만듭니다.</summary>
        [MenuItem("CarDrive/World/1. 바위 · 건물 프리팹 만들기")]
        public static void CreatePrefabs()
        {
            List<string> report = new List<string>();

            CreateRockPrefabs(report);
            CreateHousePrefabs(report);

            report.Add("  프리팹을 다시 만들었으면 2·3 도 다시 실행하세요. 씬의 참조가 갈립니다.");

            AssetDatabase.SaveAssets();
            Log("프리팹 만들기", report);
        }

        /// <summary>나무와 바위를 월드에 흩뿌립니다.</summary>
        [MenuItem("CarDrive/World/2. 나무 · 바위 흩뿌리기")]
        public static void Scatter()
        {
            List<string> report = new List<string>();

            WorldStreamer world = Object.FindAnyObjectByType<WorldStreamer>(FindObjectsInactive.Include);
            if (world == null)
            {
                report.Add("! WorldStreamer 를 찾지 못했습니다.");
                Log("흩뿌리기", report);
                return;
            }

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            if (terrains.Length == 0)
            {
                report.Add("! 터레인이 없습니다. CarDrive > World > 터레인 월드 굽기 를 먼저 실행하세요.");
                Log("흩뿌리기", report);
                return;
            }

            List<GameObject> trees = LoadPrefabs(TreeFolder);
            List<GameObject> rocks = LoadPrefabs(RockFolder);

            if (trees.Count == 0 && rocks.Count == 0)
            {
                report.Add("! 심을 프리팹이 없습니다. 먼저 프리팹 만들기를 실행하세요.");
                Log("흩뿌리기", report);
                return;
            }

            Transform root = Rebuild(ScatterRoot);
            Bounds bounds = WorldBounds(terrains);
            WorldLayout layout = WorldLayout.From(world);

            // 나무와 바위의 자리를 <b>따로</b> 뽑습니다. 같은 샘플을 나눠 쓰면 나무 옆에
            // 반드시 바위가 오는 규칙적인 배치가 되어 눈에 띕니다.
            List<Spot> treeSpots = SampleSpots(bounds, terrains, layout,
                                               minDistance: 15f, seed: 20260820,
                                               label: "나무", report: report);

            List<Spot> rockSpots = SampleSpots(bounds, terrains, layout,
                                               minDistance: 28f, seed: 77712,
                                               label: "바위", report: report);

            PlantTrees(treeSpots, trees, terrains, seed: 20260820,
                       scaleRange: new Vector2(0.85f, 1.35f), report: report);

            PlaceRocks(rockSpots, rocks, root, seed: 77712,
                       scaleRange: new Vector2(0.6f, 2.4f), sink: 0.3f, report: report);

            Log("흩뿌리기", report);
        }

        /// <summary>마을에 건물을 세웁니다.</summary>
        [MenuItem("CarDrive/World/3. 마을 세우기")]
        public static void BuildVillage()
        {
            List<string> report = new List<string>();

            WorldStreamer world = Object.FindAnyObjectByType<WorldStreamer>(FindObjectsInactive.Include);
            if (world == null)
            {
                report.Add("! WorldStreamer 를 찾지 못했습니다.");
                Log("마을", report);
                return;
            }

            List<GameObject> houses = LoadPrefabs(HouseFolder);
            if (houses.Count == 0)
            {
                report.Add("! 건물 프리팹이 없습니다. 먼저 프리팹 만들기를 실행하세요.");
                Log("마을", report);
                return;
            }

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            Transform root = Rebuild(VillageRoot);

            WorldLayout layout = WorldLayout.From(world);
            System.Random random = new System.Random(90210);

            // 이미 세운 자리들입니다. 두 길이 만나는 부근에서 집이 겹치는 것을 막습니다.
            List<Vector3> taken = new List<Vector3>();

            int built = 0;
            int skipped = 0;

            // 길을 따라 양옆으로 집을 늘어세웁니다.
            for (int r = 0; r < world.routes.Count; r++)
            {
                WorldRoute route = world.routes[r];

                Vector3 dir = route.direction.sqrMagnitude > 0.0001f
                    ? route.direction.normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;

                Vector3 start = layout.VillageCenter + route.startOffset;

                // <b>길의 시작점 앞뒤로</b> 훑습니다. 앞으로만 가면 안 됩니다.
                // 동쪽 길은 마을 중심에서 100m 떨어진 곳에서 시작하기 때문에,
                // 앞으로만 세우면 집이 전부 마을 밖에 서게 됩니다.
                for (float along = -layout.VillageRadius; along <= layout.VillageRadius; along += 24f)
                {
                    for (int s = -1; s <= 1; s += 2)
                    {
                        // 한 자리 건너 하나씩 빼서 줄이 지나치게 반듯해지지 않게 합니다.
                        if (random.NextDouble() < 0.3) continue;

                        float setback = RoadClearance + 3f + (float)random.NextDouble() * 7f;
                        Vector3 spot = start + dir * along + side * (setback * s);

                        // 마을 근처인지는 <b>길을 따라 간 거리</b>가 아니라
                        // 마을 중심에서의 거리로 판단해야 합니다.
                        if (layout.DistanceToVillage(spot.x, spot.z) > layout.VillageRadius + 30f) continue;

                        // 다른 길 위에 올라앉지 않도록 확인합니다.
                        if (layout.NearRoad(spot.x, spot.z, RoadClearance))
                        {
                            skipped++;
                            continue;
                        }

                        if (TooClose(taken, spot, 14f))
                        {
                            skipped++;
                            continue;
                        }

                        if (!SampleGround(terrains, spot, out Vector3 ground, out float slope, out _) ||
                            slope > MaxSlope)
                        {
                            skipped++;
                            continue;
                        }

                        GameObject prefab = houses[random.Next(houses.Count)];
                        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);

                        // 조금 묻습니다. 언덕에서 주춧돌이 떠 보이지 않게 합니다.
                        go.transform.position = ground - Vector3.up * 0.25f;

                        // 정면이 길을 보게 돌립니다. 등을 돌린 집이 섞이면 마을로 안 보입니다.
                        // 각도를 살짝 흔들어야 도열한 것처럼 보이지 않습니다.
                        float jitter = ((float)random.NextDouble() - 0.5f) * 10f;
                        go.transform.rotation = Quaternion.LookRotation(-side * s, Vector3.up) *
                                                Quaternion.Euler(0f, jitter, 0f);

                        taken.Add(ground);
                        built++;
                    }
                }
            }

            report.Add("· 건물 " + built + "채를 세웠습니다. (자리 없음 " + skipped + ", 마을 반경 " +
                       layout.VillageRadius + "m)");
            report.Add("  길을 향해 돌려 세웠고, 도로 중심에서 " + RoadClearance + "m 이상 물러나 있습니다.");
            Log("마을", report);
        }

        /// <summary>월드에 실제로 무엇이 들어가 있는지 세어 봅니다.</summary>
        [MenuItem("CarDrive/World/4. 월드 점검")]
        public static void Inspect()
        {
            List<string> report = new List<string>();

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            if (terrains.Length == 0)
            {
                report.Add("! 터레인이 없습니다.");
                Log("점검", report);
                return;
            }

            Bounds bounds = WorldBounds(terrains);
            report.Add("· 터레인 " + terrains.Length + "장, 월드 " +
                       bounds.size.x.ToString("F0") + " x " + bounds.size.z.ToString("F0") + "m");

            // 나무는 씬이 아니라 터레인 데이터 안에 있습니다. 거기서 세야 합니다.
            int trees = 0;
            int prototypes = 0;
            int emptyTiles = 0;

            for (int i = 0; i < terrains.Length; i++)
            {
                TerrainData data = terrains[i].terrainData;
                if (data == null) continue;

                int count = data.treeInstances.Length;
                trees += count;
                if (count == 0) emptyTiles++;

                prototypes = Mathf.Max(prototypes, data.treePrototypes.Length);
            }

            report.Add("· 터레인 트리 " + trees + "그루 (종류 " + prototypes + ", 나무 없는 타일 " +
                       emptyTiles + "장)");

            report.Add("· 씬 오브젝트 — 바위 " + CountChildren(ScatterRoot) +
                       ", 집 " + CountChildren(VillageRoot));

            ReportDressing(terrains, report);

            // 도로에서 가장 먼 지점이 곧 회랑의 반폭입니다.
            WorldStreamer world = Object.FindAnyObjectByType<WorldStreamer>(FindObjectsInactive.Include);
            if (world != null)
            {
                WorldLayout layout = WorldLayout.From(world);
                float farthest = 0f;

                for (int i = 0; i < terrains.Length; i++)
                {
                    Vector3 origin = terrains[i].transform.position;
                    Vector3 size = terrains[i].terrainData.size;

                    // 타일 네 귀퉁이만 봐도 가장 먼 점이 나옵니다.
                    for (int c = 0; c < 4; c++)
                    {
                        float cx = origin.x + ((c & 1) == 0 ? 0f : size.x);
                        float cz = origin.z + ((c & 2) == 0 ? 0f : size.z);
                        farthest = Mathf.Max(farthest, layout.DistanceToRoad(cx, cz));
                    }
                }

                report.Add("· 도로에서 가장 먼 지형: " + farthest.ToString("F0") + "m " +
                           "(안개는 340m 에서 끝납니다)");
            }

            MountainStamp stamp = AssetDatabase.LoadAssetAtPath<MountainStamp>(
                "Assets/_Project/03.DataAssets/Terrain/Generated/MountainStamp.asset");

            report.Add(stamp == null
                ? "· 산: 올린 기록이 없습니다."
                : "· 산: " + (stamp.applied ? "올라가 있습니다" : "내려가 있습니다") +
                  " (최대 " + (stamp.amplitude * 70f).ToString("F0") + "m 상당)");

            Log("점검", report);
        }

        /// <summary>
        /// 명령줄에서 씬을 열고 점검합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.ProcGen.WorldFeatureTool.InspectFromCommandLine</c>
        /// </summary>
        public static void InspectFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Inspect();
        }

        /// <summary>
        /// 명령줄에서 셋을 차례로 실행하고 씬을 저장합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.ProcGen.WorldFeatureTool.GenerateFromCommandLine</c>
        /// </summary>
        public static void GenerateFromCommandLine()
        {
            CreatePrefabs();

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Scatter();
            BuildVillage();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // --- Private Methods : 프리팹 ---

        /// <summary>저폴리 바위 프리팹을 만듭니다.</summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void CreateRockPrefabs(List<string> report)
        {
            EnsureFolder(RockFolder);
            Material material = EnsureMaterial("RockLowPoly", new Color(0.44f, 0.44f, 0.47f), report);

            for (int i = 0; i < RockVariants; i++)
            {
                string name = "Rock" + (i + 1).ToString("00");

                Mesh mesh = LowPolyMeshFactory.CreateRock(
                    seed: 1000 + i * 37,
                    subdivisions: i < 2 ? 1 : 2,          // 작은 돌은 면을 덜 씁니다
                    roughness: Mathf.Lerp(0.28f, 0.52f, i / (float)(RockVariants - 1)));

                mesh.name = name;
                mesh = SaveMesh(mesh, RockFolder + "/" + name + ".asset");

                GameObject go = new GameObject(name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;

                // 차가 부딪힐 수 있어야 합니다. 볼록 껍질이면 충돌 계산이 훨씬 쌉니다.
                MeshCollider collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = true;

                PrefabUtility.SaveAsPrefabAsset(go, RockFolder + "/" + name + ".prefab");
                Object.DestroyImmediate(go);

                report.Add("· 바위 " + name + " (면 " + (mesh.triangles.Length / 3) + "개, " +
                           FacingNote(mesh) + ")");
            }
        }

        /// <summary>저폴리 건물 프리팹을 만듭니다.</summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void CreateHousePrefabs(List<string> report)
        {
            EnsureFolder(HouseFolder);

            Material wall = EnsureMaterial("BuildingWall", new Color(0.72f, 0.68f, 0.60f), report);
            Material roof = EnsureMaterial("BuildingRoof", new Color(0.42f, 0.28f, 0.26f), report);

            System.Random random = new System.Random(4404);

            for (int i = 0; i < HouseVariants; i++)
            {
                string name = "House" + (i + 1).ToString("00");

                float width = Mathf.Lerp(6f, 11f, (float)random.NextDouble());
                float depth = Mathf.Lerp(6f, 10f, (float)random.NextDouble());
                float wallHeight = Mathf.Lerp(3.2f, 5.5f, (float)random.NextDouble());
                float roofHeight = Mathf.Lerp(1.6f, 3.4f, (float)random.NextDouble());

                // 벽과 지붕은 재질이 달라 오브젝트를 나눕니다.
                // 저폴리에서 집으로 읽히는 건 창문이 아니라 <b>지붕 색이 다르다</b>는 사실입니다.
                Mesh bodyMesh = LowPolyMeshFactory.CreateBuilding(width, depth, wallHeight, 0f);
                bodyMesh.name = name + "_Body";
                bodyMesh = SaveMesh(bodyMesh, HouseFolder + "/" + name + "_Body.asset");

                Mesh roofMesh = LowPolyMeshFactory.CreateGableRoof(width * 1.08f, depth * 1.08f, roofHeight);
                roofMesh.name = name + "_Roof";
                roofMesh = SaveMesh(roofMesh, HouseFolder + "/" + name + "_Roof.asset");

                GameObject go = new GameObject(name);
                go.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = wall;

                // 지붕까지 감싸는 상자 하나면 충분합니다. 차가 지붕에 부딪힐 일은 없습니다.
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, wallHeight * 0.5f, 0f);
                box.size = new Vector3(width, wallHeight, depth);

                GameObject roofGo = new GameObject("Roof");
                roofGo.transform.SetParent(go.transform, false);
                roofGo.transform.localPosition = new Vector3(0f, wallHeight, 0f);
                roofGo.AddComponent<MeshFilter>().sharedMesh = roofMesh;
                roofGo.AddComponent<MeshRenderer>().sharedMaterial = roof;

                PrefabUtility.SaveAsPrefabAsset(go, HouseFolder + "/" + name + ".prefab");
                Object.DestroyImmediate(go);

                report.Add("· 건물 " + name + " (" + width.ToString("F1") + " x " + depth.ToString("F1") +
                           "m, 높이 " + (wallHeight + roofHeight).ToString("F1") + "m, 벽 " +
                           FacingNote(bodyMesh) + ", 지붕 " + FacingNote(roofMesh) + ")");
            }
        }

        /// <summary>
        /// 면이 제대로 바깥을 향하는지 한 줄로 적습니다.
        ///
        /// 뒤집힌 메시는 화면에서 <b>셰이더가 깨진 것처럼</b> 보입니다.
        /// 원인이 메시라는 것을 알아채기 어려우므로 만들 때마다 찍어 둡니다.
        /// </summary>
        /// <param name="mesh">확인할 메시</param>
        /// <returns>보고에 적을 문구</returns>
        private static string FacingNote(Mesh mesh)
        {
            float ratio = LowPolyMeshFactory.OutwardFaceRatio(mesh);

            return ratio >= 0.999f
                ? "면 정상"
                : "! 면이 뒤집힘 " + ((1f - ratio) * 100f).ToString("F0") + "%";
        }

        /// <summary>
        /// 메시를 에셋으로 저장합니다.
        ///
        /// 이미 있으면 <b>지우고 새로 만들지 않고</b> 안을 갈아 끼웁니다.
        /// 지우면 이미 심어 둔 프리팹들의 참조가 한꺼번에 끊깁니다.
        /// </summary>
        /// <param name="mesh">저장할 메시</param>
        /// <param name="path">에셋 경로</param>
        /// <returns>실제로 프로젝트에 남은 메시. 참조는 이것을 써야 합니다.</returns>
        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.uv = mesh.uv;
            existing.triangles = mesh.triangles;
            existing.RecalculateBounds();

            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);

            return existing;
        }

        // --- Private Methods : 배치 ---

        /// <summary>흩뿌릴 자리 하나입니다. 어느 터레인 위인지도 함께 들고 있습니다.</summary>
        private struct Spot
        {
            /// <summary>지표면 위치</summary>
            public Vector3 ground;

            /// <summary>이 자리가 올라앉은 터레인</summary>
            public Terrain terrain;
        }

        /// <summary>
        /// 푸아송 디스크로 자리를 뽑고, 놓을 수 없는 곳을 걸러 냅니다.
        /// </summary>
        /// <param name="bounds">뿌릴 범위</param>
        /// <param name="terrains">높이를 읽을 터레인들</param>
        /// <param name="layout">피할 길과 마을</param>
        /// <param name="minDistance">서로 떨어질 최소 간격(m)</param>
        /// <param name="seed">난수 씨앗</param>
        /// <param name="label">보고에 적을 이름</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>쓸 수 있는 자리 목록</returns>
        private static List<Spot> SampleSpots(Bounds bounds, Terrain[] terrains, WorldLayout layout,
                                              float minDistance, int seed,
                                              string label, List<string> report)
        {
            List<Spot> spots = new List<Spot>();

            Vector2 size = new Vector2(bounds.size.x, bounds.size.z);
            List<Vector2> points = PoissonDisk.Sample(size, minDistance, seed);

            int rejectedRoad = 0, rejectedSlope = 0, rejectedVillage = 0, rejectedVoid = 0;

            for (int i = 0; i < points.Count; i++)
            {
                float wx = bounds.min.x + points[i].x;
                float wz = bounds.min.z + points[i].y;

                // 마을 안은 건물이 들어설 자리입니다.
                // 집은 마을 반경보다 30m 더 나가 서므로 그만큼 넉넉히 비웁니다.
                // 나무와 집은 서로를 모르기 때문에 여기서 겹치지 않게 해야 합니다.
                if (layout.DistanceToVillage(wx, wz) < layout.VillageRadius + 35f)
                {
                    rejectedVillage++;
                    continue;
                }

                // 길 위에 서 있으면 주행이 불가능해집니다.
                if (layout.NearRoad(wx, wz, RoadClearance))
                {
                    rejectedRoad++;
                    continue;
                }

                Vector3 probe = new Vector3(wx, 0f, wz);

                if (!SampleGround(terrains, probe, out Vector3 ground, out float slope, out Terrain terrain))
                {
                    rejectedVoid++;
                    continue;
                }

                if (slope > MaxSlope)
                {
                    rejectedSlope++;
                    continue;
                }

                spots.Add(new Spot { ground = ground, terrain = terrain });
            }

            report.Add("· " + label + " " + spots.Count + "자리 (후보 " + points.Count + ") — 제외: 도로 " +
                       rejectedRoad + " · 비탈 " + rejectedSlope + " · 마을 " + rejectedVillage +
                       " · 터레인 밖 " + rejectedVoid);

            return spots;
        }

        /// <summary>
        /// 나무를 <b>터레인 트리</b>로 심습니다. 게임오브젝트를 만들지 않습니다.
        ///
        /// 회랑을 넓히면서 나무가 2천 그루를 넘었습니다. 그만큼의 게임오브젝트를 씬에 두면
        /// 씬 파일이 수십 MB로 불어나고 나무 하나마다 드로우콜이 하나씩 나갑니다.
        ///
        /// 터레인 트리는 <b>터레인 데이터 안의 배열</b>일 뿐이라 씬이 커지지 않고,
        /// 유니티가 거리 컬링과 인스턴싱을 알아서 해 줍니다.
        /// 충돌도 됩니다. 터레인 콜라이더의 <c>m_EnableTreeColliders</c> 가 켜져 있고
        /// 나무 프리팹에 캡슐 콜라이더가 붙어 있습니다.
        /// </summary>
        /// <param name="spots">심을 자리들</param>
        /// <param name="prefabs">나무 프리팹들</param>
        /// <param name="terrains">모든 터레인. 남아 있던 나무를 지우는 데도 씁니다.</param>
        /// <param name="seed">난수 씨앗</param>
        /// <param name="scaleRange">크기 범위</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void PlantTrees(List<Spot> spots, List<GameObject> prefabs, Terrain[] terrains,
                                       int seed, Vector2 scaleRange, List<string> report)
        {
            if (prefabs.Count == 0)
            {
                report.Add("! 나무 프리팹이 없어 건너뜁니다.");
                return;
            }

            // 프로토타입은 <b>모든 터레인에 같은 순서로</b> 넣어야 합니다.
            // 타일마다 순서가 다르면 인덱스가 어긋나 엉뚱한 나무가 심깁니다.
            TreePrototype[] prototypes = new TreePrototype[prefabs.Count];
            for (int i = 0; i < prefabs.Count; i++)
            {
                prototypes[i] = new TreePrototype();
                prototypes[i].prefab = prefabs[i];
                prototypes[i].bendFactor = 0f;
            }

            // 자리가 하나도 없는 타일도 넣어 둡니다. 지난번에 심어 둔 나무를 지워야 합니다.
            Dictionary<Terrain, List<TreeInstance>> buckets = new Dictionary<Terrain, List<TreeInstance>>();
            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] != null && terrains[i].terrainData != null)
                {
                    buckets[terrains[i]] = new List<TreeInstance>();
                }
            }

            System.Random random = new System.Random(seed);

            for (int i = 0; i < spots.Count; i++)
            {
                Terrain terrain = spots[i].terrain;
                if (terrain == null || !buckets.ContainsKey(terrain)) continue;

                Vector3 local = spots[i].ground - terrain.transform.position;
                Vector3 size = terrain.terrainData.size;

                float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)random.NextDouble());

                TreeInstance instance = new TreeInstance();
                instance.prototypeIndex = random.Next(prefabs.Count);

                // 자리는 터레인 안에서의 <b>비율</b>로 넣습니다. y 는 아래에서 지면에 붙입니다.
                instance.position = new Vector3(local.x / size.x, 0f, local.z / size.z);

                instance.widthScale = scale;
                instance.heightScale = scale;
                instance.rotation = (float)random.NextDouble() * Mathf.PI * 2f;
                instance.color = Color.white;
                instance.lightmapColor = Color.white;

                buckets[terrain].Add(instance);
            }

            int planted = 0;

            foreach (KeyValuePair<Terrain, List<TreeInstance>> pair in buckets)
            {
                TerrainData data = pair.Key.terrainData;

                data.treePrototypes = prototypes;
                data.RefreshPrototypes();

                // true 를 넘기면 y 를 지면 높이에 맞춰 줍니다. 직접 계산할 필요가 없습니다.
                data.SetTreeInstances(pair.Value.ToArray(), true);

                pair.Key.Flush();

                EditorUtility.SetDirty(data);
                planted += pair.Value.Count;
            }

            report.Add("· 나무 " + planted + "그루를 터레인 트리로 심었습니다. (씬 오브젝트는 0개입니다)");
            report.Add("  나무가 사라지는 거리는 CarDrive > Look > 나무 디더 페이드 적용 이 정합니다.");
        }

        /// <summary>
        /// 바위를 프리팹 인스턴스로 놓습니다.
        ///
        /// 나무와 달리 게임오브젝트로 둡니다. 볼록 메시 콜라이더로 차와 부딪혀야 하는데,
        /// 터레인 트리에 붙는 콜라이더는 캡슐 정도만 믿을 만합니다.
        /// 개수도 나무보다 훨씬 적어 씬에 부담이 되지 않습니다.
        /// </summary>
        /// <param name="spots">놓을 자리들</param>
        /// <param name="prefabs">바위 프리팹들</param>
        /// <param name="root">담을 부모</param>
        /// <param name="seed">난수 씨앗</param>
        /// <param name="scaleRange">크기 범위</param>
        /// <param name="sink">땅에 묻을 깊이(m)</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void PlaceRocks(List<Spot> spots, List<GameObject> prefabs, Transform root,
                                       int seed, Vector2 scaleRange, float sink, List<string> report)
        {
            if (prefabs.Count == 0)
            {
                report.Add("! 바위 프리팹이 없어 건너뜁니다.");
                return;
            }

            System.Random random = new System.Random(seed);

            for (int i = 0; i < spots.Count; i++)
            {
                GameObject prefab = prefabs[random.Next(prefabs.Count)];
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);

                float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)random.NextDouble());

                // 땅에 살짝 묻습니다. 정확히 지표면에 놓으면 비탈에서 밑동이 떠 보입니다.
                go.transform.position = spots[i].ground - Vector3.up * (sink * scale);
                go.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * scale;
            }

            report.Add("· 바위 " + spots.Count + "개를 놓았습니다.");
        }

        /// <summary>지면의 높이와 경사를 구합니다.</summary>
        /// <param name="terrains">찾아볼 터레인들</param>
        /// <param name="worldPos">알고 싶은 자리. y는 무시합니다.</param>
        /// <param name="ground">지표면 위치</param>
        /// <param name="slope">경사(도)</param>
        /// <param name="found">그 자리를 담고 있는 터레인</param>
        /// <returns>그 자리에 터레인이 있으면 true 입니다.</returns>
        private static bool SampleGround(Terrain[] terrains, Vector3 worldPos,
                                         out Vector3 ground, out float slope, out Terrain found)
        {
            ground = worldPos;
            slope = 90f;
            found = null;

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null) continue;

                Vector3 local = worldPos - terrain.transform.position;
                Vector3 size = terrain.terrainData.size;

                if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z) continue;

                // SampleHeight 는 터레인 기준 높이라 원점을 더해야 월드 좌표가 됩니다.
                float height = terrain.SampleHeight(worldPos) + terrain.transform.position.y;
                ground = new Vector3(worldPos.x, height, worldPos.z);

                Vector3 normal = terrain.terrainData.GetInterpolatedNormal(local.x / size.x, local.z / size.z);
                slope = Vector3.Angle(normal, Vector3.up);

                found = terrain;
                return true;
            }

            return false;
        }

        // --- Private Methods : 공용 ---

        /// <summary>이미 놓인 자리 중 지정 거리 안쪽에 있는 것이 있는지 확인합니다.</summary>
        /// <param name="taken">이미 놓인 자리들</param>
        /// <param name="spot">놓으려는 자리</param>
        /// <param name="minDistance">떨어져야 할 최소 거리(m)</param>
        /// <returns>너무 가까우면 true 입니다.</returns>
        private static bool TooClose(List<Vector3> taken, Vector3 spot, float minDistance)
        {
            float minSqr = minDistance * minDistance;

            for (int i = 0; i < taken.Count; i++)
            {
                float dx = taken[i].x - spot.x;
                float dz = taken[i].z - spot.z;

                if (dx * dx + dz * dz < minSqr) return true;
            }

            return false;
        }

        /// <summary>모든 터레인을 감싸는 범위를 구합니다.</summary>
        /// <param name="terrains">감쌀 터레인들</param>
        /// <returns>월드 범위</returns>
        private static Bounds WorldBounds(Terrain[] terrains)
        {
            Bounds bounds = new Bounds(terrains[0].transform.position, Vector3.zero);

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null) continue;

                bounds.Encapsulate(terrain.transform.position);
                bounds.Encapsulate(terrain.transform.position + terrain.terrainData.size);
            }

            return bounds;
        }

        /// <summary>폴더의 프리팹을 모두 읽습니다.</summary>
        /// <param name="folder">찾을 폴더</param>
        /// <returns>프리팹 목록. 폴더가 없으면 빈 목록입니다.</returns>
        private static List<GameObject> LoadPrefabs(string folder)
        {
            List<GameObject> list = new List<GameObject>();
            if (!AssetDatabase.IsValidFolder(folder)) return list;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (go != null) list.Add(go);
            }

            return list;
        }

        /// <summary>
        /// 지면이 제대로 단장되어 있는지 봅니다.
        ///
        /// 터레인을 다시 구우면 풀과 지면 레이어가 조용히 사라집니다.
        /// 화면을 보기 전에는 알아채기 어려운데, 지면 레이어가 Default 로 돌아가 있으면
        /// <b>병 같은 소품이 지면을 뚫고 떨어집니다.</b> 그래서 여기서 함께 셉니다.
        /// </summary>
        /// <param name="terrains">확인할 터레인들</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void ReportDressing(Terrain[] terrains, List<string> report)
        {
            long grassCells = 0;
            int noGrassTiles = 0;
            int groundLayer = LayerMask.NameToLayer("Ground");
            int wrongLayer = 0;

            HashSet<string> materials = new HashSet<string>();

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                TerrainData data = terrain.terrainData;
                if (data == null) continue;

                if (groundLayer >= 0 && terrain.gameObject.layer != groundLayer) wrongLayer++;

                materials.Add(terrain.materialTemplate != null ? terrain.materialTemplate.name : "(없음)");

                if (data.detailPrototypes.Length == 0)
                {
                    noGrassTiles++;
                    continue;
                }

                int res = data.detailResolution;
                int[,] layer = data.GetDetailLayer(0, 0, res, res, 0);

                long tile = 0;
                for (int z = 0; z < res; z++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        if (layer[z, x] > 0) tile++;
                    }
                }

                if (tile == 0) noGrassTiles++;
                grassCells += tile;
            }

            report.Add("· 풀 " + grassCells + "칸 (풀 없는 타일 " + noGrassTiles + "장)");
            report.Add("· 지면 머티리얼: " + string.Join(", ", materials));

            ReportRampHealth(terrains, report);

            report.Add(wrongLayer == 0
                ? "· 지면 레이어: 전부 Ground — 소품이 지면을 뚫지 않습니다."
                : "! 지면 레이어가 Ground 가 아닌 타일 " + wrongLayer + "장 — 소품이 지면을 뚫습니다. " +
                  "CarDrive > World > 지면 단장 다시 입히기 를 실행하세요.");
        }

        /// <summary>
        /// 지면과 풀이 <b>그림자를 받을 수 있는 상태인지</b> 봅니다.
        ///
        /// 툰 램프 방식은 밝기를 램프 텍스처의 가로축으로 읽습니다. 그래서 키워드만 켜고
        /// 램프를 붙이지 않으면 기본 흰색 텍스처를 읽어, <b>밝기와 상관없이 흰색</b>이 나옵니다.
        /// 그림자를 계산하긴 하는데 결과가 버려지는 것이라, 화면에는 "그림자가 안 진다"로만
        /// 보입니다. 실제로 풀이 이 상태였습니다.
        /// </summary>
        /// <param name="terrains">확인할 터레인들</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void ReportRampHealth(Terrain[] terrains, List<string> report)
        {
            HashSet<Material> seen = new HashSet<Material>();

            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i].materialTemplate != null) seen.Add(terrains[i].materialTemplate);

                TerrainData data = terrains[i].terrainData;
                if (data == null) continue;

                DetailPrototype[] protos = data.detailPrototypes;
                for (int d = 0; d < protos.Length; d++)
                {
                    if (protos[d] == null || protos[d].prototype == null) continue;

                    Renderer renderer = protos[d].prototype.GetComponentInChildren<Renderer>(true);
                    if (renderer != null && renderer.sharedMaterial != null) seen.Add(renderer.sharedMaterial);
                }
            }

            int broken = 0;

            foreach (Material mat in seen)
            {
                if (!mat.IsKeywordEnabled("_TOON_RAMP")) continue;
                if (!mat.HasProperty("_ToonRampMap") || mat.GetTexture("_ToonRampMap") != null) continue;

                report.Add("! " + mat.name + " 은 램프 키워드가 켜져 있는데 램프가 없습니다. " +
                           "그림자를 받지 못합니다. CarDrive > Look > 툰 룩 적용 을 실행하세요.");
                broken++;
            }

            if (broken == 0) report.Add("· 지면·풀의 램프 상태 정상 — 그림자를 받습니다.");
        }

        /// <summary>이름이 같은 씬 루트의 자식 수를 셉니다. 없으면 0입니다.</summary>
        /// <param name="name">루트 이름</param>
        /// <returns>자식 수</returns>
        private static int CountChildren(string name)
        {
            GameObject root = GameObject.Find(name);
            return root != null ? root.transform.childCount : 0;
        }

        /// <summary>이름이 같은 씬 루트를 지우고 새로 만듭니다. 다시 실행해도 쌓이지 않습니다.</summary>
        /// <param name="name">루트 이름</param>
        /// <returns>새로 만든 루트</returns>
        private static Transform Rebuild(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null) Object.DestroyImmediate(existing);

            return new GameObject(name).transform;
        }

        /// <summary>절차적 메시가 쓸 재질을 만듭니다. 툰 셰이더가 있으면 그것을 씁니다.</summary>
        /// <param name="name">재질 이름</param>
        /// <param name="color">기본 색</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>재질</returns>
        private static Material EnsureMaterial(string name, Color color, List<string> report)
        {
            EnsureFolder(MaterialFolder);

            string path = MaterialFolder + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            // 땅과 같은 조명을 받아야 물체가 배경에서 떠 보이지 않습니다.
            Shader shader = Shader.Find("CarDrive/Toon Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

            mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

            AssetDatabase.CreateAsset(mat, path);
            report.Add("· 재질을 만들었습니다: " + path + " (" + shader.name + ")");

            return mat;
        }

        /// <summary>폴더가 없으면 만듭니다.</summary>
        /// <param name="path">만들 폴더 경로</param>
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

        /// <summary>진행 내용을 한 번에 찍습니다.</summary>
        /// <param name="title">머리말</param>
        /// <param name="report">적어 둔 줄들</param>
        private static void Log(string title, List<string> report)
        {
            Debug.Log("WorldFeatureTool(" + title + "):" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }
    }
}
