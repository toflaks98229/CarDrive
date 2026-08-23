using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CarDrive.Gameplay;
using CarDrive.Systems;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 마트에서 파는 물건들 — 프리팹과 <see cref="ShopItem"/> 에셋 — 을 만듭니다.
    ///
    /// <b>왜 도구로 만드는가.</b> 이 프로젝트는 씬과 프리팹을 손으로 배선하지 않고
    /// 에디터 도구가 짓습니다(<c>BeverageSetup</c>·<c>EconomySetup</c>과 같은 방식).
    /// 그래야 무엇이 어떤 값으로 만들어졌는지가 <b>코드에 남아</b> 나중에 다시 볼 수 있고,
    /// 값을 바꾸고 다시 실행하면 같은 결과가 나옵니다.
    ///
    /// <b>이미 있는 것은 건드리지 않습니다.</b> 손으로 고쳐 둔 값을 도구가 덮어쓰면
    /// 다시 실행하는 것이 무서워집니다. 없는 것만 만듭니다.
    /// </summary>
    public static class ShopAssetsSetup
    {
        // --- Constants ---

        private const string InteractableLayer = "Interactable";

        private const string PrefabFolder = "Assets/_Project/05.Prefabs/Shop";
        private const string ItemFolder = "Assets/_Project/03.DataAssets/Shop";
        private const string MaterialFolder = "Assets/_Project/04.Art/00.Materials";

        private const string BeerCasePrefabPath = "Assets/_Project/05.Prefabs/Items/bottle_case.prefab";

        // --- Public Methods ---

        /// <summary>
        /// 마트 상품 프리팹과 정의 에셋을 만듭니다.
        /// </summary>
        [MenuItem("CarDrive/Gameplay/마트 상품 만들기")]
        public static void Setup()
        {
            List<string> report = new List<string>();
            Build(report);

            Debug.Log("ShopAssetsSetup:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 상품을 만들고 그 정의들을 돌려줍니다. 마을 시설을 세우는 도구가 이것을 씁니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>만들어졌거나 이미 있던 상품 정의들</returns>
        public static List<ShopItem> Build(List<string> report)
        {
            EnsureFolder(PrefabFolder, report);
            EnsureFolder(ItemFolder, report);

            List<ShopItem> items = new List<ShopItem>();

            // 봉투에 담기는 것들.
            items.Add(BuildFood(report, "빵", 1200, new Color(0.78f, 0.60f, 0.34f),
                new Vector3(0.30f, 0.15f, 0.17f), PrimitiveType.Cube,
                hunger: 0.8f, thirst: -0.10f, heal: 5f, seconds: 2.4f));

            items.Add(BuildFood(report, "통조림", 900, new Color(0.68f, 0.70f, 0.74f),
                new Vector3(0.09f, 0.06f, 0.09f), PrimitiveType.Cylinder,
                hunger: 0.5f, thirst: -0.20f, heal: 2f, seconds: 2.0f));

            items.Add(BuildFood(report, "초코바", 500, new Color(0.32f, 0.20f, 0.13f),
                new Vector3(0.17f, 0.035f, 0.07f), PrimitiveType.Cube,
                hunger: 0.25f, thirst: -0.05f, heal: 0f, seconds: 1.2f));

            items.Add(BuildDrink(report, "생수", 700, new Color(0.62f, 0.82f, 0.92f),
                new Vector3(0.075f, 0.12f, 0.075f),
                thirst: 0.55f, urine: -0.30f, seconds: 1.6f));

            // 봉투에 담기지 않는 큰 것.
            items.Add(BuildBeerCase(report));

            items.RemoveAll(x => x == null);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return items;
        }

        /// <summary>
        /// 계산을 마쳤을 때 나올 봉투 프리팹을 만듭니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>봉투 프리팹</returns>
        public static GameObject BuildBagPrefab(List<string> report)
        {
            string path = PrefabFolder + "/ShoppingBag.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                report.Add("· 봉투: 이미 있습니다.");
                return existing;
            }

            EnsureFolder(PrefabFolder, report);

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "ShoppingBag";
            root.transform.localScale = new Vector3(0.26f, 0.34f, 0.18f);
            SetLayerDeep(root, InteractableLayer);

            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = EnsureMaterial("ShopPaperBag", new Color(0.76f, 0.66f, 0.48f), report);

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 1.5f;

            // 물건이 나올 입구. 봉투 위쪽 살짝 앞입니다.
            GameObject mouth = new GameObject("Mouth");
            mouth.transform.SetParent(root.transform, false);
            // 부모가 눌린 정육면체라 로컬 좌표가 그만큼 늘어납니다. 월드 기준 0.25m 위가 되도록 나눕니다.
            mouth.transform.localPosition = new Vector3(0f, 0.25f / 0.34f, 0f);

            ShoppingBag bag = root.AddComponent<ShoppingBag>();
            bag.mouth = mouth.transform;

            root.AddComponent<Carryable>().displayName = "장바구니";

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            report.Add("· 봉투를 만들었습니다: " + path);
            return prefab;
        }

        // --- Private Methods : 상품 ---

        /// <summary>
        /// 먹는 물건 하나를 만듭니다. 프리팹과 정의 에셋이 함께 생깁니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <param name="displayName">물건 이름</param>
        /// <param name="price">값</param>
        /// <param name="tint">색</param>
        /// <param name="size">크기(m)</param>
        /// <param name="shape">쓸 기본 도형</param>
        /// <param name="hunger">덜어 줄 허기</param>
        /// <param name="thirst">갈증 변화. 음수면 오히려 목이 마릅니다</param>
        /// <param name="heal">회복시킬 체력</param>
        /// <param name="seconds">먹는 데 걸리는 시간</param>
        /// <returns>만들어진 정의</returns>
        private static ShopItem BuildFood(List<string> report, string displayName, int price,
                                          Color tint, Vector3 size, PrimitiveType shape,
                                          float hunger, float thirst, float heal, float seconds)
        {
            GameObject prefab = EnsureItemPrefab(report, displayName, tint, size, shape, body =>
            {
                Food food = body.AddComponent<Food>();
                food.healAmount = heal;
                food.consumeSeconds = seconds;
                food.effects = new List<NeedEffect>
                {
                    new NeedEffect { type = NeedType.Hunger, relief = hunger },
                    new NeedEffect { type = NeedType.Thirst, relief = thirst }
                };
            });

            return EnsureShopItem(report, displayName, price, prefab, fitsInBag: true);
        }

        /// <summary>
        /// 마시는 물건 하나를 만듭니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <param name="displayName">물건 이름</param>
        /// <param name="price">값</param>
        /// <param name="tint">색</param>
        /// <param name="size">크기(m)</param>
        /// <param name="thirst">덜어 줄 갈증</param>
        /// <param name="urine">배뇨 변화. 음수면 차오릅니다</param>
        /// <param name="seconds">마시는 데 걸리는 시간</param>
        /// <returns>만들어진 정의</returns>
        private static ShopItem BuildDrink(List<string> report, string displayName, int price,
                                           Color tint, Vector3 size,
                                           float thirst, float urine, float seconds)
        {
            GameObject prefab = EnsureItemPrefab(report, displayName, tint, size, PrimitiveType.Cylinder, body =>
            {
                Beverage drink = body.AddComponent<Beverage>();
                drink.consumeSeconds = seconds;
                drink.effects = new List<NeedEffect>
                {
                    new NeedEffect { type = NeedType.Thirst, relief = thirst },
                    new NeedEffect { type = NeedType.Urine, relief = urine }
                };
            });

            return EnsureShopItem(report, displayName, price, prefab, fitsInBag: true);
        }

        /// <summary>
        /// 맥주 상자를 파는 물건으로 등록합니다. <b>봉투에 담기지 않습니다.</b>
        ///
        /// 프리팹은 이미 있는 <c>bottle_case</c> 를 그대로 씁니다. 마트에서 산 상자와
        /// 원래 있던 상자가 다르면 곤란하기 때문입니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>만들어진 정의. 프리팹이 없으면 null</returns>
        private static ShopItem BuildBeerCase(List<string> report)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BeerCasePrefabPath);
            if (prefab == null)
            {
                report.Add("! 맥주 상자 프리팹을 찾지 못했습니다: " + BeerCasePrefabPath);
                return null;
            }

            return EnsureShopItem(report, "맥주 상자", 12000, prefab, fitsInBag: false);
        }

        // --- Private Methods : 만들기 도우미 ---

        /// <summary>
        /// 손에 잡히는 물건 프리팹을 만듭니다. 이미 있으면 그것을 그대로 씁니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <param name="displayName">물건 이름</param>
        /// <param name="tint">색</param>
        /// <param name="size">크기(m)</param>
        /// <param name="shape">쓸 기본 도형</param>
        /// <param name="addBehaviour">먹거나 마시는 컴포넌트를 붙이는 일</param>
        /// <returns>프리팹</returns>
        private static GameObject EnsureItemPrefab(List<string> report, string displayName, Color tint,
                                                   Vector3 size, PrimitiveType shape,
                                                   System.Action<GameObject> addBehaviour)
        {
            string path = PrefabFolder + "/" + displayName + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                report.Add("· " + displayName + ": 프리팹이 이미 있습니다.");
                return existing;
            }

            GameObject root = GameObject.CreatePrimitive(shape);
            root.name = displayName;

            // 원기둥은 기본 높이가 2m 라, 넘긴 높이를 그대로 쓰려면 절반으로 줄입니다.
            root.transform.localScale = shape == PrimitiveType.Cylinder
                ? new Vector3(size.x, size.y * 0.5f, size.z)
                : size;

            SetLayerDeep(root, InteractableLayer);

            root.GetComponent<MeshRenderer>().sharedMaterial =
                EnsureMaterial("Shop_" + displayName, tint, report);

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 0.4f;

            addBehaviour(root);

            root.AddComponent<Carryable>().displayName = displayName;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            report.Add("· " + displayName + " 프리팹을 만들었습니다: " + path);
            return prefab;
        }

        /// <summary>
        /// 상품 정의 에셋을 만듭니다. 이미 있으면 그것을 그대로 씁니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <param name="displayName">물건 이름</param>
        /// <param name="price">값</param>
        /// <param name="prefab">꺼낼 때 만들어질 프리팹</param>
        /// <param name="fitsInBag">봉투에 담기는지 여부</param>
        /// <returns>정의 에셋</returns>
        private static ShopItem EnsureShopItem(List<string> report, string displayName, int price,
                                               GameObject prefab, bool fitsInBag)
        {
            string path = ItemFolder + "/" + displayName + ".asset";
            ShopItem existing = AssetDatabase.LoadAssetAtPath<ShopItem>(path);
            if (existing != null)
            {
                report.Add("· " + displayName + ": 상품 정의가 이미 있습니다.");
                return existing;
            }

            ShopItem item = ScriptableObject.CreateInstance<ShopItem>();
            item.displayName = displayName;
            item.price = price;
            item.prefab = prefab;
            item.fitsInBag = fitsInBag;

            AssetDatabase.CreateAsset(item, path);
            report.Add("· " + displayName + " 상품 정의를 만들었습니다. (" + price + "원" +
                       (fitsInBag ? "" : " · 봉투에 담기지 않음") + ")");
            return item;
        }

        /// <summary>
        /// 단색 재질을 만듭니다. 이미 있으면 그것을 그대로 씁니다.
        /// </summary>
        /// <param name="assetName">재질 이름</param>
        /// <param name="tint">색</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>재질</returns>
        public static Material EnsureMaterial(string assetName, Color tint, List<string> report)
        {
            string path = MaterialFolder + "/" + assetName + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material material = new Material(shader);
            material.SetColor("_BaseColor", tint);
            material.SetColor("_Color", tint);

            AssetDatabase.CreateAsset(material, path);
            report.Add("· 재질을 만들었습니다: " + assetName);
            return material;
        }

        /// <summary>
        /// 이 오브젝트와 자식 전부의 레이어를 바꿉니다.
        /// 조준 레이캐스트가 잡으려면 레이어가 맞아야 합니다.
        /// </summary>
        /// <param name="go">바꿀 오브젝트</param>
        /// <param name="layerName">레이어 이름</param>
        public static void SetLayerDeep(GameObject go, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0) return;

            Transform[] all = go.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++) all[i].gameObject.layer = layer;
        }

        /// <summary>
        /// 폴더가 없으면 만듭니다.
        /// </summary>
        /// <param name="folder">만들 폴더 경로</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        public static void EnsureFolder(string folder, List<string> report)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);

            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent, report);

            AssetDatabase.CreateFolder(parent, leaf);
            report.Add("· 폴더를 만들었습니다: " + folder);
        }
    }
}
