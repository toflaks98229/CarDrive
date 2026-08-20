using UnityEngine;
using UnityEngine.Serialization;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 차량 주행의 조율자입니다. <b>스스로 계산하지 않고 순서만 정합니다.</b>
    ///
    /// 한 물리 프레임에 벌어지는 일은 넷입니다.
    ///  1. 입력을 읽는다 (<see cref="CarInput"/>)
    ///  2. 토크를 구한다 (<see cref="Powertrain"/>)
    ///  3. 바퀴에 건다 (<see cref="WheelDriveline"/>)
    ///  4. 날씨만큼 접지력을 깎는다 (<see cref="WheelGripTuner"/>)
    ///
    /// <b>왜 이렇게 나눴는가.</b> 예전에는 이 클래스가 휠 콜라이더 넷을 개별 필드로 들고
    /// 구동·조향·제동·평균회전수 <b>네 메서드가 각자 같은 switch를 다시 썼습니다.</b>
    /// 거기에 접지력 캐싱용 배열 셋까지 얹혀 있어서, 주행 흐름 한 줄을 읽으려면
    /// 관계없는 코드를 계속 넘겨야 했습니다.
    ///
    /// <b>왜 새 컴포넌트가 아니라 일반 클래스인가.</b> 설정값은 이 컴포넌트가 직렬화해 들고 있어야
    /// 합니다. 값을 <see cref="WheelDriveline"/> 쪽으로 옮기면 직렬화 경로가 바뀌어
    /// <b>프리팹에 맞춰 둔 값이 전부 초기화됩니다.</b> 그래서 값은 여기 두고 계산만 넘겼습니다.
    /// 나중에 컴포넌트로 올리고 싶으면 이 파일의 필드를 그쪽으로 옮기고 프리팹에서 다시 이으면 됩니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CarInput))]
    [RequireComponent(typeof(Powertrain))]
    [RequireComponent(typeof(CarVisuals))]
    [RequireComponent(typeof(CarCollisionHandler))]
    public class CarController : MonoBehaviour
    {
        // --- Constants ---

        /// <summary>m/s를 km/h로 바꾸는 계수입니다.</summary>
        private const float MetersPerSecondToKmh = 3.6f;

        /// <summary>스로틀을 놓고 관성 주행할 때 걸리는 엔진 브레이크 토크입니다.</summary>
        private const float EngineBrakeTorque = 50f;

        /// <summary>엔진 브레이크가 걸리기 시작하는 속도(km/h)입니다. 정차 직전에는 걸지 않습니다.</summary>
        private const float EngineBrakeMinSpeed = 1f;

        /// <summary>고속에서 조향각을 줄일 때 기준으로 삼는 속도(km/h)입니다.</summary>
        private const float SteerReferenceSpeed = 100f;

        /// <summary>고속에서도 최소한 이만큼은 꺾입니다. 완전히 직진만 하게 되면 운전이 불가능합니다.</summary>
        private const float MinDynamicSteerAngle = 10f;

        // --- Serialized Fields ---
        //
        // 직렬화 이름은 예전 그대로여야 합니다. Unity 는 필드 <b>이름</b>으로 값을 찾기 때문에,
        // 이름을 바꾸면 프리팹에 맞춰 둔 값이 조용히 기본값으로 돌아갑니다.
        // FormerlySerializedAs 가 옛 이름을 읽어 주므로 배선이 그대로 살아납니다.

        /// <summary>차량의 성능을 결정하는 설정 에셋입니다.</summary>
        [Header("핵심 데이터")]
        [Tooltip("차량의 성능을 결정하는 CarData ScriptableObject")]
        [SerializeField, FormerlySerializedAs("carData")]
        private CarData _carData;

        /// <summary>어느 바퀴를 굴릴지입니다.</summary>
        [Header("구동 / 조향 방식")]
        [SerializeField, FormerlySerializedAs("driveType")]
        private WheelDriveType _driveType = WheelDriveType.AllWheelDrive;

        /// <summary>어느 바퀴를 꺾을지입니다.</summary>
        [SerializeField, FormerlySerializedAs("steerType")]
        private WheelSteerType _steerType = WheelSteerType.FrontWheelSteer;

        /// <summary>차체 무게중심입니다. 낮출수록 잘 뒤집히지 않습니다.</summary>
        [Header("물리")]
        [SerializeField, FormerlySerializedAs("centerOfMass")]
        private Vector3 _centerOfMass = new Vector3(0f, -0.5f, 0f);

        /// <summary>날씨의 미끄러움을 접지력에 반영할지 여부입니다.</summary>
        [Header("날씨 - 노면 접지력")]
        [Tooltip("체크하면 날씨의 미끄러움에 따라 타이어 접지력이 떨어집니다.")]
        [SerializeField, FormerlySerializedAs("useWeatherGrip")]
        private bool _useWeatherGrip = true;

        /// <summary>날씨를 얼마나 반영할지입니다.</summary>
        [Tooltip("날씨의 미끄러움을 얼마나 반영할지. 0이면 무시, 1이면 그대로 적용합니다.")]
        [Range(0f, 1f)]
        [SerializeField, FormerlySerializedAs("weatherGripInfluence")]
        private float _weatherGripInfluence = 1f;

        /// <summary>접지력이 떨어질 수 있는 하한입니다.</summary>
        [Tooltip("접지력이 떨어질 수 있는 하한. 0.55면 폭우에서도 원래 접지력의 55%는 남습니다. " +
                 "너무 낮추면 운전이 불가능해집니다.")]
        [Range(0.2f, 1f)]
        [SerializeField, FormerlySerializedAs("minGripFactor")]
        private float _minGripFactor = 0.55f;

        /// <summary>왼쪽 앞바퀴입니다.</summary>
        [Header("휠 콜라이더")]
        [SerializeField, FormerlySerializedAs("frontLeftWheelCollider")]
        private WheelCollider _frontLeftWheel;

        /// <summary>오른쪽 앞바퀴입니다.</summary>
        [SerializeField, FormerlySerializedAs("frontRightWheelCollider")]
        private WheelCollider _frontRightWheel;

        /// <summary>왼쪽 뒷바퀴입니다.</summary>
        [SerializeField, FormerlySerializedAs("rearLeftWheelCollider")]
        private WheelCollider _rearLeftWheel;

        /// <summary>오른쪽 뒷바퀴입니다.</summary>
        [SerializeField, FormerlySerializedAs("rearRightWheelCollider")]
        private WheelCollider _rearRightWheel;

        // --- Private Member Variables ---

        /// <summary>차체의 Rigidbody입니다. 무게중심 설정과 속도 계산에 씁니다.</summary>
        private Rigidbody _body;

        /// <summary>조향·스로틀·브레이크 입력을 읽어 올 컴포넌트입니다.</summary>
        private CarInput _input;

        /// <summary>RPM·기어·연료를 계산하는 동력계입니다.</summary>
        private Powertrain _powertrain;

        /// <summary>바퀴 회전과 조향 각도를 화면에 반영하는 컴포넌트입니다.</summary>
        private CarVisuals _visuals;

        /// <summary>엔진·시동 사운드를 담당합니다. 없으면 조용히 넘어갑니다.</summary>
        private CarSoundController _soundController;

        /// <summary>이 차량의 내구도입니다. 처음 물어볼 때 찾습니다.</summary>
        private VehicleHealth _health;

        /// <summary>구동력·조향·제동을 바퀴에 거는 구동계입니다.</summary>
        private readonly WheelDriveline _driveline = new WheelDriveline();

        /// <summary>날씨에 따라 접지력을 조절합니다.</summary>
        private readonly WheelGripTuner _gripTuner = new WheelGripTuner();

        /// <summary>지금 바퀴에 적용 중인 조향 각도(도)입니다.</summary>
        private float _currentSteerAngle;

        // --- Public Properties ---

        /// <summary>차량의 성능 설정입니다.</summary>
        public CarData CarData { get { return _carData; } }

        /// <summary>현재 주행 속도(km/h)입니다.</summary>
        public float CurrentSpeed { get; private set; }

        /// <summary>현재 엔진 회전수입니다.</summary>
        public float CurrentRpm { get { return _powertrain != null ? _powertrain.CurrentRPM : 0f; } }

        /// <summary>
        /// 이번 물리 프레임에 실제로 적용된 스로틀 값입니다. 시동이 꺼져 있으면 0입니다.
        ///
        /// 사운드처럼 스로틀을 참고해야 하는 쪽이 전역 입력을 다시 읽지 않도록 여기서 내보냅니다.
        /// 입력을 읽는 곳은 <see cref="CarInput"/> 한 군데여야 합니다.
        /// </summary>
        public float ThrottleInput { get; private set; }

        /// <summary>남은 연료량입니다.</summary>
        public float CurrentFuel { get { return _powertrain != null ? _powertrain.CurrentFuel : 0f; } }

        /// <summary>연료통 용량입니다.</summary>
        public float MaxFuel { get { return _carData != null ? _carData.maxFuel : 0f; } }

        /// <summary>시동이 걸려 있는지 여부입니다. 연료가 떨어지면 자동으로 꺼집니다.</summary>
        public bool IsEngineOn { get; private set; }

        /// <summary>계기판에 표시할 기어입니다. 후진이면 -1, 중립이면 0입니다.</summary>
        public int CurrentGear { get { return _powertrain != null ? _powertrain.GetDisplayGear() : 0; } }

        /// <summary>이 차량의 내구도입니다. 처음 물어볼 때 자식에서 찾습니다.</summary>
        public VehicleHealth Health
        {
            get
            {
                if (_health == null) _health = GetComponentInChildren<VehicleHealth>(true);
                return _health;
            }
        }

        // --- Unity Event Functions ---

        /// <summary>
        /// 협력자를 모으고 구동계·접지력을 준비합니다. 시동은 꺼진 상태로 시작합니다.
        /// </summary>
        private void Start()
        {
            if (!TryResolveDependencies()) return;

            _body.centerOfMass = _centerOfMass;
            _powertrain.Initialize(_carData);
            _visuals.Initialize(_carData.maxSteerAngle);

            _driveline.Configure(
                new[] { _frontLeftWheel, _frontRightWheel },
                new[] { _rearLeftWheel, _rearRightWheel },
                _driveType,
                _steerType);

            _gripTuner.CacheBaseStiffness(_driveline.Wheels);

            IsEngineOn = false;
        }

        /// <summary>
        /// 한 물리 프레임의 주행을 처리합니다. 순서를 정할 뿐 계산은 협력자들이 합니다.
        /// </summary>
        private void FixedUpdate()
        {
            CurrentSpeed = _body.linearVelocity.magnitude * MetersPerSecondToKmh;

            // 시동이 꺼져 있으면 스로틀을 받지 않습니다.
            ThrottleInput = IsEngineOn ? _input.ThrottleInput : 0f;

            // <b>토크를 연료 소모보다 먼저 구합니다.</b> 순서를 바꾸면 연료가 바닥나는 그 프레임에
            // 시동이 먼저 꺼져서 토크가 0이 됩니다. 한 틱 차이지만 원래 동작이 아닙니다.
            float motorTorque = _powertrain.CalculateMotorTorque(
                _driveline.GetAverageDrivenRpm(), ThrottleInput, CurrentSpeed, IsEngineOn);

            UpdateEngine();
            ApplyDriving(motorTorque);
            ApplyRoadGrip();
        }

        /// <summary>
        /// 바퀴의 시각적 회전을 갱신합니다. 물리와 그림을 다른 타이밍으로 나눠 둡니다.
        /// </summary>
        private void LateUpdate()
        {
            _visuals.UpdateVisuals(_currentSteerAngle);
        }

        // --- Public Methods ---

        /// <summary>
        /// 시동을 걸거나 끕니다. 운전대를 조준해 상호작용하면 호출됩니다.
        /// 연료가 없으면 걸리지 않습니다.
        /// </summary>
        public void ToggleEngine()
        {
            bool wantOn = !IsEngineOn;

            if (wantOn && _powertrain.IsFuelEmpty())
            {
                Debug.Log("CarController: 연료가 없어 시동이 걸리지 않습니다.");
                return;
            }

            SetEngineOn(wantOn);
        }

        /// <summary>
        /// 세이브에서 읽은 연료와 시동 상태를 되돌립니다.
        /// </summary>
        /// <param name="fuel">되돌릴 연료량</param>
        /// <param name="engineOn">되돌릴 시동 상태. 연료가 없으면 무시됩니다.</param>
        public void RestoreState(float fuel, bool engineOn)
        {
            if (_powertrain == null) _powertrain = GetComponent<Powertrain>();
            if (_powertrain == null) return;

            _powertrain.SetFuel(fuel);
            IsEngineOn = engineOn && !_powertrain.IsFuelEmpty();
        }

        // --- Private Methods : 한 프레임의 단계들 ---

        /// <summary>
        /// 동력계를 돌리고 연료를 소모합니다. 연료가 떨어지면 시동을 끕니다.
        /// </summary>
        private void UpdateEngine()
        {
            _powertrain.UpdateFuel(IsEngineOn, ThrottleInput);

            if (IsEngineOn && _powertrain.IsFuelEmpty())
            {
                Debug.Log("CarController: 연료가 떨어져 시동이 꺼졌습니다.");
                SetEngineOn(false);
            }
        }

        /// <summary>
        /// 조향·제동·구동력을 바퀴에 겁니다.
        /// </summary>
        /// <param name="motorTorque">이번 프레임에 걸 구동 토크</param>
        private void ApplyDriving(float motorTorque)
        {
            UpdateSteerAngle(_input.SteerInput);

            _driveline.ApplyBrakeTorque(CalculateBrakeTorque(_input.IsBraking));
            _driveline.ApplyMotorTorque(motorTorque);
            _driveline.ApplySteerAngle(_currentSteerAngle);
        }

        /// <summary>
        /// 날씨에 맞춰 접지력을 조절합니다. 끄면 언제나 원래 접지력을 씁니다.
        /// </summary>
        private void ApplyRoadGrip()
        {
            float grip = _useWeatherGrip
                ? WheelGripTuner.CalculateGrip(_weatherGripInfluence, _minGripFactor)
                : 1f;

            _gripTuner.Apply(grip);
        }

        // --- Private Methods : 계산 ---

        /// <summary>
        /// 조향각을 목표까지 서서히 옮깁니다. 빠를수록 덜 꺾여 고속에서 안정적입니다.
        /// </summary>
        /// <param name="steerInput">-1에서 1 사이의 조향 입력</param>
        private void UpdateSteerAngle(float steerInput)
        {
            float speedRatio = CurrentSpeed / SteerReferenceSpeed;
            float allowedAngle = _carData.maxSteerAngle * (1f - speedRatio * _carData.steerHelper);
            allowedAngle = Mathf.Clamp(allowedAngle, MinDynamicSteerAngle, _carData.maxSteerAngle);

            float targetAngle = allowedAngle * steerInput;
            _currentSteerAngle = Mathf.Lerp(
                _currentSteerAngle, targetAngle, _carData.steerSpeed * Time.fixedDeltaTime);
        }

        /// <summary>
        /// 이번 프레임에 걸 제동 토크를 구합니다.
        /// 스로틀을 놓고 굴러가는 중이면 엔진 브레이크가 약하게 걸립니다.
        /// </summary>
        /// <param name="isBraking">브레이크를 밟고 있는지 여부</param>
        /// <returns>바퀴에 걸 제동 토크</returns>
        private float CalculateBrakeTorque(bool isBraking)
        {
            if (isBraking) return _carData.brakeTorque;

            bool coasting = IsEngineOn
                            && Mathf.Approximately(ThrottleInput, 0f)
                            && CurrentSpeed > EngineBrakeMinSpeed;

            return coasting ? EngineBrakeTorque : 0f;
        }

        /// <summary>
        /// 시동 상태를 바꾸고 그에 맞는 소리를 냅니다.
        /// </summary>
        /// <param name="on">켤 것인지 여부</param>
        private void SetEngineOn(bool on)
        {
            if (IsEngineOn == on) return;

            IsEngineOn = on;
            if (_soundController == null) return;

            if (on) _soundController.PlayEngineStart();
            else _soundController.PlayEngineStop();
        }

        // --- Private Methods : 준비 ---

        /// <summary>
        /// 협력자를 모으고 필수 항목이 갖춰졌는지 확인합니다.
        ///
        /// <b>CarData 검사가 먼저입니다.</b> 아래의 모든 계산이 이 에셋을 그냥 역참조하므로,
        /// 여기서 막지 못하면 매 물리 프레임 예외가 납니다.
        /// </summary>
        /// <returns>주행할 수 있으면 true. false면 이 컴포넌트는 꺼집니다.</returns>
        private bool TryResolveDependencies()
        {
            if (_carData == null)
            {
                Debug.LogError("CarController: CarData가 연결되지 않아 주행할 수 없습니다. " +
                               "인스펙터에서 carData를 지정하세요.", this);
                enabled = false;
                return false;
            }

            _body = GetComponent<Rigidbody>();
            _input = GetComponent<CarInput>();
            _powertrain = GetComponent<Powertrain>();
            _visuals = GetComponent<CarVisuals>();

            // 사운드는 있으면 쓰고 없으면 조용히 넘어갑니다.
            _soundController = GetComponent<CarSoundController>();

            return true;
        }
    }
}
