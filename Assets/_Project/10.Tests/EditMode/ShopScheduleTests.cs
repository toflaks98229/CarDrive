using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CarDrive.Common;
using CarDrive.Gameplay;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 마트의 영업 시간과 입고 규칙에 대한 EditMode 테스트입니다.
    ///
    /// <b>왜 못박는가.</b> 이 규칙은 <b>플레이어가 자는 동안</b> 일어납니다. 눈으로 확인하려면
    /// 게임을 띄우고 밤이 될 때까지 기다렸다가 다음 날 아침까지 또 기다려야 하는데,
    /// 그 확인은 반복되지 않습니다. 게다가 어긋나도 조용합니다 —
    /// 문이 안 열리거나 물건이 안 들어와도 "오늘은 운이 없나 보다"로 보입니다.
    ///
    /// 지키는 것은 넷입니다.
    ///  1. 08:00 에 열고 18:00 에 닫는다
    ///  2. 07:00 에 진열대가 다시 찬다 &mdash; <b>문을 여는 시각보다 앞</b>
    ///  3. 문을 닫으면 담아 둔 것이 진열대로 돌아간다
    ///  4. <b>시간을 건너뛰어도</b> (수면·기절) 그 사이의 개점과 입고가 반영된다
    /// </summary>
    public class ShopScheduleTests
    {
        /// <summary>원하는 시각을 만들어 주는 가짜 시계입니다.</summary>
        private sealed class FakeClock : IGameClock
        {
            /// <summary>첫날 0시부터 흐른 분입니다.</summary>
            public float Minutes;

            public float TotalMinutes { get { return Minutes; } }
            public float Daylight { get { return 1f; } }
            public bool IsNight { get { return false; } }
            public bool IsRunning { get { return true; } }
            public float GetMinutesPerSecond(float fallback) { return fallback; }
            public void AdvanceMinutes(float minutes) { Minutes += minutes; }
        }

        /// <summary>테스트가 만든 것들을 한꺼번에 치우기 위한 표식입니다.</summary>
        private sealed class SpawnedByTest : MonoBehaviour { }

        private GameObject root;
        private ShopCounter counter;
        private ShopSchedule schedule;
        private Wallet wallet;
        private FakeClock clock;
        private readonly List<ShopItem> createdItems = new List<ShopItem>();

        [SetUp]
        public void SetUp()
        {
            GameContext.Clear();

            root = new GameObject("ShopRoot");
            root.AddComponent<SpawnedByTest>();

            wallet = root.AddComponent<Wallet>();
            wallet.EnsureInitialized();

            counter = root.AddComponent<ShopCounter>();
            counter.Construct(wallet);

            clock = new FakeClock();

            // RequireComponent 로 ShopCounter 가 이미 붙어 있습니다.
            schedule = root.AddComponent<ShopSchedule>();
            schedule.Construct(clock);
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

            root = null; counter = null; schedule = null; wallet = null; clock = null;
            GameContext.Clear();
        }

        // --- 영업 시간 ---

        /// <summary>08:00 에 열고 18:00 에 닫아야 합니다.</summary>
        [TestCase(7.0f, false, TestName = "07시_아직_닫혀_있다")]
        [TestCase(8.0f, true, TestName = "08시_문을_연다")]
        [TestCase(12.0f, true, TestName = "정오_열려_있다")]
        [TestCase(17.9f, true, TestName = "17시_54분_아직_열려_있다")]
        [TestCase(18.0f, false, TestName = "18시_문을_닫는다")]
        [TestCase(23.0f, false, TestName = "밤_닫혀_있다")]
        public void 시각에_따라_문이_열리고_닫힌다(float hour, bool expectedOpen)
        {
            SetHour(0, hour);
            schedule.Evaluate();

            Assert.AreEqual(expectedOpen, counter.IsOpen);
        }

        /// <summary>문이 닫혀 있으면 담을 수 없어야 합니다.</summary>
        [Test]
        public void 닫혀_있으면_담을_수_없다()
        {
            ShopShelfItem bread = BuildShelfItem("빵", 1200, stock: 3);

            SetHour(0, 20f);      // 밤
            schedule.Evaluate();

            bread.Interact();

            Assert.AreEqual(0, counter.SelectedCount, "닫혔는데 담겼습니다.");
            Assert.AreEqual(3, bread.RemainingCount, "진열대도 그대로여야 합니다.");
            Assert.IsTrue(bread.GetInteractionLabel().Contains("영업 시간이"),
                "왜 안 되는지 알려 주어야 합니다.");
        }

        /// <summary>
        /// 문을 닫으면 담아 두었던 것이 진열대로 돌아가야 합니다.
        /// 남겨 두면 다음 날 입고가 담긴 수를 모른 채 진열대를 채워 값과 실물이 어긋납니다.
        /// </summary>
        [Test]
        public void 문을_닫으면_담아_둔_것이_돌아간다()
        {
            ShopShelfItem bread = BuildShelfItem("빵", 1200, stock: 3);

            SetHour(0, 12f);
            schedule.Evaluate();

            bread.Interact();
            bread.Interact();
            Assert.AreEqual(2, counter.SelectedCount);
            Assert.AreEqual(1, bread.RemainingCount);

            SetHour(0, 18f);      // 폐점
            schedule.Evaluate();

            Assert.IsFalse(counter.IsOpen);
            Assert.AreEqual(0, counter.SelectedCount, "장바구니가 비워져야 합니다.");
            Assert.AreEqual(3, bread.RemainingCount, "진열대로 돌아가야 합니다.");
        }

        // --- 입고 ---

        /// <summary>
        /// 07:00 에 진열대가 다시 차야 합니다. <b>문을 여는 08:00 보다 앞</b>이라,
        /// 손님이 들어왔을 때는 이미 물건이 놓여 있습니다.
        /// </summary>
        [Test]
        public void 다음날_07시에_진열대가_다시_찬다()
        {
            ShopShelfItem bread = BuildShelfItem("빵", 1200, stock: 3);

            SetHour(0, 12f);
            schedule.Evaluate();

            bread.Interact();
            bread.Interact();
            bread.Interact();
            Assert.AreEqual(0, bread.RemainingCount, "다 팔렸어야 합니다.");

            // 폐점 — 장바구니가 진열대로 돌아가지만, 산 것이 아니라 담기만 한 것입니다.
            SetHour(0, 18f);
            schedule.Evaluate();
            Assert.AreEqual(3, bread.RemainingCount);

            // 실제로 팔아 재고를 없앱니다.
            SetHour(1, 12f);
            schedule.Evaluate();
            Assert.IsTrue(counter.IsOpen);
            bread.Interact(); bread.Interact(); bread.Interact();
            wallet.Add(CurrencyType.Money, 50000);
            Assert.IsTrue(counter.TryCheckout());
            Assert.AreEqual(0, bread.RemainingCount, "판 것은 돌아오지 않습니다.");

            // 다음 날 07시 — 입고
            SetHour(2, 7f);
            schedule.Evaluate();

            Assert.AreEqual(3, bread.RemainingCount, "07시에 다시 채워져야 합니다.");
            Assert.IsFalse(counter.IsOpen, "입고는 개점보다 앞이므로 아직 닫혀 있어야 합니다.");
        }

        /// <summary>같은 날에는 두 번 채우지 않아야 합니다.</summary>
        [Test]
        public void 같은_날에는_한_번만_채운다()
        {
            ShopShelfItem bread = BuildShelfItem("빵", 1200, stock: 2);
            wallet.Add(CurrencyType.Money, 50000);

            SetHour(1, 7f);
            schedule.Evaluate();          // 이날의 입고

            SetHour(1, 12f);
            schedule.Evaluate();
            bread.Interact();
            Assert.IsTrue(counter.TryCheckout());
            Assert.AreEqual(1, bread.RemainingCount);

            SetHour(1, 15f);
            schedule.Evaluate();          // 같은 날 다시 평가

            Assert.AreEqual(1, bread.RemainingCount, "같은 날 또 채우면 안 됩니다.");
        }

        /// <summary>
        /// <b>시간을 건너뛰어도</b> 개점과 입고가 반영되어야 합니다.
        /// 자고 일어났는데 문이 닫힌 채이거나 진열대가 빈 채면 시간이 흐른 것으로 보이지 않습니다.
        /// </summary>
        [Test]
        public void 시간을_건너뛰어도_열리고_채워진다()
        {
            ShopShelfItem bread = BuildShelfItem("빵", 1200, stock: 2);
            wallet.Add(CurrencyType.Money, 50000);

            SetHour(0, 12f);
            schedule.Evaluate();
            bread.Interact();
            Assert.IsTrue(counter.TryCheckout());
            Assert.AreEqual(1, bread.RemainingCount);

            // 낮 12시에서 다음 날 낮 12시로 한 번에 건너뜁니다. (수면)
            SetHour(1, 12f);
            schedule.Evaluate();

            Assert.IsTrue(counter.IsOpen, "건너뛴 뒤에는 열려 있어야 합니다.");
            Assert.AreEqual(2, bread.RemainingCount, "건너뛴 사이의 입고가 반영되어야 합니다.");
        }

        /// <summary>
        /// 시작할 때 이미 07시가 지났다면 <b>입고가 헛돌지 않아야</b> 합니다.
        /// 씬의 진열대는 채워진 채로 시작하므로 결과는 같지만, 연출과 로그가 헛되이 나갑니다.
        /// </summary>
        [Test]
        public void 시작_시각이_입고_시각을_지났으면_헛돌지_않는다()
        {
            ShopShelfItem bread = SellOneFrom(stock: 3);
            Assert.AreEqual(2, bread.RemainingCount, "하나를 팔았으므로 둘이 남습니다.");

            SetHour(0, 8f);       // 아침 8시에 시작 — 07시는 이미 지났습니다
            schedule.Evaluate();

            Assert.AreEqual(2, bread.RemainingCount,
                "시작하자마자 입고가 돌면 안 됩니다. 씬의 진열대는 이미 채워진 채로 시작합니다.");
        }

        /// <summary>
        /// 반대로 07시 <b>전</b>에 시작했다면 그날 07시에 채워야 합니다.
        /// </summary>
        [Test]
        public void 시작_시각이_입고_시각_전이면_그날_채운다()
        {
            ShopShelfItem bread = SellOneFrom(stock: 3);

            SetHour(0, 6f);
            schedule.Evaluate();
            Assert.AreEqual(2, bread.RemainingCount, "아직 07시가 아닙니다.");

            SetHour(0, 7f);
            schedule.Evaluate();
            Assert.AreEqual(3, bread.RemainingCount, "그날 07시에 채워야 합니다.");
        }

        // --- Helpers ---

        /// <summary>
        /// 진열 칸을 만들어 <b>실제로 하나 팔아</b> 재고를 하나 줄입니다.
        ///
        /// 입고가 돌았는지를 이벤트가 아니라 <b>재고로</b> 확인하기 위한 준비입니다.
        /// (이벤트는 <c>AddComponent</c> 로 만든 컴포넌트에서 null 이라, 그것을 듣는 것은
        /// 실제 동작이 아니라 테스트 사정을 확인하는 일이 됩니다)
        ///
        /// <see cref="ShopCounter.IsOpen"/> 의 기본값이 열림이라 일정표를 평가하기 전에도 팔립니다.
        /// </summary>
        /// <param name="stock">진열해 둘 수</param>
        /// <returns>하나 팔린 진열 칸</returns>
        private ShopShelfItem SellOneFrom(int stock)
        {
            ShopShelfItem shelf = BuildShelfItem("빵", 1200, stock);

            wallet.Add(CurrencyType.Money, 50000);
            shelf.Interact();
            Assert.IsTrue(counter.TryCheckout(), "준비 단계의 계산이 되어야 합니다.");

            return shelf;
        }

        /// <summary>가짜 시계를 원하는 날짜·시각으로 맞춥니다.</summary>
        /// <param name="day">며칠째인지 (0부터)</param>
        /// <param name="hour">그날의 시각(0~24)</param>
        private void SetHour(int day, float hour)
        {
            clock.Minutes = day * 1440f + hour * 60f;
        }

        /// <summary>진열 칸 하나를 만들어 계산대에 이어 둡니다.</summary>
        /// <param name="displayName">물건 이름</param>
        /// <param name="price">값</param>
        /// <param name="stock">진열해 둘 실물의 수</param>
        /// <returns>만들어진 진열 칸</returns>
        private ShopShelfItem BuildShelfItem(string displayName, int price, int stock)
        {
            ShopItem definition = ScriptableObject.CreateInstance<ShopItem>();
            definition.displayName = displayName;
            definition.price = price;
            definition.fitsInBag = false;   // 봉투 배선 없이도 계산이 되도록 큰 물건으로 둡니다
            createdItems.Add(definition);

            GameObject go = new GameObject("Shelf_" + displayName);
            go.AddComponent<SpawnedByTest>();
            go.transform.SetParent(root.transform, false);

            ShopShelfItem shelf = go.AddComponent<ShopShelfItem>();
            shelf.item = definition;
            shelf.counter = counter;

            for (int i = 0; i < stock; i++)
            {
                GameObject display = new GameObject(displayName + "_" + i);
                display.AddComponent<SpawnedByTest>();
                display.transform.SetParent(go.transform, false);
                shelf.displays.Add(display);
            }

            // EditMode 에서는 Start 가 돌지 않으므로 직접 등록합니다.
            counter.RegisterShelf(shelf);
            return shelf;
        }
    }
}
