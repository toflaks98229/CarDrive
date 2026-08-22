using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// <see cref="Powertrain"/>의 <b>특성 테스트</b>입니다.
    ///
    /// <b>이 파일은 리팩토링보다 먼저 쓰였습니다.</b> 동력계를 쪼개기 전에 지금 무엇을 하는지
    /// 못박아 두기 위해서입니다. 주행감은 회귀를 눈으로 잡기가 매우 어렵습니다 —
    /// 출발이 조금 굼떠지거나 변속이 한 박자 늦어져도 코드를 읽어서는 알 수 없고,
    /// 몰아 보고 이상하다고 느낄 때쯤에는 원인이 여러 커밋 뒤에 있습니다.
    ///
    /// 그래서 여기서는 <b>좋은 설계가 아니라 현재 동작</b>을 적습니다.
    /// 쪼갠 뒤에도 이 값들이 그대로 나와야 합니다.
    /// </summary>
    public class PowertrainTests
    {
        // 기어비 4 · 2 · 1. 기준비는 1단(4)이라 1단의 토크 계수가 정확히 1이 됩니다.
        private const float Idle = 800f;
        private const float MaxRpm = 6000f;
        private const float ShiftUp = 4500f;
        private const float ShiftDown = 2000f;
        private const float MotorTorque = 1000f;
        private const float NeutralRpmSpan = 1500f;

        private GameObject go;
        private Powertrain powertrain;
        private CarData data;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Powertrain");
            powertrain = go.AddComponent<Powertrain>();
            data = BuildCarData();
            powertrain.Initialize(data, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            if (data != null) Object.DestroyImmediate(data);
            go = null; powertrain = null; data = null;
        }

        // --- 초기 상태 ---

        /// <summary>초기화 직후에는 중립·공회전 0·연료 가득이어야 합니다.</summary>
        [Test]
        public void 초기화하면_중립에서_시작한다()
        {
            Assert.AreEqual(1, powertrain.CurrentGear, "중립(1)에서 시작해야 합니다.");
            Assert.AreEqual(0f, powertrain.CurrentRPM, 0.001f);
            Assert.AreEqual(data.maxFuel, powertrain.CurrentFuel, 0.001f);
        }

        // --- 기어 선택 ---

        /// <summary>시동이 꺼져 있으면 중립으로 떨어지고 토크가 나오지 않아야 합니다.</summary>
        [Test]
        public void 시동이_꺼져_있으면_중립이고_토크가_0이다()
        {
            float torque = powertrain.UpdateAndGetTorque(1000f, 1f, 0f, false);

            Assert.AreEqual(0f, torque, 0.001f);
            Assert.AreEqual(1, powertrain.CurrentGear);
        }

        /// <summary>정지 상태에서 전진 스로틀을 주면 1단(내부 2)으로 붙어야 합니다.</summary>
        [Test]
        public void 정지에서_전진_스로틀을_주면_1단으로_붙는다()
        {
            powertrain.UpdateAndGetTorque(0f, 1f, 0f, true);

            Assert.AreEqual(2, powertrain.CurrentGear, "내부 기어 2 = 전진 1단");
            Assert.AreEqual(1, powertrain.GetDisplayGear(), "표시 기어는 1이어야 합니다.");
        }

        /// <summary>저속에서 후진 스로틀을 주면 후진 기어가 되어야 합니다.</summary>
        [Test]
        public void 저속에서_후진_스로틀을_주면_후진이_된다()
        {
            powertrain.UpdateAndGetTorque(0f, -1f, 0f, true);

            Assert.AreEqual(0, powertrain.CurrentGear, "내부 기어 0 = 후진");
            Assert.AreEqual(-1, powertrain.GetDisplayGear(), "표시 기어는 -1(R)이어야 합니다.");
        }

        /// <summary>저속에서 스로틀을 놓으면 중립으로 돌아와야 합니다.</summary>
        [Test]
        public void 저속에서_스로틀을_놓으면_중립이_된다()
        {
            powertrain.UpdateAndGetTorque(0f, 1f, 0f, true);   // 1단으로 붙였다가
            powertrain.UpdateAndGetTorque(0f, 0f, 0f, true);   // 놓습니다

            Assert.AreEqual(1, powertrain.CurrentGear);
            Assert.AreEqual(0, powertrain.GetDisplayGear(), "표시 기어는 0(N)이어야 합니다.");
        }

        // --- RPM ---

        /// <summary>전진 중 엔진 RPM은 휠 RPM에 기어비를 곱한 값에 공회전을 더한 것입니다.</summary>
        [Test]
        public void 전진_RPM은_휠RPM에_기어비를_곱하고_공회전을_더한다()
        {
            // 1단(기어비 4)에 붙이고, 변속점을 넘지 않는 휠 RPM 을 줍니다.
            powertrain.UpdateAndGetTorque(0f, 1f, 0f, true);
            powertrain.UpdateAndGetTorque(500f, 1f, 30f, true);

            Assert.AreEqual(500f * 4f + Idle, powertrain.CurrentRPM, 0.01f);
        }

        /// <summary>중립과 후진에서는 스로틀 크기에 비례해 공회전이 올라갑니다.</summary>
        [Test]
        public void 중립에서는_스로틀에_비례해_공회전이_오른다()
        {
            // 저속 + 스로틀 0 이면 중립입니다. 그 상태의 RPM 은 공회전 그대로입니다.
            powertrain.UpdateAndGetTorque(0f, 0f, 0f, true);
            Assert.AreEqual(Idle, powertrain.CurrentRPM, 0.01f);

            // 후진 스로틀은 기어를 R 로 바꾸고, R 도 같은 식을 씁니다.
            powertrain.UpdateAndGetTorque(0f, -0.5f, 0f, true);
            Assert.AreEqual(Idle + 0.5f * NeutralRpmSpan, powertrain.CurrentRPM, 0.01f);
        }

        /// <summary>RPM 은 최대치를 넘지 않습니다.</summary>
        [Test]
        public void RPM은_상한에서_잘린다()
        {
            powertrain.UpdateAndGetTorque(0f, 1f, 0f, true);
            powertrain.UpdateAndGetTorque(100000f, 1f, 50f, true);

            Assert.AreEqual(MaxRpm, powertrain.CurrentRPM, 0.01f);
        }

        // --- 자동 변속 ---

        /// <summary>변속점을 넘으면 다음 단으로 올라갑니다.</summary>
        [Test]
        public void 변속점을_넘으면_다음_단으로_올라간다()
        {
            powertrain.UpdateAndGetTorque(0f, 1f, 0f, true);        // 1단
            Assert.AreEqual(2, powertrain.CurrentGear);

            // 1단 기어비 4 · 휠 1000 → 4800 RPM > 4500(변속점)
            powertrain.UpdateAndGetTorque(1000f, 1f, 40f, true);

            Assert.AreEqual(3, powertrain.CurrentGear, "2단으로 올라가야 합니다.");
        }

        /// <summary>RPM 이 낮아지면 아래 단으로 내려옵니다.</summary>
        [Test]
        public void 저RPM에서는_아래_단으로_내려온다()
        {
            powertrain.UpdateAndGetTorque(0f, 1f, 0f, true);
            powertrain.UpdateAndGetTorque(1000f, 1f, 40f, true);    // 2단으로
            Assert.AreEqual(3, powertrain.CurrentGear);

            // 2단 기어비 2 · 휠 300 → 1400 RPM < 2000(감속 변속점)
            powertrain.UpdateAndGetTorque(300f, 1f, 20f, true);

            Assert.AreEqual(2, powertrain.CurrentGear, "1단으로 내려와야 합니다.");
        }

        /// <summary>최고단에서는 더 올라가지 않습니다.</summary>
        [Test]
        public void 최고단에서는_더_올라가지_않는다()
        {
            powertrain.UpdateAndGetTorque(0f, 1f, 0f, true);
            powertrain.UpdateAndGetTorque(1000f, 1f, 40f, true);    // 2단
            powertrain.UpdateAndGetTorque(2000f, 1f, 60f, true);    // 3단(최고)
            Assert.AreEqual(4, powertrain.CurrentGear, "3단(내부 4)이어야 합니다.");

            powertrain.UpdateAndGetTorque(4000f, 1f, 90f, true);    // 더 밟아도

            Assert.AreEqual(4, powertrain.CurrentGear, "기어가 3개뿐이므로 더 올라가면 안 됩니다.");
        }

        // --- 토크 ---

        /// <summary>
        /// <b>저단이 고단보다 토크가 세야 합니다.</b>
        ///
        /// 예전에 이 계산이 기어비로 나누고 있어서 1단이 4단보다 약했습니다.
        /// 출발이 굼뜨고 고단에서 튀어 나가던 문제라, 다시 뒤집히지 않도록 못박습니다.
        /// </summary>
        [Test]
        public void 저단이_고단보다_토크가_세다()
        {
            // 1단(기어비 4 · 기준비 4 → 계수 1). 변속점 아래를 유지합니다.
            powertrain.UpdateAndGetTorque(0f, 1f, 0f, true);
            float firstGear = powertrain.UpdateAndGetTorque(500f, 1f, 30f, true);
            Assert.AreEqual(2, powertrain.CurrentGear, "아직 1단이어야 합니다.");

            // 2단으로 올린 뒤(기어비 2 → 계수 0.5) 같은 스로틀에서 비교합니다.
            powertrain.UpdateAndGetTorque(1000f, 1f, 40f, true);
            float secondGear = powertrain.UpdateAndGetTorque(1100f, 1f, 50f, true);
            Assert.AreEqual(3, powertrain.CurrentGear, "2단을 유지해야 합니다.");

            Assert.Greater(firstGear, secondGear, "1단이 2단보다 토크가 세야 합니다.");
            Assert.AreEqual(MotorTorque, firstGear, 0.01f, "1단 계수는 정확히 1이어야 합니다.");
            Assert.AreEqual(MotorTorque * 0.5f, secondGear, 0.01f, "2단 계수는 0.5여야 합니다.");
        }

        /// <summary>최대 RPM 에 닿으면 토크가 끊깁니다. (레브 리미터)</summary>
        [Test]
        public void 최대RPM에_닿으면_토크가_끊긴다()
        {
            powertrain.UpdateAndGetTorque(0f, 1f, 0f, true);
            float torque = powertrain.UpdateAndGetTorque(100000f, 1f, 50f, true);

            Assert.AreEqual(MaxRpm, powertrain.CurrentRPM, 0.01f);
            Assert.AreEqual(0f, torque, 0.001f, "리미터가 걸려야 합니다.");
        }

        /// <summary>후진 속도 상한을 넘으면 토크를 끊습니다.</summary>
        [Test]
        public void 후진_속도_상한을_넘으면_토크가_0이다()
        {
            // 저속에서 후진 기어로 넣은 뒤, 상한을 넘긴 속도로 다시 부릅니다.
            powertrain.UpdateAndGetTorque(0f, -1f, 0f, true);
            float torque = powertrain.UpdateAndGetTorque(0f, -1f, data.maxReverseSpeed + 5f, true);

            Assert.AreEqual(0f, torque, 0.001f);
        }

        /// <summary>표시 기어는 R 이 음수, N 이 0, 전진이 1부터입니다.</summary>
        [Test]
        public void 표시_기어_규약을_지킨다()
        {
            powertrain.UpdateAndGetTorque(0f, -1f, 0f, true);
            Assert.AreEqual(-1, powertrain.GetDisplayGear(), "후진은 -1");

            powertrain.UpdateAndGetTorque(0f, 0f, 0f, true);
            Assert.AreEqual(0, powertrain.GetDisplayGear(), "중립은 0");

            powertrain.UpdateAndGetTorque(0f, 1f, 0f, true);
            Assert.AreEqual(1, powertrain.GetDisplayGear(), "전진 1단은 1");
        }

        // --- 연료 ---

        /// <summary>연료는 공회전분과 회전수·스로틀 비례분을 합해 소모됩니다.</summary>
        [Test]
        public void 연료는_공회전분과_회전수_비례분을_합해_소모된다()
        {
            // 중립 공회전 상태로 만듭니다. (RPM = idle)
            powertrain.UpdateAndGetTorque(0f, 0f, 0f, true);

            float before = powertrain.CurrentFuel;
            powertrain.UpdateFuel(true, 1f);
            float used = before - powertrain.CurrentFuel;

            float rate = data.fuelConsumptionRate;
            float expected = (rate / 10f + (Idle / MaxRpm) * 1f * rate) * Time.fixedDeltaTime;

            Assert.AreEqual(expected, used, expected * 0.001f);
        }

        /// <summary>시동이 꺼져 있으면 연료를 쓰지 않습니다.</summary>
        [Test]
        public void 시동이_꺼져_있으면_연료를_쓰지_않는다()
        {
            float before = powertrain.CurrentFuel;
            powertrain.UpdateFuel(false, 1f);

            Assert.AreEqual(before, powertrain.CurrentFuel, 0.000001f);
        }

        // --- 튜닝 값이 정말 CarData 에서 오는가 ---
        //
        // 아래 셋은 특성 테스트가 아니라 <b>이번 리팩토링이 약속한 것</b>을 확인합니다.
        // 예전에는 이 값들이 Powertrain 본문에 상수로 박혀 있어서, 차량마다 다르게 두려면
        // 코드를 고쳐야 했습니다. 지금은 에셋에 있으므로 여기서 바꿔 보면 결과가 따라와야 합니다.

        /// <summary>기어가 빠지는 속도 문턱은 <see cref="CarData.gearChangeSpeed"/>가 정합니다.</summary>
        [Test]
        public void 기어_전환_문턱은_CarData에서_온다()
        {
            // 기본 문턱(5)보다 빠르면 스로틀을 놓아도 기어가 빠지지 않습니다.
            powertrain.UpdateAndGetTorque(0f, 1f, 0f, true);
            powertrain.UpdateAndGetTorque(200f, 0f, 10f, true);
            Assert.AreEqual(2, powertrain.CurrentGear, "문턱 위 속도라 기어를 물고 있어야 합니다.");

            // 문턱을 올리면 같은 속도에서 중립으로 떨어져야 합니다.
            data.gearChangeSpeed = 20f;
            powertrain.UpdateAndGetTorque(200f, 0f, 10f, true);

            Assert.AreEqual(1, powertrain.CurrentGear, "문턱을 올렸으니 중립이 되어야 합니다.");
        }

        /// <summary>중립·후진 공회전 폭은 <see cref="CarData.neutralRpmSpan"/>이 정합니다.</summary>
        [Test]
        public void 공회전_폭은_CarData에서_온다()
        {
            data.neutralRpmSpan = 3000f;
            powertrain.UpdateAndGetTorque(0f, -1f, 0f, true);

            Assert.AreEqual(Idle + 3000f, powertrain.CurrentRPM, 0.01f);
        }

        /// <summary>공회전 연료 몫은 <see cref="CarData.idleFuelPortion"/>이 정합니다.</summary>
        [Test]
        public void 공회전_연료_몫은_CarData에서_온다()
        {
            // 스로틀 0 이면 부하분이 없으므로 소모가 공회전 몫만 남습니다.
            powertrain.UpdateAndGetTorque(0f, 0f, 0f, true);

            data.idleFuelPortion = 0.2f;
            float before = powertrain.CurrentFuel;
            powertrain.UpdateFuel(true, 0f);
            float used = before - powertrain.CurrentFuel;

            float expected = data.fuelConsumptionRate * 0.2f * Time.fixedDeltaTime;
            Assert.AreEqual(expected, used, expected * 0.001f);
        }

        // --- Helpers ---

        /// <summary>
        /// 시험용 차량 제원입니다. 기어비 4·2·1, 기준비 4 라서 1단 토크 계수가 정확히 1입니다.
        /// 토크 곡선은 상수 1이라 곡선의 영향을 빼고 나머지 항만 봅니다.
        /// </summary>
        /// <returns>시험용 CarData</returns>
        private static CarData BuildCarData()
        {
            CarData d = ScriptableObject.CreateInstance<CarData>();
            d.motorTorque = MotorTorque;
            d.maxReverseSpeed = 20f;
            d.torqueCurve = AnimationCurve.Constant(0f, 1f, 1f);
            d.idleRPM = Idle;
            d.maxRPM = MaxRpm;
            d.shiftUpRPM = ShiftUp;
            d.shiftDownRPM = ShiftDown;
            d.gearRatios = new List<float> { 4f, 2f, 1f };
            d.referenceGearRatio = 4f;
            d.maxFuel = 50f;
            d.fuelConsumptionRate = 10f;
            return d;
        }
    }
}
