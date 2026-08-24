using UnityEngine;

namespace CarDrive.Common
{
    /// <summary>
    /// 점을 줄로 이어 놓고 <b>Verlet 적분</b>으로 흔드는 사슬입니다. 꼬리·안테나·케이블에 씁니다.
    ///
    /// <b>Verlet 이 무엇인가.</b> 속도를 따로 들고 있지 않고, <b>지난 위치와의 차이</b>를 속도로 씁니다.
    /// <code>
    /// x(n+1) = x(n) + (x(n) − x(n−1))·(T/T_prev)·(1−감쇠) + a·T²
    /// </code>
    /// 뒤의 (T/T_prev) 가 <b>시간 보정</b>입니다. 원래 Verlet 은 시간 간격이 일정하다고 가정하는데,
    /// 게임의 프레임은 매번 다릅니다. 보정 없이 쓰면 프레임이 흔들릴 때마다 사슬이
    /// 이유 없이 튀거나 죽습니다.
    ///
    /// <b>왜 용수철이 아니라 이것인가.</b> 마디를 용수철로 이으면 뻣뻣하게 만들수록
    /// (진짜 줄처럼 보이게 하려면 아주 뻣뻣해야 합니다) 적분이 터집니다.
    /// Verlet 에서는 <b>거리 조건을 위치로 직접 밀어서</b> 맞춥니다. 그리고 속도가 위치의 차이로
    /// 정의되어 있으므로, 위치를 밀면 <b>속도가 자동으로 함께 고쳐집니다.</b>
    /// 조건을 아무리 세게 맞춰도 에너지가 새로 생기지 않는다는 뜻입니다.
    /// (Jakobsen 의 위치 기반 제약 방식 — Wikipedia "Verlet integration" 의 Constraints 절)
    ///
    /// <b>2차 시스템과의 역할 분담.</b> <see cref="SecondOrderDynamics"/> 는 <b>하나의 값</b>이
    /// 목표를 따라가는 방식을 정합니다. 이쪽은 <b>여러 점이 서로 묶여</b> 있을 때 씁니다.
    /// 로봇에서는 몸통·머리·발이 앞의 것을, 꼬리가 이것을 씁니다.
    ///
    /// <b>쓰는 순서</b>는 언제나 같습니다.
    /// <code>
    /// chain.Pin(0, 꼬리가 붙은 자리);          // 매달린 점을 옮기고
    /// chain.Step(dt, 중력, 감쇠);              // 한 걸음 적분한 뒤
    /// chain.Solve(반복 횟수, 최대 꺾임 각도);   // 줄 길이와 꺾임을 맞춥니다
    /// </code>
    /// </summary>
    public class VerletChain
    {
        // --- Private Member Variables ---

        /// <summary>각 점의 현재 위치입니다.</summary>
        private readonly Vector3[] positions;

        /// <summary>각 점의 지난 위치입니다. 속도가 여기에 들어 있습니다.</summary>
        private readonly Vector3[] previous;

        /// <summary>고정된 점인지 여부입니다. 고정된 점은 적분도 제약도 움직이지 못합니다.</summary>
        private readonly bool[] pinned;

        /// <summary>이웃한 두 점 사이의 목표 거리입니다. 길이는 점 개수 − 1 입니다.</summary>
        private readonly float[] restLengths;

        /// <summary>지난 프레임의 시간 간격입니다. 시간 보정에 씁니다.</summary>
        private float previousStep;

        // --- Constants ---

        /// <summary>0으로 보는 제곱 길이입니다.</summary>
        private const float Epsilon = 1e-10f;

        /// <summary>시간 보정 비율의 상한입니다. 프레임 하나가 통째로 밀렸을 때 사슬이 폭발하는 것을 막습니다.</summary>
        private const float MaxStepRatio = 3f;

        // --- Constructors ---

