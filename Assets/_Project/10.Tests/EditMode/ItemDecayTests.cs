using NUnit.Framework;
using UnityEngine;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// "월드에 놓인 물건은 하루가 지나면 사라진다"는 규칙에 대한 EditMode 테스트입니다.
    ///
    /// <b>이 규칙이 두 가지를 동시에 떠받칩니다.</b> 물리 오브젝트가 무한히 쌓이지 않는 것과,
    /// 세이브 파일이 무한히 커지지 않는 것입니다. 그래서 경계를 정확히 못박습니다.
    ///  1. 놓인 물건은 하루가 지나면 사라진다
    ///  2. <b>들고 있는 것과 차에 실린 것은 사라지지 않는다</b>
    ///  3. 다시 집으면 수명이 되돌아간다
    ///  4. 하루에서 <b>1분 모자라면</b> 아직 사라지지 않는다
    ///
    /// 2번이 가장 중요합니다. 손에 든 물건이 사라지는 것은 규칙이 아니라 고장으로 보입니다.
    /// </summary>
    public class ItemDecayTests
    {
        /// <summary>원하는 시각을 만들어 주는 가짜 시계입니다.</summary>
        private sealed class FakeClock : IGameClock
        {
            /// <summary>테스트가 직접 밀어 올리는 시각입니다.</summary>
            public float Minutes;

            public float TotalMinutes { get { return Minutes; } }
            public float Daylight { get { return 1f; } }
            public bool IsNight { get { return false; } }
            public bool IsRunning { get { return true; } }
            public float GetMinutesPerSecond(float fallback) { return fallback; }
            public void AdvanceMinutes(float minutes) { Minutes += minutes; }
        }

        /// <summary>하루입니다.</summary>
        private const float OneDay = 1440f;

        private GameObject decayGo;
        private ItemDecay decay;
        private FakeClock clock;

        [SetUp]
        public void SetUp()
        {
            GameContext.Clear();
            SaveableItem.Clear();

            clock = new FakeClock();

            decayGo = new GameObject("ItemDecay");
            decay = decayGo.AddComponent<ItemDecay>();
            decay.Construct(clock);
            decay.lifetimeGameMinutes = OneDay;
        }

        [TearDown]
        public void TearDown()
        {
            // 남아 있는 물건을 전부 치웁니다. 등록부가 정적이라 테스트 사이에 새어 나갑니다.
            SaveableItem[] leftovers = Object.FindObjectsByType<SaveableItem>(FindObjectsInactive.Include);
            for (int i = 0; i < leftovers.Length; i++)
            {
                if (leftovers[i] != null) Object.DestroyImmediate(leftovers[i].gameObject);
            }

            if (decayGo != null) Object.DestroyImmediate(decayGo);
            decayGo = null;
            decay = null;
            clock = null;

            // EditMode 에서는 OnDisable 이 돌지 않아 등록부가 저절로 비워지지 않습니다.
            SaveableItem.Clear();
            GameContext.Clear();
        }

        // --- 사라지는 경우 ---

        /// <summary>놓인 물건은 하루가 지나면 사라져야 합니다.</summary>
        [Test]
        public void 놓인_물건은_하루가_지나면_사라진다()
        {
            SaveableItem item = BuildItem("beer");

            // 첫 훑기가 "지금부터 놓여 있음"을 기록합니다.
            decay.Sweep();
            Assert.IsTrue(item != null, "아직 사라지면 안 됩니다.");
            Assert.AreEqual(0f, item.LooseSinceMinute, "놓인 시각이 기록되어야 합니다.");

            clock.Minutes = OneDay;
            decay.Sweep();

            Assert.IsTrue(item == null, "하루가 지났으므로 사라져야 합니다.");
        }

        /// <summary>
        /// 하루에서 1분 모자라면 아직 남아 있어야 합니다.
        /// 경계를 못박지 않으면 "거의 하루"에서 사라지는 것을 알아채지 못합니다.
        /// </summary>
        [Test]
        public void 하루에서_1분_모자라면_남아_있는다()
        {
            SaveableItem item = BuildItem("beer");

            decay.Sweep();

            clock.Minutes = OneDay - 1f;
            decay.Sweep();

            Assert.IsTrue(item != null, "아직 하루가 지나지 않았습니다.");
        }

        // --- 사라지지 않는 경우 ---

        /// <summary>
        /// 들고 있는 물건은 하루가 아무리 지나도 사라지면 안 됩니다.
        /// </summary>
        [Test]
        public void 들고_있는_물건은_사라지지_않는다()
        {
            SaveableItem item = BuildItem("beer", withCarryable: true);

            Carryable carryable = item.GetComponent<Carryable>();
            carryable.OnPickedUp();

            Assert.AreEqual(ItemPlacement.Held, item.Placement, "들고 있는 것으로 판정되어야 합니다.");

            clock.Minutes = OneDay * 5f;
            decay.Sweep();

            Assert.IsTrue(item != null, "손에 든 물건이 사라지면 안 됩니다.");
        }

        /// <summary>
        /// 차에 실린 물건도 사라지면 안 됩니다. 실어 두는 것이 곧 보관이기 때문입니다.
        /// </summary>
        [Test]
        public void 차에_실린_물건은_사라지지_않는다()
        {
            // Vehicle 은 RequireComponent 사슬이 길어, 붙이면 필요한 것이 함께 붙습니다.
            GameObject car = new GameObject("Car");
            car.AddComponent<Vehicle>();

            SaveableItem item = BuildItem("beer");
            item.transform.SetParent(car.transform, true);

            Assert.AreEqual(ItemPlacement.Stored, item.Placement, "실려 있는 것으로 판정되어야 합니다.");

            clock.Minutes = OneDay * 5f;
            decay.Sweep();

            Assert.IsTrue(item != null, "차에 실린 물건이 사라지면 안 됩니다.");

            Object.DestroyImmediate(car);
        }

        /// <summary>
        /// 놓아 두었다가 다시 집으면 수명이 처음부터 다시 흘러야 합니다.
        /// </summary>
        [Test]
        public void 다시_집으면_수명이_되돌아간다()
        {
            SaveableItem item = BuildItem("beer", withCarryable: true);
            Carryable carryable = item.GetComponent<Carryable>();

            // 반나절 동안 놓여 있었습니다.
            decay.Sweep();
            clock.Minutes = OneDay * 0.5f;
            decay.Sweep();

            // 집었습니다.
            carryable.OnPickedUp();
            decay.Sweep();
            Assert.AreEqual(SaveableItem.NotLoose, item.LooseSinceMinute, "들면 수명 시계가 멈춰야 합니다.");

            // 다시 놓았습니다. 여기서부터 새로 셉니다.
            carryable.OnDropped();
            decay.Sweep();
            Assert.AreEqual(OneDay * 0.5f, item.LooseSinceMinute, "놓은 시각이 새로 기록되어야 합니다.");

            // 놓은 지 반나절 — 처음 놓은 때로부터는 하루가 지났지만 아직 살아 있어야 합니다.
            clock.Minutes = OneDay;
            decay.Sweep();

            Assert.IsTrue(item != null, "다시 집었으므로 수명이 되돌아가 있어야 합니다.");
        }

        // --- 이름표 ---

        /// <summary>이름표가 없는 물건은 저장 대상이 아닙니다.</summary>
        [Test]
        public void 이름표가_없으면_신원이_없다()
        {
            SaveableItem item = BuildItem("");

            Assert.IsFalse(item.HasIdentity, "이름표가 없으면 저장되지 않아야 합니다.");
        }

        // --- Helpers ---

        /// <summary>물건 하나를 만듭니다.</summary>
        /// <param name="id">붙일 이름표</param>
        /// <param name="withCarryable">들 수 있게 만들지 여부</param>
        /// <returns>만들어진 물건</returns>
        private SaveableItem BuildItem(string id, bool withCarryable = false)
        {
            GameObject go = new GameObject("Item_" + id);

            if (withCarryable)
            {
                go.AddComponent<Rigidbody>();
                go.AddComponent<Carryable>();
            }

            SaveableItem item = go.AddComponent<SaveableItem>();
            item.itemId = id;

            // EditMode 에서는 OnEnable 이 돌지 않으므로 직접 등록합니다.
            item.EnsureRegistered();

            return item;
        }
    }
}
