using System.Collections.Generic;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 엔진, 기어, 연료 등 동력계 로직을 전담하는 클래스입니다.
    /// CarController로부터 휠 RPM과 입력 값을 받아 토크를 계산하고 상태를 업데이트합니다.
    /// 이 컴포넌트는 CarController와 같은 GameObject에 추가해야 합니다.
    /// </summary>
    public class Powertrain : MonoBehaviour
    {
        // --- Constants : 기어 번호 ---
        //
        // 기어는 정수 하나로 표현됩니다. 0 이 후진, 1 이 중립, 2 부터가 전진 1단입니다.
        // 예전에는 이 숫자들이 조건문마다 그대로 박혀 있어서, CurrentGear > 1 이 "전진인가"를
        // 뜻하는지 "2단 이상인가"를 뜻하는지 매번 세어 봐야 했습니다.

        /// <summary>후진 기어의 번호입니다.</summary>
        private const int ReverseGear = 0;

        /// <summary>중립 기어의 번호입니다.</summary>
        private const int NeutralGear = 1;

        /// <summary>전진 1단의 번호입니다. 기어비 목록의 0번이 여기에 해당합니다.</summary>
        private const int FirstForwardGear = 2;

        // --- Public Properties ---

        /// <summary>현재 엔진 회전수입니다. 사운드 피치와 계기판 표시에 쓰입니다.</summary>
        public float CurrentRPM { get; private set; }

        public int CurrentGear { get; private set; } // 0: 후진, 1: 중립, 2부터: 전진 1단

        /// <summary>현재 남은 연료량입니다.</summary>
        public float CurrentFuel { get; private set; }

        /// <summary>
        /// 이 차가 담을 수 있는 연료입니다. 데이터가 없으면 0 입니다.
        ///
        /// <b>왜 내주는가.</b> 바깥에서 "얼마나 비었는가" 를 알 길이 없었습니다 —
        /// <see cref="CurrentFuel"/> 만 보이고 가득이 얼마인지는 안 보였습니다.
        /// 주유하는 쪽(<c>RepairArm</c>)이 값을 매기려면 그 차이가 필요합니다.
        /// </summary>
        public float MaxFuel { get { return carData != null ? carData.maxFuel : 0f; } }

        // --- Private Member Variables ---

        /// <summary>
        /// 토크·기어비·연료 설정을 담은 데이터입니다.
        /// CarController가 <see cref="Initialize"/>로 주입해 주며, 없으면 모든 계산이 0을 돌려줍니다.
        ///
        /// <b>읽기 전용으로 다룹니다.</b> 이것은 프로젝트 전체가 공유하는 에셋이라,
        /// 여기에 무언가를 대입하면 그 변경이 다른 차량에도 나타나고 에디터에서는 디스크까지 저장됩니다.
        /// </summary>
        private CarData carData;

        /// <summary>
        /// 이번 주행에 실제로 쓸 기어비입니다.
        ///
        /// 보통은 <see cref="CarData.gearRatios"/>를 그대로 가리킵니다. 에셋의 기어비가 비어 있을 때만
        /// <b>여기에만</b> 임시 값을 담습니다. 예전에는 그 임시 값을 에셋에 직접 써 넣었는데,
        /// <c>CarData</c>는 ScriptableObject라 그 대입이 <b>에셋 자체를 고쳤습니다.</b>
        /// 에디터에서는 저장까지 되어, 한 번 잘못 시작하면 비어 있던 기어비가 4.0짜리 1단으로 굳었습니다.
        /// (같은 문제를 <c>NeedsSystem.BuildSettings</c>가 이미 복사본으로 피하고 있습니다)
        /// </summary>
        private List<float> gearRatios;

        /// <summary>
        /// 기어비를 정규화할 기준값입니다. <see cref="Initialize"/>에서 한 번 정합니다.
        /// 계산식은 <see cref="GearTorqueFactor"/>의 주석을 보세요.
        /// </summary>
        private float referenceRatio = 1f;

        /// <summary>
        /// 노면·바람 상태입니다. 연료 소모 배율을 여기서 읽습니다.
        ///
        /// 예전에는 연료 계산 본문에서 <c>WeatherSystem.GetFuelConsumption()</c>을 정적으로 불렀습니다.
        /// 그래서 이 클래스의 어떤 시그니처에도 날씨가 없는데 연료는 날씨에 좌우되었고,
        /// 순수 산술인 동력계를 <b>씬 없이 검증할 수 없었습니다.</b>
        /// 이제 <see cref="Initialize"/>로 받으므로, 테스트가 가짜 노면을 끼워
        /// 폭우와 맑은 날의 연료 소모를 각각 확인할 수 있습니다.
        /// </summary>
        private IRoadConditions road = NullWeather.Instance;

        // --- Public Methods ---

        /// <summary>
        /// CarController가 Start()에서 호출하여 CarData를 주입하고 초기화합니다.
        /// </summary>
        /// <param name="data">토크·기어비·연료 설정을 담은 차량 데이터</param>
        /// <param name="conditions">노면 상태. null이면 날씨의 영향을 받지 않습니다.</param>
        public void Initialize(CarData data, IRoadConditions conditions)
        {
            carData = data;
            road = conditions != null ? conditions : NullWeather.Instance;

            if (carData == null)
            {
                GameLog.Error(GameLog.Channel.Player, "Powertrain: CarData가 없어 동력계를 초기화할 수 없습니다.", this);
                return;
            }

            // 기어비는 UpdateRpm·EvaluateTorque에서 인덱스로 직접 접근하므로,
            // 비어 있으면 첫 주행에서 IndexOutOfRange가 납니다. 여기서 미리 막습니다.
            //
            // 대체값은 <b>이 컴포넌트 안에만</b> 담습니다. 에셋은 건드리지 않습니다.
            if (carData.gearRatios == null || carData.gearRatios.Count == 0)
            {
                GameLog.Error(GameLog.Channel.Player, "Powertrain: " + carData.name + "의 기어비가 비어 있습니다. " +
                               "최소 1단은 있어야 하므로 이 차량에만 임시로 4.0을 사용합니다. " +
                               "에셋을 고쳐 주세요.", this);
                gearRatios = new List<float> { 4.0f };
            }
            else
            {
                gearRatios = carData.gearRatios;
            }

            // 기준 기어비를 정합니다. 0 이하로 두면 1단 기어비를 씁니다.
            // (기본값이 0이므로, 이 항목을 모르는 기존 에셋도 예전과 같은 토크 크기를 유지합니다)
            referenceRatio = carData.referenceGearRatio > 0.0001f
                ? carData.referenceGearRatio
                : gearRatios[0];

            // 1단 기어비가 0이면 나눗셈이 무한대가 됩니다.
            if (referenceRatio <= 0.0001f) referenceRatio = 1f;

            CurrentFuel = carData.maxFuel;
            CurrentGear = NeutralGear;
            CurrentRPM = 0;
        }

        /// <summary>
        /// 연료 소모 로직을 처리합니다. CarController가 FixedUpdate()에서 호출합니다.
        /// </summary>
        /// <param name="isEngineOn">시동이 걸려 있는지 여부. 꺼져 있으면 연료를 쓰지 않습니다.</param>
        /// <param name="throttleInput">스로틀 입력값. 절댓값이 클수록 소모가 늘어납니다.</param>
        public void UpdateFuel(bool isEngineOn, float throttleInput)
        {
            if (carData == null) return;

            if (CurrentFuel <= 0)
            {
                CurrentFuel = 0;
                return;
            }

            if (!isEngineOn) return;

            // 소모는 두 몫으로 나뉩니다.
            //  - 공회전분: 시동이 걸려 있기만 하면 나가는 몫
            //  - 부하분  : 회전수와 스로틀에 비례해 더 나가는 몫
            float idlePortion = carData.fuelConsumptionRate * carData.idleFuelPortion;
            float loadPortion = (CurrentRPM / carData.maxRPM) * Mathf.Abs(throttleInput) * carData.fuelConsumptionRate;

            // 맞바람·젖은 노면에서는 연료를 더 먹습니다.
            // 노면이 주입되지 않았으면 1이 돌아오므로 아무 영향이 없습니다.
            float consumption = (idlePortion + loadPortion) * road.FuelConsumptionMultiplier;

            CurrentFuel -= consumption * Time.fixedDeltaTime;
        }

        /// <summary>
        /// 현재 연료가 0이하인지 확인합니다.
        /// </summary>
        /// <returns>연료가 바닥났으면 true를 반환합니다.</returns>
        public bool IsFuelEmpty() => CurrentFuel <= 0;

        /// <summary>
        /// 연료를 직접 지정합니다. (세이브 복원·주유 등에 씁니다)
        /// </summary>
        /// <param name="amount">채워 넣을 연료량. 0과 최대 연료량 사이로 잘립니다.</param>
        public void SetFuel(float amount)
        {
            float max = carData != null ? carData.maxFuel : amount;
            CurrentFuel = Mathf.Clamp(amount, 0f, max);
        }

        /// <summary>
        /// 동력계를 이번 물리 프레임만큼 <b>진행시키고</b> 그 결과 걸어야 할 토크를 돌려줍니다.
        /// <see cref="CarController"/>가 <c>FixedUpdate</c>에서 한 번 부릅니다.
        ///
        /// <b>이름이 바뀐 이유가 있습니다.</b> 예전 이름은 <c>CalculateMotorTorque</c>였지만
        /// 실제로는 기어와 RPM 을 바꾸는 <b>명령</b>이었습니다. "계산한다"는 이름을 믿고
        /// 값만 보려고 한 번 더 부르면 기어가 한 번 더 바뀝니다. 이름이 하는 일을 말하게 했습니다.
        ///
        /// 안은 네 단계이고 <b>순서가 서로의 전제</b>입니다 — 기어를 정해야 RPM 을 알고,
        /// RPM 을 알아야 변속을 판단하고, 변속이 끝나야 어느 기어비로 토크를 낼지 정해집니다.
        /// 예전에는 이 넷이 한 메서드 안에서 <c>if</c> 사슬로 뒤엉켜 있었습니다.
        /// </summary>
        /// <param name="wheelRPM">구동륜의 현재 회전수. 전진 기어일 때 엔진 RPM 계산에 쓰입니다.</param>
        /// <param name="throttleInput">스로틀 입력값. 음수면 후진으로 봅니다.</param>
        /// <param name="currentSpeed">현재 주행 속도. 기어 결정과 후진 속도 제한에 쓰입니다.</param>
        /// <param name="isEngineOn">시동이 걸려 있는지 여부. 꺼져 있으면 토크가 0입니다.</param>
        /// <returns>이번 프레임에 구동륜에 걸 모터 토크</returns>
        public float UpdateAndGetTorque(float wheelRPM, float throttleInput, float currentSpeed, bool isEngineOn)
        {
            if (carData == null) return 0f;

            SelectGear(throttleInput, currentSpeed, isEngineOn);

            // 시동이 꺼져 있으면 회전수는 SelectGear 가 잦아들게 했고, 바퀴에 걸 것은 없습니다.
            if (!isEngineOn) return 0f;

            UpdateRpm(wheelRPM, throttleInput);
            ApplyAutoShift();

            return EvaluateTorque(throttleInput, currentSpeed);
        }

        /// <summary>
        /// UI 표시용 기어 값을 반환합니다.
        /// </summary>
        /// <returns>후진이면 -1(R), 중립이면 0(N), 전진이면 1부터의 단수</returns>
        public int GetDisplayGear()
        {
            if (CurrentGear == ReverseGear) return -1;  // R
            if (CurrentGear == NeutralGear) return 0;   // N
            return CurrentGear - NeutralGear;           // 1, 2...
        }

        // --- Private Methods : 진행 네 단계 ---

        /// <summary>
        /// 지금 어느 기어에 있어야 하는지 정합니다. <b>①단계</b>
        ///
        /// <b>속도 문턱이 있는 이유.</b> 달리는 중에 스로틀을 놓았다고 기어가 빠지면
        /// 엔진 브레이크가 사라지고 다시 밟을 때 1단부터 붙습니다. 그래서
        /// <see cref="CarData.gearChangeSpeed"/> 아래에서만 후진·중립으로 바뀝니다.
        /// </summary>
        /// <param name="throttleInput">스로틀 입력값</param>
        /// <param name="currentSpeed">현재 주행 속도(km/h)</param>
        /// <param name="isEngineOn">시동 여부</param>
        private void SelectGear(float throttleInput, float currentSpeed, bool isEngineOn)
        {
            // 시동이 꺼지면 중립으로 떨어지고 회전수가 잦아듭니다.
            if (!isEngineOn)
            {
                CurrentRPM = Mathf.Lerp(CurrentRPM, 0f, Time.fixedDeltaTime * carData.engineStopRpmDecay);
                CurrentGear = NeutralGear;
                return;
            }

            bool slowEnoughToChange = currentSpeed < carData.gearChangeSpeed;

            if (throttleInput < 0f && slowEnoughToChange) CurrentGear = ReverseGear;
            else if (throttleInput == 0f && slowEnoughToChange) CurrentGear = NeutralGear;
            else if (CurrentGear < FirstForwardGear && throttleInput > 0f) CurrentGear = FirstForwardGear;
        }

        /// <summary>
        /// 지금 기어에서 엔진이 몇 바퀴 도는지 구합니다. <b>②단계</b>
        ///
        /// 전진 중에는 <b>바퀴가 엔진을 돌립니다</b> — 휠 회전수에 기어비를 곱한 것이 엔진 회전수입니다.
        /// 중립·후진에서는 바퀴와 엔진이 떨어져 있으므로 스로틀을 밟은 만큼만 공회전이 오릅니다.
        /// </summary>
        /// <param name="wheelRPM">구동륜의 현재 회전수</param>
        /// <param name="throttleInput">스로틀 입력값</param>
        private void UpdateRpm(float wheelRPM, float throttleInput)
        {
            if (CurrentGear > NeutralGear)
            {
                CurrentRPM = Mathf.Abs(wheelRPM * gearRatios[CurrentGear - FirstForwardGear]) + carData.idleRPM;
            }
            else
            {
                CurrentRPM = carData.idleRPM + Mathf.Abs(throttleInput) * carData.neutralRpmSpan;
            }

            CurrentRPM = Mathf.Clamp(CurrentRPM, 0f, carData.maxRPM);
        }

        /// <summary>
        /// 회전수를 보고 한 단 올리거나 내립니다. <b>③단계</b>
        ///
        /// 한 번에 한 단만 움직입니다. 다음 프레임에 다시 판단하므로 급가속에서는
        /// 여러 프레임에 걸쳐 연속으로 올라갑니다.
        /// </summary>
        private void ApplyAutoShift()
        {
            bool hasHigherGear = CurrentGear - FirstForwardGear < gearRatios.Count - 1;

            if (CurrentGear > NeutralGear && CurrentRPM > carData.shiftUpRPM && hasHigherGear)
            {
                CurrentGear++;
            }
            else if (CurrentRPM < carData.shiftDownRPM && CurrentGear > FirstForwardGear)
            {
                CurrentGear--;
            }
        }

        /// <summary>
        /// 지금 상태에서 바퀴에 걸 토크를 구합니다. <b>④단계 · 부수 효과가 없습니다.</b>
        ///
        /// <b>이 메서드는 아무것도 바꾸지 않습니다.</b> 읽기만 하므로 같은 상태에서 몇 번을 불러도
        /// 같은 값이 나옵니다. 예전에는 이 계산이 기어·RPM 갱신과 한 메서드에 뒤엉켜 있어
        /// "지금 토크가 얼마인가"를 물어볼 방법 자체가 없었습니다.
        /// </summary>
        /// <param name="throttleInput">스로틀 입력값</param>
        /// <param name="currentSpeed">현재 주행 속도(km/h)</param>
        /// <returns>바퀴에 걸 모터 토크</returns>
        private float EvaluateTorque(float throttleInput, float currentSpeed)
        {
            // 레브 리미터. 최대 회전수에 닿으면 더 밀지 않습니다.
            if (CurrentRPM >= carData.maxRPM) return 0f;

            // 후진 속도 상한. 뒤로 이만큼 빨라지면 더 밀지 않습니다.
            if (throttleInput < 0f && currentSpeed > carData.maxReverseSpeed) return 0f;

            if (CurrentGear > NeutralGear)
            {
                float normalizedRPM = Mathf.Clamp01(CurrentRPM / carData.maxRPM);
                float torqueMultiplier = carData.torqueCurve.Evaluate(normalizedRPM);
                return carData.motorTorque * throttleInput * torqueMultiplier
                       * GearTorqueFactor(CurrentGear - FirstForwardGear);
            }

            if (CurrentGear == ReverseGear)
            {
                // 후진은 1단과 같은 기어비를 씁니다. (실제 차도 후진비는 1단과 비슷합니다)
                return carData.motorTorque * throttleInput * GearTorqueFactor(0);
            }

            // 중립에서는 바퀴에 아무것도 걸리지 않습니다.
            return 0f;
        }

        // --- Private Methods ---

        /// <summary>
        /// 이 기어에서 <see cref="CarData.motorTorque"/>에 곱할 배율을 돌려줍니다.
        ///
        /// <b>기어비는 곱합니다. 예전에는 나눴습니다.</b>
        /// 구동계에서 기어비는 회전수와 토크에 <b>같은 방향으로</b> 걸립니다.
        /// 회전수는 엔진 쪽이 빨라지고(<see cref="UpdateRpm"/>에서 이미 곱하고 있습니다)
        /// 토크는 바퀴 쪽이 세집니다. 그런데 예전에는 여기만 나누고 있어서,
        /// 기어비 4.0인 1단이 1.0인 4단보다 <b>토크가 약했습니다.</b>
        /// 인스펙터 툴팁("높을수록 초반 가속에 유리")과도 정반대라, 값을 조율하는 사람이
        /// 반대 방향으로 튜닝하게 되는 것이 더 나빴습니다.
        ///
        /// <b>기준 기어비로 나눠서 정규화합니다.</b> 기어비를 그대로 곱하면 기본 에셋 기준으로
        /// 1단 토크가 20,000이 되어, 브레이크 힘·차체 질량·타이어 마찰을 전부 다시 잡아야 합니다.
        /// 기준으로 나누면 <b>1단에서 정확히 motorTorque가 나오고</b> 위 단수로 갈수록 줄어듭니다.
        /// 기어 사이의 관계는 물리대로 바로잡히면서, 이미 맞춰 둔 다른 수치는 그대로 쓸 수 있습니다.
        ///
        /// 기본 에셋(motorTorque 5000, 기어비 4 / 2.5 / 1.5 / 1) 기준으로:
        /// <code>
        ///        예전(나눗셈)   지금(곱셈·정규화)
        ///   1단      1250            5000
        ///   2단      2000            3125
        ///   3단      3333            1875
        ///   4단      5000            1250
        /// </code>
        /// 최대 토크의 크기는 5000으로 같고, <b>순서만 뒤집혔습니다.</b>
        ///
        /// 실제 기어비를 그대로 쓰고 싶다면 <see cref="CarData.referenceGearRatio"/>를 1로 두고
        /// <see cref="CarData.motorTorque"/>를 엔진 토크 수준으로 낮추면 됩니다.
        /// </summary>
        /// <param name="index">기어비 목록에서의 위치. 0이 1단입니다.</param>
        /// <returns>motorTorque에 곱할 배율. 목록 범위를 벗어나면 1을 돌려줍니다.</returns>
        private float GearTorqueFactor(int index)
        {
            if (gearRatios == null || index < 0 || index >= gearRatios.Count) return 1f;
            return gearRatios[index] / referenceRatio;
        }
    }
}
