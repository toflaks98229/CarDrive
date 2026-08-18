using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>WeatherType 하나를 넘기는 인스펙터 연결용 이벤트입니다.</summary>
[System.Serializable]
public class WeatherTypeEvent : UnityEvent<WeatherType> { }

/// <summary>
/// 날씨 상태를 관리합니다.
///
/// 이 컴포넌트는 "지금 날씨가 무엇이고 각 수치가 얼마인가"만 들고 있습니다.
/// 비를 뿌리는 연출은 WeatherRig가, 미끄러움·귀신 활동량은 각 게임플레이 시스템이
/// 여기서 읽어 갑니다. 그래서 요소를 늘릴 때 WeatherPreset에 필드만 추가하면 됩니다.
///
/// 날씨는 다음 네 가지 방식으로 자연스럽게 바뀝니다.
///  1. 강도 편차 — 같은 "비"라도 그날 뽑힌 강도에 따라 굵기가 다릅니다.
///  2. 경로 전환 — 맑음에서 폭우로 한 번에 가지 않고 흐림·비를 거칩니다.
///  3. 선행/지연 — 하늘이 먼저 흐려지고 비는 나중에 굵어집니다.
///  4. 간격 — 같은 날씨가 연달아 오지 않고, 궂은 날씨 뒤엔 잠잠한 시간이 옵니다.
/// </summary>
public class WeatherSystem : MonoBehaviour
{
    // --- Public Member Variables ---

    /// <summary>날씨별 설정 목록입니다. 비워두면 WeatherDefaults의 기본값이 채워집니다.</summary>
    [Header("설정")]
    [Tooltip("날씨별 설정. 비워두면 WeatherDefaults의 기본값이 사용됩니다.")]
    public List<WeatherPreset> presets = new List<WeatherPreset>();

    /// <summary>게임을 시작할 때의 날씨입니다.</summary>
    [Tooltip("시작 날씨")]
    public WeatherType startWeather = WeatherType.Clear;

    /// <summary>시간이 지나면서 저절로 날씨가 바뀔지 여부입니다.</summary>
    [Tooltip("체크하면 시간이 지나면서 저절로 날씨가 바뀝니다.")]
    public bool autoChange = true;

    /// <summary>
    /// TimeSystem이 없을 때 쓸 시간 배율(실제 1초당 게임 분)입니다.
    /// TimeSystem이 있으면 그쪽 값을 따릅니다.
    /// </summary>
    [Tooltip("TimeSystem이 없을 때 쓸 시간 배율(실제 1초당 게임 분). " +
             "TimeSystem이 있으면 그쪽 값을 따릅니다.")]
    public float fallbackMinutesPerSecond = 1f;

    /// <summary>
    /// 심각도가 한 칸(1.0) 차이 날 때의 전환 시간(게임 분)입니다.
    /// 차이가 작으면 그만큼 짧아집니다.
    /// </summary>
    [Header("전환 - 얼마나 천천히 바뀌는가")]
    [Tooltip("심각도가 한 칸(1.0) 차이 날 때의 전환 시간(게임 분). " +
             "차이가 작으면 그만큼 짧아집니다.")]
    public float transitionMinutesPerSeverity = 90f;

    /// <summary>전환 시간의 하한(게임 분)입니다. 아무리 가까운 날씨라도 이보다 빨리 바뀌지 않습니다.</summary>
    [Tooltip("전환 시간의 최소값(게임 분)")]
    public float minTransitionMinutes = 15f;

    /// <summary>
    /// 한 번에 건널 수 있는 최대 심각도 차이입니다.
    /// 이보다 크면 중간 날씨를 거쳐 갑니다. (맑음 → 폭우 직행 방지)
    /// </summary>
    [Tooltip("한 번에 건널 수 있는 최대 심각도 차이. " +
             "이보다 크면 중간 날씨를 거쳐 갑니다. (맑음 → 폭우 직행 방지)")]
    [Range(0.1f, 1f)]
    public float maxSeverityJump = 0.3f;

    /// <summary>경로 중간 날씨에서 잠시 머무는 시간(게임 분)입니다. 하늘이 서서히 무거워지는 느낌을 만듭니다.</summary>
    [Tooltip("경로 중간 날씨에서 잠시 머무는 시간(게임 분). 하늘이 서서히 무거워지는 느낌을 만듭니다.")]
    public float routeHoldMinutes = 25f;