        /// <summary>
        /// 곧게 뻗은 사슬을 만듭니다. 0번 점이 뿌리이고, 기본으로 고정되어 있습니다.
        /// </summary>
        /// <param name="pointCount">점의 개수. 2 이상이어야 합니다.</param>
        /// <param name="origin">0번 점의 위치</param>
        /// <param name="segment">한 마디의 방향과 길이</param>
        public VerletChain(int pointCount, Vector3 origin, Vector3 segment)
        {
            int count = Mathf.Max(2, pointCount);

            positions = new Vector3[count];
            previous = new Vector3[count];
            pinned = new bool[count];
            restLengths = new float[count - 1];

            for (int i = 0; i < count; i++)
            {
                positions[i] = origin + segment * i;
                previous[i] = positions[i];
            }

            for (int i = 0; i < restLengths.Length; i++) restLengths[i] = segment.magnitude;

            pinned[0] = true;
            previousStep = 0f;
        }

        // --- Public Properties ---

        /// <summary>점의 개수입니다.</summary>
        public int Count { get { return positions.Length; } }

        // --- Public Methods : 읽기 · 쓰기 ---

        /// <summary>점 하나의 위치를 읽습니다.</summary>
        /// <param name="index">점 번호</param>
        /// <returns>월드 위치</returns>
        public Vector3 GetPosition(int index)
        {
            return positions[index];
        }

        /// <summary>이웃한 두 점 사이의 목표 거리를 읽습니다.</summary>
        /// <param name="index">마디 번호 (0 이상, 점 개수 − 1 미만)</param>
        /// <returns>목표 거리</returns>
        public float GetRestLength(int index)
        {
            return restLengths[index];
        }

        /// <summary>점을 고정할지 정합니다. 고정된 점은 <see cref="Pin"/> 으로만 움직입니다.</summary>
        /// <param name="index">점 번호</param>
        /// <param name="value">고정 여부</param>
        public void SetPinned(int index, bool value)
        {
            pinned[index] = value;
        }

        /// <summary>
        /// 고정된 점을 옮깁니다. 매달린 자리(꼬리 밑동)를 매 프레임 여기로 밀어 넣습니다.
        ///
        /// 지난 위치는 건드리지 않습니다. 그래야 뒤따르는 점들이 <b>거리 조건을 통해</b>
        /// 끌려오면서 속도를 얻습니다. 이것이 꼬리가 몸을 따라 휘는 힘의 전부입니다.
        /// </summary>
        /// <param name="index">점 번호</param>
        /// <param name="position">옮길 위치</param>
        public void Pin(int index, Vector3 position)
        {
            pinned[index] = true;
            positions[index] = position;
        }

        /// <summary>
        /// 점 하나를 <b>속도 없이</b> 그 자리에 놓습니다. 고정 여부는 건드리지 않습니다.
        ///
        /// <see cref="Pin"/> 과 다릅니다. Pin 은 위치만 바꾸므로 지난 위치와의 차이가 그대로 남아
        /// <b>그 차이가 속도로 읽힙니다.</b> 사슬을 지금 상태에서 이어받아 시작할 때는
        /// 속도가 0이어야 하므로 지난 위치까지 함께 옮깁니다.
        /// </summary>
        /// <param name="index">점 번호</param>
        /// <param name="position">놓을 자리</param>
        public void Place(int index, Vector3 position)
        {
            positions[index] = position;
            previous[index] = position;
        }

