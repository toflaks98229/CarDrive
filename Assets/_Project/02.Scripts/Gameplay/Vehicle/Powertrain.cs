using System.Collections.Generic;
using UnityEngine;

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
    /// </summary>
    private CarData carData;

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
        if (carData.gearRatios == null || carData.gearRatios.Count == 0)
        {
            Debug.LogError("Powertrain: " + carData.name + "의 기어비가 비어 있습니다. " +
                           "최소 1단은 있어야 하므로 임시로 4.0을 사용합니다.", this);
            carData.gearRatios = new List<float> { 4.0f };
        }

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
                CurrentRPM = Mathf.Abs(wheelRPM * carData.gearRatios[CurrentGear - 2]) + carData.idleRPM;
            }
            else // 후진 또는 중립
            {
                CurrentRPM = carData.idleRPM + (Mathf.Abs(throttleInput) * 1500f);
            }
            CurrentRPM = Mathf.Clamp(CurrentRPM, 0, carData.maxRPM);

            // 3. 자동 변속 (원본 HandleEngineAndGears 로직)
            if (CurrentGear > 1 && CurrentRPM > carData.shiftUpRPM && CurrentGear - 2 < carData.gearRatios.Count - 1)
            {
                CurrentGear++;
            }
            else if (CurrentRPM < carData.shiftDownRPM && CurrentGear > 2)
            {
                CurrentGear--;
            }
        }

        // 4. 모터 토크 계산 (원본 HandleMotor 로직)
        float motorTorque = 0f;
        if (isEngineOn)
        {
            if (CurrentGear > 1) // 전진
            {
                float normalizedRPM = Mathf.Clamp01(CurrentRPM / carData.maxRPM);
                float torqueMultiplier = carData.torqueCurve.Evaluate(normalizedRPM);
                motorTorque = carData.motorTorque * throttleInput * torqueMultiplier / carData.gearRatios[CurrentGear - 2];
            }
            else if (CurrentGear == 0) // 후진
            {
                motorTorque = carData.motorTorque * throttleInput / carData.gearRatios[0];
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
}