    /// <summary>
    /// 구름·어두워짐이 전환의 앞쪽 몇 지점에서 완료되는지 정합니다.
    /// 0.45면 전환의 45% 시점에 하늘이 이미 다 흐려집니다.
    /// </summary>
    [Header("전환 - 무엇이 먼저 변하는가")]
    [Tooltip("구름·어두워짐이 전환의 앞쪽 몇 지점에서 완료되는지. " +
             "0.45면 전환의 45% 시점에 하늘이 이미 다 흐려집니다.")]
    [Range(0.1f, 1f)]
    public float cloudLeadPortion = 0.45f;

    /// <summary>
    /// 비가 전환의 몇 지점부터 내리기 시작하는지 정합니다.
    /// 0.35면 하늘이 어느 정도 흐려진 뒤에야 빗방울이 떨어집니다.
    /// </summary>
    [Tooltip("비가 전환의 몇 지점부터 내리기 시작하는지. " +
             "0.35면 하늘이 어느 정도 흐려진 뒤에야 빗방울이 떨어집니다.")]
    [Range(0f, 0.9f)]
    public float rainStartPortion = 0.35f;

    /// <summary>심각도가 이 값을 넘으면 "궂은 날씨"로 봅니다.</summary>
    [Header("간격")]
    [Tooltip("심각도가 이 값을 넘으면 '궂은 날씨'로 봅니다.")]
    [Range(0f, 1f)]
    public float severeThreshold = 0.65f;

    /// <summary>궂은 날씨 뒤에는 이 시간(게임 분) 동안 온화한 날씨만 옵니다.</summary>
    [Tooltip("궂은 날씨 뒤에는 이 시간(게임 분) 동안 온화한 날씨만 옵니다.")]
    public float calmAfterSevereMinutes = 180f;

    /// <summary>새 날씨로 전환이 시작될 때 목표 날씨를 넘겨 호출됩니다.</summary>
    [Header("이벤트")]
    [Tooltip("새 날씨로 전환이 시작될 때 (목표 날씨를 넘깁니다)")]
    public WeatherTypeEvent onWeatherChangeStarted;

    /// <summary>전환이 끝나 완전히 그 날씨가 되었을 때 호출됩니다.</summary>
    [Tooltip("전환이 끝나 완전히 그 날씨가 되었을 때")]
    public WeatherTypeEvent onWeatherChanged;

    // --- Public Properties : 접근 ---

    /// <summary>씬의 날씨 시스템입니다. 둘 이상이면 나중 것이 스스로 비활성화됩니다.</summary>
    public static WeatherSystem Instance { get; private set; }

    // --- Public Properties : 상태 ---

    /// <summary>전환 전(또는 현재) 날씨입니다.</summary>
    public WeatherType Current { get; private set; }

    /// <summary>전환 중이라면 다음 단계 날씨입니다.</summary>
    public WeatherType Target { get; private set; }

    /// <summary>경로를 거쳐 최종적으로 도달할 날씨입니다.</summary>
    public WeatherType FinalTarget { get; private set; }

    /// <summary>전환 진행도(0~1)입니다.</summary>
    public float Blend { get; private set; }

    /// <summary>전환 중인지 여부입니다.</summary>
    public bool IsTransitioning { get { return Current != Target; } }

    /// <summary>지금 적용 중인 강도(0~1)입니다. 같은 날씨라도 날마다 다릅니다.</summary>
    public float Intensity { get; private set; }

    // --- Public Properties : 표현 수치 ---

    /// <summary>지금 적용 중인 구름 양입니다.</summary>
    public float CloudCover { get; private set; }

    /// <summary>지금 적용 중인 빗줄기 양입니다. 파티클 방출량에 곱하는 배율이라 1을 넘을 수 있습니다.</summary>
    public float RainIntensity { get; private set; }

    /// <summary>지금 적용 중인 안개 짙기입니다.</summary>
    public float FogDensity { get; private set; }

    /// <summary>지금 적용 중인 바람 세기입니다.</summary>
    public float WindStrength { get; private set; }

    /// <summary>지금 적용 중인 하늘 어둡기입니다.</summary>
    public float Darkness { get; private set; }

    // --- Public Properties : 불이익 ---

    /// <summary>날씨만 반영한 시야 배율입니다. 밤까지 반영하려면 <see cref="GetEffectiveVisibility"/>를 쓰세요.</summary>
    public float VisibilityMultiplier { get; private set; }

    /// <summary>노면 미끄러움 배율입니다. 1이 평소입니다.</summary>
    public float RoadSlipperiness { get; private set; }

