using System.Collections.Generic;
using NUnit.Framework;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 걸음을 <b>누가 언제 떼는지</b>를 검증합니다.
    ///
    /// 이 규칙은 보행이 절뚝이지 않는 이유 전부인데, <see cref="WalkerRobot"/> 안에 있을 때는
    /// 눈으로 보고 판단하는 수밖에 없었습니다. 실제로 예전에 <b>두 다리는 10초 동안 98걸음,
    /// 나머지 둘은 0걸음</b>이 나온 적이 있고 (묶음이 어긋난 채 굳는 버그) 그것을 알아채는 데
    /// 시뮬레이션이 필요했습니다. 이제 그 종류의 회귀는 여기서 잡힙니다.
    /// </summary>
    public class WalkerStepPlannerTests
    {
        // --- Private Member Variables ---

        /// <summary>검사 대상입니다.</summary>
        private WalkerStepPlanner planner;

        /// <summary>계획자가 채워 줄 출발 목록입니다.</summary>
        private List<int> begin;

        // --- Setup ---

        /// <summary>매 검사마다 새 계획자를 씁니다.</summary>
        [SetUp]
        public void SetUp()
        {
            planner = new WalkerStepPlanner();
            begin = new List<int>();
        }

        // --- Helpers ---

        /// <summary>
        /// 네 다리를 대각선 두 묶음으로 나눈 상태를 만듭니다. (0·3 이 한 묶음, 1·2 가 다른 묶음)
        /// 전부 땅에 있고 아무도 문턱을 넘지 않은 상태에서 출발합니다.
        /// </summary>
        /// <returns>다리 상태 배열</returns>
        private static WalkerLegState[] Quadruped()
        {
            WalkerLegState[] legs = new WalkerLegState[4];

            for (int i = 0; i < legs.Length; i++)
            {
                legs[i].strideRadius = 0.5f;
                legs[i].strideError = 0f;
                legs[i].homeDistance = 0f;
                legs[i].isStepping = false;
            }

            legs[0].group = 0;
            legs[1].group = 1;
            legs[2].group = 1;
            legs[3].group = 0;

            return legs;
        }

        /// <summary>시차 없이 0.25초짜리 걸음을 쓰는 기본 설정입니다.</summary>
        /// <param name="stagger">묶음 안의 시차</param>
        /// <returns>걸음의 성질</returns>
        private static WalkerStepTuning Tuning(float stagger = 0f)
        {
            WalkerStepTuning tuning;
            tuning.strideUsage = 0.8f;
            tuning.triggerFraction = 0.5f;
            tuning.stagger = stagger;
            tuning.stepDuration = 0.25f;

            return tuning;
        }

        /// <summary>이 설정에서 걸음이 시작되는 문턱을 <b>확실히</b> 넘긴 거리입니다.</summary>
        /// <returns>문턱보다 큰 거리(m)</returns>
        private static float OverTrigger()
        {
            return WalkerStepPlanner.StepTrigger(0.5f, 0.8f, 0.5f) * 1.5f;
        }

        // --- Tests : 문턱 ---

        /// <summary>아무도 제자리에서 벗어나지 않았으면 걸음이 나가지 않습니다.</summary>
        [Test]
        public void 문턱을_넘지_않으면_아무도_나가지_않는다()
        {
            WalkerLegState[] legs = Quadruped();

            planner.Resize(legs.Length);
            planner.Plan(0.02f, legs, Tuning(), begin);

            CollectionAssert.IsEmpty(begin);
        }

        /// <summary>문턱을 넘긴 다리가 있으면 <b>그 다리가 속한 묶음 전체</b>가 나갑니다.</summary>
        [Test]
        public void 문턱을_넘으면_그_다리의_묶음이_통째로_나간다()
        {
            WalkerLegState[] legs = Quadruped();
            legs[1].strideError = OverTrigger();

            planner.Resize(legs.Length);
            planner.Plan(0.02f, legs, Tuning(), begin);

            // 1번이 속한 묶음은 1·2 입니다. 대각선 짝이 함께 나가야 합니다.
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, begin);
        }

        /// <summary>작업 반경이 다르면 문턱도 달라집니다. 짧은 다리가 먼저 나갑니다.</summary>
        [Test]
        public void 문턱은_절대거리가_아니라_다리_반경에_대한_비율이다()
        {
            WalkerLegState[] legs = Quadruped();

            // 짧은 다리와 긴 다리가 <b>같은 거리</b>만큼 벌어졌습니다.
            legs[0].strideRadius = 0.2f;
            legs[1].strideRadius = 2f;
            legs[0].strideError = 0.15f;
            legs[1].strideError = 0.15f;

            planner.Resize(legs.Length);
            planner.Plan(0.02f, legs, Tuning(), begin);

            // 반경이 짧은 0번 쪽이 더 급하므로 0번의 묶음(0·3)이 나가야 합니다.
            CollectionAssert.AreEquivalent(new[] { 0, 3 }, begin);
        }

        // --- Tests : 시차 ---

        /// <summary>시차가 0이면 묶음이 <b>같은 프레임에</b> 함께 나갑니다.</summary>
        [Test]
        public void 시차가_0이면_묶음이_동시에_나간다()
        {
            WalkerLegState[] legs = Quadruped();
            legs[0].strideError = OverTrigger();

            planner.Resize(legs.Length);
            planner.Plan(0.02f, legs, Tuning(0f), begin);

            CollectionAssert.AreEquivalent(new[] { 0, 3 }, begin);
            Assert.IsFalse(planner.HasPending, "시차가 없는데 예약이 남았습니다.");
        }

        /// <summary>시차가 있으면 첫 다리만 지금 나가고 나머지는 예약됩니다.</summary>
        [Test]
        public void 시차가_있으면_첫_다리만_지금_나가고_나머지는_예약된다()
        {
            WalkerLegState[] legs = Quadruped();
            legs[0].strideError = OverTrigger();

            planner.Resize(legs.Length);
            planner.Plan(0.02f, legs, Tuning(1f), begin);

            CollectionAssert.AreEqual(new[] { 0 }, begin, "묶음의 첫 다리만 나가야 합니다.");
            Assert.IsTrue(planner.HasPending, "나머지 다리의 예약이 잡히지 않았습니다.");
        }

        /// <summary>예약해 둔 다리는 시차만큼 지난 뒤에 나갑니다.</summary>
        [Test]
        public void 예약된_다리는_시차가_지나면_나간다()
        {
            WalkerLegState[] legs = Quadruped();
            legs[0].strideError = OverTrigger();

            planner.Resize(legs.Length);
            planner.Plan(0.02f, legs, Tuning(1f), begin);

            begin.Clear();

            // 시차는 stagger × stepDuration = 0.25초입니다. 그만큼 흘립니다.
            planner.Plan(0.3f, legs, Tuning(1f), begin);

            CollectionAssert.AreEqual(new[] { 3 }, begin, "예약된 짝이 나오지 않았습니다.");
            Assert.IsFalse(planner.HasPending, "예약이 비워지지 않았습니다.");
        }

        /// <summary>
        /// <b>예약이 남아 있는 동안에는 새 묶음을 부르지 않습니다.</b>
        /// 부르면 묶음이 겹쳐 쌓여 다리가 전부 공중에 뜹니다.
        /// </summary>
        [Test]
        public void 예약이_남아_있으면_새_묶음을_부르지_않는다()
        {
            WalkerLegState[] legs = Quadruped();
            legs[0].strideError = OverTrigger();

            planner.Resize(legs.Length);
            planner.Plan(0.02f, legs, Tuning(1f), begin);
            begin.Clear();

            // 반대쪽 묶음도 문턱을 넘겼지만, 예약이 남아 있으므로 나가면 안 됩니다.
            legs[1].strideError = OverTrigger();
            legs[2].strideError = OverTrigger();

            planner.Plan(0.02f, legs, Tuning(1f), begin);

            CollectionAssert.IsEmpty(begin);
        }

        // --- Tests : 모든 발이 땅에 있을 때만 ---

        /// <summary>
        /// 한 발이라도 떠 있으면 새 묶음이 나가지 않습니다.
        /// (예전에 "자기 짝을 뺀 나머지가 땅에 있으면" 떼게 했다가 다리 둘이 한 걸음도 못 뗀 적이 있습니다)
        /// </summary>
        [Test]
        public void 한_발이라도_떠_있으면_새_묶음이_나가지_않는다()
        {
            WalkerLegState[] legs = Quadruped();
            legs[2].isStepping = true;
            legs[0].strideError = OverTrigger();

            planner.Resize(legs.Length);
            planner.Plan(0.02f, legs, Tuning(), begin);

            // 구조 대상도 아니므로(제자리에서 벗어나지 않음) 아무도 나가지 않습니다.
            CollectionAssert.IsEmpty(begin);
        }

        // --- Tests : 구조 ---

        /// <summary>
        /// 보행 표가 막고 있는 사이에 작업 반경 밖으로 끌려나간 다리는 규칙을 어기고 구합니다.
        /// 그대로 두면 다리가 뻗은 채 땅을 긁습니다.
        /// </summary>
        [Test]
        public void 작업_반경_밖으로_끌려나간_다리는_구조된다()
        {
            WalkerLegState[] legs = Quadruped();
            legs[2].isStepping = true;

            // 1번이 제자리에서 반경보다 멀리 끌려나갔습니다.
            legs[1].homeDistance = 0.9f;

            planner.Resize(legs.Length);
            planner.Plan(0.02f, legs, Tuning(), begin);

            CollectionAssert.AreEqual(new[] { 1 }, begin);
        }

        /// <summary>구조하더라도 <b>땅에 두 발은 남깁니다.</b> 그러지 않으면 넘어집니다.</summary>
        [Test]
        public void 딛고_있는_발이_둘_미만이면_구조하지_않는다()
        {
            WalkerLegState[] legs = Quadruped();
            legs[0].isStepping = true;
            legs[2].isStepping = true;
            legs[3].isStepping = true;

            legs[1].homeDistance = 5f;

            planner.Resize(legs.Length);
            planner.Plan(0.02f, legs, Tuning(), begin);

            CollectionAssert.IsEmpty(begin);
        }

        /// <summary>끌려나간 정도가 반경 안이면 구조하지 않습니다.</summary>
        [Test]
        public void 반경_안에_있으면_구조하지_않는다()
        {
            WalkerLegState[] legs = Quadruped();
            legs[2].isStepping = true;
            legs[1].homeDistance = 0.4f;

            planner.Resize(legs.Length);
            planner.Plan(0.02f, legs, Tuning(), begin);

            CollectionAssert.IsEmpty(begin);
        }

        // --- Tests : 예약 비우기 ---

        /// <summary>
        /// 순간이동하거나 일어난 직후에는 예약을 비웁니다.
        /// 남겨 두면 <b>이미 없어진 상황을 위해 잡아 둔 걸음</b>이 뒤늦게 나갑니다.
        /// </summary>
        [Test]
        public void 예약_비우기는_남은_걸음을_지운다()
        {
            WalkerLegState[] legs = Quadruped();
            legs[0].strideError = OverTrigger();

            planner.Resize(legs.Length);
            planner.Plan(0.02f, legs, Tuning(1f), begin);
            Assert.IsTrue(planner.HasPending);

            planner.ClearPending();

            Assert.IsFalse(planner.HasPending);

            begin.Clear();
            planner.Plan(1f, Quadruped(), Tuning(1f), begin);

            CollectionAssert.IsEmpty(begin, "지운 예약이 되살아났습니다.");
        }

        /// <summary>다리 수가 바뀌면 자리를 다시 잡고 예약을 비웁니다.</summary>
        [Test]
        public void 다리_수가_바뀌면_예약이_비워진다()
        {
            WalkerLegState[] legs = Quadruped();
            legs[0].strideError = OverTrigger();

            planner.Resize(legs.Length);
            planner.Plan(0.02f, legs, Tuning(1f), begin);
            Assert.IsTrue(planner.HasPending);

            planner.Resize(3);

            Assert.IsFalse(planner.HasPending);
        }

        // --- Tests : 문턱 계산 ---

        /// <summary>문턱은 0으로 내려가지 않습니다. 반경이 0이어도 나눗셈이 폭발하지 않아야 합니다.</summary>
        [Test]
        public void 문턱은_0으로_내려가지_않는다()
        {
            Assert.Greater(WalkerStepPlanner.StepTrigger(0f, 0.8f, 0.5f), 0f);
            Assert.Greater(WalkerStepPlanner.StepTrigger(1f, 0f, 0f), 0f);
        }

        /// <summary>다리가 없어도 아무 일 없이 지나갑니다.</summary>
        [Test]
        public void 다리가_없으면_아무_일도_하지_않는다()
        {
            planner.Resize(0);
            planner.Plan(0.02f, new WalkerLegState[0], Tuning(), begin);

            CollectionAssert.IsEmpty(begin);
            Assert.IsFalse(planner.HasPending);
        }
    }
}
