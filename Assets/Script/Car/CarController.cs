using UnityEngine;

/// <summary>
/// [리팩토링됨]
/// 자동차의 핵심 조율자(Coordinator) 클래스입니다.
/// CarInput, Powertrain, CarVisuals 등 분리된 컴포넌트들을 관리하고,
/// 최종 물리 계산(토크, 조향, 브레이크 적용)을 WheelCollider에 명령합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CarInput))]
[RequireComponent(typeof(Powertrain))]
[RequireComponent(typeof(CarVisuals))]
[RequireComponent(typeof(CarCollisionHandler))] // 충돌 핸들러도 필수 컴포넌트로 추가
public class CarController : MonoBehaviour
{
    #region --- Enums (원본 유지) ---
    public enum DriveType { FrontWheelDrive, RearWheelDrive, AllWheelDrive }
    public enum SteerType { FrontWheelSteer, AllWheelSteer }
    #endregion

    [Header("핵심 데이터 및 컴포넌트")]
    [Tooltip("차량의 성능을 결정하는 CarData ScriptableObject")]
    public CarData carData;
    public TextHealthBar healthBar; // 체력바 참조는 유지 (PlayerInteractor가 사용)

    [Header("자동차 구동/조향 방식 설정")]
    public DriveType driveType = DriveType.AllWheelDrive;
    public SteerType steerType = SteerType.FrontWheelSteer;

    [Header("자동차 물리 설정")]
    public Vector3 centerOfMass = new Vector3(0, -0.5f, 0);

    [Header("시동 설정")]
    public KeyCode engineStartKey = KeyCode.E;

    [Header("휠 콜라이더 (물리)")]
    public WheelCollider frontLeftWheelCollider;
    public WheelCollider frontRightWheelCollider;
    public WheelCollider rearLeftWheelCollider;
    public WheelCollider rearRightWheelCollider;

    // --- 리팩토링된 컴포넌트 참조 ---
    private Rigidbody rb;
    private CarInput input;
    private Powertrain powertrain;
    private CarVisuals visuals;
    // CarCollisionHandler는 독립적으로 작동하므로 참조 불필요

    // --- Private State Variables ---
    private float currentSpeed;
    private float currentSteerAngle;
    private bool isEngineOn = false;

    // --- Unity Event Functions ---

    void Start()
    {
        // 필수 컴포넌트 가져오기
        rb = GetComponent<Rigidbody>();
        input = GetComponent<CarInput>();
        powertrain = GetComponent<Powertrain>();
        visuals = GetComponent<CarVisuals>();

        // 컴포넌트 초기화
        rb.centerOfMass = centerOfMass;
        powertrain.Initialize(carData);
        visuals.Initialize(carData.maxSteerAngle); // CarVisuals에 최대 조향각 전달

        isEngineOn = false; // 시동 꺼진 상태로 시작
    }

    void Update()
    {

    }

    void FixedUpdate()
    {
        currentSpeed = rb.velocity.magnitude * 3.6f; // m/s를 km/h로 변환

        // 1. 입력 가져오기 (from CarInput)
        float steerInput = input.SteerInput;
        float throttleInput = isEngineOn ? input.ThrottleInput : 0f; // 시동 상태에 따라 입력 차단
        bool brakingInput = input.IsBraking;

        // 2. 동력계 업데이트 및 토크 계산 (from Powertrain)
        float motorTorque = powertrain.CalculateMotorTorque(GetAverageWheelRPM(), throttleInput, currentSpeed, isEngineOn);
        powertrain.UpdateFuel(isEngineOn, throttleInput);

        // 3. 연료 상태 확인 및 시동 관리
        if (isEngineOn && powertrain.IsFuelEmpty())
        {
            isEngineOn = false;
        }

        // 4. 조향각 계산 (원본 로직 유지)
        HandleSteering(steerInput);

        // 5. 물리 적용 (원본 로직 유지)
        HandleBraking(brakingInput, throttleInput);
        ApplyMotorTorque(motorTorque);
        ApplySteering();
    }

    void LateUpdate()
    {
        // 6. 시각적 요소 업데이트 (to CarVisuals)
        visuals.UpdateVisuals(currentSteerAngle);
    }

    // --- Public Methods (Getters & Actions) ---

    #region --- UI 및 외부 데이터 반환 ---
    public float GetCurrentSpeed() => currentSpeed;
    public float GetCurrentRPM() => powertrain.CurrentRPM;
    public float GetCurrentFuel() => powertrain.CurrentFuel;
    public float GetMaxFuel() => carData != null ? carData.maxFuel : 0;
    public bool IsEngineOn() => isEngineOn;
    public int GetCurrentGear() => powertrain.GetDisplayGear();
    #endregion

