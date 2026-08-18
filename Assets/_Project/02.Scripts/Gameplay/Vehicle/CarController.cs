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

    // 내구도는 이제 VehicleHealth가 소유합니다.
    // 예전에는 CarController와 CarCollisionHandler가 각각 체력바 참조를 들고 있어서
    // 한쪽만 연결하면 조용히 어긋났습니다. 지금은 Health 프로퍼티 하나로 모읍니다.

    [Header("자동차 구동/조향 방식 설정")]
    public DriveType driveType = DriveType.AllWheelDrive;
    public SteerType steerType = SteerType.FrontWheelSteer;

    [Header("자동차 물리 설정")]
    public Vector3 centerOfMass = new Vector3(0, -0.5f, 0);

    // 시동은 운전대를 조준해서 겁니다. (SteeringWheelInteractable)
    // 그래서 이 컴포넌트는 시동 키를 직접 받지 않습니다.

    [Header("날씨 - 노면 접지력")]
    [Tooltip("체크하면 날씨의 미끄러움에 따라 타이어 접지력이 떨어집니다.")]
    public bool useWeatherGrip = true;

    [Tooltip("날씨의 미끄러움을 얼마나 반영할지. 0이면 무시, 1이면 그대로 적용합니다.")]
    [Range(0f, 1f)]
    public float weatherGripInfluence = 1f;

    [Tooltip("접지력이 떨어질 수 있는 하한. 0.55면 폭우에서도 원래 접지력의 55%는 남습니다. " +
             "너무 낮추면 운전이 불가능해집니다.")]
    [Range(0.2f, 1f)]
    public float minGripFactor = 0.55f;

    [Header("휠 콜라이더 (물리)")]
    public WheelCollider frontLeftWheelCollider;
    public WheelCollider frontRightWheelCollider;
    public WheelCollider rearLeftWheelCollider;
    public WheelCollider rearRightWheelCollider;

    // --- 리팩토링된 컴포넌트 참조 ---

    /// <summary>차체의 Rigidbody입니다. 무게중심 설정과 속도 계산에 씁니다.</summary>
    private Rigidbody rb;

    /// <summary>조향·스로틀·브레이크 입력을 읽어 올 컴포넌트입니다.</summary>
    private CarInput input;

    /// <summary>RPM·기어·연료를 계산하는 동력계입니다.</summary>
    private Powertrain powertrain;

    /// <summary>바퀴 회전과 조향 각도를 화면에 반영하는 컴포넌트입니다.</summary>
    private CarVisuals visuals;

    /// <summary>엔진·시동 사운드를 담당합니다. 없으면 조용히 넘어갑니다.</summary>
    private CarSoundController soundController;

    /// <summary>이 차량의 내구도입니다.</summary>
    private VehicleHealth vehicleHealth;
    // CarCollisionHandler는 독립적으로 작동하므로 참조 불필요

    // --- Private State Variables ---

    /// <summary>현재 주행 속도(km/h)입니다. FixedUpdate에서 매번 다시 계산합니다.</summary>
    private float currentSpeed;

    /// <summary>지금 바퀴에 적용 중인 조향 각도(도)입니다.</summary>
    private float currentSteerAngle;

    /// <summary>
    /// 이번 물리 프레임에 실제로 적용된 스로틀 값입니다. 시동이 꺼져 있으면 0입니다.
    ///
    /// 사운드처럼 스로틀을 참고해야 하는 쪽이 전역 Input을 다시 읽지 않도록 여기서 내보냅니다.
    /// 입력을 읽는 곳은 CarInput 한 군데여야 합니다.
    /// </summary>
    private float currentThrottle;

    /// <summary>시동이 걸려 있는지 여부입니다. 연료가 떨어지면 자동으로 꺼집니다.</summary>
    private bool isEngineOn = false;

    // --- 노면 접지력 ---
    // 프리팹에 잡아 둔 원래 마찰 강성을 기억해 두고 거기에 배율을 곱합니다.
    private WheelCollider[] wheels;

    /// <summary>바퀴별 원래 전방 마찰 강성입니다. 여기에 접지력 배율을 곱해 적용합니다.</summary>
    private float[] baseForwardStiffness;

    /// <summary>바퀴별 원래 측면 마찰 강성입니다. 여기에 접지력 배율을 곱해 적용합니다.</summary>
    private float[] baseSidewaysStiffness;

    /// <summary>마지막으로 적용한 접지력 배율입니다. 값이 그대로면 다시 적용하지 않습니다.</summary>
    private float appliedGrip = -1f;

    // --- Unity Event Functions ---

    /// <summary>
    /// 필수 컴포넌트를 가져와 동력계·시각 요소를 초기화하고, 바퀴의 원래 마찰값을 기억해 둡니다.
    /// 시동은 꺼진 상태로 시작합니다.
    /// </summary>
    void Start()
    {
        // 필수 컴포넌트 가져오기
        rb = GetComponent<Rigidbody>();
        input = GetComponent<CarInput>();
        powertrain = GetComponent<Powertrain>();
        visuals = GetComponent<CarVisuals>();

        // 사운드는 있으면 쓰고 없으면 조용히 넘어갑니다.
        soundController = GetComponent<CarSoundController>();

        // 컴포넌트 초기화
        rb.centerOfMass = centerOfMass;
        powertrain.Initialize(carData);
        visuals.Initialize(carData.maxSteerAngle); // CarVisuals에 최대 조향각 전달

        CacheWheelGrip();

        isEngineOn = false; // 시동 꺼진 상태로 시작
    }

    /// <summary>
    /// 한 물리 프레임의 주행을 처리합니다.
    /// 입력을 읽어 토크를 계산하고, 연료가 떨어졌으면 시동을 끄며,
    /// 조향·제동·구동력과 날씨에 따른 접지력을 차례로 적용합니다.
    /// </summary>
    void FixedUpdate()
    {
        currentSpeed = rb.linearVelocity.magnitude * 3.6f; // m/s를 km/h로 변환

        // 1. 입력 가져오기 (from CarInput)
        float steerInput = input.SteerInput;
        float throttleInput = isEngineOn ? input.ThrottleInput : 0f; // 시동 상태에 따라 입력 차단
        bool brakingInput = input.IsBraking;

        // 계기판·사운드가 참고할 수 있도록 실제로 적용한 스로틀을 남겨 둡니다.
        currentThrottle = throttleInput;

        // 2. 동력계 업데이트 및 토크 계산 (from Powertrain)
        float motorTorque = powertrain.CalculateMotorTorque(GetAverageWheelRPM(), throttleInput, currentSpeed, isEngineOn);
        powertrain.UpdateFuel(isEngineOn, throttleInput);

        // 3. 연료 상태 확인 및 시동 관리
        if (isEngineOn && powertrain.IsFuelEmpty())
        {
            isEngineOn = false;
            Debug.Log("CarController: 연료가 떨어져 시동이 꺼졌습니다.");
            if (soundController != null) soundController.PlayEngineStop();
        }

        // 4. 조향각 계산 (원본 로직 유지)
        HandleSteering(steerInput);

        // 5. 물리 적용 (원본 로직 유지)
        HandleBraking(brakingInput, throttleInput);
        ApplyMotorTorque(motorTorque);
        ApplySteering();

        // 6. 날씨에 따른 접지력 반영
        ApplyRoadGrip();
    }

    /// <summary>
    /// 물리 계산이 끝난 뒤 바퀴 메시와 조향 각도를 화면에 반영합니다.
    /// </summary>
    void LateUpdate()
    {
        // 6. 시각적 요소 업데이트 (to CarVisuals)
        visuals.UpdateVisuals(currentSteerAngle);
    }

    // --- Public Methods (Getters & Actions) ---

    /// <summary>
    /// 이 차량의 내구도입니다. 계기판이 자식에 있는 경우가 많아 자식까지 찾습니다.
    ///
    /// 처음 물어볼 때 찾아서 기억해 둡니다. Start 실행 순서에 기대지 않으므로
    /// GhostSpawner처럼 다른 컴포넌트가 자기 Start에서 물어봐도 안전합니다.
    /// </summary>
    public VehicleHealth Health
    {
        get
        {
            if (vehicleHealth == null) vehicleHealth = GetComponentInChildren<VehicleHealth>(true);
            return vehicleHealth;
        }
    }

    #region --- UI 및 외부 데이터 반환 ---
    public float GetCurrentSpeed() => currentSpeed;
    public float GetCurrentRPM() => powertrain.CurrentRPM;
    public float GetThrottleInput() => currentThrottle;
    public float GetCurrentFuel() => powertrain.CurrentFuel;
    public float GetMaxFuel() => carData != null ? carData.maxFuel : 0;
    public bool IsEngineOn() => isEngineOn;
    public int GetCurrentGear() => powertrain.GetDisplayGear();
    #endregion

    /// <summary>
    /// 세이브에서 읽은 연료와 시동 상태로 되돌립니다.
    /// 연료가 비어 있으면 시동은 걸리지 않습니다.
    /// </summary>
    public void RestoreState(float fuel, bool engineOn)
    {
        if (powertrain == null) powertrain = GetComponent<Powertrain>();
        if (powertrain == null) return;

        powertrain.SetFuel(fuel);
        isEngineOn = engineOn && !powertrain.IsFuelEmpty();
    }

    /// <summary>
    /// 엔진 시동 토글 (원본 로직 수정)
    /// </summary>
    public void ToggleEngine()
    {
        bool wasOn = isEngineOn;

        if (!isEngineOn && !powertrain.IsFuelEmpty())
        {
            isEngineOn = true;
        }
        else
        {
            isEngineOn = false;
        }

        // 상태가 그대로면 연료가 없어 시동이 걸리지 않은 경우입니다.
        if (isEngineOn == wasOn)
        {
            Debug.Log("CarController: 연료가 없어 시동이 걸리지 않습니다.");
            return;
        }

        if (soundController != null)
        {
            if (isEngineOn) soundController.PlayEngineStart();
            else soundController.PlayEngineStop();
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
    /// 프리팹에 잡혀 있는 원래 마찰 강성을 기억해 둡니다.
    /// 날씨에 따른 접지력은 이 값에 배율을 곱해서 만듭니다.
    /// </summary>
    private void CacheWheelGrip()
    {
        wheels = new WheelCollider[]
        {
            frontLeftWheelCollider, frontRightWheelCollider,
            rearLeftWheelCollider, rearRightWheelCollider
        };

        baseForwardStiffness = new float[wheels.Length];
        baseSidewaysStiffness = new float[wheels.Length];

        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] == null) continue;

            baseForwardStiffness[i] = wheels[i].forwardFriction.stiffness;
            baseSidewaysStiffness[i] = wheels[i].sidewaysFriction.stiffness;
        }
    }

    /// <summary>
    /// 날씨의 미끄러움을 타이어 접지력에 반영합니다.
    ///
    /// WeatherSystem이 씬에 없으면 미끄러움이 1로 돌아오므로 아무 일도 일어나지 않습니다.
    /// 마찰 곡선은 구조체라 대입할 때마다 복사가 일어나므로, 값이 실제로 달라졌을 때만 씁니다.
    /// </summary>
    private void ApplyRoadGrip()
    {
        if (wheels == null) return;

        float grip = 1f;

        if (useWeatherGrip)
        {
            // 미끄러움은 1(평소)에서 1.9(폭우)까지 올라갑니다.
            float slipperiness = Mathf.Max(0.01f, WeatherSystem.GetRoadSlipperiness());
            float effective = Mathf.Lerp(1f, slipperiness, weatherGripInfluence);
            grip = Mathf.Clamp(1f / effective, minGripFactor, 1f);
        }

        if (Mathf.Abs(grip - appliedGrip) < 0.005f) return;
        appliedGrip = grip;

        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] == null) continue;

            WheelFrictionCurve forward = wheels[i].forwardFriction;
            forward.stiffness = baseForwardStiffness[i] * grip;
            wheels[i].forwardFriction = forward;

            WheelFrictionCurve sideways = wheels[i].sidewaysFriction;
            sideways.stiffness = baseSidewaysStiffness[i] * grip;
            wheels[i].sidewaysFriction = sideways;
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
