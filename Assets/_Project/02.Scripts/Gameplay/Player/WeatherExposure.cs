using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 하늘 아래 서 있는 동안 날씨가 니즈에 주는 상시 효과를 적용합니다.
///
/// WeatherSystem은 날씨별 수치를 계산해 두기만 할 뿐 아무에게도 주지 않습니다.
/// 그 값을 실제로 몸에 흘리는 것이 이 컴포넌트입니다.
///  - 청결: 비를 맞으면 <b>씻깁니다</b>. (프리셋의 hygieneChangePerSecond가 음수)
///  - 갈증: 비를 맞는 것만으로도 조금 해소됩니다.
///          하늘을 보고 받아 마시면 훨씬 빠릅니다. (RainDrinking)
///  - 스트레스: 궂은 날씨는 올리고, 좋은 날씨는 풀어 줍니다.
///
/// 머리 위 판정은 <see cref="SkyCover"/> 하나로 통일했습니다. 처마 밑에 서 있으면
/// 비를 못 받아 마시는 것과 마찬가지로 씻기지도 않습니다.
///
/// <b>반드시 도보 리그(Player_OnFoot)에 붙이세요.</b>
/// 차에 타면 그 오브젝트가 통째로 꺼지므로 차 안에서는 날씨를 타지 않습니다.
/// </summary>
public class WeatherExposure : MonoBehaviour
{
    // --- Public Member Variables ---

    /// <summary>머리 위 가림 검사를 할지 여부입니다.</summary>
    [Header("머리 위 가림 검사")]
    [Tooltip("체크하면 머리 위로 광선을 쏴서 지붕·처마 아래인지 확인합니다.")]
    public bool requireOpenSky = true;

    /// <summary>가림 검사를 시작할 위치입니다. 보통 머리 높이인 메인 카메라를 씁니다.</summary>
    [Tooltip("가림 검사를 시작할 위치(보통 메인 카메라 = 머리 높이). 비워두면 Camera.main을 씁니다.")]
    public Transform originSource;

    /// <summary>이 거리 안에 무언가 있으면 가려진 것으로 봅니다.</summary>
    [Tooltip("이 거리(m) 안에 무언가 있으면 날씨를 타지 않습니다.")]
    public float coverCheckDistance = 6f;

    /// <summary>가림으로 칠 레이어입니다.</summary>
    [Tooltip("가림으로 칠 레이어. 플레이어 자신은 빼 두세요.")]
    public LayerMask coverMask = ~0;

    /// <summary>가림 검사 주기(초)입니다.</summary>
    [Tooltip("가림 검사 주기(초). 매 프레임 할 필요가 없습니다.")]
    public float coverCheckInterval = 0.25f;

    /// <summary>청결 변화에 곱하는 배율입니다.</summary>
    [Header("배율 - 날씨 프리셋 값에 곱합니다")]
    [Tooltip("청결 변화 배율. 0이면 비를 맞아도 씻기지 않습니다.")]
    public float hygieneMultiplier = 1f;

    /// <summary>스트레스 증가에 곱하는 배율입니다.</summary>
    [Tooltip("스트레스 증가 배율")]
    public float stressMultiplier = 1f;

    /// <summary>갈증 해소에 곱하는 배율입니다.</summary>
    [Tooltip("갈증 해소 배율")]
    public float thirstReliefMultiplier = 1f;

    /// <summary>스트레스 해소에 곱하는 배율입니다.</summary>
    [Tooltip("스트레스 해소 배율")]
    public float stressReliefMultiplier = 1f;

    /// <summary>하늘 아래로 나왔을 때 발생합니다.</summary>
    [Header("이벤트")]
    [Tooltip("하늘 아래로 나왔을 때 (젖는 소리·연출 등)")]
    public UnityEvent onExposed;

    /// <summary>지붕 아래로 들어갔을 때 발생합니다.</summary>
    [Tooltip("지붕 아래로 들어갔을 때")]
    public UnityEvent onSheltered;

    // --- Public Properties ---

    /// <summary>지금 하늘이 뚫린 곳에 있는지 여부입니다.</summary>
    public bool IsExposed { get; private set; }

