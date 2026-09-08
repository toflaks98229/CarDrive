using NUnit.Framework;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 쓰러진 로봇이 <b>어떤 차례로</b> 다리를 딛는지를 검증합니다.
    ///
    /// <see cref="WalkerRiseOrder"/> 셋의 뜻이 전부 여기서 확인됩니다.
    /// 특히 "보행 묶음으로 세우면 4족의 차례는 넷이 아니라 둘"이라는 규칙은
    /// 남는 시간을 나누는 분모라, 깨지면 일어나는 속도가 조용히 두 배가 됩니다.
    /// </summary>
    public class WalkerRiseOrderingTests
    {
        // --- Helpers ---

        /// <summary>4족의 대각선 묶음입니다. (0·3 이 한 묶음, 1·2 가 다른 묶음)</summary>
        private static readonly int[] DiagonalGroups = { 0, 1, 1, 0 };

        /// <summary>차례를 담을 빈 배열 한 쌍을 만듭니다.</summary>
        /// <param name="count">다리 개수</param>
        /// <param name="slots">차례를 적을 배열</param>
        /// <param name="scratch">정렬용 임시 배열</param>
        private static void Buffers(int count, out int[] slots, out int[] scratch)
        {
            slots = new int[count];
            scratch = new int[count];
        }

        // --- Tests ---

        /// <summary>배선 순서대로 세우면 다리 번호가 곧 차례가 됩니다.</summary>
        [Test]
        public void 배선_순서는_번호대로_하나씩_딛는다()
        {
            Buffers(4, out int[] slots, out int[] scratch);

            int count = WalkerRiseOrdering.BuildSlots(WalkerRiseOrder.LegOrder, 4, null,
                                                      DiagonalGroups, 2, slots, scratch);

            Assert.AreEqual(4, count, "다리마다 차례가 하나씩이어야 합니다.");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, slots);
        }

        /// <summary>
        /// 보행 묶음으로 세우면 대각선 짝이 <b>같은 차례</b>를 받고, 차례 수는 묶음 수가 됩니다.
        /// 이 값이 남는 시간을 나누는 분모입니다.
        /// </summary>
        [Test]
        public void 보행_묶음은_대각선_짝이_같은_차례를_받는다()
        {
            Buffers(4, out int[] slots, out int[] scratch);

            int count = WalkerRiseOrdering.BuildSlots(WalkerRiseOrder.GaitGroups, 4, null,
                                                      DiagonalGroups, 2, slots, scratch);

            Assert.AreEqual(2, count, "4족을 묶음으로 세우면 차례는 둘이어야 합니다.");
            Assert.AreEqual(slots[0], slots[3], "대각선 짝이 함께 나가야 합니다.");
            Assert.AreEqual(slots[1], slots[2], "대각선 짝이 함께 나가야 합니다.");
            Assert.AreNotEqual(slots[0], slots[1], "두 묶음이 같은 차례를 받았습니다.");
        }

        /// <summary>차례 수는 최소 1입니다. 0으로 내려가면 부르는 쪽에서 0으로 나눕니다.</summary>
        [Test]
        public void 보행_묶음의_차례_수는_1_밑으로_내려가지_않는다()
        {
            Buffers(4, out int[] slots, out int[] scratch);

            int count = WalkerRiseOrdering.BuildSlots(WalkerRiseOrder.GaitGroups, 4, null,
                                                      DiagonalGroups, 0, slots, scratch);

            Assert.AreEqual(1, count);
        }

        /// <summary>
        /// 낮은 발부터 세우면 <b>땅에 가까운 다리</b>가 먼저 차례를 받습니다.
        /// 옆으로 누웠을 때 아래쪽 다리로 먼저 몸을 받치는 모습이 여기서 나옵니다.
        /// </summary>
        [Test]
        public void 낮은_발부터는_땅에_가까운_다리가_먼저다()
        {
            Buffers(4, out int[] slots, out int[] scratch);

            // 2번이 가장 낮고, 그 다음이 0번, 3번, 1번입니다.
            float[] heights = { 1.0f, 3.0f, 0.5f, 2.0f };

            int count = WalkerRiseOrdering.BuildSlots(WalkerRiseOrder.LowestFirst, 4, heights,
                                                      DiagonalGroups, 2, slots, scratch);

            Assert.AreEqual(4, count);
            Assert.AreEqual(0, slots[2], "가장 낮은 발이 첫 차례여야 합니다.");
            Assert.AreEqual(1, slots[0]);
            Assert.AreEqual(2, slots[3]);
            Assert.AreEqual(3, slots[1], "가장 높은 발이 마지막 차례여야 합니다.");
        }

        /// <summary>같은 높이면 원래 번호 순서를 지킵니다. (안정 정렬)</summary>
        [Test]
        public void 같은_높이면_배선_순서를_지킨다()
        {
            Buffers(3, out int[] slots, out int[] scratch);

            float[] heights = { 1f, 1f, 1f };

            WalkerRiseOrdering.BuildSlots(WalkerRiseOrder.LowestFirst, 3, heights,
                                          new[] { 0, 0, 0 }, 1, slots, scratch);

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, slots);
        }

        /// <summary>다리가 홀수라도 낮은 발부터 규칙은 그대로 성립합니다. (스트라이더는 3족입니다)</summary>
        [Test]
        public void 삼족도_낮은_발부터_세운다()
        {
            Buffers(3, out int[] slots, out int[] scratch);

            float[] heights = { 2f, 0.1f, 1f };

            int count = WalkerRiseOrdering.BuildSlots(WalkerRiseOrder.LowestFirst, 3, heights,
                                                      new[] { 0, 1, 2 }, 3, slots, scratch);

            Assert.AreEqual(3, count);
            Assert.AreEqual(0, slots[1]);
            Assert.AreEqual(1, slots[2]);
            Assert.AreEqual(2, slots[0]);
        }

        /// <summary>필요한 자료가 없으면 배선 순서로 물러납니다. 로봇이 못 일어나는 것보다 낫습니다.</summary>
        [Test]
        public void 자료가_없으면_배선_순서로_물러난다()
        {
            Buffers(4, out int[] slots, out int[] scratch);

            int byHeight = WalkerRiseOrdering.BuildSlots(WalkerRiseOrder.LowestFirst, 4, null,
                                                        DiagonalGroups, 2, slots, scratch);
            Assert.AreEqual(4, byHeight);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, slots);

            int byGroups = WalkerRiseOrdering.BuildSlots(WalkerRiseOrder.GaitGroups, 4, null,
                                                        null, 2, slots, scratch);
            Assert.AreEqual(4, byGroups);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, slots);
        }

        /// <summary>다리가 없으면 1을 돌려줍니다. 부르는 쪽이 이 값으로 나누기 때문입니다.</summary>
        [Test]
        public void 다리가_없으면_차례_수는_1이다()
        {
            Buffers(0, out int[] slots, out int[] scratch);

            Assert.AreEqual(1, WalkerRiseOrdering.BuildSlots(WalkerRiseOrder.LegOrder, 0, null,
                                                             null, 0, slots, scratch));
        }
    }
}
