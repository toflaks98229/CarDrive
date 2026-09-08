using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 다리들이 <b>어떤 묶음으로 함께 나가는지</b>를 정합니다.
    ///
    /// 이름을 다리 수와 무관하게 지었습니다. 예전에는 "속보 = 대각선 두 다리"처럼
    /// <b>네 다리를 전제로</b> 불렀는데, 그러면 2족·3족에서는 뜻이 없어집니다.
    /// 지금은 <see cref="Alternate"/> 하나가 <b>2족의 걷기 · 4족의 속보 · 6족의 삼각보</b>를 전부 가리킵니다.
    /// 셋 다 "절반이 나가고 절반이 버틴다"는 같은 규칙이기 때문입니다.
    /// </summary>
    public enum WalkerGaitType
    {
        /// <summary>파도보. 한 발씩 차례로 뗍니다. 다리 수에 상관없이 되고, 가장 안정하고 느립니다.</summary>
        Wave,

        /// <summary>교대보. 절반씩 번갈아 뗍니다. 2족의 걷기, 4족의 속보(대각선), 6족의 삼각보가 이것입니다.</summary>
        Alternate,

        /// <summary>측대보. 같은 쪽 다리끼리 함께 나갑니다. 네 다리에서만 뜻이 있습니다.</summary>
        Lateral,

        /// <summary>뜀걸음. 앞다리끼리, 뒷다리끼리 나갑니다. 네 다리에서만 뜻이 있습니다.</summary>
        Bound
    }

    /// <summary>
    /// 보행을 <b>묶음 번호</b>로 바꿔 주는 표입니다. 상태를 갖지 않습니다.
    ///
    /// <b>왜 짝이 아니라 묶음인가.</b> 예전에는 다리마다 "짝"을 하나씩 적어 두었습니다.
    /// 그 방식은 네 다리에서만 통합니다. 2족에는 짝이 없고, 3족은 셋이 돌아가며,
    /// 6족은 셋씩 묶입니다. <b>묶음 번호</b>로 적으면 셋 다 같은 코드로 처리됩니다.
    /// 걸음은 "지금 가장 뒤처진 묶음을 통째로 내보낸다"가 전부입니다.
    ///
    /// <b>다리 순서 규약</b>이 하나 있습니다. 앞에서 뒤로, 각 줄마다 <b>왼쪽 · 오른쪽</b> 순서입니다.
    /// (4족이면 왼앞 · 오른앞 · 왼뒤 · 오른뒤) 교대보의 대각선 묶음이 이 순서를 전제로 나옵니다.
    /// </summary>
    public static class WalkerGait
    {
        // --- Public Methods ---

        /// <summary>
        /// 다리마다 묶음 번호를 채워 넣고 묶음의 개수를 돌려줍니다.
        ///
        /// <b>교대보의 대각선 규칙</b>이 재미있습니다. 줄 번호와 좌우를 더해 홀짝을 보면
        /// (i/2 + i%2) 4족에서는 대각선 짝이, 6족에서는 삼각보가 <b>같은 식 하나로</b> 나옵니다.
        /// <code>
        /// 4족: 왼앞0 오른앞1 왼뒤1 오른뒤0   → {왼앞·오른뒤} {오른앞·왼뒤}
        /// 6족: 왼앞0 오른앞1 왼중1 오른중0 왼뒤0 오른뒤1 → {왼앞·오른중·왼뒤} {오른앞·왼중·오른뒤}
        /// </code>
        /// <b>다리가 홀수면 교대보를 쓰지 않습니다.</b> 절반으로 못 나누므로 한쪽이 하나만 남아
        /// 버티는 발이 부족해집니다. 그때는 파도보로 물러납니다. (3족 스트라이더가 이 경우입니다)
        ///
        /// <b>뜀걸음은 예외입니다.</b> 앞줄과 뒷줄로 나눌 뿐이라 홀수에서도 식이 성립하고,
        /// 3족에서는 앞이 나갈 때 <b>뒷발 하나만 남는 것을 일부러 받아들입니다.</b>
        /// 그 한 발 구간이 몸을 앞으로 떨어뜨렸다 받치는 구간이라, 안정을 내주고 역동을 얻는 선택입니다.
        /// </summary>
        /// <param name="gait">보행 종류</param>
        /// <param name="legCount">다리 개수</param>
        /// <param name="groups">묶음 번호를 채워 넣을 배열. 길이가 다리 개수 이상이어야 합니다.</param>
        /// <returns>묶음의 개수. 이것이 곧 한 바퀴를 이루는 걸음 수입니다.</returns>
        public static int Assign(WalkerGaitType gait, int legCount, int[] groups)
        {
            bool splittable = legCount >= 4 && legCount % 2 == 0;

            switch (gait)
            {
                case WalkerGaitType.Alternate when legCount == 2:
                    return Fill(groups, legCount, i => i);

                case WalkerGaitType.Alternate when splittable:
                    return Fill(groups, legCount, i => (i / 2 + i % 2) % 2);

                case WalkerGaitType.Lateral when legCount == 4:
                    return Fill(groups, legCount, i => i % 2);

                // 뜀걸음은 <b>앞줄과 뒷줄</b>을 나눌 뿐이라 홀수 다리에서도 뜻이 성립합니다.
                // 3족이면 {앞왼·앞오} {뒤} 가 되어, 앞이 나갈 때는 <b>뒷발 하나로 버팁니다.</b>
                // 그 한 발 구간이 곧 몸이 앞으로 떨어졌다 받쳐지는 구간입니다.
                case WalkerGaitType.Bound when legCount == 3:
                case WalkerGaitType.Bound when legCount == 4:
                    return Fill(groups, legCount, i => i / 2);

                default:
                    // 파도보 — 한 발씩. 어떤 다리 수에서도 반드시 성립합니다.
                    return Fill(groups, legCount, i => i);
            }
        }

        /// <summary>이 보행이 이 다리 수에서 뜻이 있는지입니다. 없으면 파도보로 걷게 됩니다.</summary>
        /// <param name="gait">보행 종류</param>
        /// <param name="legCount">다리 개수</param>
        /// <returns>그대로 쓸 수 있으면 true</returns>
        public static bool IsMeaningful(WalkerGaitType gait, int legCount)
        {
            switch (gait)
            {
                case WalkerGaitType.Wave: return true;
                case WalkerGaitType.Alternate: return legCount == 2 || (legCount >= 4 && legCount % 2 == 0);

                // 뜀걸음만 3족까지 내려옵니다. 앞줄·뒷줄로 나누는 데에 짝수가 필요 없습니다.
                case WalkerGaitType.Bound: return legCount == 3 || legCount == 4;

                default: return legCount == 4;
            }
        }

        /// <summary>보행 이름을 돌려줍니다. 디버그 표시에 씁니다.</summary>
        /// <param name="gait">보행 종류</param>
        /// <returns>한글 이름</returns>
        public static string DisplayName(WalkerGaitType gait)
        {
            switch (gait)
            {
                case WalkerGaitType.Wave: return "파도보";
                case WalkerGaitType.Alternate: return "교대보";
                case WalkerGaitType.Lateral: return "측대보";
                default: return "뜀걸음";
            }
        }

        // --- Private Methods ---

        /// <summary>묶음 번호를 채우고 <b>실제로 쓰인</b> 묶음의 개수를 셉니다.</summary>
        /// <param name="groups">채울 배열</param>
        /// <param name="legCount">다리 개수</param>
        /// <param name="selector">다리 번호를 묶음 번호로 바꾸는 식</param>
        /// <returns>묶음 개수</returns>
        private static int Fill(int[] groups, int legCount, System.Func<int, int> selector)
        {
            int highest = -1;

            for (int i = 0; i < legCount; i++)
            {
                groups[i] = selector(i);
                highest = Mathf.Max(highest, groups[i]);
            }

            return highest + 1;
        }
    }
}
