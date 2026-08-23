using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CarDrive.Gameplay;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 마트의 장바구니·계산·봉투 규칙에 대한 EditMode 테스트입니다.
    ///
    /// <b>돈이 오가는 곳이라 규칙이 어긋나면 조용히 손해가 납니다.</b> 값을 치렀는데 봉투가
    /// 비어 있거나, 모자란데도 물건이 나오면 플레이어는 그것을 버그로 인식하기 전에
    /// 게임을 불신하게 됩니다. 그래서 다음 넷을 못박습니다.
    ///  1. 고르고 무르는 것이 합계에 정확히 반영된다
    ///  2. 돈이 모자라면 <b>아무것도 빠져나가지 않는다</b>
    ///  3. 봉투에 안 들어가는 큰 물건은 봉투가 아니라 계산대에 놓인다
    ///  4. 봉투는 <b>마지막에 산 것부터</b> 꺼낸다
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

        /// <summary>고르면 합계가 오르고, 다시 고르면 내려가야 합니다.</summary>
        [Test]
        public void 고르고_무르면_합계가_따라간다()
        {
            ShopShelfItem bread = BuildShelfItem("빵", 1200);
            ShopShelfItem milk = BuildShelfItem("우유", 800);

            Assert.AreEqual(0, counter.TotalPrice, "처음에는 비어 있어야 합니다.");

            counter.Toggle(bread);
            Assert.AreEqual(1200, counter.TotalPrice);

            counter.Toggle(milk);
            Assert.AreEqual(2000, counter.TotalPrice);
            Assert.AreEqual(2, counter.SelectedCount);

            // 같은 것을 다시 고르면 무릅니다.
            counter.Toggle(bread);
            Assert.AreEqual(800, counter.TotalPrice, "무른 값이 빠져야 합니다.");
            Assert.AreEqual(1, counter.SelectedCount);
        }

        /// <summary>진열품 자신도 자기가 골라졌는지 알아야 합니다.</summary>
        [Test]
        public void 진열품은_자기가_골라졌는지_안다()
        {
            ShopShelfItem bread = BuildShelfItem("빵", 1200);

            Assert.IsFalse(bread.IsSelected);

            bread.Interact();
            Assert.IsTrue(bread.IsSelected, "상호작용하면 골라져야 합니다.");

            bread.Interact();
            Assert.IsFalse(bread.IsSelected, "다시 상호작용하면 물러야 합니다.");
        }

        // --- 계산 ---

        /// <summary>값을 치르면 지갑에서 그만큼 빠지고 장바구니가 비어야 합니다.</summary>
        [Test]
        public void 계산하면_값이_빠지고_장바구니가_비워진다()
        {
            wallet.Add(CurrencyType.Money, 5000);
            counter.Toggle(BuildShelfItem("빵", 1200));
            counter.Toggle(BuildShelfItem("우유", 800));

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
            counter.Toggle(BuildShelfItem("빵", 1200));

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

        // --- 봉투 ---

        /// <summary>봉투에는 담을 수 있는 것만 들어가야 합니다.</summary>
        [Test]
        public void 큰_물건은_봉투에_담기지_않는다()
        {
            wallet.Add(CurrencyType.Money, 50000);

            counter.Toggle(BuildShelfItem("빵", 1200));
            counter.Toggle(BuildShelfItem("맥주 상자", 12000, fitsInBag: false));

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
            counter.Toggle(BuildShelfItem("맥주 상자", 12000, fitsInBag: false));

            Assert.IsTrue(counter.TryCheckout());

            Assert.IsNull(FindBag(), "담을 것이 없으면 빈 봉투를 만들 이유가 없습니다.");
        }

        /// <summary>봉투는 마지막에 담은 것부터 꺼내야 합니다.</summary>
        [Test]
        public void 봉투는_마지막에_산_것부터_꺼낸다()
        {
            wallet.Add(CurrencyType.Money, 50000);

            counter.Toggle(BuildShelfItem("빵", 1000));
            counter.Toggle(BuildShelfItem("우유", 1000));
            counter.Toggle(BuildShelfItem("사탕", 1000));

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

        /// <summary>진열품 하나를 만들어 계산대에 이어 둡니다.</summary>
        /// <param name="displayName">물건 이름</param>
        /// <param name="price">값</param>
        /// <param name="fitsInBag">봉투에 담기는지 여부</param>
        /// <returns>만들어진 진열품</returns>
        private ShopShelfItem BuildShelfItem(string displayName, int price, bool fitsInBag = true)
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
            shelf.counter = counter;   // EditMode 에서는 Start 가 돌지 않으므로 직접 잇습니다
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
