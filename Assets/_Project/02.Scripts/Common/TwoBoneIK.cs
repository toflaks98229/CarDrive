using UnityEngine;

namespace CarDrive.Common
{
    /// <summary>
    /// 뼈 두 개짜리 팔·다리를 목표 지점으로 뻗습니다. <b>코사인 법칙 한 번</b>으로 끝나는 해석해입니다.
    ///
    /// <b>왜 반복법(CCD·FABRIK)을 쓰지 않는가.</b> 뼈가 둘이면 답이 <b>닫힌 식으로</b> 나옵니다.
    /// 밑동에서 목표까지의 거리를 c 라 하면, 두 뼈 길이 a·b 와 c 가 삼각형을 이루므로
    /// <code>cos(밑동 각) = (a² + c² − b²) / (2ac)</code>
    /// 하나로 무릎이 놓일 자리가 정해집니다. 반복법은 매 프레임 수렴 상태가 달라
    /// <b>같은 자세에서 미세하게 떨리는데</b>, 해석해에는 그럴 여지가 없습니다.
    /// 다리 넷이 매 프레임 도는 코드라 이 차이가 그대로 화면에 남습니다.
    ///
    /// <b>무릎이 어디로 접히는가</b>는 삼각형만으로 정해지지 않습니다. 밑동과 목표를 잇는 축을
    /// 중심으로 무릎이 원을 그리며 어디든 갈 수 있기 때문입니다. 그래서 <b>극 방향</b>(pole)을
    /// 받아서 그쪽으로 접습니다. 개의 앞다리는 뒤로, 뒷다리는 앞으로 접히는데
    /// 그 차이가 이 인자 하나입니다.
    ///
    /// <b>닿지 않는 목표</b>는 억지로 늘이지 않고 <b>뻗을 수 있는 데까지</b> 폅니다.
    /// 늘이면 뼈 길이가 변해 눈에 띄고, 반대로 각도만 0으로 만들면 발이 목표에서
    /// 옆으로 새 버립니다. 방향은 목표 쪽으로 두고 거리만 자릅니다.
    /// </summary>
    public static class TwoBoneIK
    {
        // --- Public Types ---

        /// <summary>풀린 자세입니다. 두 위치와 도달 여부만 있으면 뼈를 세울 수 있습니다.</summary>
        public struct Solution
        {
            /// <summary>가운데 관절(무릎·팔꿈치)의 위치입니다.</summary>
            public Vector3 Joint;

            /// <summary>끝(발끝·손끝)의 위치입니다. 닿지 않는 목표라면 잘린 자리입니다.</summary>
            public Vector3 End;

            /// <summary>목표에 실제로 닿았는지 여부입니다. 다리가 뻗은 채로 끌려가는 것을 감지할 때 씁니다.</summary>
            public bool Reached;
        }

        // --- Constants ---

        /// <summary>0으로 보는 제곱 길이입니다.</summary>
        private const float Epsilon = 1e-8f;

        /// <summary>완전히 펴진·완전히 접힌 자리에서 살짝 물러나는 여유입니다. 각도가 튀는 것을 막습니다.</summary>
        private const float Margin = 1e-4f;

        // --- Public Methods ---

        /// <summary>
        /// 두 뼈를 목표 쪽으로 뻗습니다.
        /// </summary>
        /// <param name="root">밑동(엉덩이·어깨)의 위치</param>
        /// <param name="target">끝이 가려는 목표 위치</param>
        /// <param name="upperLength">위 뼈의 길이</param>
        /// <param name="lowerLength">아래 뼈의 길이</param>
        /// <param name="poleDirection">무릎이 접힐 방향(월드). 정규화되어 있지 않아도 됩니다.</param>
        /// <returns>무릎과 끝의 위치</returns>
        public static Solution Solve(Vector3 root, Vector3 target, float upperLength, float lowerLength, Vector3 poleDirection)
        {
            Solution solution;

            float upper = Mathf.Max(upperLength, Margin);
            float lower = Mathf.Max(lowerLength, Margin);

            Vector3 toTarget = target - root;
            float distance = toTarget.magnitude;

            // 목표가 밑동과 겹치면 방향을 정할 수 없습니다. 극 방향의 반대쪽으로 내려 둡니다.
            Vector3 direction = distance > Margin
                ? toTarget / distance
                : (poleDirection.sqrMagnitude > Epsilon ? -poleDirection.normalized : Vector3.down);

            float maxReach = upper + lower;
            float minReach = Mathf.Abs(upper - lower);

            float low = minReach + Margin;
            float high = maxReach - Margin;
            float reach = high > low ? Mathf.Clamp(distance, low, high) : maxReach * 0.5f;

            // 코사인 법칙 — 밑동에서 위 뼈가 목표 방향과 이루는 각입니다.
            float cosine = (upper * upper + reach * reach - lower * lower) / (2f * upper * reach);
            float hipAngle = Mathf.Acos(Mathf.Clamp(cosine, -1f, 1f)) * Mathf.Rad2Deg;

            // direction 을 (direction x pole) 축으로 양의 각만큼 돌리면 극 방향 쪽으로 다가갑니다.
            Vector3 axis = Vector3.Cross(direction, poleDirection);
            if (axis.sqrMagnitude < Epsilon) axis = FallbackAxis(direction);

            Vector3 upperDirection = Quaternion.AngleAxis(hipAngle, axis.normalized) * direction;

            solution.Joint = root + upperDirection * upper;
            solution.End = root + direction * reach;
            solution.Reached = distance <= maxReach && distance >= minReach;

            return solution;
        }

        /// <summary>
        /// 뼈 하나를 <b>로컬 +Z 가 다음 관절을 향하도록</b> 세우는 회전을 만듭니다.
        ///
        /// 뼈의 축을 +Z 로 정한 이유는 <see cref="Quaternion.LookRotation(Vector3,Vector3)"/> 를
        /// 그대로 쓸 수 있어서입니다. 메시가 +Y 로 서 있다면 자식 오브젝트에서 한 번 돌려 맞춥니다.
        /// (프리팹을 만드는 쪽에서 처리합니다)
        /// </summary>
        /// <param name="from">이 뼈가 시작하는 관절</param>
        /// <param name="to">이 뼈가 끝나는 관절</param>
        /// <param name="upHint">비틀림을 정하는 위쪽 힌트. 보통 무릎이 접히는 방향의 반대입니다.</param>
        /// <param name="fallback">방향을 정할 수 없을 때 쓸 회전</param>
        /// <returns>뼈에 넣을 월드 회전</returns>
        public static Quaternion BoneRotation(Vector3 from, Vector3 to, Vector3 upHint, Quaternion fallback)
        {
            Vector3 forward = to - from;
            if (forward.sqrMagnitude < Epsilon) return fallback;

            Vector3 up = upHint;
            if (up.sqrMagnitude < Epsilon || Vector3.Cross(forward, up).sqrMagnitude < Epsilon)
            {
                up = FallbackAxis(forward.normalized);
            }

            return Quaternion.LookRotation(forward, up);
        }

        // --- Private Methods ---

        /// <summary>주어진 방향과 나란하지 않은 축 하나를 고릅니다.</summary>
        /// <param name="direction">기준 방향(정규화되어 있어야 합니다)</param>
        /// <returns>기준과 나란하지 않은 축</returns>
        private static Vector3 FallbackAxis(Vector3 direction)
        {
            Vector3 axis = Vector3.Cross(direction, Vector3.up);
            if (axis.sqrMagnitude < Epsilon) axis = Vector3.Cross(direction, Vector3.right);

            return axis;
        }
    }
}
