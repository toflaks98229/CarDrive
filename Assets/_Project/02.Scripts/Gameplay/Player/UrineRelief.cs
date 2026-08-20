using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 배뇨 해소의 <b>계산</b>을 맡습니다. 그리는 일은 <see cref="UrineStreamView"/>가 합니다.
    ///
    /// 배뇨 키(<c>GameAction.Relieve</c>, 기본 P)를 누르고 있는 동안 물줄기가 이어집니다.
    /// 출력은 두 가지가 합쳐져 결정됩니다.
    ///  - 잔뇨: 남은 양이 적을수록 약해집니다. 끝에 가면 힘없이 흘러내립니다.
    ///  - 압력: 배뇨 키를 연타할 때마다 붙습니다. 잔뇨가 적어도 연타하면 다시 세게 나갑니다.
    ///
    /// 조준은 시선의 <b>상하 각도</b>를 따릅니다. 하늘을 향해 쏘면 포물선을 그리며
    /// 자기 위로 되떨어지고(역류), 그만큼 갈증이 줄되 청결이 급격히 나빠집니다.
    ///
    /// <b>이 클래스에는 ParticleSystem 코드가 한 줄도 없습니다.</b>
    /// 뷰에 넘기는 것은 숫자 둘뿐입니다 — 출력(0~1)과 노즐 각도(도).
    /// 그래서 물의 양을 조율하는 사람이 파티클 API를 읽을 필요가 없고,
    /// 반대로 연출을 바꾸는 사람이 니즈 계산을 건드릴 위험도 없습니다.
    ///
    /// 인스펙터 값은 예전 이름 그대로 읽어 옵니다. Unity는 필드 <b>이름</b>으로 직렬화하므로,
    /// <see cref="FormerlySerializedAsAttribute"/>가 없으면 씬에 맞춰 둔 수치가 전부 초기화됩니다.
    /// </summary>
    public class UrineRelief : MonoBehaviour
    {
        // --- Constants ---

        /// <summary>이 아래로 내려가면 손을 뗀 것으로 봅니다.</summary>
        private const float HoldEpsilon = 0.001f;

        /// <summary>배뇨 수치는 한계 1.5까지 오르므로, 출력 계산에서는 1을 최대로 봅니다.</summary>
        private const float FullVolume = 1f;

        // --- Serialized Fields : 연동 ---

        /// <summary>물줄기를 그릴 파티클입니다. 비워두면 자식에서 찾습니다.</summary>
        [Header("연동")]
        [Tooltip("노란 물줄기 파티클. 방출은 코드가 직접 하므로 Emission 모듈은 꺼 둡니다.")]
        [SerializeField, FormerlySerializedAs("stream")]
        private ParticleSystem _stream;

        /// <summary>니즈를 반영할 시스템입니다. 비워두면 실행 중에 찾습니다.</summary>
        [Tooltip("니즈 시스템. 비워두면 씬에서 자동으로 찾습니다.")]
        [SerializeField, FormerlySerializedAs("needsSystem")]
        private NeedsSystem _needsSystem;

        // --- Serialized Fields : 조준 ---

        /// <summary>상하 각도를 읽어 올 대상입니다. 보통 메인 카메라입니다.</summary>
        [Header("조준 - 시선의 상하 각도를 따릅니다")]
        [Tooltip("상하 각도를 읽어 올 대상(보통 메인 카메라). 비워두면 Camera.main을 씁니다.")]
        [SerializeField, FormerlySerializedAs("aimSource")]
        private Transform _aimSource;

        /// <summary>시선 각도가 노즐에 반영되는 비율입니다.</summary>
        [Tooltip("시선 각도가 노즐에 반영되는 비율. 1이면 보는 각도 그대로 쏩니다.")]
        [Range(0f, 1f)]
        [SerializeField, FormerlySerializedAs("pitchInfluence")]
        private float _pitchInfluence = 1f;

        /// <summary>노즐이 향할 수 있는 최대 각도(도)입니다.</summary>
        [Tooltip("노즐이 향할 수 있는 최대 각도(도). 위아래 공통입니다.")]
        [Range(10f, 89f)]
        [SerializeField, FormerlySerializedAs("maxAimAngle")]
        private float _maxAimAngle = 85f;

        // --- Serialized Fields : 배출 ---

        /// <summary>출력이 최대일 때 초당 줄어드는 배뇨량입니다.</summary>
        [Header("배출 속도")]
        [Tooltip("출력이 최대일 때 초당 줄어드는 배뇨량. 낮출수록 비우는 데 오래 걸립니다.")]
        [SerializeField, FormerlySerializedAs("reliefPerSecond")]
        private float _reliefPerSecond = 0.13f;

        /// <summary>이 값 아래로는 더 나오지 않습니다.</summary>
        [Tooltip("이 값 아래로는 더 나오지 않습니다.")]
        [SerializeField, FormerlySerializedAs("emptyThreshold")]
        private float _emptyThreshold = 0.015f;

        /// <summary>거의 다 비웠을 때 남는 기본 출력 비율입니다.</summary>
        [Header("잔뇨 - 남은 양이 적을수록 약해집니다")]
        [Tooltip("거의 다 비웠을 때 남는 기본 출력 비율. 0에 가까울수록 끝이 확 죽습니다.")]
        [Range(0f, 1f)]
        [SerializeField, FormerlySerializedAs("minFlowFromVolume")]
        private float _minFlowFromVolume = 0.16f;

        /// <summary>남은 양에 따른 출력 곡선입니다.</summary>
        [Tooltip("남은 양(가로 0~1)에 따른 출력(세로 0~1) 곡선")]
        [SerializeField, FormerlySerializedAs("volumeCurve")]
        private AnimationCurve _volumeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // --- Serialized Fields : 압력 ---

        /// <summary>한 번 누를 때 오르는 압력입니다.</summary>
        [Header("연타 압력 - 누를 때마다 힘이 붙습니다")]
        [Tooltip("한 번 누를 때 오르는 압력")]
        [Range(0f, 1f)]
        [SerializeField, FormerlySerializedAs("pressurePerPress")]
        private float _pressurePerPress = 0.5f;

        /// <summary>압력이 초당 빠지는 속도입니다.</summary>
        [Tooltip("압력이 초당 빠지는 속도")]
        [SerializeField, FormerlySerializedAs("pressureDecayPerSecond")]
        private float _pressureDecayPerSecond = 1.6f;

        /// <summary>압력이 출력에 더해지는 양입니다.</summary>
        [Tooltip("압력이 출력에 더해지는 양")]
        [Range(0f, 1f)]
        [SerializeField, FormerlySerializedAs("pressureFlowBonus")]
        private float _pressureFlowBonus = 0.9f;

        /// <summary>연타로 끌어올릴 수 있는 출력 상한입니다.</summary>
        [Tooltip("연타로 끌어올릴 수 있는 출력 상한. 기본 최대 출력(1.0)에 대한 비율입니다. " +
                 "잔뇨가 넉넉해서 이미 이 값보다 세게 나오고 있다면 깎지 않습니다.")]
        [Range(0f, 1f)]
        [SerializeField, FormerlySerializedAs("pressureFlowCeiling")]
        private float _pressureFlowCeiling = 0.8f;

        /// <summary>압력이 최대일 때 배출 속도에 붙는 추가 배율입니다.</summary>
        [Tooltip("압력이 최대일 때 배출 속도에 붙는 추가 배율")]
        [SerializeField, FormerlySerializedAs("pressureDrainBonus")]
        private float _pressureDrainBonus = 0.8f;

        /// <summary>키를 놓은 뒤 줄기가 잦아드는 속도입니다.</summary>
        [Header("놓았을 때")]
        [Tooltip("키를 놓은 뒤 줄기가 잦아드는 속도. 연타할 때 뚝뚝 끊기지 않게 해 줍니다.")]
        [SerializeField, FormerlySerializedAs("releaseFadeSpeed")]
        private float _releaseFadeSpeed = 6f;

        // --- Serialized Fields : 연출 수치 ---
        //
        // 값은 여기 남습니다. UrineStreamView 로 옮기면 직렬화 경로가 바뀌어
        // 씬에 맞춰 둔 수치(속도 15, 수명 3 등)가 전부 초기화됩니다.
        // 뷰는 Start 에서 이 값들을 넘겨받습니다.

        /// <summary>출력이 최대일 때 초당 방출 입자 수입니다.</summary>
        [Header("파티클 출력")]
        [Tooltip("출력이 최대일 때 초당 방출 입자 수")]
        [SerializeField, FormerlySerializedAs("maxEmissionRate")]
        private float _maxEmissionRate = 110f;

        /// <summary>출력이 최소일 때 초당 방출 입자 수입니다.</summary>
        [Tooltip("출력이 최소일 때 초당 방출 입자 수")]
        [SerializeField, FormerlySerializedAs("minEmissionRate")]
        private float _minEmissionRate = 16f;

        /// <summary>출력이 최대일 때의 입자 속도입니다.</summary>
        [SerializeField, FormerlySerializedAs("maxSpeed")]
        private float _maxSpeed = 6.2f;

        /// <summary>출력이 최소일 때의 입자 속도입니다.</summary>
        [SerializeField, FormerlySerializedAs("minSpeed")]
        private float _minSpeed = 1.4f;

        /// <summary>출력이 최대일 때의 입자 수명입니다.</summary>
        [SerializeField, FormerlySerializedAs("maxLifetime")]
        private float _maxLifetime = 1.2f;

        /// <summary>출력이 최소일 때의 입자 수명입니다.</summary>
        [SerializeField, FormerlySerializedAs("minLifetime")]
        private float _minLifetime = 0.45f;

        /// <summary>물줄기가 퍼지는 원뿔 각도(도)입니다.</summary>
        [Tooltip("물줄기가 퍼지는 원뿔 각도(도). 작을수록 일직선에 가깝습니다.")]
        [SerializeField, FormerlySerializedAs("coneAngle")]
        private float _coneAngle = 1.2f;

        // --- Serialized Fields : 부수 효과 ---

        /// <summary>초당 오르는 더러움입니다.</summary>
        [Header("부수 효과 (초당)")]
        [SerializeField, FormerlySerializedAs("hygieneCostPerSecond")]
        private float _hygieneCostPerSecond = 0.006f;

        /// <summary>초당 줄어드는 스트레스입니다.</summary>
        [SerializeField, FormerlySerializedAs("stressReliefPerSecond")]
        private float _stressReliefPerSecond = 0.02f;

        /// <summary>이 각도 위로 쏘기 시작하면 조금씩 되떨어집니다.</summary>
        [Header("역류 - 위로 쏘면 자기가 뒤집어씁니다")]
        [Tooltip("이 각도(도) 위로 쏘기 시작하면 조금씩 되떨어집니다.")]
        [Range(0f, 89f)]
        [SerializeField, FormerlySerializedAs("backsplashStartAngle")]
        private float _backsplashStartAngle = 25f;

        /// <summary>이 각도부터는 전부 자기 위로 떨어집니다.</summary>
        [Tooltip("이 각도(도)부터는 전부 자기 위로 떨어집니다.")]
        [Range(0f, 89f)]
        [SerializeField, FormerlySerializedAs("backsplashFullAngle")]
        private float _backsplashFullAngle = 70f;

        /// <summary>역류가 최대일 때 초당 줄어드는 갈증입니다.</summary>
        [Tooltip("역류가 최대일 때 초당 줄어드는 갈증. 마시는 게 아니라 얼굴로 받는 것이라 " +
                 "물을 마시는 것보다 훨씬 적게 잡습니다.")]
        [SerializeField, FormerlySerializedAs("backsplashThirstReliefPerSecond")]
        private float _backsplashThirstReliefPerSecond = 0.012f;

        /// <summary>역류가 최대일 때 청결 악화에 곱해지는 배율입니다.</summary>
        [Tooltip("역류가 최대일 때 청결 악화에 곱해지는 배율. 8이면 평소의 9배로 더러워집니다.")]
        [SerializeField, FormerlySerializedAs("backsplashHygieneMultiplier")]
        private float _backsplashHygieneMultiplier = 8f;

        /// <summary>역류가 최대일 때 초당 오르는 스트레스입니다.</summary>
        [Tooltip("역류가 최대일 때 초당 오르는 스트레스. 역류 중에는 시원함(스트레스 해소)이 사라집니다.")]
        [SerializeField, FormerlySerializedAs("backsplashStressPerSecond")]
        private float _backsplashStressPerSecond = 0.05f;

        // --- Serialized Fields : 이벤트 ---

        /// <summary>줄기가 시작될 때 호출됩니다.</summary>
        [Header("이벤트")]
        [Tooltip("줄기가 시작될 때")]
        [SerializeField, FormerlySerializedAs("onStreamStart")]
        private UnityEvent _onStreamStart;

        /// <summary>줄기가 멈출 때 호출됩니다.</summary>
        [Tooltip("줄기가 멈출 때")]
        [SerializeField, FormerlySerializedAs("onStreamStop")]
        private UnityEvent _onStreamStop;

        /// <summary>다 비워서 더 나오지 않을 때 호출됩니다.</summary>
        [Tooltip("다 비워서 더 나오지 않을 때")]
        [SerializeField, FormerlySerializedAs("onEmpty")]
        private UnityEvent _onEmpty;

        // --- Public Properties ---

        /// <summary>현재 출력 비율(0~1)입니다. 사운드 볼륨 등에 쓸 수 있습니다.</summary>
        public float CurrentOutput { get; private set; }

        /// <summary>연타로 쌓인 압력(0~1)입니다.</summary>
        public float Pressure { get { return _pressure; } }

        /// <summary>지금 나오고 있는지 여부입니다.</summary>
        public bool IsStreaming { get; private set; }

        /// <summary>물줄기가 수평에서 위로 향한 각도(도)입니다. 아래를 향하면 음수입니다.</summary>
        public float StreamElevation { get; private set; }

        /// <summary>
        /// 역류 정도(0~1)입니다. 1이면 쏜 것이 전부 자기 위로 되떨어집니다.
        /// </summary>
        public float Backsplash
        {
            get
            {
                if (_backsplashFullAngle <= _backsplashStartAngle)
                {
                    return StreamElevation >= _backsplashStartAngle ? 1f : 0f;
                }
                return Mathf.Clamp01(
                    Mathf.InverseLerp(_backsplashStartAngle, _backsplashFullAngle, StreamElevation));
            }
        }

        // --- Private Member Variables ---

        /// <summary>물줄기를 그리는 쪽입니다. 이 클래스는 숫자만 넘깁니다.</summary>
        private readonly UrineStreamView _view = new UrineStreamView();

        /// <summary>지금 물줄기의 압력(0~1)입니다.</summary>
        private float _pressure;

        /// <summary>1이면 누르는 중, 놓으면 서서히 0으로 내려갑니다.</summary>
        private float _holdAmount;

        /// <summary>직전 프레임에 배뇨가 비어 있었는지 여부입니다. 비는 순간에만 이벤트를 던지는 데 씁니다.</summary>
        private bool _wasEmptyLastFrame;

        // --- Unity Event Functions ---

        /// <summary>
        /// 니즈 시스템과 파티클을 찾아 뷰를 준비합니다.
        /// </summary>
        private void Start()
        {
            if (_needsSystem == null) _needsSystem = GameContext.Resolve<NeedsSystem>(this);
            if (_needsSystem == null) Debug.LogWarning("UrineRelief: NeedsSystem을 찾지 못했습니다.", this);

            if (_stream == null) _stream = GetComponentInChildren<ParticleSystem>(true);
            if (!_view.Configure(_stream, _coneAngle, BuildEmissionRange()))
            {
                Debug.LogWarning("UrineRelief: 파티클이 없어 물줄기가 보이지 않습니다.", this);
                return;
            }

            // 정면을 볼 때의 기본 숙임을 기준으로 삼습니다.
            StreamElevation = -_view.BasePitch;
        }

        /// <summary>
        /// 입력에 따라 압력·조준·출력을 갱신하고 니즈에 반영합니다.
        /// </summary>
        private void Update()
        {
            // <b>여기는 Suspended를 직접 봅니다.</b> 키가 false가 되는 것만으로는 물줄기가
            // 여운(_releaseFadeSpeed)을 남기며 서서히 잦아듭니다. 오버레이가 열린 동안
            // 파티클이 계속 뿜어지지 않도록 즉시 끊습니다.
            if (GameInput.Suspended)
            {
                StopStream();
                return;
            }

            float deltaTime = Time.deltaTime;

            UpdateAim();
            UpdatePressure(deltaTime);
            UpdateHold(deltaTime);

            if (!TryGetRemaining(out float remaining))
            {
                StopStream();
                return;
            }

            float flow = CalculateFlow(remaining);
            CurrentOutput = flow;

            BeginStreamIfNeeded();

            _view.Emit(flow, deltaTime);
            ApplyDrain(flow, deltaTime);
        }

        // --- Public Methods ---

        /// <summary>
        /// 물줄기를 멈춥니다. 이미 멈춰 있으면 아무 일도 하지 않습니다.
        /// </summary>
        public void StopStream()
        {
            CurrentOutput = 0f;
            _view.ResetEmission();

            if (!IsStreaming) return;

            IsStreaming = false;
            if (_onStreamStop != null) _onStreamStop.Invoke();
        }

        // --- Private Methods : 한 프레임의 단계들 ---

        /// <summary>
        /// 시선의 상하 각도를 노즐 각도로 바꿔 뷰에 넘깁니다.
        /// </summary>
        private void UpdateAim()
        {
            if (!_view.IsReady) return;

            if (_aimSource == null)
            {
                _aimSource = GameContext.MainCameraTransform;
                if (_aimSource == null) return;
            }

            // 시선이 수평에서 위로 향한 각도. 위를 보면 양수입니다.
            float lookElevation = Mathf.Asin(Mathf.Clamp(_aimSource.forward.y, -1f, 1f)) * Mathf.Rad2Deg;

            // Unity의 X 회전은 아래를 볼 때 양수라 부호를 뒤집어 더합니다.
            float pitch = _view.BasePitch - lookElevation * _pitchInfluence;
            pitch = Mathf.Clamp(pitch, -_maxAimAngle, _maxAimAngle);

            _view.SetPitch(pitch);
            StreamElevation = -pitch;
        }

        /// <summary>
        /// 연타로 압력을 쌓고, 시간이 지나면 빠지게 합니다.
        /// </summary>
        /// <param name="deltaTime">이번 프레임의 시간</param>
        private void UpdatePressure(float deltaTime)
        {
            if (GameInput.RelievePressed)
            {
                _pressure = Mathf.Clamp01(_pressure + _pressurePerPress);
            }

            _pressure = Mathf.MoveTowards(_pressure, 0f, _pressureDecayPerSecond * deltaTime);
        }

        /// <summary>
        /// 누르고 있으면 1, 놓으면 서서히 0으로 내려갑니다.
        /// 연타할 때 줄기가 뚝뚝 끊기지 않도록 여운을 남깁니다.
        /// </summary>
        /// <param name="deltaTime">이번 프레임의 시간</param>
        private void UpdateHold(float deltaTime)
        {
            if (GameInput.Relieve)
            {
                _holdAmount = 1f;
                return;
            }

            _holdAmount = Mathf.MoveTowards(_holdAmount, 0f, _releaseFadeSpeed * deltaTime);
        }

        /// <summary>
        /// 지금 나올 수 있는지 확인하고, 나올 수 있으면 남은 양을 돌려줍니다.
        ///
        /// 손을 뗐는지·니즈 시스템이 있는지·다 비웠는지 세 가지를 한자리에서 봅니다.
        /// 예전에는 Update 안에 세 개의 조기 반환으로 흩어져 있었습니다.
        /// </summary>
        /// <param name="remaining">남은 배뇨량. 나올 수 없으면 0입니다.</param>
        /// <returns>물줄기를 이어도 되면 true</returns>
        private bool TryGetRemaining(out float remaining)
        {
            remaining = 0f;

            if (_holdAmount <= HoldEpsilon) return false;
            if (_needsSystem == null) return false;

            remaining = _needsSystem.GetValue(NeedType.Urine);
            if (remaining > _emptyThreshold)
            {
                _wasEmptyLastFrame = false;
                return true;
            }

            // 비는 <b>순간</b>에만 알립니다. 계속 누르고 있다고 매 프레임 던지지 않습니다.
            if (!_wasEmptyLastFrame && _onEmpty != null) _onEmpty.Invoke();
            _wasEmptyLastFrame = true;
            return false;
        }

        /// <summary>
        /// 줄기가 막 시작되었다면 알립니다.
        /// </summary>
        private void BeginStreamIfNeeded()
        {
            if (IsStreaming) return;

            IsStreaming = true;
            if (_onStreamStart != null) _onStreamStart.Invoke();
        }

        // --- Private Methods : 계산 ---

        /// <summary>
        /// 잔뇨와 연타 압력을 합쳐 이번 프레임의 출력을 구합니다.
        /// </summary>
        /// <param name="remaining">남은 배뇨량</param>
        /// <returns>0에서 1 사이의 출력</returns>
        private float CalculateFlow(float remaining)
        {
            // 배뇨는 한계 1.5까지 차오르므로 1을 넘는 부분은 최대로 봅니다.
            float volume = Mathf.Clamp01(remaining / FullVolume);
            float fromVolume = Mathf.Lerp(
                _minFlowFromVolume, 1f, Mathf.Clamp01(_volumeCurve.Evaluate(volume)));

            // 연타로 올린 출력은 상한을 넘지 못합니다. 단, 잔뇨만으로 이미 그보다 세게 나오는 중이라면
            // 압력 때문에 오히려 약해지면 안 되므로 잔뇨 출력을 하한으로 잡습니다.
            float boosted = Mathf.Min(fromVolume + _pressure * _pressureFlowBonus, _pressureFlowCeiling);
            float flow = Mathf.Clamp01(Mathf.Max(fromVolume, boosted));

            return flow * _holdAmount;
        }

        /// <summary>
        /// 출력만큼 배뇨를 덜고, 그에 딸린 청결·갈증·스트레스 변화를 반영합니다.
        /// </summary>
        /// <param name="flow">이번 프레임의 출력</param>
        /// <param name="deltaTime">이번 프레임의 시간</param>
        private void ApplyDrain(float flow, float deltaTime)
        {
            float drain = _reliefPerSecond * flow * (1f + _pressure * _pressureDrainBonus);
            _needsSystem.Satisfy(NeedType.Urine, drain * deltaTime);

            float splash = Backsplash;

            if (_hygieneCostPerSecond > 0f)
            {
                float dirtiness = _hygieneCostPerSecond * (1f + splash * _backsplashHygieneMultiplier);
                _needsSystem.Add(NeedType.Hygiene, dirtiness * flow * deltaTime);
            }

            if (splash > 0f && _backsplashThirstReliefPerSecond > 0f)
            {
                _needsSystem.Satisfy(
                    NeedType.Thirst, _backsplashThirstReliefPerSecond * splash * flow * deltaTime);
            }

            if (_stressReliefPerSecond > 0f)
            {
                _needsSystem.Satisfy(
                    NeedType.Stress, _stressReliefPerSecond * (1f - splash) * flow * deltaTime);
            }

            if (splash > 0f && _backsplashStressPerSecond > 0f)
            {
                _needsSystem.Add(
                    NeedType.Stress, _backsplashStressPerSecond * splash * flow * deltaTime);
            }
        }

        /// <summary>
        /// 인스펙터에 적어 둔 연출 수치를 뷰가 쓸 형태로 묶습니다.
        /// </summary>
        /// <returns>출력에 따라 보간할 방출 범위</returns>
        private StreamEmissionRange BuildEmissionRange()
        {
            return new StreamEmissionRange
            {
                MinRate = _minEmissionRate,
                MaxRate = _maxEmissionRate,
                MinSpeed = _minSpeed,
                MaxSpeed = _maxSpeed,
                MinLifetime = _minLifetime,
                MaxLifetime = _maxLifetime
            };
        }
    }
}
