using System.Collections.Generic;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 엔진, 기어, 연료 등 동력계 로직을 전담하는 클래스입니다.
    /// CarController로부터 휠 RPM과 입력 값을 받아 토크를 계산하고 상태를 업데이트합니다.
    /// 이 컴포넌트는 CarController와 같은 GameObject에 추가해야 합니다.
    /// </summary>
    public class Powertrain : MonoBehaviour
    {
        // --- Public Properties ---

        /// <summary>현재 엔진 회전수입니다. 사운드 피치와 계기판 표시에 쓰입니다.</summary>
        public float CurrentRPM { get; private set; }

        public int CurrentGear { get; private set; } // 0: 후진, 1: 중립, 2부터: 전진 1단

        /// <summary>현재 남은 연료량입니다.</summary>
        public float CurrentFuel { get; private set; }

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
        /// 계산식은 <see cref="CalculateMotorTorque"/>의 주석을 보세요.
        /// </summary>
        private float referenceRatio = 1f;

        // --- Public Methods ---

        /// <summary>
        /// CarController가 Start()에서 호출하여 CarData를 주입하고 초기화합니다.
        /// </summary>
        /// <param name="data">토크·기어비·연료 설정을 담은 차량 데이터</param>
        public void Initialize(CarData data)
        {
            carData = data;

            if (carData == null)
            {
                Debug.LogError("Powertrain: CarData가 없어 동력계를 초기화할 수 없습니다.", this);
                return;
            }

            // 기어비는 CalculateMotorTorque에서 인덱스로 직접 접근하므로,
            // 비어 있으면 첫 주행에서 IndexOutOfRange가 납니다. 여기서 미리 막습니다.
            //
            // 대체값은 <b>이 컴포넌트 안에만</b> 담습니다. 에셋은 건드리지 않습니다.
            if (carData.gearRatios == null || carData.gearRatios.Count == 0)
            {
                Debug.LogError("Powertrain: " + carData.name + "의 기어비가 비어 있습니다. " +
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
            CurrentGear = 1; // 중립에서 시작
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

            if (isEngineOn)
            {
                float consumption = (carData.fuelConsumptionRate / 10f) + (CurrentRPM / carData.maxRPM) * Mathf.Abs(throttleInput) * carData.fuelConsumptionRate;

                // 맞바람·젖은 노면에서는 연료를 더 먹습니다.
                // WeatherSystem이 씬에 없으면 1이 돌아오므로 아무 영향이 없습니다.
                consumption *= WeatherSystem.GetFuelConsumption();

                CurrentFuel -= consumption * Time.fixedDeltaTime;
            }
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
        /// RPM, 기어, 최종 모터 토크를 계산하여 반환합니다.
        /// CarController가 FixedUpdate()에서 호출합니다.
        /// </summary>
        /// <param name="wheelRPM">구동륜의 현재 회전수. 전진 기어일 때 엔진 RPM 계산에 쓰입니다.</param>
        /// <param name="throttleInput">스로틀 입력값. 음수면 후진으로 봅니다.</param>
        /// <param name="currentSpeed">현재 주행 속도. 기어 결정과 후진 속도 제한에 쓰입니다.</param>
        /// <param name="isEngineOn">시동이 걸려 있는지 여부. 꺼져 있으면 토크가 0입니다.</param>
        /// <returns>이번 프레임에 구동륜에 걸 모터 토크</returns>
        public float CalculateMotorTorque(float wheelRPM, float throttleInput, float currentSpeed, bool isEngineOn)
        {
            if (carData == null) return 0;

            // 1. 기어 상태 결정 (원본 HandleEngineAndGears 로직)
            if (!isEngineOn)
            {
                CurrentRPM = Mathf.Lerp(CurrentRPM, 0, Time.fixedDeltaTime * 2f);
                CurrentGear = 1; // 시동 꺼지면 중립
            }
            else if (throttleInput < 0 && currentSpeed < 5f) { CurrentGear = 0; } // 후진
            else if (throttleInput == 0 && currentSpeed < 5f) { CurrentGear = 1; } // 중립
            else if (CurrentGear < 2 && throttleInput > 0) { CurrentGear = 2; } // 1단 출발

            // 2. RPM 계산 (원본 HandleEngineAndGears 로직)
            if (isEngineOn)
            {
                if (CurrentGear > 1) // 전진
                {
                    CurrentRPM = Mathf.Abs(wheelRPM * gearRatios[CurrentGear - 2]) + carData.idleRPM;
                }
                else // 후진 또는 중립
                {
                    CurrentRPM = carData.idleRPM + (Mathf.Abs(throttleInput) * 1500f);
                }
                CurrentRPM = Mathf.Clamp(CurrentRPM, 0, carData.maxRPM);

                // 3. 자동 변속 (원본 HandleEngineAndGears 로직)
                if (CurrentGear > 1 && CurrentRPM > carData.shiftUpRPM && CurrentGear - 2 < gearRatios.Count - 1)
                {
                    CurrentGear++;
                }
                else if (CurrentRPM < carData.shiftDownRPM && CurrentGear > 2)
                {
                    CurrentGear--;
                }
            }

            // 4. 모터 토크 계산 (원본 HandleMotor 로직)
            //
            // <b>기어비는 곱합니다. 예전에는 나눴습니다.</b>
            // 구동계에서 기어비는 회전수와 토크에 <b>같은 방향으로</b> 걸립니다.
            // 회전수는 엔진 쪽이 빨라지고(위 2번에서 이미 곱하고 있습니다) 토크는 바퀴 쪽이 세집니다.
            // 그런데 여기만 나누고 있어서, 기어비 4.0인 1단이 1.0인 4단보다 <b>토크가 약했습니다.</b>
            // 기본 에셋 기준으로 1단 1250 · 4단 5000 — 출발이 굼뜨고 고단에서 튀어 나갔습니다.
            // 인스펙터 툴팁("높을수록 초반 가속에 유리")과도 정반대라, 값을 조율하는 사람이
            // 반대 방향으로 튜닝하게 되는 것이 더 나빴습니다.
            float motorTorque = 0f;
            if (isEngineOn)
            {
                if (CurrentGear > 1) // 전진
                {
                    float normalizedRPM = Mathf.Clamp01(CurrentRPM / carData.maxRPM);
                    float torqueMultiplier = carData.torqueCurve.Evaluate(normalizedRPM);
                    motorTorque = carData.motorTorque * throttleInput * torqueMultiplier * GearTorqueFactor(CurrentGear - 2);
                }
                else if (CurrentGear == 0) // 후진
                {
                    // 후진은 1단과 같은 기어비를 씁니다. (실제 차도 후진비는 1단과 비슷합니다)
                    motorTorque = carData.motorTorque * throttleInput * GearTorqueFactor(0);
                }

                // 5. 속도 제한 (원본 HandleMotor 로직)
                if (throttleInput < 0 && currentSpeed > carData.maxReverseSpeed) { motorTorque = 0; }
                if (CurrentRPM >= carData.maxRPM) { motorTorque = 0; }
            }

            return motorTorque;
        }

        /// <summary>
        /// UI 표시용 기어 값을 반환합니다.
        /// </summary>
        /// <returns>후진이면 -1(R), 중립이면 0(N), 전진이면 1부터의 단수</returns>
        public int GetDisplayGear()
        {
            if (CurrentGear == 0) return -1; // R
            if (CurrentGear == 1) return 0;  // N
            return CurrentGear - 1;          // 1, 2...
        }

        // --- Private Methods ---

        /// <summary>
        /// 이 기어에서 <see cref="CarData.motorTorque"/>에 곱할 배율을 돌려줍니다.
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