    /// <summary>
    /// 엔진 시동 토글 (원본 로직 수정)
    /// </summary>
    public void ToggleEngine()
    {
        if (!isEngineOn && !powertrain.IsFuelEmpty())
        {
            isEngineOn = true;
        }
        else
        {
            isEngineOn = false;
        }
    }

    // --- Private Methods (Core Physics Logic) ---
    // 이 메서드들은 CarController의 핵심 책임이므로 유지합니다.

    /// <summary>
    /// 조향 각도를 계산합니다 (원본 HandleSteering)
    /// </summary>
    private void HandleSteering(float steerInput)
    {
        float dynamicSteerAngle = carData.maxSteerAngle * (1f - (currentSpeed / 100f) * carData.steerHelper);
        dynamicSteerAngle = Mathf.Clamp(dynamicSteerAngle, 10f, carData.maxSteerAngle);
        float targetSteerAngle = dynamicSteerAngle * steerInput;
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteerAngle, carData.steerSpeed * Time.fixedDeltaTime);
    }

    /// <summary>
    /// 브레이크를 적용합니다 (원본 HandleBraking 수정)
    /// </summary>
    private void HandleBraking(bool isBraking, float throttleInput)
    {
        float currentBrakeTorque = isBraking ? carData.brakeTorque : 0f;

        // 엔진 브레이크 (관성 주행 시)
        if (throttleInput == 0 && currentSpeed > 1f && !isBraking && isEngineOn)
        {
            currentBrakeTorque = 50f;
        }

        frontLeftWheelCollider.brakeTorque = currentBrakeTorque;
        frontRightWheelCollider.brakeTorque = currentBrakeTorque;
        rearLeftWheelCollider.brakeTorque = currentBrakeTorque;
        rearRightWheelCollider.brakeTorque = currentBrakeTorque;
    }

    /// <summary>
    /// 모터 토크를 휠에 적용합니다 (원본 ApplyMotorTorque)
    /// </summary>
    private void ApplyMotorTorque(float torque)
    {
        switch (driveType)
        {
            case DriveType.FrontWheelDrive:
                frontLeftWheelCollider.motorTorque = torque;
                frontRightWheelCollider.motorTorque = torque;
                break;
            case DriveType.RearWheelDrive:
                rearLeftWheelCollider.motorTorque = torque;
                rearRightWheelCollider.motorTorque = torque;
                break;
            case DriveType.AllWheelDrive:
                float awdTorque = torque / 2;
                frontLeftWheelCollider.motorTorque = awdTorque;
                frontRightWheelCollider.motorTorque = awdTorque;
                rearLeftWheelCollider.motorTorque = awdTorque;
                rearRightWheelCollider.motorTorque = awdTorque;
                break;
        }
    }

    /// <summary>
    /// 조향 각도를 휠에 적용합니다 (원본 ApplySteering)
    /// </summary>
    private void ApplySteering()
    {
        switch (steerType)
        {
            case SteerType.FrontWheelSteer:
                frontLeftWheelCollider.steerAngle = currentSteerAngle;
                frontRightWheelCollider.steerAngle = currentSteerAngle;
                rearLeftWheelCollider.steerAngle = 0;
                rearRightWheelCollider.steerAngle = 0;
                break;
            case SteerType.AllWheelSteer:
                frontLeftWheelCollider.steerAngle = currentSteerAngle;
                frontRightWheelCollider.steerAngle = currentSteerAngle;
                rearLeftWheelCollider.steerAngle = -currentSteerAngle * 0.5f;
                rearRightWheelCollider.steerAngle = -currentSteerAngle * 0.5f;
                break;
        }
    }

    /// <summary>
    /// 평균 휠 RPM을 계산합니다 (원본 GetAverageWheelRPM)
    /// </summary>
    private float GetAverageWheelRPM()
    {
        float sumRPM = 0;
        int wheelCount = 0;
        switch (driveType)
        {
            case DriveType.FrontWheelDrive:
                sumRPM += frontLeftWheelCollider.rpm + frontRightWheelCollider.rpm;
                wheelCount = 2;
                break;
            case DriveType.RearWheelDrive:
                sumRPM += rearLeftWheelCollider.rpm + rearRightWheelCollider.rpm;
                wheelCount = 2;
                break;
            case DriveType.AllWheelDrive:
                sumRPM += frontLeftWheelCollider.rpm + frontRightWheelCollider.rpm + rearLeftWheelCollider.rpm + rearRightWheelCollider.rpm;
                wheelCount = 4;
                break;
        }
        return wheelCount > 0 ? sumRPM / wheelCount : 0;
    }
}