    /// <summary>연료 소모 배율입니다. 1이 평소입니다.</summary>
    public float FuelConsumptionMultiplier { get; private set; }

    /// <summary>
    /// 하늘 아래 있을 때 초당 청결 변화입니다.
    /// 양수면 더러워지고, 음수면 씻겨 나갑니다. (비를 맞으면 몸이 씻깁니다)
    /// </summary>
    public float HygieneChangePerSecond { get; private set; }

    /// <summary>밖에 있을 때 초당 오르는 스트레스입니다.</summary>
    public float StressPerSecond { get; private set; }

    /// <summary>날씨만 반영한 귀신 활동량입니다. 밤까지 반영하려면 <see cref="GetEffectiveGhostActivity"/>를 쓰세요.</summary>
    public float GhostActivity { get; private set; }

    // --- Public Properties : 이익 ---

    /// <summary>귀신이 플레이어를 알아채는 정도입니다. 낮을수록 잘 숨을 수 있습니다.</summary>
    public float GhostDetectionMultiplier { get; private set; }

    /// <summary>수면 회복 배율입니다. 침대(NeedSatisfier)가 해소량에 곱해 씁니다.</summary>
    public float SleepQualityMultiplier { get; private set; }

    /// <summary>밖에서 비를 맞을 때 초당 줄어드는 갈증입니다.</summary>
    public float ThirstReliefPerSecond { get; private set; }

    /// <summary>좋은 날씨에 밖에 있을 때 초당 줄어드는 스트레스입니다.</summary>
    public float StressReliefPerSecond { get; private set; }

    // --- Private Member Variables ---

    /// <summary>날씨 종류로 프리셋을 바로 찾기 위한 표입니다. Awake에서 한 번 만듭니다.</summary>
    private readonly Dictionary<WeatherType, WeatherPreset> lookup = new Dictionary<WeatherType, WeatherPreset>();

    /// <summary>날씨별로 마지막에 끝난 게임 시각(분)입니다. 같은 날씨가 너무 빨리 돌아오는 것을 막는 데 씁니다.</summary>
    private readonly Dictionary<WeatherType, float> lastEndedMinute = new Dictionary<WeatherType, float>();

    private float currentIntensity;     // Current 날씨의 강도
    private float targetIntensity;      // Target 날씨의 강도
    private float transitionMinutes;    // 이번 전환에 걸리는 총 시간
    private float transitionElapsed;    // 지금까지 경과한 시간
    private float holdRemaining;        // 현재 날씨를 더 유지할 시간
    private float calmUntilMinute;      // 이 시각까지는 온화한 날씨만

    // --- Unity Event Functions ---

