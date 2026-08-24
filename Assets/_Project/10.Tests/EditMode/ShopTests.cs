using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CarDrive.Gameplay;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 마트의 장바구니·계산·봉투 규칙에 대한 EditMode 테스트입니다.
    ///
    /// <b>돈이 오가는 곳이라 규칙이 어긋나면 조용히 손해가 납니다.</b> 값을 치렀는데 봉투가
    /// 비어 있거나, 모자란데도 물건이 나오면 플레이어는 그것을 버그로 인식하기 전에
    /// 게임을 불신하게 됩니다. 그래서 다음 여섯을 못박습니다.
    ///  1. 담고 무르는 것이 합계에 정확히 반영된다
    ///  2. <b>진열대의 실물과 장바구니가 어긋나지 않는다</b> — 담으면 앞에서부터 사라지고,
    ///     무르면 마지막 것부터 돌아오고, <b>판 것은 돌아오지 않는다</b>
    ///  3. 돈이 모자라면 <b>아무것도 빠져나가지 않는다</b>
    ///  4. <b>내줄 수 없으면 값을 받지 않는다</b> — 그리고 내줄 수 있으면 과잉 거절하지 않는다
    ///  5. 봉투에 안 들어가는 큰 물건은 봉투가 아니라 계산대에 놓인다
    ///  6. 봉투는 <b>마지막에 산 것부터</b> 꺼낸다
    ///
    /// 4번이 이 파일에서 가장 중요합니다. 나머지는 어긋나면 눈에 보이지만,
    /// 4번은 <b>값을 치른 뒤에 조용히</b> 어긋나기 때문입니다.
    /// 2번의 마지막 항목이 그다음입니다 — 판 물건이 진열대로 돌아오면 복제가 됩니다.
    /// </summary>
    public class ShopTests
    {
        /// <summary>테스트가 만든 것들을 한꺼번에 치우기 위한 표식입니다.</summary>
        private sealed class SpawnedByTest : MonoBehaviour { }

        private GameObject root;
        private ShopCounter counter;
        private Wallet wallet;
        private GameObject bagPrefab;
        private GameObject itemPrefab;
        private readonly List<ShopItem> createdItems = new List<ShopItem>();

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ShopRoot");
            root.AddComponent<SpawnedByTest>();

            wallet = root.AddComponent<Wallet>();
            wallet.EnsureInitialized();

            counter = root.AddComponent<ShopCounter>();
            counter.Construct(wallet);

            // 꺼낸 물건과 봉투가 만들어질 때 쓸 프리팹입니다.
            // 씬 오브젝트를 그대로 Instantiate 해도 프리팹처럼 동작합니다.
            itemPrefab = new GameObject("ItemPrefab");
            itemPrefab.AddComponent<SpawnedByTest>();

            bagPrefab = new GameObject("BagPrefab");
            bagPrefab.AddComponent<SpawnedByTest>();
            ShoppingBag bagTemplate = bagPrefab.AddComponent<ShoppingBag>();
            bagTemplate.destroyWhenEmpty = false;   // EditMode 에서는 Destroy 가 즉시 돌지 않습니다
            bagTemplate.popSpeed = 0f;

            counter.bagPrefab = bagPrefab;
        }

        [TearDown]
        public void TearDown()
        {
            SpawnedByTest[] spawned = Object.FindObjectsByType<SpawnedByTest>(FindObjectsInactive.Include);
            for (int i = 0; i < spawned.Length; i++)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i].gameObject);
            }

            for (int i = 0; i < createdItems.Count; i++)
            {
                if (createdItems[i] != null) Object.DestroyImmediate(createdItems[i]);
            }
            createdItems.Clear();

            root = null; counter = null; wallet = null; bagPrefab = null; itemPrefab = null;
        }

        // --- 장바구니 ---

        /// <summary>담으면 합계가 오르고, 계산대에서 무르면 내려가야 합니다.</summary>
        [Test]
        public void 담고_무르면_합계가_따라간다()
        {
            ShopShelfItem bread = BuildShelfItem("빵", 1200);
            ShopShelfItem milk = BuildShelfItem("우유", 800);

            Assert.AreEqual(0, counter.TotalPrice, "처음에는 비어 있어야 합니다.");

            Take(bread);
            Assert.AreEqual(1200, counter.TotalPrice);

            Take(milk);
            Assert.AreEqual(2000, counter.TotalPrice);
            Assert.AreEqual(2, counter.SelectedCount);

            // 무르는 것은 진열대가 아니라 계산대에서 합니다. 마지막에 담은 것부터입니다.
            Assert.IsTrue(counter.ReturnLast(), "담은 것이 있으므로 무를 수 있어야 합니다.");
            Assert.AreEqual(1200, counter.TotalPrice, "마지막에 담은 우유 값이 빠져야 합니다.");
            Assert.AreEqual(1, counter.SelectedCount);
        }

        /// <summary>
        /// <b>같은 칸에서 여러 개 담을 수 있어야 합니다.</b>
        /// 항목 하나가 물건 한 개이므로, 세 번 담으면 값도 세 배여야 합니다.
        /// </summary>
        [Test]
        public void 같은_칸에서_여러_개_담을_수_있다()
        {
            ShopShelfItem bread = BuildShelfItem("빵", 1200, stock: 3);

            Take(bread);
            Take(bread);
            Take(bread);

            Assert.AreEqual(3, counter.SelectedCount, "세 개가 담겨야 합니다.");
            Assert.AreEqual(3600, counter.TotalPrice, "값도 세 배여야 합니다.");
        }

        // --- 진열 ---

        /// <summary>
        /// 담을 때마다 진열된 실물이 <b>앞에서부터</b> 하나씩 사라져야 합니다.
        /// 진열대가 줄어드는 것이 곧 장바구니가 차는 것입니다.
        /// </summary>
        [Test]
        public void 담으면_앞에_있는_실물부터_사라진다()
        {
            ShopShelfItem bread = BuildShelfItem("빵", 1200, stock: 3);
            List<GameObject> shown = bread.displays;

            Assert.AreEqual(3, bread.RemainingCount, "처음에는 셋이 놓여 있어야 합니다.");

            Take(bread);
            Assert.AreEqual(2, bread.RemainingCount);
            Assert.IsFalse(shown[0].activeSelf, "앞에 있는 것이 사라져야 합니다.");
            Assert.IsTrue(shown[1].activeSelf, "뒤엣것은 남아 있어야 합니다.");

            Take(bread);
            Assert.AreEqual(1, bread.RemainingCount);
            Assert.IsFalse(shown[1].activeSelf, "그다음 것이 사라져야 합니다.");
            Assert.IsTrue(shown[2].activeSelf);
        }

        /// <summary>진열이 비면 더 담을 수 없어야 합니다.</summary>
        [Test]
        public void 진열이_비면_더_담을_수_없다()
        {
            ShopShelfItem bread = BuildShelfItem("빵", 1200, stock: 2);

            Take(bread);
            Take(bread);

            Assert.AreEqual(0, bread.RemainingCount);

            // 거절도 안내합니다. 조준했는데 아무 문구가 없으면 다 팔린 것인지
            // 고장인지 구분되지 않습니다. (VehicleDoorInteractable 과 같은 규칙)
            Assert.AreEqual(bread.soldOutLabel, bread.GetInteractionLabel(),
                "다 팔렸다는 것을 알려 주어야 합니다.");

            // 그래도 눌렀다면 아무 일도 일어나지 않아야 합니다.
            Take(bread);
            Assert.AreEqual(2, counter.SelectedCount, "장바구니가 늘어나면 안 됩니다.");
        }

        /// <summary>무르면 실물이 진열대로 돌아와야 합니다.</summary>
        [Test]
        public void 무르면_실물이_진열대로_돌아온다()
        {
            ShopShelfItem bread = BuildShelfItem("빵", 1200, stock: 2);
            List<GameObject> shown = bread.displays;

            Take(bread);
            Take(bread);
            Assert.AreEqual(0, bread.RemainingCount);

            counter.ReturnLast();

            Assert.AreEqual(1, bread.RemainingCount, "하나가 진열대로 돌아와야 합니다.");
            Assert.IsTrue(shown[1].activeSelf, "마지막에 사라진 것이 먼저 돌아와야 합니다.");
            Assert.IsFalse(shown[0].activeSelf, "앞엣것은 아직 담겨 있어야 합니다.");
        }

        /// <summary>
        /// <b>계산을 마치면 되돌아오지 않아야 합니다.</b> 팔린 물건이기 때문입니다.
        /// 여기가 어긋나면 사고 나서도 진열대가 도로 가득 차 물건이 복제됩니다.
        /// </summary>
        [Test]
        public void 계산을_마치면_진열대로_돌아오지_않는다()
        {
            wallet.Add(CurrencyType.Money, 50000);

            ShopShelfItem bread = BuildShelfItem("빵", 1200, stock: 3);
            Take(bread);
            Take(bread);

            Assert.IsTrue(counter.TryCheckout());

            Assert.AreEqual(1, bread.RemainingCount, "판 것은 진열대로 돌아오지 않아야 합니다.");
        }

        // --- 계산 ---

        /// <summary>값을 치르면 지갑에서 그만큼 빠지고 장바구니가 비어야 합니다.</summary>
        [Test]
        public void 계산하면_값이_빠지고_장바구니가_비워진다()
        {
            wallet.Add(CurrencyType.Money, 5000);
            Take(BuildShelfItem("빵", 1200));
            Take(BuildShelfItem("우유", 800));

            Assert.IsTrue(counter.TryCheckout(), "돈이 넉넉하므로 계산되어야 합니다.");

            Assert.AreEqual(3000, wallet.Get(CurrencyType.Money), "합계만큼 빠져야 합니다.");
            Assert.AreEqual(0, counter.SelectedCount, "계산을 마치면 장바구니가 비어야 합니다.");
        }

        /// <summary>
        /// 돈이 모자라면 <b>아무것도 일어나지 않아야</b> 합니다.
        /// 값만 빠지고 물건이 안 나오는 것이 가장 나쁜 결말입니다.
        /// </summary>
        [Test]
        public void 돈이_모자라면_아무것도_빠지지_않는다()
        {
            wallet.Add(CurrencyType.Money, 500);
            Take(BuildShelfItem("빵", 1200));

            Assert.IsFalse(counter.TryCheckout(), "모자라므로 거절되어야 합니다.");

            Assert.AreEqual(500, wallet.Get(CurrencyType.Money), "돈이 그대로 남아야 합니다.");
            Assert.AreEqual(1, counter.SelectedCount, "장바구니도 그대로 남아야 합니다.");
            Assert.IsNull(FindBag(), "봉투가 만들어지면 안 됩니다.");
        }

        /// <summary>빈 장바구니로는 계산되지 않아야 합니다.</summary>
        [Test]
        public void 빈_장바구니는_계산되지_않는다()
        {
            wallet.Add(CurrencyType.Money, 5000);

            Assert.IsFalse(counter.TryCheckout());
            Assert.AreEqual(5000, wallet.Get(CurrencyType.Money));
        }

        /// <summary>
        /// <b>봉투 프리팹이 없으면 값을 받지 말아야 합니다.</b>
        ///
        /// 예전에는 돈을 먼저 빼고 봉투를 만들다 실패하면 경고만 남겼습니다. 그래서
        /// <b>돈은 나갔는데 물건은 없는</b> 상태가 되었고, 세이브에 소지품이 없어
        /// 불러오기로도 되돌릴 수 없었습니다. 이 테스트가 그 순서를 못박습니다.
        /// </summary>
        [Test]
        public void 봉투_프리팹이_없으면_값을_받지_않는다()
        {
            LogAssert.Expect(LogType.Error, new Regex("봉투 프리팹이 연결되지 않아"));

            counter.bagPrefab = null;

            wallet.Add(CurrencyType.Money, 5000);
            Take(BuildShelfItem("빵", 1200));

            Assert.IsFalse(counter.TryCheckout(), "내줄 수 없으므로 거절되어야 합니다.");

            Assert.AreEqual(5000, wallet.Get(CurrencyType.Money), "돈이 그대로 남아야 합니다.");
            Assert.AreEqual(1, counter.SelectedCount, "장바구니도 그대로 남아야 합니다.");
            Assert.IsNull(FindBag(), "봉투가 만들어지면 안 됩니다.");
        }

        /// <summary>
        /// 봉투 프리팹은 있는데 <see cref="ShoppingBag"/> 이 붙어 있지 않은 경우입니다.
        /// 담을 그릇이 없다는 점에서 프리팹이 아예 없는 것과 결과가 같으므로, 똑같이 막습니다.
        /// </summary>
        [Test]
        public void 봉투_프리팹에_ShoppingBag_이_없으면_값을_받지_않는다()
        {
            LogAssert.Expect(LogType.Error, new Regex("ShoppingBag 이 없어"));

            GameObject brokenBag = new GameObject("BrokenBagPrefab");
            brokenBag.AddComponent<SpawnedByTest>();
            counter.bagPrefab = brokenBag;

            wallet.Add(CurrencyType.Money, 5000);
            Take(BuildShelfItem("빵", 1200));

            Assert.IsFalse(counter.TryCheckout(), "담을 그릇이 없으므로 거절되어야 합니다.");

            Assert.AreEqual(5000, wallet.Get(CurrencyType.Money), "돈이 그대로 남아야 합니다.");
            Assert.AreEqual(1, counter.SelectedCount, "장바구니도 그대로 남아야 합니다.");
        }

        /// <summary>
        /// <b>과잉 거절도 버그입니다.</b> 봉투가 없어도 큰 물건은 계산대에 그대로 놓이므로
        /// 살 수 있어야 합니다. 확인이 "봉투가 필요한 장바구니"에만 걸리는지 못박습니다.
        /// </summary>
        [Test]
        public void 봉투가_없어도_큰_물건은_살_수_있다()
        {
            counter.bagPrefab = null;

            wallet.Add(CurrencyType.Money, 50000);
            Take(BuildShelfItem("맥주 상자", 12000, fitsInBag: false));

            Assert.IsTrue(counter.TryCheckout(), "봉투가 필요 없는 장바구니는 통과해야 합니다.");

            Assert.AreEqual(38000, wallet.Get(CurrencyType.Money), "값만큼 빠져야 합니다.");
            Assert.AreEqual(0, counter.SelectedCount, "계산을 마치면 장바구니가 비어야 합니다.");
        }

        // --- 봉투 ---

        /// <summary>봉투에는 담을 수 있는 것만 들어가야 합니다.</summary>
        [Test]
        public void 큰_물건은_봉투에_담기지_않는다()
        {
            wallet.Add(CurrencyType.Money, 50000);

            Take(BuildShelfItem("빵", 1200));
            Take(BuildShelfItem("맥주 상자", 12000, fitsInBag: false));

            Assert.IsTrue(counter.TryCheckout());

            ShoppingBag bag = FindBag();
            Assert.IsNotNull(bag, "담을 수 있는 것이 있으므로 봉투가 나와야 합니다.");
            Assert.AreEqual(1, bag.RemainingCount, "봉투에는 빵 하나만 들어가야 합니다.");
            Assert.AreEqual("빵", bag.Next.displayName);
        }

        /// <summary>담을 수 있는 것이 하나도 없으면 봉투를 만들지 않아야 합니다.</summary>
        [Test]
        public void 큰_물건만_사면_봉투가_나오지_않는다()
        {
            wallet.Add(CurrencyType.Money, 50000);
            Take(BuildShelfItem("맥주 상자", 12000, fitsInBag: false));

            Assert.IsTrue(counter.TryCheckout());

            Assert.IsNull(FindBag(), "담을 것이 없으면 빈 봉투를 만들 이유가 없습니다.");
        }

        /// <summary>봉투는 마지막에 담은 것부터 꺼내야 합니다.</summary>
        [Test]
        public void 봉투는_마지막에_산_것부터_꺼낸다()
        {
            wallet.Add(CurrencyType.Money, 50000);

            Take(BuildShelfItem("빵", 1000));
            Take(BuildShelfItem("우유", 1000));
            Take(BuildShelfItem("사탕", 1000));

            Assert.IsTrue(counter.TryCheckout());

            ShoppingBag bag = FindBag();
            Assert.IsNotNull(bag);
            Assert.AreEqual(3, bag.RemainingCount);

            Assert.AreEqual("사탕", bag.Next.displayName, "마지막에 담은 것이 먼저 나와야 합니다.");
            bag.Interact();

            Assert.AreEqual("우유", bag.Next.displayName);
            bag.Interact();

            Assert.AreEqual("빵", bag.Next.displayName);
            bag.Interact();

            Assert.AreEqual(0, bag.RemainingCount, "다 꺼내면 비어야 합니다.");
            Assert.IsFalse(bag.CanInteract(), "빈 봉투는 더 꺼낼 수 없어야 합니다.");
        }

        // --- Helpers ---

        /// <summary>진열대에서 하나 담습니다. 실제 조작과 같은 길을 지납니다.</summary>
        /// <param name="shelf">담아 올 진열 칸</param>
        private static void Take(ShopShelfItem shelf)
        {
            if (shelf != null) shelf.Interact();
        }

        /// <summary>진열 칸 하나를 만들어 계산대에 이어 둡니다.</summary>
        /// <param name="displayName">물건 이름</param>
        /// <param name="price">값</param>
        /// <param name="fitsInBag">봉투에 담기는지 여부</param>
        /// <param name="stock">진열해 둘 실물의 수</param>
        /// <returns>만들어진 진열 칸</returns>
        private ShopShelfItem BuildShelfItem(string displayName, int price, bool fitsInBag = true, int stock = 1)
        {
            ShopItem definition = ScriptableObject.CreateInstance<ShopItem>();
            definition.displayName = displayName;
            definition.price = price;
            definition.fitsInBag = fitsInBag;
            definition.prefab = itemPrefab;
            createdItems.Add(definition);

            GameObject go = new GameObject("Shelf_" + displayName);
            go.AddComponent<SpawnedByTest>();
            go.transform.SetParent(root.transform, false);

            ShopShelfItem shelf = go.AddComponent<ShopShelfItem>();
            shelf.item = definition;

            // EditMode 에서는 Awake·Start 가 돌지 않으므로 직접 잇고 직접 채웁니다.
            shelf.counter = counter;
            for (int i = 0; i < stock; i++)
            {
                GameObject display = new GameObject(displayName + "_" + i);
                display.AddComponent<SpawnedByTest>();
                display.transform.SetParent(go.transform, false);
                shelf.displays.Add(display);
            }

            return shelf;
        }

        /// <summary>계산으로 만들어진 봉투를 찾습니다. 없으면 null입니다.</summary>
        /// <returns>봉투. 프리팹 원본은 제외합니다.</returns>
        private ShoppingBag FindBag()
        {
            ShoppingBag[] bags = Object.FindObjectsByType<ShoppingBag>(FindObjectsInactive.Include);
            for (int i = 0; i < bags.Length; i++)
            {
                // 프리팹 원본이 아니라 계산으로 만들어진 것만 봅니다.
                if (bags[i] != null && bags[i].gameObject != bagPrefab) return bags[i];
            }
            return null;
        }
    }
}
