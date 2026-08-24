using NUnit.Framework;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Tests
{
    /// <summary>
    /// Verlet 사슬이 <b>줄 길이를 지키고</b>, <b>순간이동에 채찍질하지 않는다</b>는 것을 고정합니다.
    ///
    /// <b>왜 이 테스트인가.</b> 사슬이 어긋나는 증상은 대부분 눈에 잘 띄지 않습니다.
    /// 꼬리가 1cm 늘어나도 아무도 모릅니다. 그런데 같은 원인(제약이 수렴하지 않음)이
    /// 로봇이 빠르게 회전하는 순간에는 <b>꼬리가 몸을 관통하거나 폭발하는</b> 것으로 나타납니다.
    /// 눈으로 확인하려면 그 순간을 재현해야 하는데, 그 확인은 반복되지 않습니다.
    ///
    /// <b>줄 길이는 완전히 정확해지지 않습니다.</b> 마디를 하나씩 차례로 고치는 방식이라
    /// 뒤쪽 마디를 고치면 앞쪽이 조금 어긋나고, 중력이 계속 당기므로 <b>늘어난 채로 균형</b>을 이룹니다.
    /// 남는 오차는 반복 횟수에 반비례합니다. 마디 0.2m 기준으로 4회에 2.2mm, 8회에 0.9mm,
    /// 16회에 0.3mm 입니다. 그래서 여기서는 <b>정확히 같은지</b>가 아니라
    /// <b>1% 안에 있는지</b>를 봅니다. 이 값이 갑자기 커지면 제약이 깨진 것입니다.
    /// </summary>
    public class VerletChainTests
    {
        /// <summary>테스트에 쓰는 마디 길이입니다.</summary>
        private const float SegmentLength = 0.2f;

        /// <summary>테스트에 쓰는 점 개수입니다.</summary>
        private const int PointCount = 6;

        // --- Tests ---

        /// <summary>중력에 늘어져도 마디 길이는 그대로여야 합니다.</summary>
        [Test]
        public void 중력_아래에서도_마디_길이가_유지된다()
        {
            VerletChain chain = CreateHangingChain();

            for (int i = 0; i < 200; i++)
            {
                chain.Step(1f / 60f, Vector3.up * -9.81f, 0.05f);
                chain.Solve(8, 180f);
            }

            AssertSegmentLengths(chain, SegmentLength * 0.01f);
        }

        /// <summary>고정된 점은 어떤 힘에도 움직이지 않아야 합니다.</summary>
        [Test]
        public void 고정된_점은_움직이지_않는다()
        {
            VerletChain chain = CreateHangingChain();
            Vector3 pin = new Vector3(1f, 2f, 3f);

            chain.Pin(0, pin);

            for (int i = 0; i < 60; i++)
            {
                chain.Step(1f / 60f, Vector3.up * -9.81f, 0f);
                chain.Solve(4, 180f);
            }

            Assert.AreEqual(0f, Vector3.Distance(chain.GetPosition(0), pin), 1e-5f, "고정된 점이 끌려갔습니다.");
        }

        /// <summary>옆으로 뻗어 있던 사슬은 중력에 아래로 늘어져야 합니다.</summary>
        [Test]
        public void 중력_아래에서_아래로_늘어진다()
        {
            VerletChain chain = new VerletChain(PointCount, Vector3.zero, new Vector3(SegmentLength, 0f, 0f));

            for (int i = 0; i < 400; i++)
            {
                chain.Step(1f / 60f, Vector3.up * -9.81f, 0.1f);
                chain.Solve(8, 180f);
            }

            float tipHeight = chain.GetPosition(PointCount - 1).y;
            float totalLength = SegmentLength * (PointCount - 1);

            Assert.Less(tipHeight, -totalLength * 0.5f, "사슬이 늘어지지 않았습니다.");
            AssertSegmentLengths(chain, SegmentLength * 0.01f);
        }

        /// <summary>
        /// 순간이동은 <b>속도를 만들지 않아야</b> 합니다.
        /// 지난 위치를 함께 옮기지 않으면, 사슬은 그 거리를 한 프레임에 이동한 것으로 보고 날아갑니다.
        /// </summary>
        [Test]
        public void 순간이동은_속도를_만들지_않는다()
        {
            VerletChain chain = CreateHangingChain();
            Vector3 delta = new Vector3(100f, 40f, -70f);

            Vector3[] before = new Vector3[PointCount];
            for (int i = 0; i < PointCount; i++) before[i] = chain.GetPosition(i);

            chain.Teleport(delta);
            chain.Step(1f / 60f, Vector3.zero, 0f);
            chain.Solve(4, 180f);

            for (int i = 0; i < PointCount; i++)
            {
                Assert.AreEqual(0f, Vector3.Distance(chain.GetPosition(i), before[i] + delta), 1e-3f,
                    i + "번 점이 순간이동 뒤에 스스로 움직였습니다.");
            }
        }

        /// <summary>땅 아래로 내려간 점은 끌어올려져야 합니다.</summary>
        [Test]
        public void 평면_아래로_내려가지_않는다()
        {
            VerletChain chain = CreateHangingChain();
            float floor = -0.35f;

            for (int i = 0; i < 200; i++)
            {
                chain.Step(1f / 60f, Vector3.up * -9.81f, 0.05f);
                chain.Solve(4, 180f);
                chain.CollideWithPlane(new Vector3(0f, floor, 0f), Vector3.up, 0.5f);
            }

            for (int i = 1; i < PointCount; i++)
            {
                Assert.GreaterOrEqual(chain.GetPosition(i).y, floor - 1e-4f, i + "번 점이 바닥을 뚫었습니다.");
            }
        }

        /// <summary>
        /// 밑동을 마구 휘둘러도 <b>이웃한 두 마디가 한계 이상으로 꺾이지 않아야</b> 합니다.
        /// 이것이 무너지면 꼬리가 자기 몸을 접어 관통합니다.
        ///
        /// 마지막 한 프레임만 보면 안 됩니다. 흔들기를 멈춘 뒤에는 어차피 펴져 있어
        /// 한계를 지우고 돌려도 통과합니다. 그래서 <b>매 프레임 최대 각을 재</b> 둡니다.
        /// 같은 흔들기에서 한계를 풀면 58도까지 꺾이므로, 이 확인은 실제로 무언가를 붙잡습니다.
        /// </summary>
        [Test]
        public void 꺾임_한계를_넘지_않는다()
        {
            VerletChain chain = CreateHangingChain();
            float limit = 25f;
            float worst = 0f;

            for (int i = 0; i < 240; i++)
            {
                // 밑동을 원으로 돌려 사슬을 채찍처럼 흔듭니다.
                float angle = i * 0.4f;
                chain.Pin(0, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.6f);

                chain.Step(1f / 60f, Vector3.up * -9.81f, 0.02f);
                chain.Solve(6, limit);

                worst = Mathf.Max(worst, WorstBendAngle(chain));
            }

            Assert.LessOrEqual(worst, limit + 0.5f, "관절이 한계보다 꺾였습니다.");
        }

        // --- Helpers ---

        /// <summary>아래로 곧게 뻗은 사슬을 만듭니다. 0번 점은 원점에 고정되어 있습니다.</summary>
        /// <returns>만들어진 사슬</returns>
        private static VerletChain CreateHangingChain()
        {
            return new VerletChain(PointCount, Vector3.zero, new Vector3(0f, -SegmentLength, 0f));
        }

        /// <summary>지금 가장 크게 꺾인 관절의 각도를 돌려줍니다.</summary>
        /// <param name="chain">확인할 사슬</param>
        /// <returns>가장 큰 꺾임 각도(도)</returns>
        private static float WorstBendAngle(VerletChain chain)
        {
            float worst = 0f;

            for (int i = 1; i < chain.Count - 1; i++)
            {
                Vector3 a = chain.GetPosition(i) - chain.GetPosition(i - 1);
                Vector3 b = chain.GetPosition(i + 1) - chain.GetPosition(i);

                worst = Mathf.Max(worst, Vector3.Angle(a, b));
            }

            return worst;
        }

        /// <summary>모든 마디가 목표 길이인지 확인합니다.</summary>
        /// <param name="chain">확인할 사슬</param>
        /// <param name="tolerance">허용 오차</param>
        private static void AssertSegmentLengths(VerletChain chain, float tolerance)
        {
            for (int i = 0; i < chain.Count - 1; i++)
            {
                float length = Vector3.Distance(chain.GetPosition(i), chain.GetPosition(i + 1));

                Assert.AreEqual(chain.GetRestLength(i), length, tolerance, i + "번 마디의 길이가 달라졌습니다.");
            }
        }
    }
}