    /// <summary>
    /// 자신을 전역 인스턴스로 등록하고 프리셋 표를 만든 뒤, 시작 날씨를 강도와 함께 적용합니다.
    /// 이미 다른 인스턴스가 있으면 경고를 남기고 자신을 끕니다.
    /// </summary>
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("WeatherSystem: 씬에 두 개 이상 존재합니다. 나중 것을 비활성화합니다.", this);
            enabled = false;
            return;
        }
        Instance = this;

        BuildLookup();

        Current = startWeather;
        Target = startWeather;
        FinalTarget = startWeather;
        Blend = 0f;

        currentIntensity = RollIntensity(startWeather);
        targetIntensity = currentIntensity;

        ApplyValues();
        holdRemaining = RollDuration(startWeather);
    }

    /// <summary>
    /// 자신이 전역 인스턴스였다면 그 참조를 비웁니다.
    /// </summary>
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 흐른 게임 시간만큼 전환 또는 유지를 진행시키고, 적용 수치를 다시 계산합니다.
    /// </summary>
    void Update()
    {
        float gameMinutes = Time.deltaTime * TimeSystem.GetMinutesPerSecond(fallbackMinutesPerSecond);

        if (IsTransitioning) AdvanceTransition(gameMinutes);
        else if (autoChange) AdvanceHold(gameMinutes);

        ApplyValues();
    }

    // --- Public Methods ---

    /// <summary>
    /// 날씨를 바꿉니다. 심각도 차이가 크면 중간 날씨를 거쳐 갑니다.
    /// </summary>
    /// <param name="type">최종 목표 날씨</param>
    /// <param name="instant">true면 경로도 전환도 없이 즉시 적용합니다.</param>
    public void SetWeather(WeatherType type, bool instant)
    {
        FinalTarget = type;

        if (instant)
        {
            RecordEnded(Current);
            Current = type;
            Target = type;
            Blend = 0f;
            transitionElapsed = 0f;
            currentIntensity = RollIntensity(type);
            targetIntensity = currentIntensity;
            holdRemaining = RollDuration(type);
            ApplyValues();

            if (onWeatherChanged != null) onWeatherChanged.Invoke(type);
            return;
        }

        StepTowardFinalTarget();
    }

    /// <summary>강도까지 직접 지정해 즉시 적용합니다. (디버그·컷신용)</summary>
    /// <param name="type">적용할 날씨</param>
    /// <param name="intensity">적용할 강도. 0~1로 잘립니다.</param>
    public void SetWeatherImmediate(WeatherType type, float intensity)
    {
        SetWeather(type, true);
        currentIntensity = Mathf.Clamp01(intensity);
        targetIntensity = currentIntensity;
        ApplyValues();
    }

    /// <summary>
    /// 세이브용으로 지금 상태를 복사해 돌려줍니다.
    /// 전환 중간에 저장해도 이어서 진행되도록 진행도와 남은 유지 시간까지 담습니다.
    /// </summary>
    /// <returns>날씨 종류·강도·전환 진행도·유지 시간이 담긴 저장 항목</returns>
    public WeatherSave CaptureState()
    {
        return new WeatherSave
        {
            current = Current,
            target = Target,
            finalTarget = FinalTarget,
            blend = Blend,
            currentIntensity = currentIntensity,
            targetIntensity = targetIntensity,
            transitionMinutes = transitionMinutes,
            transitionElapsed = transitionElapsed,
            holdRemaining = holdRemaining,
            calmUntilMinute = calmUntilMinute
        };
    }

    /// <summary>
    /// 세이브에서 읽은 상태로 되돌립니다.
    /// </summary>
    /// <param name="saved">되돌릴 저장 항목. null이면 아무것도 하지 않습니다.</param>
    public void RestoreState(WeatherSave saved)
    {
        if (saved == null) return;

        Current = saved.current;
        Target = saved.target;
        FinalTarget = saved.finalTarget;
        Blend = saved.blend;

        currentIntensity = saved.currentIntensity;
        targetIntensity = saved.targetIntensity;
        transitionMinutes = saved.transitionMinutes;
        transitionElapsed = saved.transitionElapsed;
        holdRemaining = saved.holdRemaining;
        calmUntilMinute = saved.calmUntilMinute;

        ApplyValues();
    }

    /// <summary>
    /// 날씨 종류에 해당하는 프리셋을 돌려줍니다.
    /// </summary>
    /// <param name="type">찾을 날씨 종류</param>
    /// <returns>해당 프리셋. 등록되어 있지 않으면 null입니다.</returns>
    public WeatherPreset GetPreset(WeatherType type)
    {
        WeatherPreset preset;
        return lookup.TryGetValue(type, out preset) ? preset : null;
    }

    /// <summary>UI에 표시할 날씨 이름입니다. 전환 중이면 다가오는 날씨를 보여 줍니다.</summary>
    /// <returns>프리셋의 표시 이름. 비어 있으면 날씨 종류의 이름을 그대로 반환합니다.</returns>
    public string GetDisplayName()
    {
        WeatherPreset preset = GetPreset(IsTransitioning ? Target : Current);
        return preset != null && !string.IsNullOrEmpty(preset.displayName) ? preset.displayName : Current.ToString();
    }

    /// <summary>
    /// 밤과 날씨를 함께 반영한 시야 배율입니다. 실제 게임플레이는 보통 이 값을 씁니다.
    /// </summary>
    /// <returns>날씨 시야 배율에 밤 보정을 곱한 값</returns>
    public float GetEffectiveVisibility()
    {
        float night = Mathf.Lerp(0.5f, 1f, TimeSystem.GetDaylight());
        return VisibilityMultiplier * night;
    }

    /// <summary>
    /// 밤과 날씨를 함께 반영한 귀신 활동량입니다.
    /// </summary>
    /// <returns>날씨 활동량에 밤이면 1.5배를 곱한 값</returns>
    public float GetEffectiveGhostActivity()
    {
        float night = TimeSystem.IsNightNow() ? 1.5f : 1f;
        return GhostActivity * night;
    }

    // 참조 없이 읽고 싶을 때 쓰는 편의 메서드들입니다.
    // 시스템이 씬에 없으면 "아무 영향 없음"에 해당하는 값을 돌려주므로,
    // 호출부는 WeatherSystem의 존재 여부를 신경 쓰지 않아도 됩니다.

    /// <summary>지금 빗줄기 양입니다.</summary>
    /// <returns>적용 중인 빗줄기 양. 시스템이 없으면 0입니다.</returns>
    public static float GetRainIntensity() { return Instance != null ? Instance.RainIntensity : 0f; }

    /// <summary>밤까지 반영한 귀신 활동량입니다.</summary>
    /// <returns>적용 중인 귀신 활동량. 시스템이 없으면 1입니다.</returns>
    public static float GetGhostActivity() { return Instance != null ? Instance.GetEffectiveGhostActivity() : 1f; }

    /// <summary>노면 미끄러움 배율입니다.</summary>
    /// <returns>적용 중인 미끄러움 배율. 시스템이 없으면 1입니다.</returns>
    public static float GetRoadSlipperiness() { return Instance != null ? Instance.RoadSlipperiness : 1f; }

    /// <summary>연료 소모 배율입니다.</summary>
    /// <returns>적용 중인 연료 소모 배율. 시스템이 없으면 1입니다.</returns>
    public static float GetFuelConsumption() { return Instance != null ? Instance.FuelConsumptionMultiplier : 1f; }

    /// <summary>밤까지 반영한 시야 배율입니다.</summary>
    /// <returns>적용 중인 시야 배율. 시스템이 없으면 1입니다.</returns>
    public static float GetVisibility() { return Instance != null ? Instance.GetEffectiveVisibility() : 1f; }

    /// <summary>수면 회복 배율입니다.</summary>
    /// <returns>적용 중인 수면 회복 배율. 시스템이 없으면 1입니다.</returns>
    public static float GetSleepQuality() { return Instance != null ? Instance.SleepQualityMultiplier : 1f; }

    /// <summary>
    /// 하늘 아래 있을 때 초당 니즈에 가해지는 변화를 한 번에 돌려줍니다.
    /// (청결 변화, 스트레스, 갈증 해소, 스트레스 해소) — WeatherExposure가 씁니다.
    /// </summary>
    /// <param name="hygieneChange">
    /// 초당 청결 변화. <b>양수면 더러워지고 음수면 씻깁니다.</b>
    /// 비를 맞으면 몸이 씻기므로 비 계열에서 음수가 나옵니다. 시스템이 없으면 0입니다.
    /// </param>
    /// <param name="stress">초당 오르는 스트레스. 시스템이 없으면 0입니다.</param>
    /// <param name="thirstRelief">초당 줄어드는 갈증. 시스템이 없으면 0입니다.</param>
    /// <param name="stressRelief">초당 줄어드는 스트레스. 시스템이 없으면 0입니다.</param>
    public static void GetExposureRates(out float hygieneChange, out float stress,
                                        out float thirstRelief, out float stressRelief)
    {
        if (Instance == null)
        {
            hygieneChange = 0f; stress = 0f; thirstRelief = 0f; stressRelief = 0f;
            return;
        }

        hygieneChange = Instance.HygieneChangePerSecond;
        stress = Instance.StressPerSecond;
        thirstRelief = Instance.ThirstReliefPerSecond;
        stressRelief = Instance.StressReliefPerSecond;
    }

    // --- Private Methods : 진행 ---

    /// <summary>
    /// 현재 날씨를 유지하다가 시간이 다 되면 다음 날씨를 뽑습니다.
    /// </summary>
    /// <param name="gameMinutes">이번 프레임에 흐른 게임 시간(분)</param>
    private void AdvanceHold(float gameMinutes)
    {
        holdRemaining -= gameMinutes;
        if (holdRemaining > 0f) return;

        // 경로 중간이라면 최종 목표를 향해 한 걸음 더 갑니다.
        if (Current != FinalTarget)
        {
            StepTowardFinalTarget();
            return;
        }

        SetWeather(PickRandomWeather(), false);
    }

    /// <summary>
    /// 전환을 진행시키고, 다 되면 다음 단계로 넘어갑니다.
    /// 최종 목표에 도착했다면 유지 시간을 뽑고, 궂은 날씨였다면 잠잠한 시간대를 걸어 둡니다.
    /// </summary>
    /// <param name="gameMinutes">이번 프레임에 흐른 게임 시간(분)</param>
    private void AdvanceTransition(float gameMinutes)
    {
        transitionElapsed += gameMinutes;
        Blend = transitionMinutes > 0f ? Mathf.Clamp01(transitionElapsed / transitionMinutes) : 1f;

        if (Blend < 1f) return;

        RecordEnded(Current);

        Current = Target;
        currentIntensity = targetIntensity;
        Blend = 0f;
        transitionElapsed = 0f;

        if (Current == FinalTarget)
        {
            holdRemaining = RollDuration(Current);

            // 궂은 날씨였다면 한동안 온화한 날씨만 오게 합니다.
            WeatherPreset preset = GetPreset(Current);
            if (preset != null && preset.severity >= severeThreshold)
            {
                calmUntilMinute = NowMinutes() + RollDuration(Current) + calmAfterSevereMinutes;
            }

            if (onWeatherChanged != null) onWeatherChanged.Invoke(Current);
        }
        else
        {
            // 경로 중간이면 잠시 머문 뒤 계속 갑니다.
            holdRemaining = routeHoldMinutes;
            if (onWeatherChanged != null) onWeatherChanged.Invoke(Current);
        }
    }

    /// <summary>
    /// 최종 목표를 향해 한 단계만 전환을 시작합니다.
    /// 심각도 차이가 maxSeverityJump보다 크면 중간 날씨를 골라 거쳐 갑니다.
    /// </summary>
    private void StepTowardFinalTarget()
    {
        float from = SeverityOf(Current);
        float to = SeverityOf(FinalTarget);
        float diff = to - from;

        WeatherType next;

        if (Mathf.Abs(diff) <= maxSeverityJump)
        {
            next = FinalTarget;
        }
        else
        {
            // 한 칸만큼 이동한 심각도에 가장 가까운 날씨를 중간 기착지로 삼습니다.
            float wanted = from + Mathf.Sign(diff) * maxSeverityJump;
            next = FindClosestSeverity(wanted, Current, FinalTarget);
        }

        if (next == Current)
        {
            // 더 갈 곳이 없으면 그냥 목표로 확정합니다.
            next = FinalTarget;
        }

        Target = next;
        targetIntensity = RollIntensity(next);

        // 심각도 차이가 클수록 오래 걸립니다.
        float stepDiff = Mathf.Abs(SeverityOf(next) - from);
        transitionMinutes = Mathf.Max(minTransitionMinutes, transitionMinutesPerSeverity * stepDiff);
        transitionElapsed = 0f;
        Blend = 0f;

        Debug.Log("WeatherSystem: " + Current + " → " + next
            + (next != FinalTarget ? " (최종 " + FinalTarget + ")" : "")
            + "  강도 " + targetIntensity.ToString("F2")
            + "  전환 " + transitionMinutes.ToString("F0") + "분");

        if (onWeatherChangeStarted != null) onWeatherChangeStarted.Invoke(next);
    }

    // --- Private Methods : 값 계산 ---

    /// <summary>
    /// 현재/목표 프리셋을 강도와 함께 섞어 실제 적용 값을 만듭니다.
    ///
    /// 채널마다 섞이는 시점이 다릅니다.
    ///  - 구름·어두워짐·바람: 앞쪽에서 먼저 완료 (하늘이 먼저 변합니다)
    ///  - 비: 뒤쪽에서 시작 (하늘이 흐려진 뒤에야 내립니다)
    ///  - 나머지: 전 구간에 걸쳐 고르게
    /// </summary>
    private void ApplyValues()
    {
        WeatherPreset a = GetPreset(Current);
        if (a == null) return;

        WeatherPreset b = GetPreset(Target);
        if (b == null) b = a;

        float t = Blend;
        float cloudT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, Mathf.Max(0.01f, cloudLeadPortion), t));
        float rainT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Mathf.Min(0.99f, rainStartPortion), 1f, t));
        float midT = Mathf.SmoothStep(0f, 1f, t);

        float ia = currentIntensity;
        float ib = targetIntensity;

        // 표현
        CloudCover = Mathf.Lerp(a.cloudCover * ia, b.cloudCover * ib, cloudT);
        Darkness = Mathf.Lerp(a.darkness * ia, b.darkness * ib, cloudT);
        WindStrength = Mathf.Lerp(a.windStrength * ia, b.windStrength * ib, cloudT);
        FogDensity = Mathf.Lerp(a.fogDensity * ia, b.fogDensity * ib, midT);
        RainIntensity = Mathf.Lerp(a.rainIntensity * ia, b.rainIntensity * ib, rainT);

        Intensity = Mathf.Lerp(ia, ib, midT);

        // 불이익 — 배율은 1에서 강도만큼 벌어지게 보간합니다.
        VisibilityMultiplier = LerpScaled(a.visibilityMultiplier, ia, b.visibilityMultiplier, ib, midT);
        RoadSlipperiness = LerpScaled(a.roadSlipperiness, ia, b.roadSlipperiness, ib, midT);
        FuelConsumptionMultiplier = LerpScaled(a.fuelConsumptionMultiplier, ia, b.fuelConsumptionMultiplier, ib, midT);
        GhostActivity = LerpScaled(a.ghostActivity, ia, b.ghostActivity, ib, midT);

        HygieneChangePerSecond = Mathf.Lerp(a.hygieneChangePerSecond * ia, b.hygieneChangePerSecond * ib, rainT);
        StressPerSecond = Mathf.Lerp(a.stressPerSecond * ia, b.stressPerSecond * ib, midT);

        // 이익
        GhostDetectionMultiplier = LerpScaled(a.ghostDetectionMultiplier, ia, b.ghostDetectionMultiplier, ib, midT);
        SleepQualityMultiplier = LerpScaled(a.sleepQualityMultiplier, ia, b.sleepQualityMultiplier, ib, midT);
        ThirstReliefPerSecond = Mathf.Lerp(a.thirstReliefPerSecond * ia, b.thirstReliefPerSecond * ib, rainT);
        StressReliefPerSecond = Mathf.Lerp(a.stressReliefPerSecond * ia, b.stressReliefPerSecond * ib, midT);
    }

    /// <summary>
    /// 배율 값(1이 기준)을 강도에 맞춰 약하게/세게 만든 뒤 보간합니다.
    /// 강도가 0.5면 1과의 차이도 절반이 됩니다.
    /// </summary>
    /// <param name="valueA">현재 날씨의 배율 값</param>
    /// <param name="intensityA">현재 날씨의 강도</param>
    /// <param name="valueB">목표 날씨의 배율 값</param>
    /// <param name="intensityB">목표 날씨의 강도</param>
    /// <param name="t">보간 비율(0~1)</param>
    /// <returns>강도를 반영해 보간한 배율 값</returns>
    private static float LerpScaled(float valueA, float intensityA, float valueB, float intensityB, float t)
    {
        float a = 1f + (valueA - 1f) * intensityA;
        float b = 1f + (valueB - 1f) * intensityB;
        return Mathf.Lerp(a, b, t);
    }

    // --- Private Methods : 선택 ---

    /// <summary>
    /// 가중치에 따라 다음 날씨를 뽑습니다.
    /// 현재 날씨, 최근에 나왔던 날씨, 그리고 잠잠해야 하는 시간대의 궂은 날씨는 제외합니다.
    /// </summary>
    /// <returns>뽑힌 날씨. 후보가 모두 걸러지면 가장 온화한 날씨를 반환합니다.</returns>
    private WeatherType PickRandomWeather()
    {
        float now = NowMinutes();
        bool calmOnly = now < calmUntilMinute;
        bool night = TimeSystem.IsNightNow();

        float total = 0f;
        for (int i = 0; i < presets.Count; i++)
        {
            total += WeightOf(presets[i], now, calmOnly, night);
        }

        if (total <= 0f)
        {
            // 전부 걸러졌으면 가장 온화한 날씨로 갑니다.
            return FindClosestSeverity(0f, Current, Current);
        }

        float roll = Random.Range(0f, total);
        for (int i = 0; i < presets.Count; i++)
        {
            float w = WeightOf(presets[i], now, calmOnly, night);
            if (roll < w) return presets[i].type;
            roll -= w;
        }

        return Current;
    }

    /// <summary>
    /// 이 날씨가 지금 뽑힐 수 있는 가중치를 계산합니다. 0이면 후보에서 빠집니다.
    /// </summary>
    /// <param name="preset">가중치를 계산할 날씨 프리셋</param>
    /// <param name="now">지금까지 흐른 총 게임 시간(분)</param>
    /// <param name="calmOnly">궂은 날씨를 제외해야 하는 시간대인지 여부</param>
    /// <param name="night">지금이 밤인지 여부</param>
    /// <returns>뽑기 가중치. 후보에서 제외되면 0입니다.</returns>
    private float WeightOf(WeatherPreset preset, float now, bool calmOnly, bool night)
    {
        if (preset.type == Current) return 0f;
        if (preset.weight <= 0f) return 0f;

        // 궂은 날씨 직후에는 온화한 날씨만
        if (calmOnly && preset.severity >= severeThreshold) return 0f;

        // 같은 날씨가 너무 빨리 돌아오지 않게
        float lastEnded;
        if (lastEndedMinute.TryGetValue(preset.type, out lastEnded))
        {
            if (now - lastEnded < preset.minRepeatGapMinutes) return 0f;
        }

        float w = preset.weight;
        if (night) w *= Mathf.Max(0f, preset.nightWeightMultiplier);
        return w;
    }

    /// <summary>
    /// 원하는 심각도에 가장 가까운 날씨를 찾습니다.
    /// </summary>
    /// <param name="wanted">목표로 하는 심각도</param>
    /// <param name="exclude">후보에서 뺄 날씨 (보통 현재 날씨)</param>
    /// <param name="allowAnyway">exclude에 해당해도 허용할 날씨이자, 아무것도 못 찾았을 때의 기본값</param>
    /// <returns>심각도가 가장 가까운 날씨</returns>
    private WeatherType FindClosestSeverity(float wanted, WeatherType exclude, WeatherType allowAnyway)
    {
        WeatherType best = allowAnyway;
        float bestDiff = float.MaxValue;

        for (int i = 0; i < presets.Count; i++)
        {
            WeatherPreset p = presets[i];
            if (p.type == exclude && p.type != allowAnyway) continue;

            float diff = Mathf.Abs(p.severity - wanted);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                best = p.type;
            }
        }

        return best;
    }

    /// <summary>
    /// 날씨의 심각도를 돌려줍니다.
    /// </summary>
    /// <param name="type">심각도를 구할 날씨</param>
    /// <returns>프리셋의 심각도. 프리셋이 없으면 0입니다.</returns>
    private float SeverityOf(WeatherType type)
    {
        WeatherPreset p = GetPreset(type);
        return p != null ? p.severity : 0f;
    }

    /// <summary>
    /// 날씨의 강도를 최소~최대 범위에서 무작위로 뽑습니다.
    /// 같은 날씨라도 날마다 굵기가 달라지는 이유입니다.
    /// </summary>
    /// <param name="type">강도를 뽑을 날씨</param>
    /// <returns>뽑힌 강도. 프리셋이 없으면 1입니다.</returns>
    private float RollIntensity(WeatherType type)
    {
        WeatherPreset p = GetPreset(type);
        if (p == null) return 1f;

        float min = Mathf.Min(p.minIntensity, p.maxIntensity);
        float max = Mathf.Max(p.minIntensity, p.maxIntensity);
        return Random.Range(min, max);
    }

    /// <summary>
    /// 날씨의 유지 시간을 최소~최대 범위에서 무작위로 뽑습니다.
    /// </summary>
    /// <param name="type">유지 시간을 뽑을 날씨</param>
    /// <returns>뽑힌 유지 시간(게임 분). 프리셋이 없으면 120입니다.</returns>
    private float RollDuration(WeatherType type)
    {
        WeatherPreset p = GetPreset(type);
        return p != null ? Random.Range(p.minDurationMinutes, p.maxDurationMinutes) : 120f;
    }

    /// <summary>
    /// 날씨가 끝난 시각을 기록합니다. 같은 날씨가 너무 빨리 돌아오는 것을 막는 데 씁니다.
    /// </summary>
    /// <param name="type">방금 끝난 날씨</param>
    private void RecordEnded(WeatherType type)
    {
        lastEndedMinute[type] = NowMinutes();
    }

    /// <summary>
    /// 지금까지 흐른 총 게임 시간(분)입니다. TimeSystem이 없으면 실행 시간으로 대신합니다.
    /// </summary>
    /// <returns>누적 게임 시간(분)</returns>
    private float NowMinutes()
    {
        if (TimeSystem.Instance != null) return TimeSystem.Instance.TotalMinutes;
        return Time.time * fallbackMinutesPerSecond;
    }

    /// <summary>
    /// 날씨 종류로 프리셋을 찾는 표를 만듭니다.
    /// 목록이 비어 있으면 기본값을 채우고, 빠진 종류가 있으면 경고와 함께 기본 프리셋을 보충합니다.
    /// </summary>
    private void BuildLookup()
    {
        if (presets == null || presets.Count == 0) presets = WeatherDefaults.CreatePresets();

        lookup.Clear();
        for (int i = 0; i < presets.Count; i++) lookup[presets[i].type] = presets[i];

        List<WeatherPreset> fallback = WeatherDefaults.CreatePresets();
        for (int i = 0; i < fallback.Count; i++)
        {
            if (lookup.ContainsKey(fallback[i].type)) continue;

            Debug.LogWarning("WeatherSystem: " + fallback[i].type + " 프리셋이 없어 기본값을 씁니다.", this);
            presets.Add(fallback[i]);
            lookup[fallback[i].type] = fallback[i];
        }
    }
}
