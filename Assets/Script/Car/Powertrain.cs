using UnityEngine;

/// <summary>
/// 엔진, 기어, 연료 등 동력계 로직을 전담하는 클래스입니다.
/// CarController로부터 휠 RPM과 입력 값을 받아 토크를 계산하고 상태를 업데이트합니다.
/// 이 컴포넌트는 CarController와 같은 GameObject에 추가해야 합니다.
/// </summary>
public class Powertrain : MonoBehaviour
{
    // --- Public Properties ---
    public float CurrentRPM { get; private set; }
    public int CurrentGear { get; private set; } // 0: 후진, 1: 중립, 2부터: 전진 1단
    public float CurrentFuel { get; private set; }

    // --- Private Member Variables ---
    private CarData carData;

    // --- Public Methods ---

    /// <summary>
    /// CarController가 Start()에서 호출하여 CarData를 주입하고 초기화합니다.
    /// </summary>
    public void Initialize(CarData data)
    {
        carData = data;
        CurrentFuel = carData.maxFuel;
        CurrentGear = 1; // 중립에서 시작
        CurrentRPM = 0;
    }

    /// <summary>
    /// 연료 소모 로직을 처리합니다. CarController가 FixedUpdate()에서 호출합니다.
    /// </summary>
    public void UpdateFuel(bool isEngineOn, float throttleInput)
    {
        if (CurrentFuel <= 0)
        {
            CurrentFuel = 0;
            return;
        }

        if (isEngineOn)
        {
            float consumption = (carData.fuelConsumptionRate / 10f) + (CurrentRPM / carData.maxRPM) * Mathf.Abs(throttleInput) * carData.fuelConsumptionRate;
            CurrentFuel -= consumption * Time.fixedDeltaTime;
        }
    }

    /// <summary>
    /// 현재 연료가 0이하인지 확인합니다.
    /// </summary>
    public bool IsFuelEmpty() => CurrentFuel <= 0;

    /// <summary>
    /// RPM, 기어, 최종 모터 토크를 계산하여 반환합니다.
    /// CarController가 FixedUpdate()에서 호출합니다.
    /// </summary>
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
    public int GetDisplayGear()
    {
        if (CurrentGear == 0) return -1; // R
        if (CurrentGear == 1) return 0;  // N
        return CurrentGear - 1;          // 1, 2...
    }
}
