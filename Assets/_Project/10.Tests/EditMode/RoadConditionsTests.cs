using NUnit.Framework;
using UnityEngine;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 날씨가 <b>주행에</b> 주는 영향에 대한 EditMode 테스트입니다.
    ///
    /// <b>이 파일이 존재할 수 있다는 사실 자체가 요점입니다.</b>
    ///
    /// 예전에는 <see cref="Powertrain"/>과 <see cref="WheelGripTuner"/>가 계산 본문에서
    /// <c>WeatherSystem.GetFuelConsumption()</c> · <c>GetRoadSlipperiness()</c>를
    /// 정적으로 불렀습니다. 순수 산술인데도 <b>씬에 WeatherSystem 이 있어야만</b>
    /// 폭우의 값이 나왔고, 없으면 언제나 "맑음"이었습니다. 그래서 이 두 계산은
    /// 이 프로젝트에서 유일하게 검증할 수 없는 주행 로직이었습니다.
    ///
    /// 이제 노면 상태를 <see cref="IRoadConditions"/>로 주입받으므로,
    /// 가짜 노면 하나로 맑은 날과 폭우를 나란히 놓고 확인할 수 있습니다.
    /// </summary>
    public class RoadConditionsTests
    {
        /// <summary>
        /// 시험용 노면입니다. 값 두 개짜리 계약이라 이만큼이면 충분합니다.
        /// </summary>
        private sealed class FakeRoad : IRoadConditions
        {
            public float Slipperiness { get; set; }
            public float FuelConsumptionMultiplier { get; set; }

            public FakeRoad(float slipperiness, float fuel)
            {
                Slipperiness = slipperiness;
                FuelConsumptionMultiplier = fuel;
            }
        }

        // --- 접지력 ---

        /// <summary>마른 노면에서는 접지력이 깎이지 않아야 합니다.</summary>
        [Test]
        public void 마른_노면에서는_접지력이_그대로다()
        {
            FakeRoad dry = new FakeRoad(1f, 1f);

            float grip = WheelGripTuner.CalculateGrip(dry, 1f, 0.55f);

            Assert.AreEqual(1f, grip, 0.0001f);
        }

        /// <summary>젖은 노면에서는 접지력이 떨어져야 합니다.</summary>
        [Test]
        public void 젖은_노면에서는_접지력이_떨어진다()
        {
            float dry = WheelGripTuner.CalculateGrip(new FakeRoad(1f, 1f), 1f, 0.55f);
            float wet = WheelGripTuner.CalculateGrip(new FakeRoad(1.5f, 1f), 1f, 0.55f);

            Assert.Less(wet, dry, "젖은 노면인데 접지력이 줄지 않았습니다.");
        }

        /// <summary>
        /// 아무리 미끄러워도 하한 아래로는 내려가지 않아야 합니다.
        /// 이 하한이 없으면 폭우에서 운전 자체가 불가능해집니다.
        /// </summary>
        [Test]
        public void 접지력은_하한_아래로_내려가지_않는다()
        {
            float grip = WheelGripTuner.CalculateGrip(new FakeRoad(99f, 1f), 1f, 0.55f);

            Assert.AreEqual(0.55f, grip, 0.0001f);
        }

        /// <summary>
        /// 반영 비율이 0이면 날씨가 아무리 궂어도 접지력이 그대로여야 합니다.
        /// </summary>
        [Test]
        public void 반영_비율이_0이면_날씨를_무시한다()
        {
            float grip = WheelGripTuner.CalculateGrip(new FakeRoad(3f, 1f), 0f, 0.55f);

            Assert.AreEqual(1f, grip, 0.0001f);
        }

        /// <summary>
        /// 노면이 주입되지 않아도 마른 노면으로 보고 계속 돌아야 합니다.
        /// 예전 정적 접근자가 시스템이 없을 때 1을 돌려주던 것과 같은 규약입니다.
        /// </summary>
        [Test]
        public void 노면이_없으면_마른_노면으로_본다()
        {
            float grip = WheelGripTuner.CalculateGrip(null, 1f, 0.55f);

            Assert.AreEqual(1f, grip, 0.0001f);
        }

        // --- 연료 ---

        /// <summary>
        /// 같은 조건에서 궂은 날씨는 연료를 더 먹어야 합니다.
        ///
        /// <b>예전에는 이 한 줄을 쓸 수 없었습니다.</b> 배율이 정적 호출 뒤에 숨어 있어
        /// 테스트가 그 값을 정할 방법이 없었기 때문입니다.
        /// </summary>
        [Test]
        public void 궂은_날씨는_연료를_더_먹는다()
        {
            float calm = ConsumeFuelForOneStep(1f);
            float storm = ConsumeFuelForOneStep(1.4f);

            Assert.Greater(storm, calm, "배율이 커졌는데 연료 소모가 늘지 않았습니다.");
            Assert.AreEqual(1.4f, storm / calm, 0.001f, "소모량이 배율에 비례하지 않습니다.");
        }

        /// <summary>노면이 주입되지 않으면 날씨의 영향 없이 소모되어야 합니다.</summary>
        [Test]
        public void 노면이_없으면_배율_1로_소모된다()
        {
            Assert.AreEqual(ConsumeFuelForOneStep(1f), ConsumeFuelForOneStep(null), 0.000001f);
        }

        // --- Helpers ---

        /// <summary>
        /// 물리 스텝 한 번만큼 연료를 태우고 줄어든 양을 돌려줍니다.
        /// </summary>
        /// <param name="fuelMultiplier">노면의 연료 배율. null이면 노면을 주입하지 않습니다.</param>
        /// <returns>한 스텝 동안 줄어든 연료</returns>
        private static float ConsumeFuelForOneStep(float? fuelMultiplier)
        {
            GameObject go = new GameObject("Powertrain");
            try
            {
                Powertrain powertrain = go.AddComponent<Powertrain>();
                CarData data = BuildCarData();

                IRoadConditions road = fuelMultiplier.HasValue
                    ? new FakeRoad(1f, fuelMultiplier.Value)
                    : null;

                powertrain.Initialize(data, road);

                float before = powertrain.CurrentFuel;
                powertrain.UpdateFuel(true, 1f);

                Object.DestroyImmediate(data);
                return before - powertrain.CurrentFuel;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>시험용 차량 제원입니다. 연료 계산에 쓰이는 값만 의미가 있습니다.</summary>
        /// <returns>기본값을 가진 CarData</returns>
        private static CarData BuildCarData()
        {
            CarData data = ScriptableObject.CreateInstance<CarData>();
            data.maxFuel = 50f;

            // 실제 차량보다 훨씬 큽니다. 한 스텝의 소모량을 float 정밀도 위로 올려
            // 두 배율의 비를 정확히 비교하기 위한 값입니다.
            data.fuelConsumptionRate = 10f;
            data.maxRPM = 6000f;
            data.idleRPM = 800f;
            data.torqueCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            return data;
        }
    }
}