        /// <summary>
        /// 사슬 전체를 <b>속도 없이</b> 옮깁니다. 로봇을 순간이동시킬 때 씁니다.
        /// 이것 없이 옮기면 사슬이 한 프레임 만에 그 거리를 "움직인 것"으로 보고 채찍처럼 튑니다.
        /// </summary>
        /// <param name="delta">옮길 거리</param>
        public void Teleport(Vector3 delta)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] += delta;
                previous[i] += delta;
            }
        }

        // --- Public Methods : 적분 ---

        /// <summary>
        /// 한 걸음 적분합니다. 고정된 점은 속도를 갖지 않도록 지난 위치를 현재로 맞춰 둡니다.
        /// </summary>
        /// <param name="dt">시간 간격(초)</param>
        /// <param name="acceleration">모든 점에 걸리는 가속도. 보통 중력입니다.</param>
        /// <param name="damping">속도를 깎는 비율 (0~1). 0이면 영원히 흔들립니다.</param>
        public void Step(float dt, Vector3 acceleration, float damping)
        {
            if (dt <= 0f) return;

            // 첫 걸음에는 비교할 지난 간격이 없습니다. 같은 간격이었다고 봅니다.
            float ratio = previousStep > 0f ? Mathf.Min(dt / previousStep, MaxStepRatio) : 1f;
            float keep = 1f - Mathf.Clamp01(damping);
            Vector3 push = acceleration * (dt * dt);

            for (int i = 0; i < positions.Length; i++)
            {
                if (pinned[i])
                {
                    previous[i] = positions[i];
                    continue;
                }

                Vector3 velocity = (positions[i] - previous[i]) * (ratio * keep);

                previous[i] = positions[i];
                positions[i] += velocity + push;
            }

            previousStep = dt;
        }

        // --- Public Methods : 제약 ---

        /// <summary>
        /// 줄 길이와 꺾임 한계를 맞춥니다.
        ///
        /// <b>왜 여러 번 반복하는가.</b> 한 마디를 고치면 옆 마디가 다시 어긋납니다.
        /// 전부를 한 번에 푸는 방법도 있지만(선형계 풀이) 비용이 크고, 사슬처럼 마디가 짧게 이어진
        /// 구조에서는 <b>몇 번 반복하면 눈에 띄지 않을 만큼</b> 수렴합니다.
        /// 3~6회면 충분하고, 늘릴수록 줄이 뻣뻣해집니다.
        /// </summary>
        /// <param name="iterations">반복 횟수</param>
        /// <param name="maxBendDegrees">이웃한 두 마디가 꺾일 수 있는 최대 각도. 180 이상이면 제한하지 않습니다.</param>
        public void Solve(int iterations, float maxBendDegrees)
        {
            int passes = Mathf.Max(1, iterations);

            for (int pass = 0; pass < passes; pass++)
            {
                SolveDistances();
                if (maxBendDegrees < 180f) SolveBendLimit(maxBendDegrees);
            }
        }

        /// <summary>
        /// 사슬을 정해진 방향으로 <b>펴려는 힘</b>을 겁니다.
        ///
        /// 이것이 없으면 꼬리는 그냥 아래로 늘어집니다. 로봇의 꼬리는 기계로 들려 있어야 하므로,
        /// 뿌리에서부터 곧게 뻗은 자세를 목표로 두고 조금씩 당깁니다.
        /// 위치를 직접 옮기지만 Verlet 이라 <b>그 변위가 곧 속도</b>가 되므로,
        /// 세게 걸면 꼬리가 스스로 튀어 오르는 움직임까지 나옵니다.
        /// </summary>
        /// <param name="direction">펴려는 방향(월드, 정규화되어 있지 않아도 됩니다)</param>
        /// <param name="stiffness">한 프레임에 목표 자세로 다가가는 비율 (0~1)</param>
        public void ApplyRestDirection(Vector3 direction, float stiffness)
        {
            if (stiffness <= 0f) return;
            if (direction.sqrMagnitude < Epsilon) return;

            Vector3 dir = direction.normalized;
            float t = Mathf.Clamp01(stiffness);

            for (int i = 1; i < positions.Length; i++)
            {
                if (pinned[i]) continue;

                Vector3 target = positions[i - 1] + dir * restLengths[i - 1];
                positions[i] = Vector3.Lerp(positions[i], target, t);
            }
        }

        /// <summary>
        /// 평면 아래로 내려간 점을 끌어올립니다. 꼬리가 땅을 파고드는 것을 막습니다.
        ///
        /// <b>위치를 밀어내는 것만으로는 부족합니다.</b> Verlet 에서 속도는 지난 위치와의 차이라,
        /// 점을 위로 밀면 <b>그만큼 위로 튀어 오르는 속도가 생깁니다.</b> 그대로 두면 꼬리가
        /// 바닥에서 통통 튀다가 결국 밑동보다 높이 쌓입니다. (실제로 그렇게 나왔습니다)
        ///
        /// 그래서 밀어낸 뒤 <b>지난 위치를 다시 잡아</b> 법선 방향 속도를 없애고
        /// 접선 방향만 마찰만큼 깎아 남깁니다. 마찰 0이면 미끄러지고, 1이면 붙습니다.
        /// </summary>
        /// <param name="pointOnPlane">평면 위의 한 점</param>
        /// <param name="normal">평면의 법선(위쪽)</param>
        /// <param name="friction">닿은 점의 접선 속도를 깎는 비율 (0~1)</param>
        public void CollideWithPlane(Vector3 pointOnPlane, Vector3 normal, float friction)
        {
            if (normal.sqrMagnitude < Epsilon) return;

            Vector3 n = normal.normalized;
            float slide = 1f - Mathf.Clamp01(friction);

            for (int i = 0; i < positions.Length; i++)
            {
                if (pinned[i]) continue;

                float depth = Vector3.Dot(positions[i] - pointOnPlane, n);
                if (depth >= 0f) continue;

                positions[i] -= n * depth;

                Vector3 velocity = positions[i] - previous[i];
                Vector3 tangent = velocity - n * Vector3.Dot(velocity, n);

                previous[i] = positions[i] - tangent * slide;
            }
        }

        // --- Private Methods ---

        /// <summary>이웃한 두 점의 거리를 목표 거리로 되돌립니다.</summary>
        private void SolveDistances()
        {
            for (int i = 0; i < restLengths.Length; i++)
            {
                int a = i;
                int b = i + 1;

                Vector3 delta = positions[b] - positions[a];
                float lengthSqr = delta.sqrMagnitude;
                if (lengthSqr < Epsilon) continue;

                float length = Mathf.Sqrt(lengthSqr);
                Vector3 correction = delta * ((length - restLengths[i]) / length);

                // 한쪽이 고정되어 있으면 반대쪽이 전부 감당합니다.
                bool aFixed = pinned[a];
                bool bFixed = pinned[b];

                if (aFixed && bFixed) continue;

                if (aFixed)
                {
                    positions[b] -= correction;
                }
                else if (bFixed)
                {
                    positions[a] += correction;
                }
                else
                {
                    positions[a] += correction * 0.5f;
                    positions[b] -= correction * 0.5f;
                }
            }
        }

        /// <summary>
        /// 이웃한 두 마디가 이루는 각을 한계 안으로 되돌립니다.
        ///
        /// 바깥쪽 점을 <b>가운데 점을 축으로 회전</b>시켜 되돌리므로 마디 길이가 변하지 않습니다.
        /// 그래서 거리 제약과 싸우지 않습니다.
        /// </summary>
        /// <param name="maxDegrees">허용하는 최대 꺾임 각도</param>
        private void SolveBendLimit(float maxDegrees)
        {
            for (int i = 1; i < positions.Length - 1; i++)
            {
                if (pinned[i + 1]) continue;

                Vector3 a = positions[i] - positions[i - 1];
                Vector3 b = positions[i + 1] - positions[i];
                if (a.sqrMagnitude < Epsilon || b.sqrMagnitude < Epsilon) continue;

                float angle = Vector3.Angle(a, b);
                if (angle <= maxDegrees) continue;

                Vector3 axis = Vector3.Cross(a, b);
                if (axis.sqrMagnitude < Epsilon) continue;

                // a x b 축으로 음의 각만큼 돌리면 b 가 a 쪽으로 다가갑니다.
                Quaternion correction = Quaternion.AngleAxis(-(angle - maxDegrees), axis.normalized);
                positions[i + 1] = positions[i] + correction * b;
            }
        }
    }
}
