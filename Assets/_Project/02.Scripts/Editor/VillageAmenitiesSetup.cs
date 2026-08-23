using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using CarDrive.Gameplay;
using CarDrive.Systems;
using CarDrive.UI;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 마을에 <b>플레이어의 집</b>과 <b>마트</b>를 세웁니다.
    ///
    /// 이 둘이 없어서 게임에 구멍이 두 개 나 있었습니다 — 허기와 피로를 푸는 방법이
    /// 아예 없었습니다. 니즈는 차오르는데 되돌릴 길이 없으면 그것은 난이도가 아니라 고장입니다.
    ///
    /// <b>왜 절차적으로 짓는가.</b> 손으로 놓으면 무엇을 왜 그 자리에 두었는지가 씬 파일 안에만
    /// 남습니다. 여기서 지으면 치수와 배치가 코드에 적혀 있어, 마을을 옮기거나 크기를 바꿀 때
    /// 다시 실행하면 됩니다. 건물은 ProBuilder 메시라 <b>지은 뒤에 손으로 마저 다듬을 수 있습니다.</b>
    ///
    /// <b>두 번 실행해도 안전합니다.</b> 이미 세워진 것이 있으면 그대로 두고 지나갑니다.
    /// </summary>
    public static class VillageAmenitiesSetup
    {
        // --- Constants ---

        private const string InteractableLayer = "Interactable";

        private const string HomeName = "PlayerHome";
        private const string MartName = "VillageMart";

        private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
        private const string BeerCasePrefabPath = "Assets/_Project/05.Prefabs/Items/bottle_case.prefab";

        /// <summary>마을 중심에서 집이 놓일 자리입니다. 길(+Z·+X)을 피해 뒤쪽에 둡니다.</summary>
        private static readonly Vector3 HomeOffset = new Vector3(-22f, 0f, -14f);

        /// <summary>마을 중심에서 마트가 놓일 자리입니다.</summary>
        private static readonly Vector3 MartOffset = new Vector3(20f, 0f, -16f);

        // --- Public Methods ---

        /// <summary>
        /// 집과 마트를 세우고 안을 채웁니다.
        /// </summary>
        [MenuItem("CarDrive/Gameplay/마을 시설 세우기 (집·마트)")]
        public static void Setup()
        {
            List<string> report = new List<string>();

            if (LayerMask.NameToLayer(InteractableLayer) < 0)
            {
                report.Add("! " + InteractableLayer + " 레이어가 없어 조준할 수 없습니다. 먼저 레이어를 만드세요.");
            }

            Vector3 center = ResolveVillageCenter(report);

            List<ShopItem> items = ShopAssetsSetup.Build(report);
            GameObject bagPrefab = ShopAssetsSetup.BuildBagPrefab(report);

            BuildHome(center + HomeOffset, report);
            BuildMart(center + MartOffset, items, bagPrefab, report);
            PlaceBeerCases(report);

            Debug.Log("VillageAmenitiesSetup:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 씬을 열고 세운 뒤 저장합니다. 배치 없이 명령줄에서 돌릴 때 씁니다.
        /// <c>BeverageSetup.SetupFromCommandLine</c> 과 같은 방식입니다.
        /// </summary>
        public static void SetupFromCommandLine()
        {
            UnityEngine.SceneManagement.Scene scene =
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath,
                    UnityEditor.SceneManagement.OpenSceneMode.Single);

            Setup();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // --- Private Methods : 맥주 상자 ---

        /// <summary>
        /// 맥주 상자를 실제로 손이 닿는 곳에 놓습니다.
        ///
        /// <b>지금까지 프리팹만 있고 씬에 없었습니다.</b> 음료 시스템이 다 만들어져 있는데
        /// 마실 것이 세상에 하나도 없는 상태였습니다.
        ///
        /// 두 곳에 놓습니다. 집 안의 것은 값을 치르지 않아도 바로 쓸 수 있고,
        /// 차 안의 것은 <c>BeverageBox.vehicle</c> 규칙(그 차에 타고 있을 때만 꺼낼 수 있음)이
        /// 실제로 동작하는지 확인해 줍니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void PlaceBeerCases(List<string> report)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BeerCasePrefabPath);
            if (prefab == null)
            {
                report.Add("! 맥주 상자 프리팹을 찾지 못했습니다: " + BeerCasePrefabPath);
                return;
            }

            // 집 안 — 바닥에 그대로 둡니다.
            GameObject home = GameObject.Find(HomeName);
            if (home != null && home.transform.Find("BeerCase") == null)
            {
                GameObject inHome = (GameObject)PrefabUtility.InstantiatePrefab(prefab, home.transform);
                inHome.name = "BeerCase";
                inHome.transform.localPosition = new Vector3(2.6f, 0f, -1.6f);
                ShopAssetsSetup.SetLayerDeep(inHome, InteractableLayer);
                report.Add("· 집 안에 맥주 상자를 놓았습니다.");
            }

            // 차 안 — 그 차에 타고 있을 때만 꺼낼 수 있습니다.
            Vehicle vehicle = Object.FindAnyObjectByType<Vehicle>(FindObjectsInactive.Include);
            if (vehicle == null)
            {
                report.Add("! 차량을 찾지 못해 차 안에는 놓지 않았습니다.");
                return;
            }

            if (vehicle.transform.Find("BeerCase") != null)
            {
                report.Add("· 차 안: 이미 맥주 상자가 있습니다.");
                return;
            }

            GameObject inCar = (GameObject)PrefabUtility.InstantiatePrefab(prefab, vehicle.transform);
            inCar.name = "BeerCase";

            // 뒷좌석 자리입니다. 차체 모양은 프리팹마다 다르므로, 파묻히면 여기 값을 고치세요.
            inCar.transform.localPosition = new Vector3(0f, 0.55f, -0.9f);
            ShopAssetsSetup.SetLayerDeep(inCar, InteractableLayer);

            BeverageBox box = inCar.GetComponent<BeverageBox>();
            if (box != null) box.vehicle = vehicle;

            report.Add("· 차 안에 맥주 상자를 놓았습니다. (로컬 " + inCar.transform.localPosition +
                       " · 차체에 파묻히면 이 값을 조정하세요)");
        }

        // --- Private Methods : 플레이어의 집 ---

        /// <summary>
        /// 안이 있는 집 한 채를 세우고 침대를 놓습니다.
        /// </summary>
        /// <param name="position">집이 설 자리</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void BuildHome(Vector3 position, List<string> report)
        {
            if (GameObject.Find(HomeName) != null)
            {
                report.Add("· 집: 이미 세워져 있습니다.");
                return;
            }

            Material wall = ShopAssetsSetup.EnsureMaterial("HomeWall", new Color(0.72f, 0.68f, 0.60f), report);
            Material floor = ShopAssetsSetup.EnsureMaterial("HomeFloor", new Color(0.38f, 0.31f, 0.25f), report);
            Material roof = ShopAssetsSetup.EnsureMaterial("HomeRoof", new Color(0.42f, 0.26f, 0.22f), report);

            GameObject root = new GameObject(HomeName);
            Undo.RegisterCreatedObjectUndo(root, "집 세우기");
            root.transform.position = Ground(position);

            const float width = 8f;      // X
            const float depth = 6f;      // Z
            const float height = 2.8f;
            const float wallThickness = 0.2f;

            BuildShell(root.transform, width, depth, height, wallThickness, wall, floor, roof,
                doorOnMinusZ: true, windowOnPlusX: true);

            BuildBed(root.transform, new Vector3(-2.2f, 0f, 1.6f), report);
            BuildInteriorLight(root.transform, new Vector3(0f, height - 0.5f, 0f), new Color(1f, 0.92f, 0.78f), 6f);

            AddLocation(root, "집", LocationKind.Landmark, 8f);

            report.Add("· 집을 세웠습니다. " + root.transform.position + " (내부 " + width + "×" + depth + "m)");
        }

        /// <summary>
        /// 잠자리를 놓습니다. 8시간을 건너뛰고 피로와 스트레스를 덜어 줍니다.
        /// </summary>
        /// <param name="parent">집의 루트</param>
        /// <param name="localPosition">집 안에서의 자리</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void BuildBed(Transform parent, Vector3 localPosition, List<string> report)
        {
            Material frame = ShopAssetsSetup.EnsureMaterial("BedFrame", new Color(0.35f, 0.24f, 0.18f), report);
            Material sheet = ShopAssetsSetup.EnsureMaterial("BedSheet", new Color(0.82f, 0.82f, 0.86f), report);

            GameObject bed = new GameObject("Bed");
            bed.transform.SetParent(parent, false);
            bed.transform.localPosition = localPosition;

            PbBox("Frame", bed.transform, new Vector3(0f, 0.22f, 0f), new Vector3(1.1f, 0.44f, 2.05f), frame);
            PbBox("Mattress", bed.transform, new Vector3(0f, 0.55f, 0f), new Vector3(1.0f, 0.22f, 1.95f), sheet);
            PbBox("Pillow", bed.transform, new Vector3(0f, 0.72f, -0.75f), new Vector3(0.6f, 0.14f, 0.34f), sheet);

            // 조준해서 쓰는 자리입니다. 침대 위쪽을 넉넉히 덮는 트리거 하나로 받습니다.
            GameObject useZone = new GameObject("UseZone");
            useZone.transform.SetParent(bed.transform, false);
            useZone.transform.localPosition = new Vector3(0f, 0.6f, 0f);

            BoxCollider trigger = useZone.AddComponent<BoxCollider>();
            trigger.size = new Vector3(1.2f, 0.7f, 2.1f);
            trigger.isTrigger = true;

            NeedSatisfier sleep = useZone.AddComponent<NeedSatisfier>();
            sleep.promptLabel = "잠자기";
            sleep.gameMinutesElapsed = 480f;          // 8시간
            sleep.advanceTimeBeforeEffects = true;    // 자고 나서 회복됩니다
            sleep.affectedBySleepQuality = true;      // 궂은 날에는 덜 잡니다
            sleep.effects = new List<NeedEffect>
            {
                new NeedEffect { type = NeedType.Fatigue, relief = 1.5f },
                new NeedEffect { type = NeedType.Stress, relief = 0.3f }
            };

            ShopAssetsSetup.SetLayerDeep(useZone, InteractableLayer);
            report.Add("· 침대를 놓았습니다. (8시간 · 피로 1.5 · 스트레스 0.3 · 날씨 반영)");
        }

        // --- Private Methods : 마트 ---

        /// <summary>
        /// 마트를 세우고 진열장·계산대·점원을 놓습니다.
        /// </summary>
        /// <param name="position">마트가 설 자리</param>
        /// <param name="items">진열할 상품 정의들</param>
        /// <param name="bagPrefab">계산을 마쳤을 때 나올 봉투</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void BuildMart(Vector3 position, List<ShopItem> items, GameObject bagPrefab,
                                      List<string> report)
        {
            if (GameObject.Find(MartName) != null)
            {
                report.Add("· 마트: 이미 세워져 있습니다.");
                return;
            }

            Material wall = ShopAssetsSetup.EnsureMaterial("MartWall", new Color(0.80f, 0.80f, 0.76f), report);
            Material floor = ShopAssetsSetup.EnsureMaterial("MartFloor", new Color(0.55f, 0.56f, 0.58f), report);
            Material roof = ShopAssetsSetup.EnsureMaterial("MartRoof", new Color(0.30f, 0.42f, 0.48f), report);
            Material fixture = ShopAssetsSetup.EnsureMaterial("MartFixture", new Color(0.62f, 0.64f, 0.66f), report);

            GameObject root = new GameObject(MartName);
            Undo.RegisterCreatedObjectUndo(root, "마트 세우기");
            root.transform.position = Ground(position);

            const float width = 12f;
            const float depth = 9f;
            const float height = 3.2f;

            BuildShell(root.transform, width, depth, height, 0.2f, wall, floor, roof,
                doorOnMinusZ: true, windowOnPlusX: true);

            ShopCounter counter = BuildCounter(root.transform, new Vector3(3.6f, 0f, 2.6f), fixture, bagPrefab, report);
            BuildClerk(root.transform, new Vector3(3.6f, 0f, 3.6f), counter, report);
            BuildPriceTag(counter, report);
            BuildShelves(root.transform, items, counter, fixture, report);

            BuildInteriorLight(root.transform, new Vector3(0f, height - 0.5f, 0f), Color.white, 12f);
            AddLocation(root, "마트", LocationKind.Landmark, 10f);

            report.Add("· 마트를 세웠습니다. " + root.transform.position);
        }

        /// <summary>
        /// 계산대를 놓습니다. 봉투와 큰 물건이 놓일 자리도 함께 만듭니다.
        /// </summary>
        /// <param name="parent">마트의 루트</param>
        /// <param name="localPosition">마트 안에서의 자리</param>
        /// <param name="fixture">쓸 재질</param>
        /// <param name="bagPrefab">계산을 마쳤을 때 나올 봉투</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>만들어진 계산대</returns>
        private static ShopCounter BuildCounter(Transform parent, Vector3 localPosition, Material fixture,
                                                GameObject bagPrefab, List<string> report)
        {
            GameObject go = new GameObject("Counter");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            PbBox("Desk", go.transform, new Vector3(0f, 0.5f, 0f), new Vector3(2.6f, 1.0f, 0.8f), fixture);

            GameObject bagSpot = new GameObject("BagPlacement");
            bagSpot.transform.SetParent(go.transform, false);
            bagSpot.transform.localPosition = new Vector3(-0.7f, 1.2f, 0f);

            GameObject bulkySpot = new GameObject("BulkyPlacement");
            bulkySpot.transform.SetParent(go.transform, false);
            bulkySpot.transform.localPosition = new Vector3(1.9f, 0.3f, 0f);

            ShopCounter counter = go.AddComponent<ShopCounter>();
            counter.bagPlacement = bagSpot.transform;
            counter.bulkyPlacement = bulkySpot.transform;
            counter.bagPrefab = bagPrefab;

            report.Add("· 계산대를 놓았습니다.");
            return counter;
        }

        /// <summary>
        /// 점원을 세웁니다. 말을 걸면 계산합니다.
        /// </summary>
        /// <param name="parent">마트의 루트</param>
        /// <param name="localPosition">마트 안에서의 자리</param>
        /// <param name="counter">이 점원이 보는 계산대</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void BuildClerk(Transform parent, Vector3 localPosition, ShopCounter counter,
                                       List<string> report)
        {
            Material skin = ShopAssetsSetup.EnsureMaterial("ClerkBody", new Color(0.30f, 0.42f, 0.58f), report);

            GameObject go = new GameObject("Clerk");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            PbBox("Body", go.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.5f, 1.5f, 0.32f), skin);
            PbBox("Head", go.transform, new Vector3(0f, 1.66f, 0f), new Vector3(0.28f, 0.3f, 0.28f), skin);

            // 말을 걸 수 있게 사람 크기의 트리거를 덮습니다.
            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.center = new Vector3(0f, 0.9f, 0f);
            trigger.size = new Vector3(0.8f, 1.8f, 0.7f);
            trigger.isTrigger = true;

            ShopClerk clerk = go.AddComponent<ShopClerk>();
            clerk.counter = counter;

            ShopAssetsSetup.SetLayerDeep(go, InteractableLayer);
            report.Add("· 점원을 세웠습니다.");
        }

        /// <summary>
        /// 계산대 위에 가격표를 답니다. 고른 물건과 합계를 보여 줍니다.
        /// </summary>
        /// <param name="counter">가격표가 볼 계산대</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void BuildPriceTag(ShopCounter counter, List<string> report)
        {
            GameObject canvasObject = new GameObject("PriceTag", typeof(Canvas));
            canvasObject.transform.SetParent(counter.transform, false);
            canvasObject.transform.localPosition = new Vector3(0f, 1.75f, -0.45f);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400f, 260f);
            rect.localScale = Vector3.one * 0.0035f;   // 400px 가 약 1.4m 가 됩니다

            TMP_Text total = CreateLabel(rect, "Total", new Vector2(0f, -95f), new Vector2(380f, 60f), 44f,
                TextAlignmentOptions.Center);
            TMP_Text list = CreateLabel(rect, "Items", new Vector2(0f, 40f), new Vector2(380f, 170f), 24f,
                TextAlignmentOptions.TopLeft);

            ShopPriceTagUI tag = canvasObject.AddComponent<ShopPriceTagUI>();
            tag.counter = counter;
            tag.totalText = total;
            tag.itemListText = list;

            report.Add("· 가격표를 달았습니다.");
        }

        /// <summary>
        /// 진열대를 놓고 상품을 올립니다.
        /// </summary>
        /// <param name="parent">마트의 루트</param>
        /// <param name="items">진열할 상품 정의들</param>
        /// <param name="counter">고른 것을 담을 계산대</param>
        /// <param name="fixture">진열대 재질</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void BuildShelves(Transform parent, List<ShopItem> items, ShopCounter counter,
                                         Material fixture, List<string> report)
        {
            if (items == null || items.Count == 0)
            {
                report.Add("! 진열할 상품이 없습니다.");
                return;
            }

            GameObject shelves = new GameObject("Shelves");
            shelves.transform.SetParent(parent, false);

            // 봉투에 담기는 것은 선반 위에, 큰 것은 바닥에 그대로 놓습니다.
            List<ShopItem> small = new List<ShopItem>();
            List<ShopItem> bulky = new List<ShopItem>();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;
                (items[i].fitsInBag ? small : bulky).Add(items[i]);
            }

            const float shelfTop = 0.9f;
            Vector3 shelfCenter = new Vector3(-3.2f, 0f, 0f);

            PbBox("ShelfDesk", shelves.transform, shelfCenter + new Vector3(0f, shelfTop * 0.5f, 0f),
                new Vector3(1.0f, shelfTop, 5.2f), fixture);

            for (int i = 0; i < small.Count; i++)
            {
                float z = -2.0f + i * (4.0f / Mathf.Max(1, small.Count - 1));
                Vector3 spot = shelfCenter + new Vector3(0f, shelfTop + 0.12f, z);
                PlaceShelfItem(shelves.transform, small[i], spot, counter, report);
            }

            for (int i = 0; i < bulky.Count; i++)
            {
                Vector3 spot = new Vector3(0.5f + i * 1.4f, 0.3f, -3.0f);
                PlaceShelfItem(shelves.transform, bulky[i], spot, counter, report);
            }

            report.Add("· 진열장에 " + (small.Count + bulky.Count) + "종을 올렸습니다.");
        }

        /// <summary>
        /// 상품 하나를 진열합니다.
        ///
        /// <b>진열품은 먹을 수 없습니다.</b> 프리팹을 그대로 놓으면 집어서 그 자리에서 먹을 수
        /// 있게 되어 값을 치를 이유가 사라집니다. 그래서 먹고·들 수 있게 하는 부품을 떼고
        /// <see cref="ShopShelfItem"/>만 남깁니다. 보이는 것은 같고 할 수 있는 일만 다릅니다.
        /// </summary>
        /// <param name="parent">진열장</param>
        /// <param name="item">진열할 상품</param>
        /// <param name="localPosition">놓을 자리</param>
        /// <param name="counter">고른 것을 담을 계산대</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void PlaceShelfItem(Transform parent, ShopItem item, Vector3 localPosition,
                                           ShopCounter counter, List<string> report)
        {
            if (item.prefab == null)
            {
                report.Add("! " + item.displayName + " 에 프리팹이 없어 진열하지 못했습니다.");
                return;
            }

            GameObject display = (GameObject)PrefabUtility.InstantiatePrefab(item.prefab, parent);
            PrefabUtility.UnpackPrefabInstance(display, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            display.name = "Display_" + item.displayName;
            display.transform.localPosition = localPosition;

            StripComponent<ConsumableItem>(display);
            StripComponent<Carryable>(display);
            StripComponent<Rigidbody>(display);

            ShopShelfItem shelfItem = display.AddComponent<ShopShelfItem>();
            shelfItem.item = item;
            shelfItem.counter = counter;

            ShopAssetsSetup.SetLayerDeep(display, InteractableLayer);
        }

        // --- Private Methods : 건물 껍데기 ---

        /// <summary>
        /// 바닥·벽·천장·지붕으로 이루어진 건물 껍데기를 세웁니다.
        ///
        /// 벽은 <b>구멍을 낼 수 있게 조각으로</b> 짓습니다. 통짜 벽에서 구멍을 파는 것보다
        /// 조각을 배치하는 편이 결과가 예측 가능하고, 나중에 손으로 옮기기도 쉽습니다.
        /// </summary>
        /// <param name="parent">건물 루트</param>
        /// <param name="width">가로(X) 안쪽 치수</param>
        /// <param name="depth">세로(Z) 안쪽 치수</param>
        /// <param name="height">천장 높이</param>
        /// <param name="thickness">벽 두께</param>
        /// <param name="wall">벽 재질</param>
        /// <param name="floor">바닥 재질</param>
        /// <param name="roof">지붕 재질</param>
        /// <param name="doorOnMinusZ">-Z 벽에 문을 낼지 여부</param>
        /// <param name="windowOnPlusX">+X 벽에 창을 낼지 여부</param>
        private static void BuildShell(Transform parent, float width, float depth, float height, float thickness,
                                       Material wall, Material floor, Material roof,
                                       bool doorOnMinusZ, bool windowOnPlusX)
        {
            float halfW = width * 0.5f;
            float halfD = depth * 0.5f;
            float outerW = width + thickness * 2f;
            float outerD = depth + thickness * 2f;

            PbBox("Floor", parent, new Vector3(0f, -thickness * 0.5f, 0f),
                new Vector3(outerW, thickness, outerD), floor);

            PbBox("Ceiling", parent, new Vector3(0f, height + thickness * 0.5f, 0f),
                new Vector3(outerW, thickness, outerD), wall);

            // -Z 벽: 문
            BuildWallRun(parent, "Wall_S", alongX: true, offset: -(halfD + thickness * 0.5f),
                runLength: outerW, height: height, thickness: thickness, material: wall,
                hasOpening: doorOnMinusZ, openingCenter: 0f, openingWidth: 1.4f,
                openingBottom: 0f, openingTop: 2.1f);

            // +Z 벽: 통짜
            BuildWallRun(parent, "Wall_N", alongX: true, offset: halfD + thickness * 0.5f,
                runLength: outerW, height: height, thickness: thickness, material: wall,
                hasOpening: false, openingCenter: 0f, openingWidth: 0f,
                openingBottom: 0f, openingTop: 0f);

            // +X 벽: 창문
            BuildWallRun(parent, "Wall_E", alongX: false, offset: halfW + thickness * 0.5f,
                runLength: depth, height: height, thickness: thickness, material: wall,
                hasOpening: windowOnPlusX, openingCenter: 0f, openingWidth: 2.0f,
                openingBottom: 1.0f, openingTop: 2.1f);

            // -X 벽: 통짜
            BuildWallRun(parent, "Wall_W", alongX: false, offset: -(halfW + thickness * 0.5f),
                runLength: depth, height: height, thickness: thickness, material: wall,
                hasOpening: false, openingCenter: 0f, openingWidth: 0f,
                openingBottom: 0f, openingTop: 0f);

            // 지붕. 삼각기둥이라 비가 흘러내리는 모양이 납니다.
            ProBuilderMesh prism = ShapeGenerator.GeneratePrism(PivotLocation.Center,
                new Vector3(outerW + 0.4f, 1.4f, outerD + 0.4f));
            GameObject roofObject = prism.gameObject;
            roofObject.name = "Roof";
            roofObject.transform.SetParent(parent, false);
            roofObject.transform.localPosition = new Vector3(0f, height + thickness + 0.7f, 0f);
            roofObject.GetComponent<MeshRenderer>().sharedMaterial = roof;
            roofObject.AddComponent<MeshCollider>();
        }

        /// <summary>
        /// 벽 한 면을 짓습니다. 구멍이 있으면 좌·우·위·아래 조각으로 나눠 짓습니다.
        /// </summary>
        /// <param name="parent">건물 루트</param>
        /// <param name="name">벽 이름</param>
        /// <param name="alongX">벽이 X축을 따라 뻗는지 여부</param>
        /// <param name="offset">뻗지 않는 축에서의 위치</param>
        /// <param name="runLength">벽의 길이</param>
        /// <param name="height">벽 높이</param>
        /// <param name="thickness">벽 두께</param>
        /// <param name="material">벽 재질</param>
        /// <param name="hasOpening">구멍이 있는지 여부</param>
        /// <param name="openingCenter">구멍의 중심(벽 길이 방향)</param>
        /// <param name="openingWidth">구멍의 너비</param>
        /// <param name="openingBottom">구멍의 아래 높이. 0이면 문입니다</param>
        /// <param name="openingTop">구멍의 위 높이</param>
        private static void BuildWallRun(Transform parent, string name, bool alongX, float offset,
                                         float runLength, float height, float thickness, Material material,
                                         bool hasOpening, float openingCenter, float openingWidth,
                                         float openingBottom, float openingTop)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);

            if (!hasOpening)
            {
                PlaceWallPiece(group.transform, "Full", alongX, offset, 0f, runLength, height * 0.5f, height,
                    thickness, material);
                return;
            }

            float half = runLength * 0.5f;
            float openHalf = openingWidth * 0.5f;

            float leftLength = (openingCenter - openHalf) - (-half);
            float rightLength = half - (openingCenter + openHalf);

            if (leftLength > 0.01f)
            {
                float centre = -half + leftLength * 0.5f;
                PlaceWallPiece(group.transform, "Left", alongX, offset, centre, leftLength,
                    height * 0.5f, height, thickness, material);
            }

            if (rightLength > 0.01f)
            {
                float centre = half - rightLength * 0.5f;
                PlaceWallPiece(group.transform, "Right", alongX, offset, centre, rightLength,
                    height * 0.5f, height, thickness, material);
            }

            if (openingBottom > 0.01f)
            {
                PlaceWallPiece(group.transform, "Sill", alongX, offset, openingCenter, openingWidth,
                    openingBottom * 0.5f, openingBottom, thickness, material);
            }

            float lintel = height - openingTop;
            if (lintel > 0.01f)
            {
                PlaceWallPiece(group.transform, "Lintel", alongX, offset, openingCenter, openingWidth,
                    openingTop + lintel * 0.5f, lintel, thickness, material);
            }
        }

        /// <summary>
        /// 벽 조각 하나를 놓습니다.
        /// </summary>
        /// <param name="parent">벽 묶음</param>
        /// <param name="name">조각 이름</param>
        /// <param name="alongX">벽이 X축을 따라 뻗는지 여부</param>
        /// <param name="offset">뻗지 않는 축에서의 위치</param>
        /// <param name="along">뻗는 축에서의 중심</param>
        /// <param name="length">조각의 길이</param>
        /// <param name="centreY">조각의 높이 중심</param>
        /// <param name="pieceHeight">조각의 높이</param>
        /// <param name="thickness">벽 두께</param>
        /// <param name="material">벽 재질</param>
        private static void PlaceWallPiece(Transform parent, string name, bool alongX, float offset,
                                           float along, float length, float centreY, float pieceHeight,
                                           float thickness, Material material)
        {
            Vector3 centre = alongX
                ? new Vector3(along, centreY, offset)
                : new Vector3(offset, centreY, along);

            Vector3 size = alongX
                ? new Vector3(length, pieceHeight, thickness)
                : new Vector3(thickness, pieceHeight, length);

            PbBox(name, parent, centre, size, material);
        }

        // --- Private Methods : 도우미 ---

        /// <summary>
        /// ProBuilder 상자 하나를 만들어 붙입니다. 벽·바닥·가구가 모두 이것으로 지어집니다.
        /// </summary>
        /// <param name="name">오브젝트 이름</param>
        /// <param name="parent">붙일 부모</param>
        /// <param name="localCenter">부모 기준 중심</param>
        /// <param name="size">크기</param>
        /// <param name="material">재질</param>
        /// <returns>만들어진 오브젝트</returns>
        private static GameObject PbBox(string name, Transform parent, Vector3 localCenter, Vector3 size,
                                        Material material)
        {
            ProBuilderMesh mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            GameObject go = mesh.gameObject;
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;

            // 상자는 중심이 원점이고 크기가 그대로라, 박스 콜라이더가 정확히 맞습니다.
            // 메시 콜라이더보다 훨씬 싸고, 벽은 어차피 직육면체입니다.
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = size;

            return go;
        }

        /// <summary>
        /// 실내 조명을 답니다. 없으면 밤에 안이 아무것도 보이지 않습니다.
        /// </summary>
        /// <param name="parent">건물 루트</param>
        /// <param name="localPosition">달 자리</param>
        /// <param name="color">빛 색</param>
        /// <param name="range">닿는 거리</param>
        private static void BuildInteriorLight(Transform parent, Vector3 localPosition, Color color, float range)
        {
            GameObject go = new GameObject("InteriorLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = 1.4f;
            light.shadows = LightShadows.None;   // 픽셀 룩이라 그림자까지 켤 이유가 적습니다
        }

        /// <summary>글자 하나를 만듭니다.</summary>
        /// <param name="parent">붙일 캔버스</param>
        /// <param name="name">오브젝트 이름</param>
        /// <param name="anchoredPosition">캔버스 안에서의 자리</param>
        /// <param name="size">글자 상자 크기</param>
        /// <param name="fontSize">글자 크기</param>
        /// <param name="alignment">정렬</param>
        /// <returns>만들어진 글자</returns>
        private static TMP_Text CreateLabel(RectTransform parent, string name, Vector2 anchoredPosition,
                                            Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = "";

            return text;
        }

        /// <summary>이 오브젝트에서 해당 컴포넌트를 모두 떼어 냅니다.</summary>
        /// <typeparam name="T">뗄 컴포넌트</typeparam>
        /// <param name="go">대상 오브젝트</param>
        private static void StripComponent<T>(GameObject go) where T : Component
        {
            T[] found = go.GetComponentsInChildren<T>(true);
            for (int i = 0; i < found.Length; i++) Object.DestroyImmediate(found[i]);
        }

        /// <summary>건물을 이름 있는 장소로 등록합니다. 앞으로의 의뢰·안내에 쓰입니다.</summary>
        /// <param name="root">건물 루트</param>
        /// <param name="displayName">장소 이름</param>
        /// <param name="kind">장소 종류</param>
        /// <param name="radius">장소 반경</param>
        private static void AddLocation(GameObject root, string displayName, LocationKind kind, float radius)
        {
            WorldLocation location = root.AddComponent<WorldLocation>();
            location.displayName = displayName;
            location.kind = kind;
            location.radius = radius;
        }

        /// <summary>
        /// 마을 중심을 찾습니다. <see cref="WorldStreamer"/>가 알고 있습니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>마을 중심 위치</returns>
        private static Vector3 ResolveVillageCenter(List<string> report)
        {
            WorldStreamer streamer = Object.FindAnyObjectByType<WorldStreamer>(FindObjectsInactive.Include);
            if (streamer == null)
            {
                report.Add("! WorldStreamer 를 찾지 못해 원점을 기준으로 세웁니다.");
                return Vector3.zero;
            }

            Vector3 center = streamer.origin != null ? streamer.origin.position : streamer.transform.position;
            report.Add("· 마을 중심: " + center);
            return center;
        }

        /// <summary>
        /// 이 위치의 지면 높이를 찾아 붙여 줍니다.
        ///
        /// 하늘에서 아래로 쏘아 맞은 자리를 씁니다. 못 맞히면 지형에게 직접 물어봅니다.
        /// </summary>
        /// <param name="position">지면에 붙일 위치</param>
        /// <returns>지면에 붙은 위치</returns>
        private static Vector3 Ground(Vector3 position)
        {
            Vector3 from = new Vector3(position.x, position.y + 500f, position.z);

            RaycastHit hit;
            if (Physics.Raycast(from, Vector3.down, out hit, 2000f))
            {
                return new Vector3(position.x, hit.point.y, position.z);
            }

            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                return new Vector3(position.x, terrain.SampleHeight(position) + terrain.transform.position.y, position.z);
            }

            return position;
        }
    }
}
