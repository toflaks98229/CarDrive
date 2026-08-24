using NUnit.Framework;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Tests
{
    /// <summary>
    /// 2차 시스템이 <b>발산하지 않고</b>, <b>프레임률에 흔들리지 않는다</b>는 두 가지를 고정합니다.
    ///
    /// <b>왜 이 테스트인가.</b> 이 코드가 틀리면 증상이 "값이 조금 이상하다"가 아니라
    /// <b>로봇이 화면 밖으로 날아가는 것</b>으로 나타납니다. 그리고 그 폭발은 특정 프레임률에서만
    /// 나타나므로 손으로 확인하기가 가장 어려운 종류입니다. 60fps 로 개발하다가
    /// 무거운 씬에서 20fps 가 되는 순간 터지는 식입니다.
    ///
    /// 특히 <see cref="SecondOrderCoefficients.Resolve"/> 의 극점 맞추기는 <b>유도한 식이 맞는지</b>를
    /// 눈으로 볼 방법이 없습니다. 그래서 여기서 재귀식의 특성다항식을 직접 세워
    /// 연속계의 극점 exp(sT) 와 같은지 봅니다.
    ///
    /// 순수 계산이라 씬 없이 EditMode 에서 돕니다.
    /// </summary>
    public class SecondOrderDynamicsTests
    {
        // --- Tests : 기본 성질 ---

        /// <summary>가만히 있는 목표를 오래 따라가면 그 값에 앉아야 합니다.</summary>
        [Test]
        public void 상수_입력이면_그_값으로_수렴한다()
        {
            SecondOrderDynamics system = new SecondOrderDynamics(new SecondOrderSettings(3f, 1f, 0f), 0f);

            for (int i = 0; i < 600; i++) system.Update(1f / 60f, 1f);

            Assert.AreEqual(1f, system.Value, 0.001f, "목표에 앉지 못했습니다.");
            Assert.AreEqual(0f, system.Velocity, 0.01f, "멈추지 않고 계속 움직이고 있습니다.");
        }

        /// <summary>
        /// 감쇠비가 1이면 <b>넘어서지 않아야</b> 합니다. 이것이 임계 감쇠의 정의입니다.
        /// 넘어선다면 계수 계산이나 적분 순서가 틀린 것입니다.
        /// </summary>
        [Test]
        public void 임계감쇠는_목표를_넘지_않는다()
        {
            SecondOrderDynamics system = new SecondOrderDynamics(new SecondOrderSettings(2f, 1f, 0f), 0f);
            float peak = 0f;

            for (int i = 0; i < 600; i++) peak = Mathf.Max(peak, system.Update(1f / 60f, 1f));

            Assert.LessOrEqual(peak, 1.005f, "임계 감쇠인데 목표를 넘어섰습니다.");
        }

        /// <summary>감쇠비가 작으면 반대로 <b>넘어서야</b> 합니다. 튀지 않으면 관성이 없는 것입니다.</summary>
        [Test]
        public void 감쇠가_작으면_목표를_넘어선다()
        {
            SecondOrderDynamics system = new SecondOrderDynamics(new SecondOrderSettings(2f, 0.2f, 0f), 0f);
            float peak = 0f;

            for (int i = 0; i < 600; i++) peak = Mathf.Max(peak, system.Update(1f / 60f, 1f));

            Assert.Greater(peak, 1.1f, "감쇠가 작은데 목표를 넘어서지 않았습니다.");
        }

        /// <summary>
        /// 초기 반응이 음수면 <b>목표와 반대로 먼저 움직여야</b> 합니다. (예비 동작)
        /// 이 항이 k3 이고, 없으면 로봇의 움직임에서 "준비하는 느낌"이 사라집니다.
        /// </summary>
        [Test]
        public void 초기반응이_음수면_먼저_반대로_움직인다()
        {
            SecondOrderDynamics system = new SecondOrderDynamics(new SecondOrderSettings(2f, 1f, -3f), 0f);

            system.Update(1f / 60f, 1f);
            float afterTwoSteps = system.Update(1f / 60f, 1f);

            Assert.Less(afterTwoSteps, 0f, "예비 동작이 나오지 않았습니다.");
        }

        // --- Tests : 안정성 ---

        /// <summary>
        /// 시간 간격이 시스템 주기보다 <b>훨씬 클 때</b>도 값이 폭발하면 안 됩니다.
        /// 극점 맞추기가 없으면 여기서 무한대가 됩니다.
        /// </summary>
        /// <param name="frequency">시스템의 고유 진동수</param>
        /// <param name="damping">감쇠비</param>
        [TestCase(20f, 0.5f)]
        [TestCase(20f, 1f)]
        [TestCase(20f, 2f)]
        [TestCase(120f, 0.3f)]
        public void 큰_시간간격에서도_발산하지_않는다(float frequency, float damping)
        {
            SecondOrderDynamics system = new SecondOrderDynamics(new SecondOrderSettings(frequency, damping, 0f), 0f);

            for (int i = 0; i < 400; i++)
            {
                // 목표를 매번 뒤집어 가장 나쁜 입력을 줍니다.
                system.Update(0.5f, i % 2 == 0 ? 1f : -1f);
            }

            Assert.IsFalse(float.IsNaN(system.Value), "값이 NaN 이 되었습니다.");
            Assert.Less(Mathf.Abs(system.Value), 2f, "입력 범위를 크게 벗어났습니다. 발산하고 있습니다.");
        }

        /// <summary>
        /// 프레임률이 네 배 달라도 <b>같은 시각에 같은 자리</b>에 있어야 합니다.
        /// 이것이 어긋나면 무거운 씬에서 로봇의 움직임이 달라집니다.
        /// </summary>
        [Test]
        public void 프레임률이_달라도_같은_시각에_같은_곳에_있다()
        {
            SecondOrderSettings settings = new SecondOrderSettings(1f, 1f, 0f);

            float fast = Simulate(settings, 1f / 120f, 0.3f);
            float slow = Simulate(settings, 1f / 30f, 0.3f);

            Assert.AreEqual(fast, slow, 0.03f, "프레임률에 따라 결과가 달라집니다.");
        }

        // --- Tests : 극점 맞추기 ---

        /// <summary>
        /// 극점 맞추기가 만든 k1·k2 를 재귀식에 넣었을 때, 그 특성다항식의 근이
        /// 연속계 극점을 z = exp(sT) 로 옮긴 것과 <b>정확히 같아야</b> 합니다.
        ///
        /// semi-implicit 재귀식에서 속도를 소거하면 특성다항식은 이렇습니다.
        /// <code>z² − (2 − T·k1/k2 − T²/k2)·z + (1 − T·k1/k2)</code>
        /// 여기서 뽑은 α·β 를 목표값과 비교합니다. 유도가 틀리면 여기서 잡힙니다.
        /// </summary>
        /// <param name="frequency">시스템의 고유 진동수</param>
        /// <param name="damping">감쇠비</param>
        /// <param name="dt">시간 간격</param>
        [TestCase(10f, 0.5f, 1f / 60f)]
        [TestCase(10f, 1f, 1f / 60f)]
        [TestCase(10f, 2f, 0.1f)]
        [TestCase(30f, 0.25f, 1f / 30f)]
        public void 극점_맞추기가_연속계_극점과_일치한다(float frequency, float damping, float dt)
        {
            SecondOrderCoefficients coefficients = new SecondOrderCoefficients(new SecondOrderSettings(frequency, damping, 0f));
            coefficients.Resolve(dt, out float k1, out float k2);

            // 이 자리는 극점 맞추기 갈래여야 합니다. (아니라면 테스트가 아무것도 확인하지 않습니다)
            float w = 2f * Mathf.PI * frequency;
            Assert.GreaterOrEqual(w * dt, damping, "이 입력은 극점 맞추기 갈래로 가지 않습니다. 테스트 값이 잘못되었습니다.");

            float actualAlpha = 2f - dt * k1 / k2 - dt * dt / k2;
            float actualBeta = 1f - dt * k1 / k2;

            float d = w * Mathf.Sqrt(Mathf.Abs(damping * damping - 1f));
            float decay = Mathf.Exp(-damping * w * dt);

            float expectedBeta = decay * decay;
            float expectedAlpha = 2f * decay * (damping <= 1f ? Mathf.Cos(dt * d) : Cosh(dt * d));

            Assert.AreEqual(expectedBeta, actualBeta, 1e-4f, "극점의 크기(감쇠)가 연속계와 다릅니다.");
            Assert.AreEqual(expectedAlpha, actualAlpha, 1e-4f, "극점의 위상(진동)이 연속계와 다릅니다.");
        }

        // --- Tests : 충격 ---

        /// <summary>
        /// 목표가 0인 시스템을 때리면 <b>몇 번 넘나들다 제자리로</b> 돌아와야 합니다.
        /// 이것이 로봇이 얻어맞고 휘청이는 움직임의 전부입니다.
        /// </summary>
        [Test]
        public void 속도를_때리면_흔들리다_돌아온다()
        {
            SecondOrderDynamics system = new SecondOrderDynamics(new SecondOrderSettings(2.4f, 0.35f, 0f), 0f);
            system.AddVelocity(300f);

            int crossings = 0;
            float previous = 0f;

            for (int i = 0; i < 300; i++)
            {
                float value = system.Update(1f / 60f, 0f);

                if ((previous < 0f && value >= 0f) || (previous > 0f && value <= 0f)) crossings++;
                previous = value;
            }

            Assert.GreaterOrEqual(crossings, 2, "넘나들지 않았습니다. 흔들림이 아니라 그냥 되돌아왔습니다.");
            Assert.AreEqual(0f, system.Value, 0.01f, "제자리로 돌아오지 않았습니다.");
        }

        /// <summary>
        /// <see cref="SecondOrderDynamics.ImpulseForPeak"/> 가 계산한 속도를 넣으면
        /// <b>실제로 그 최댓값</b>이 나와야 합니다.
        ///
        /// <b>왜 이 테스트인가.</b> 이 식을 처음 유도했을 때 지수의 √(1−ζ²) 를 빠뜨리고
        /// ω 대신 ωd 로 나눴습니다. 눈으로는 "좀 덜 흔들리네" 정도라 알아채기 어려웠고,
        /// 감쇠비를 0.5로 올리자 <b>17% 나 모자랐습니다.</b> 숫자로 확인하지 않으면 지나갑니다.
        /// </summary>
        /// <param name="frequency">시스템의 고유 진동수</param>
        /// <param name="damping">감쇠비</param>
        [TestCase(2.4f, 0.35f)]
        [TestCase(1.3f, 0.25f)]
        [TestCase(0.9f, 0.22f)]
        [TestCase(2f, 0.5f)]
        [TestCase(3f, 0.1f)]
        public void 충격의_최댓값이_예측과_맞는다(float frequency, float damping)
        {
            const float Target = 15f;

            SecondOrderSettings settings = new SecondOrderSettings(frequency, damping, 0f);
            SecondOrderDynamics system = new SecondOrderDynamics(settings, 0f);

            system.AddVelocity(SecondOrderDynamics.ImpulseForPeak(settings, Target));

            float peak = 0f;
            for (int i = 0; i < 2000; i++) peak = Mathf.Max(peak, Mathf.Abs(system.Update(1f / 240f, 0f)));

            Assert.AreEqual(Target, peak, Target * 0.05f, "최댓값이 예측과 다릅니다.");
        }

        // --- Tests : 벡터 · 회전 ---

        /// <summary>벡터판도 목표에 앉아야 합니다. 축마다 따로 새지 않는지 봅니다.</summary>
        [Test]
        public void 벡터판도_목표로_수렴한다()
        {
            SecondOrderDynamics3 system = new SecondOrderDynamics3(new SecondOrderSettings(3f, 1f, 0f), Vector3.zero);
            Vector3 target = new Vector3(3f, -2f, 7f);

            for (int i = 0; i < 600; i++) system.Update(1f / 60f, target);

            Assert.Less(Vector3.Distance(system.Value, target), 0.01f, "목표에 앉지 못했습니다.");
        }

        /// <summary>
        /// 회전판은 목표가 <b>반구를 넘어가도</b> 엉뚱한 쪽으로 돌지 않아야 합니다.
        /// 쿼터니언 성분에 직접 용수철을 걸면 여기서 180도 튑니다.
        /// </summary>
        [Test]
        public void 회전판은_반구를_넘어도_수렴한다()
        {
            Quaternion target = Quaternion.Euler(0f, 179f, 0f);
            SecondOrderRotation system = new SecondOrderRotation(new SecondOrderSettings(4f, 1f, 0f), Quaternion.identity);

            for (int i = 0; i < 600; i++) system.Update(1f / 60f, target);

            Assert.Less(Quaternion.Angle(system.Value, target), 1f, "목표 회전에 도달하지 못했습니다.");
        }

        // --- Helpers ---

        /// <summary>0에서 1로 뛴 목표를 정해진 시간만큼 따라가게 하고 마지막 값을 돌려줍니다.</summary>
        /// <param name="settings">시스템 설정</param>
        /// <param name="dt">시간 간격</param>
        /// <param name="duration">전체 시간</param>
        /// <returns>마지막 값</returns>
        private static float Simulate(SecondOrderSettings settings, float dt, float duration)
        {
            SecondOrderDynamics system = new SecondOrderDynamics(settings, 0f);
            int steps = Mathf.RoundToInt(duration / dt);

            for (int i = 0; i < steps; i++) system.Update(dt, 1f);

            return system.Value;
        }

        /// <summary>쌍곡코사인입니다.</summary>
        /// <param name="x">입력</param>
        /// <returns>cosh(x)</returns>
        private static float Cosh(float x)
        {
            return (Mathf.Exp(x) + Mathf.Exp(-x)) * 0.5f;
        }
    }
}
