using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 쓰러진 로봇이 일어날 때 <b>다리를 어떤 차례로 딛을지</b>를 정하는 규칙입니다.
    ///
    /// <b>왜 떼어냈는가.</b> <see cref="WalkerRiseOrder"/> 셋의 뜻은 전부 여기 스무 줄에 있는데,
    /// 그 스무 줄이 2,000줄짜리 <see cref="WalkerRobot"/> 안에 있어서 검증할 수 없었습니다.
    /// "4족을 보행 묶음으로 세우면 차례가 넷이 아니라 둘"이라는 규칙이 깨져도
    /// 로봇이 일어나는 모습을 눈으로 보고 알아채는 수밖에 없었습니다.
    ///
    /// 여기에는 <see cref="Transform"/> 도 <see cref="WalkerLeg"/> 도 없습니다.
    /// 발 높이는 숫자로 받고, 결과도 숫자로 돌려줍니다.
    /// </summary>
    public static class WalkerRiseOrdering
    {
        /// <summary>
        /// 다리마다 <b>몇 번째로 딛을지</b>를 <paramref name="slots"/> 에 적고,
        /// 서로 다른 차례가 몇 개인지 돌려줍니다.
        ///
        /// 같은 번호를 받은 다리는 <b>함께</b> 나갑니다. 그래서 보행 묶음을 쓰면 4족이 대각선 둘씩
        /// 딛고, 차례 수는 넷이 아니라 둘이 됩니다. 부르는 쪽이 남는 시간을 그 수로 나누므로
        /// <b>차례가 적을수록 한 번에 더 여유 있게</b> 딛습니다.
        /// </summary>
        /// <param name="order">딛는 차례를 정하는 방식</param>
        /// <param name="legCount">다리 개수</param>
        /// <param name="footHeights">다리별 지금 발 높이(m). <see cref="WalkerRiseOrder.LowestFirst"/> 에서만 씁니다.</param>
        /// <param name="gaitGroups">다리별 보행 묶음 번호. <see cref="WalkerRiseOrder.GaitGroups"/> 에서만 씁니다.</param>
        /// <param name="groupCount">보행 묶음의 개수</param>
        /// <param name="slots">차례를 적을 곳. 길이가 <paramref name="legCount"/> 이상이어야 합니다.</param>
        /// <param name="scratch">정렬에 쓸 임시 자리. 길이가 <paramref name="legCount"/> 이상이어야 합니다.</param>
        /// <returns>서로 다른 차례의 개수. 다리가 없으면 1입니다.</returns>
        public static int BuildSlots(WalkerRiseOrder order, int legCount, float[] footHeights,
                                     int[] gaitGroups, int groupCount, int[] slots, int[] scratch)
        {
            if (legCount <= 0 || slots == null) return 1;

            switch (order)
            {
                case WalkerRiseOrder.GaitGroups:
                    if (gaitGroups == null) break;

                    for (int i = 0; i < legCount; i++) slots[i] = gaitGroups[i];
                    return Mathf.Max(groupCount, 1);

                case WalkerRiseOrder.LowestFirst:
                    if (footHeights == null || scratch == null) break;

                    SortByFootHeight(legCount, footHeights, scratch);
                    for (int rank = 0; rank < legCount; rank++) slots[scratch[rank]] = rank;
                    return legCount;
            }

            // 배선 순서 그대로. 앞에서 뒤로, 왼쪽에서 오른쪽으로 하나씩 딛습니다.
            for (int i = 0; i < legCount; i++) slots[i] = i;
            return legCount;
        }

        /// <summary>
        /// 발 높이가 낮은 다리부터 오도록 번호를 정렬합니다.
        /// (다리가 여섯을 넘지 않으므로 삽입 정렬입니다. <b>같은 높이면 원래 순서를 지킵니다.</b>)
        /// </summary>
        /// <param name="legCount">다리 개수</param>
        /// <param name="footHeights">다리별 발 높이(m)</param>
        /// <param name="sorted">정렬된 다리 번호를 적을 곳</param>
        private static void SortByFootHeight(int legCount, float[] footHeights, int[] sorted)
        {
            for (int i = 0; i < legCount; i++) sorted[i] = i;

            for (int i = 1; i < legCount; i++)
            {
                int current = sorted[i];
                float key = footHeights[current];
                int j = i - 1;

                while (j >= 0 && footHeights[sorted[j]] > key)
                {
                    sorted[j + 1] = sorted[j];
                    j--;
                }

                sorted[j + 1] = current;
            }
        }
    }
}
