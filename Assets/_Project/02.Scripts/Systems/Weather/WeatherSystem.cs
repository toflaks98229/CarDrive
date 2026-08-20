using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>날씨 종류 하나를 인자로 넘기는 인스펙터 연결용 이벤트입니다.</summary>
    [System.Serializable]
    public class WeatherTypeEvent : UnityEvent<WeatherType> { }

    /// <summary>
    /// 날씨의 조율자입니다. <b>스스로 계산하지 않고 순서만 정합니다.</b>
    ///
    /// 예전에는 이 클래스 하나가 다섯 가지를 했습니다.
    ///  ① 프리셋 표 관리 ② 다음 날씨 선택 ③ 경로 탐색과 전환 진행
    ///  ④ 표현·영향 수치 보간 ⑤ 세이브 직렬화
    /// 각각은 잘 쓰였지만 한 파일에 있어서, "눈"을 추가하려면 다섯 곳을 동시에 이해해야 했습니다.
    ///
    /// 지금은 넷으로 나뉘어 있습니다.
    ///  - <see cref="WeatherCatalog"/> — 프리셋에 대해 <b>묻는 곳</b>. 아무것도 바꾸지 않습니다.
    ///  - <see cref="WeatherPicker"/> — 무엇이 다음에 오는가. <b>기획 판단</b>이라 갈아 끼울 수 있게 뺐습니다.
    ///  - <see cref="WeatherTransition"/> — 어디에서 어디로 얼마나 왔는가. 상태 여덟 개를 소유합니다.
    ///  - <see cref="WeatherValueBlender"/> — 그래서 지금 수치는 얼마인가. 상태가 없습니다.
    ///
    /// 이 클래스에 남은 것은 <b>인스펙터 설정, 공개 API, 이벤트, 세이브</b>뿐입니다.
    ///
    /// <b>설정값은 여기 남습니다.</b> 다른 클래스로 옮기면 직렬화 경로가 바뀌어 씬에 맞춰 둔
    /// 값이 전부 초기화됩니다. 계산에 필요한 값은 호출할 때 넘깁니다.
    /// </summary>
    public class WeatherSystem : MonoBehaviour, ISaveable
    {
        // --- Serialized Fields ---

        /// <summary>날씨별 설정입니다. 비워두면 <see cref="WeatherDefaults"/>의 기본값을 씁니다.</summary>
        [Header("설정")]
        [Tooltip("날씨별 설정. 비워두면 WeatherDefaults의 기본값이 사용됩니다.")]
        [SerializeField, FormerlySerializedAs("presets")]
        private List<WeatherPreset> _presets = new List<WeatherPreset>();

        /// <summary>게임을 시작할 때의 날씨입니다.</summary>
        [Tooltip("시작 날씨")]
        [SerializeField, FormerlySerializedAs("startWeather")]
        private WeatherType _startWeather = WeatherType.Clear;

        /// <summary>시간이 지나면서 저절로 날씨가 바뀔지 여부입니다.</summary>
        [Tooltip("체크하면 시간이 지나면서 저절로 날씨가 바뀝니다.")]
        [SerializeField, FormerlySerializedAs("autoChange")]
        private bool _autoChange = true;

        /// <summary>TimeSystem이 없을 때 쓸 시간 배율입니다.</summary>
        [Tooltip("TimeSystem이 없을 때 쓸 시간 배율(실제 1초당 게임 분). " +
                 "TimeSystem이 있으면 그쪽 값을 따릅니다.")]
        [SerializeField, FormerlySerializedAs("fallbackMinutesPerSecond")]
        private float _fallbackMinutesPerSecond = 1f;

        /// <summary>심각도가 한 칸 차이 날 때의 전환 시간(게임 분)입니다.</summary>
        [Header("전환 - 얼마나 천천히 바뀌는가")]
        [Tooltip("심각도가 한 칸(1.0) 차이 날 때의 전환 시간(게임 분). " +
                 "차이가 작으면 그만큼 짧아집니다.")]
        [SerializeField, FormerlySerializedAs("transitionMinutesPerSeverity")]
        private float _transitionMinutesPerSeverity = 90f;

        /// <summary>전환 시간의 최소값(게임 분)입니다.</summary>
        [Tooltip("전환 시간의 최소값(게임 분)")]
        [SerializeField, FormerlySerializedAs("minTransitionMinutes")]
        private float _minTransitionMinutes = 15f;

        /// <summary>한 번에 건널 수 있는 최대 심각도 차이입니다.</summary>
        [Tooltip("한 번에 건널 수 있는 최대 심각도 차이. " +
                 "이보다 크면 중간 날씨를 거쳐 갑니다. (맑음 → 폭우 직행 방지)")]
        [Range(0.1f, 1f)]
        [SerializeField, FormerlySerializedAs("maxSeverityJump")]
        private float _maxSeverityJump = 0.3f;

        /// <summary>경로 중간 날씨에서 잠시 머무는 시간(게임 분)입니다.</summary>
        [Tooltip("경로 중간 날씨에서 잠시 머무는 시간(게임 분). 하늘이 서서히 무거워지는 느낌을 만듭니다.")]
        [SerializeField, FormerlySerializedAs("routeHoldMinutes")]
        private float _routeHoldMinutes = 25f;

        /// <summary>구름·어두워짐이 전환의 몇 지점에서 완료되는지입니다.</summary>
        [Header("전환 - 무엇이 먼저 변하는가")]
        [Tooltip("구름·어두워짐이 전환의 앞쪽 몇 지점에서 완료되는지. " +
                 "0.45면 전환의 45% 시점에 하늘이 이미 다 흐려집니다.")]
        [Range(0.1f, 1f)]
        [SerializeField, FormerlySerializedAs("cloudLeadPortion")]
        private float _cloudLeadPortion = 0.45f;

        /// <summary>비가 전환의 몇 지점부터 내리기 시작하는지입니다.</summary>
        [Tooltip("비가 전환의 몇 지점부터 내리기 시작하는지. " +
                 "0.35면 하늘이 어느 정도 흐려진 뒤에야 빗방울이 떨어집니다.")]
        [Range(0f, 0.9f)]
        [SerializeField, FormerlySerializedAs("rainStartPortion")]
        private float _rainStartPortion = 0.35f;

        /// <summary>이 심각도를 넘으면 '궂은 날씨'로 봅니다.</summary>
        [Header("간격")]
        [Tooltip("심각도가 이 값을 넘으면 '궂은 날씨'로 봅니다.")]
        [Range(0f, 1f)]
        [SerializeField, FormerlySerializedAs("severeThreshold")]
        private float _severeThreshold = 0.65f;

        /// <summary>궂은 날씨 뒤 온화하게 유지할 시간(게임 분)입니다.</summary>
        [Tooltip("궂은 날씨 뒤에는 이 시간(게임 분) 동안 온화한 날씨만 옵니다.")]
        [SerializeField, FormerlySerializedAs("calmAfterSevereMinutes")]
        private float _calmAfterSevereMinutes = 180f;

        /// <summary>새 날씨로 전환이 시작될 때 호출됩니다.</summary>
        [Header("이벤트")]
        [Tooltip("새 날씨로 전환이 시작될 때 (목표 날씨를 넘깁니다)")]
        [SerializeField, FormerlySerializedAs("onWeatherChangeStarted")]
        private WeatherTypeEvent _onWeatherChangeStarted;

        /// <summary>전환이 끝나 완전히 그 날씨가 되었을 때 호출됩니다.</summary>
        [Tooltip("전환이 끝나 완전히 그 날씨가 되었을 때")]
        [SerializeField, FormerlySerializedAs("onWeatherChanged")]
        private WeatherTypeEvent _onWeatherChanged;

        // --- Private Member Variables ---

        /// <summary>프리셋에 대해 묻는 곳입니다.</summary>
        private readonly WeatherCatalog _catalog = new WeatherCatalog();

        /// <summary>다음 날씨를 고르는 정책입니다.</summary>
        private WeatherPicker _picker;

        /// <summary>어디에서 어디로 얼마나 왔는지입니다.</summary>
        private WeatherTransition _transition;

        /// <summary>지금 수치가 얼마인지 계산합니다.</summary>
        private readonly WeatherValueBlender _blender = new WeatherValueBlender();

        // --- Public Properties : 접근 ---

        /// <summary>씬의 날씨 시스템입니다. 없으면 정적 접근자들이 "영향 없음" 값을 돌려줍니다.</summary>
        public static WeatherSystem Instance { get { return GameContext.Get<WeatherSystem>(); } }

        // --- Public Properties : 상태 ---

        /// <summary>지금 날씨입니다.</summary>
        public WeatherType Current { get { return _transition.Current; } }

        /// <summary>이번 걸음의 목표입니다. 중간 기착지일 수 있습니다.</summary>
        public WeatherType Target { get { return _transition.Target; } }

        /// <summary>최종적으로 가려는 날씨입니다.</summary>
        public WeatherType FinalTarget { get { return _transition.FinalTarget; } }

        /// <summary>전환 진행도(0~1)입니다.</summary>
        public float Blend { get { return _transition.Blend; } }

        /// <summary>지금 전환 중인지 여부입니다.</summary>
        public bool IsTransitioning { get { return _transition.IsTransitioning; } }

        /// <summary>지금 날씨의 강도(0~1)입니다.</summary>
        public float Intensity { get { return _blender.Intensity; } }

        // --- Public Properties : 표현 수치 ---

        /// <summary>구름이 하늘을 덮은 정도(0~1)입니다.</summary>
        public float CloudCover { get { return _blender.CloudCover; } }

        /// <summary>비의 세기입니다.</summary>
        public float RainIntensity { get { return _blender.RainIntensity; } }

        /// <summary>안개의 짙기(0~1)입니다.</summary>
        public float FogDensity { get { return _blender.FogDensity; } }

        /// <summary>바람의 세기(0~1)입니다.</summary>
        public float WindStrength { get { return _blender.WindStrength; } }

        /// <summary>날씨 때문에 어두워진 정도(0~1)입니다.</summary>
        public float Darkness { get { return _blender.Darkness; } }

        // --- Public Properties : 불이익 ---

        /// <summary>시야 배율입니다.</summary>
        public float VisibilityMultiplier { get { return _blender.VisibilityMultiplier; } }

        /// <summary>노면 미끄러움입니다.</summary>
        public float RoadSlipperiness { get { return _blender.RoadSlipperiness; } }

        /// <summary>연료 소모 배율입니다.</summary>
        public float FuelConsumptionMultiplier { get { return _blender.FuelConsumptionMultiplier; } }

        /// <summary>초당 더러움 변화입니다. 음수면 비에 씻깁니다.</summary>
        public float HygieneChangePerSecond { get { return _blender.HygieneChangePerSecond; } }

        /// <summary>초당 오르는 스트레스입니다.</summary>
        public float StressPerSecond { get { return _blender.StressPerSecond; } }

        /// <summary>귀신 활동량 배율입니다.</summary>
        public float GhostActivity { get { return _blender.GhostActivity; } }

        // --- Public Properties : 이익 ---

        /// <summary>귀신을 알아채는 거리 배율입니다.</summary>
        public float GhostDetectionMultiplier { get { return _blender.GhostDetectionMultiplier; } }

        /// <summary>수면 회복 배율입니다.</summary>
        public float SleepQualityMultiplier { get { return _blender.SleepQualityMultiplier; } }

        /// <summary>초당 줄어드는 갈증입니다.</summary>
        public float ThirstReliefPerSecond { get { return _blender.ThirstReliefPerSecond; } }

        /// <summary>초당 줄어드는 스트레스입니다.</summary>
        public float StressReliefPerSecond { get { return _blender.StressReliefPerSecond; } }

        // --- Unity Event Functions ---

        /// <summary>
        /// 자신을 등록하고 협력자들을 준비한 뒤 시작 날씨를 세웁니다.
        /// </summary>
        private void Awake()
        {
            // 등록이 거부되면 이미 다른 것이 있다는 뜻입니다. (경고는 GameContext가 남깁니다)
            if (!GameContext.Register(this))
            {
                enabled = false;
                return;
            }

            SaveRegistry.Register(this);

            _presets = _catalog.Build(_presets, this);
            _picker = new WeatherPicker(_catalog);
            _transition = new WeatherTransition(_catalog);

            _transition.SetImmediate(_startWeather);
            ApplyValues();
        }

        /// <summary>등록을 해제합니다.</summary>
        private void OnDestroy()
        {
            GameContext.Unregister(this);
            SaveRegistry.Unregister(this);
        }

        /// <summary>
        /// 게임 시간만큼 날씨를 진행시키고 수치를 다시 계산합니다.
        /// </summary>
        private void Update()
        {
            float gameMinutes = Time.deltaTime * TimeSystem.GetMinutesPerSecond(_fallbackMinutesPerSecond);

            if (_transition.IsTransitioning) AdvanceTransition(gameMinutes);
            else if (_autoChange) AdvanceHold(gameMinutes);

            ApplyValues();
        }

        // --- ISaveable ---

        /// <summary>시계 다음입니다. 전환 진행도가 시각에 묶여 있습니다.</summary>
        public int SaveOrder { get { return SaveOrders.Weather; } }

        /// <summary>날씨 상태를 세이브에 적습니다.</summary>
        /// <param name="data">적어 넣을 세이브 자료</param>
        public void CaptureInto(SaveData data)
        {
            data.weather = CaptureState();
        }

        /// <summary>세이브에서 날씨 상태를 되돌립니다.</summary>
        /// <param name="data">읽어 올 세이브 자료</param>
        public void RestoreFrom(SaveData data)
        {
            RestoreState(data.weather);
        }

        // --- Public Methods ---

        /// <summary>
        /// 날씨를 바꿉니다.
        /// </summary>
        /// <param name="type">바꿀 날씨</param>
        /// <param name="instant">true면 전환 없이 즉시 바꿉니다.</param>
        public void SetWeather(WeatherType type, bool instant)
        {
            if (!instant)
            {
                RaiseStep(_transition.BeginStepToward(
                    type, _maxSeverityJump, _transitionMinutesPerSeverity, _minTransitionMinutes));
                return;
            }

            _picker.RecordEnded(_transition.Current, NowMinutes());
            _transition.SetImmediate(type);

            ApplyValues();
            if (_onWeatherChanged != null) _onWeatherChanged.Invoke(type);
        }

        /// <summary>
        /// 날씨와 강도를 함께 즉시 지정합니다. 디버그와 세이브 복원에 씁니다.
        /// </summary>
        /// <param name="type">바꿀 날씨</param>
        /// <param name="intensity">쓸 강도(0~1)</param>
        public void SetWeatherImmediate(WeatherType type, float intensity)
        {
            SetWeather(type, true);
            _transition.SetImmediate(type, intensity);
            ApplyValues();
        }

        /// <summary>세이브용으로 지금 날씨 상태를 담습니다.</summary>
        /// <returns>전환 상태와 진정 시각이 담긴 저장 항목</returns>
        public WeatherSave CaptureState()
        {
            WeatherSave save = new WeatherSave();
            _transition.CaptureInto(save);
            save.calmUntilMinute = _picker.CalmUntilMinute;
            return save;
        }

        /// <summary>세이브에서 읽은 날씨 상태를 되돌립니다.</summary>
        /// <param name="saved">되돌릴 상태. null이면 아무것도 하지 않습니다.</param>
        public void RestoreState(WeatherSave saved)
        {
            if (saved == null) return;

            _transition.Restore(saved);
            _picker.CalmUntilMinute = saved.calmUntilMinute;

            ApplyValues();
        }

        /// <summary>날씨 종류에 해당하는 설정을 돌려줍니다.</summary>
        /// <param name="type">찾을 날씨</param>
        /// <returns>프리셋. 없으면 null입니다.</returns>
        public WeatherPreset GetPreset(WeatherType type)
        {
            return _catalog.Get(type);
        }

        /// <summary>UI에 표시할 날씨 이름입니다. 전환 중이면 목표 날씨의 이름을 보여 줍니다.</summary>
        /// <returns>표시용 이름</returns>
        public string GetDisplayName()
        {
            WeatherPreset preset = _catalog.Get(IsTransitioning ? Target : Current);
            return preset != null && !string.IsNullOrEmpty(preset.displayName)
                ? preset.displayName
                : Current.ToString();
        }

        /// <summary>날씨와 밤을 함께 반영한 시야 배율입니다.</summary>
        /// <returns>낮에 맑으면 1, 밤에 폭우면 그보다 훨씬 작습니다.</returns>
        public float GetEffectiveVisibility()
        {
            float night = Mathf.Lerp(0.5f, 1f, TimeSystem.GetDaylight());
            return VisibilityMultiplier * night;
        }

        /// <summary>날씨와 밤을 함께 반영한 귀신 활동량입니다.</summary>
        /// <returns>밤이면 1.5배가 곱해집니다.</returns>
        public float GetEffectiveGhostActivity()
        {
            float night = TimeSystem.IsNightNow() ? 1.5f : 1f;
            return GhostActivity * night;
        }

        // --- Public Methods : 정적 편의 접근자 ---
        //
        // 참조 없이 읽고 싶을 때 씁니다. 시스템이 씬에 없으면 "아무 영향 없음"에 해당하는
        // 값을 돌려주므로, 호출부는 WeatherSystem의 존재 여부를 신경 쓰지 않아도 됩니다.

        /// <summary>지금 비의 세기입니다. 시스템이 없으면 0입니다.</summary>
        public static float GetRainIntensity() { return Instance != null ? Instance.RainIntensity : 0f; }

        /// <summary>지금 귀신 활동량입니다. 시스템이 없으면 1입니다.</summary>
        public static float GetGhostActivity() { return Instance != null ? Instance.GetEffectiveGhostActivity() : 1f; }

        /// <summary>지금 노면 미끄러움입니다. 시스템이 없으면 1입니다.</summary>
        public static float GetRoadSlipperiness() { return Instance != null ? Instance.RoadSlipperiness : 1f; }

        /// <summary>지금 연료 소모 배율입니다. 시스템이 없으면 1입니다.</summary>
        public static float GetFuelConsumption() { return Instance != null ? Instance.FuelConsumptionMultiplier : 1f; }

        /// <summary>지금 시야 배율입니다. 시스템이 없으면 1입니다.</summary>
        public static float GetVisibility() { return Instance != null ? Instance.GetEffectiveVisibility() : 1f; }

        /// <summary>지금 수면 회복 배율입니다. 시스템이 없으면 1입니다.</summary>
        public static float GetSleepQuality() { return Instance != null ? Instance.SleepQualityMultiplier : 1f; }

        /// <summary>
        /// 밖에 서 있을 때 초당 받는 영향들을 한 번에 돌려줍니다.
        /// 시스템이 없으면 전부 0입니다.
        /// </summary>
        /// <param name="hygieneChange">초당 더러움 변화. 음수면 씻깁니다.</param>
        /// <param name="stress">초당 오르는 스트레스</param>
        /// <param name="thirstRelief">초당 줄어드는 갈증</param>
        /// <param name="stressRelief">초당 줄어드는 스트레스</param>
        public static void GetExposureRates(out float hygieneChange, out float stress,
                                            out float thirstRelief, out float stressRelief)
        {
            WeatherSystem instance = Instance;
            if (instance == null)
            {
                hygieneChange = 0f; stress = 0f; thirstRelief = 0f; stressRelief = 0f;
                return;
            }

            hygieneChange = instance.HygieneChangePerSecond;
            stress = instance.StressPerSecond;
            thirstRelief = instance.ThirstReliefPerSecond;
            stressRelief = instance.StressReliefPerSecond;
        }

        // --- Private Methods ---

        /// <summary>
        /// 유지 시간을 줄이고, 다 되면 다음 걸음을 뗍니다.
        /// </summary>
        /// <param name="gameMinutes">흐른 게임 시간(분)</param>
        private void AdvanceHold(float gameMinutes)
        {
            if (!_transition.AdvanceHold(gameMinutes)) return;

            // 경로 중간이라면 최종 목표를 향해 한 걸음 더 갑니다.
            if (_transition.Current != _transition.FinalTarget)
            {
                RaiseStep(_transition.StepTowardFinalTarget(
                    _maxSeverityJump, _transitionMinutesPerSeverity, _minTransitionMinutes));
                return;
            }

            WeatherType next = _picker.PickNext(
                _transition.Current, NowMinutes(), _severeThreshold, TimeSystem.IsNightNow());

            SetWeather(next, false);
        }

        /// <summary>
        /// 전환을 진행시키고, 도착했으면 그에 맞는 뒤처리를 합니다.
        /// </summary>
        /// <param name="gameMinutes">흐른 게임 시간(분)</param>
        private void AdvanceTransition(float gameMinutes)
        {
            WeatherType leaving = _transition.Current;

            WeatherStep step = _transition.Advance(
                gameMinutes, _maxSeverityJump, _transitionMinutesPerSeverity,
                _minTransitionMinutes, _routeHoldMinutes);

            if (step == WeatherStep.None) return;

            _picker.RecordEnded(leaving, NowMinutes());

            // 최종 목표에 닿았다면, 그것이 궂은 날씨였는지 보고 진정 기간을 잡습니다.
            if (step == WeatherStep.ReachedFinal)
            {
                _picker.BeginCalmIfSevere(
                    _transition.Current, NowMinutes(), _severeThreshold, _calmAfterSevereMinutes);
            }

            RaiseStep(step);
        }

        /// <summary>
        /// 전환의 결과를 이벤트로 알립니다.
        /// </summary>
        /// <param name="step">이번 걸음에 벌어진 일</param>
        private void RaiseStep(WeatherStep step)
        {
            switch (step)
            {
                case WeatherStep.TransitionStarted:
                    LogStep();
                    if (_onWeatherChangeStarted != null) _onWeatherChangeStarted.Invoke(_transition.Target);
                    break;

                // 중간 기착지든 최종 목적지든 "그 날씨가 되었다"는 사실은 같습니다.
                case WeatherStep.ReachedWaypoint:
                case WeatherStep.ReachedFinal:
                    if (_onWeatherChanged != null) _onWeatherChanged.Invoke(_transition.Current);
                    break;
            }
        }

        /// <summary>전환이 시작될 때 어디로 가는지 남깁니다.</summary>
        private void LogStep()
        {
            bool isWaypoint = _transition.Target != _transition.FinalTarget;

            Debug.Log("WeatherSystem: " + _transition.Current + " → " + _transition.Target
                      + (isWaypoint ? " (최종 " + _transition.FinalTarget + ")" : "")
                      + "  강도 " + _transition.TargetIntensity.ToString("F2"));
        }

        /// <summary>
        /// 지금 상태로 표현·영향 수치를 다시 계산합니다.
        /// </summary>
        private void ApplyValues()
        {
            _blender.Blend(
                _catalog.Get(_transition.Current), _transition.CurrentIntensity,
                _catalog.Get(_transition.Target), _transition.TargetIntensity,
                _transition.Blend, _cloudLeadPortion, _rainStartPortion);
        }

        /// <summary>
        /// 지금까지 흐른 게임 시간(분)입니다. 재등장 간격과 진정 기간을 재는 기준입니다.
        /// </summary>
        /// <returns>시계가 있으면 그 총 시간, 없으면 실행 시간에 배율을 곱한 값</returns>
        private float NowMinutes()
        {
            if (TimeSystem.Instance != null) return TimeSystem.Instance.TotalMinutes;
            return Time.time * _fallbackMinutesPerSecond;
        }
    }
}