    /// <summary>
    /// 마지막으로 적용한 초당 청결 변화입니다. 음수면 씻기는 중입니다. (디버그 표시용)
    /// </summary>
    public float LastHygieneChange { get; private set; }

    /// <summary>지금 비에 씻기고 있는지 여부입니다.</summary>
    public bool IsBeingWashed { get { return LastHygieneChange < 0f; } }

    // --- Private Member Variables ---

    /// <summary>다음 가림 검사까지 남은 시간(초)입니다.</summary>
    private float coverTimer;

    /// <summary>지난 프레임의 노출 여부입니다. 바뀌는 순간에만 이벤트를 던지기 위해 들고 있습니다.</summary>
    private bool wasExposed;

    // --- Unity Event Functions ---

    void Start()
    {
        // 첫 검사가 돌기 전까지는 하늘 아래에 있다고 봅니다.
        IsExposed = true;
        wasExposed = true;
    }

    void OnDisable()
    {
        // 차에 타는 등으로 꺼질 때 표시 상태가 남지 않게 정리합니다.
        LastHygieneChange = 0f;
    }

    void Update()
    {
        NeedsSystem needs = NeedsSystem.Instance;
        if (needs == null) return;

        UpdateExposure();

        LastHygieneChange = 0f;
        if (!IsExposed) return;

        float hygieneChange, stress, thirstRelief, stressRelief;
        WeatherSystem.GetExposureRates(out hygieneChange, out stress, out thirstRelief, out stressRelief);

        float dt = Time.deltaTime;

        // 청결: 양수면 더러워지고, 음수면 씻깁니다.
        // 비 계열 프리셋이 음수라서 비를 맞으면 몸이 씻겨 나갑니다.
        hygieneChange *= hygieneMultiplier;
        if (hygieneChange != 0f)
        {
            needs.Add(NeedType.Hygiene, hygieneChange * dt);
            LastHygieneChange = hygieneChange;
        }

        // 갈증: 비를 맞는 것만으로도 조금 해소됩니다.
        if (thirstRelief > 0f)
        {
            needs.Satisfy(NeedType.Thirst, thirstRelief * thirstReliefMultiplier * dt);
        }

        // 스트레스: 증가와 해소를 각각 적용합니다.
        // 한 날씨가 둘 다 가질 수 있어서 배율도 따로 둡니다.
        if (stress > 0f)
        {
            needs.Add(NeedType.Stress, stress * stressMultiplier * dt);
        }

        if (stressRelief > 0f)
        {
            needs.Satisfy(NeedType.Stress, stressRelief * stressReliefMultiplier * dt);
        }
    }

    // --- Private Methods ---

    /// <summary>
    /// 머리 위가 뚫려 있는지 주기적으로 갱신합니다.
    /// 매 프레임 광선을 쏠 필요는 없어서 coverCheckInterval마다 한 번만 봅니다.
    /// </summary>
    private void UpdateExposure()
    {
        if (!requireOpenSky)
        {
            SetExposed(true);
            return;
        }

        coverTimer -= Time.deltaTime;
        if (coverTimer > 0f) return;

        coverTimer = Mathf.Max(0.02f, coverCheckInterval);

        if (originSource == null)
        {
            if (Camera.main != null) originSource = Camera.main.transform;
        }

        Vector3 origin = originSource != null ? originSource.position : transform.position;

        // 판정은 SkyCover에 모아 두었습니다. RainDrinking이 같은 규칙을 씁니다.
        SetExposed(SkyCover.IsOpen(origin, coverCheckDistance, coverMask));
    }

    /// <summary>
    /// 노출 상태를 갱신하고, 바뀌는 순간에만 이벤트를 던집니다.
    /// </summary>
    /// <param name="exposed">지금 하늘이 뚫려 있는지 여부</param>
    private void SetExposed(bool exposed)
    {
        IsExposed = exposed;

        if (exposed == wasExposed) return;
        wasExposed = exposed;

        if (exposed)
        {
            if (onExposed != null) onExposed.Invoke();
        }
        else
        {
            if (onSheltered != null) onSheltered.Invoke();
        }
    }
}
