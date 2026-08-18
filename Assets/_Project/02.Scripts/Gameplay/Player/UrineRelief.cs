using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 배뇨 해소를 담당합니다.
///
/// P를 <b>누르고 있는 동안</b> 물줄기가 이어집니다.
/// 프레임마다 조금씩 방출하므로 한 지점에서 부채꼴로 퍼지지 않고 한 줄기로 뻗습니다.
///
/// 출력은 두 가지가 합쳐져 결정됩니다.
///  - 잔뇨: 남은 양이 적을수록 약해집니다. 끝에 가면 힘없이 흘러내립니다.
///  - 압력: P를 연타할 때마다 붙습니다. 잔뇨가 적어도 연타하면 다시 세게 나갑니다.
///
/// 조준은 시선의 <b>상하 각도</b>를 따릅니다. 하늘을 향해 쏘면 포물선을 그리며
/// 자기 위로 되떨어지고(역류), 그만큼 갈증이 줄되 청결이 급격히 나빠집니다.
/// </summary>
public class UrineRelief : MonoBehaviour
{
    // --- Public Member Variables ---

    [Header("입력")]
    [Tooltip("배뇨 키. 누르고 있는 동안 계속 나오고, 연타하면 압력이 붙습니다.")]
    public KeyCode relieveKey = KeyCode.P;

    [Header("연동")]
    [Tooltip("노란 물줄기 파티클. 방출은 코드가 직접 하므로 Emission 모듈은 꺼 둡니다.")]
    public ParticleSystem stream;

    [Tooltip("니즈 시스템. 비워두면 씬에서 자동으로 찾습니다.")]
    public NeedsSystem needsSystem;

    [Header("조준 - 시선의 상하 각도를 따릅니다")]
    [Tooltip("상하 각도를 읽어 올 대상(보통 메인 카메라). 비워두면 Camera.main을 씁니다.")]
    public Transform aimSource;

    [Tooltip("시선 각도가 노즐에 반영되는 비율. 1이면 보는 각도 그대로 쏩니다.")]
    [Range(0f, 1f)]
    public float pitchInfluence = 1f;

    [Tooltip("노즐이 향할 수 있는 최대 각도(도). 위아래 공통입니다.")]
    [Range(10f, 89f)]
    public float maxAimAngle = 85f;

    [Header("배출 속도")]
    [Tooltip("출력이 최대일 때 초당 줄어드는 배뇨량. 낮출수록 비우는 데 오래 걸립니다.")]
    public float reliefPerSecond = 0.13f;

    [Tooltip("이 값 아래로는 더 나오지 않습니다.")]
    public float emptyThreshold = 0.015f;

    [Header("잔뇨 - 남은 양이 적을수록 약해집니다")]
    [Tooltip("거의 다 비웠을 때 남는 기본 출력 비율. 0에 가까울수록 끝이 확 죽습니다.")]
    [Range(0f, 1f)]
    public float minFlowFromVolume = 0.16f;

    [Tooltip("남은 양(가로 0~1)에 따른 출력(세로 0~1) 곡선")]
    public AnimationCurve volumeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("연타 압력 - 누를 때마다 힘이 붙습니다")]
    [Tooltip("한 번 누를 때 오르는 압력")]
    [Range(0f, 1f)]
    public float pressurePerPress = 0.5f;

    [Tooltip("압력이 초당 빠지는 속도")]
    public float pressureDecayPerSecond = 1.6f;

    [Tooltip("압력이 출력에 더해지는 양")]
    [Range(0f, 1f)]
    public float pressureFlowBonus = 0.9f;

    [Tooltip("연타로 끌어올릴 수 있는 출력 상한. 기본 최대 출력(1.0)에 대한 비율입니다. " +
             "잔뇨가 넉넉해서 이미 이 값보다 세게 나오고 있다면 깎지 않습니다.")]
    [Range(0f, 1f)]
    public float pressureFlowCeiling = 0.8f;

    [Tooltip("압력이 최대일 때 배출 속도에 붙는 추가 배율")]
    public float pressureDrainBonus = 0.8f;

    [Header("놓았을 때")]
    [Tooltip("키를 놓은 뒤 줄기가 잦아드는 속도. 연타할 때 뚝뚝 끊기지 않게 해 줍니다.")]
    public float releaseFadeSpeed = 6f;

    [Header("파티클 출력")]
    [Tooltip("출력이 최대일 때 초당 방출 입자 수")]
    public float maxEmissionRate = 110f;

    [Tooltip("출력이 최소일 때 초당 방출 입자 수")]
    public float minEmissionRate = 16f;

    public float maxSpeed = 6.2f;
    public float minSpeed = 1.4f;
    public float maxLifetime = 1.2f;
    public float minLifetime = 0.45f;

    [Tooltip("물줄기가 퍼지는 원뿔 각도(도). 작을수록 일직선에 가깝습니다.")]
    public float coneAngle = 1.2f;

    [Header("부수 효과 (초당)")]
    public float hygieneCostPerSecond = 0.006f;
    public float stressReliefPerSecond = 0.02f;

    [Header("역류 - 위로 쏘면 자기가 뒤집어씁니다")]
    [Tooltip("이 각도(도) 위로 쏘기 시작하면 조금씩 되떨어집니다.")]
    [Range(0f, 89f)]
    public float backsplashStartAngle = 25f;

    [Tooltip("이 각도(도)부터는 전부 자기 위로 떨어집니다.")]
    [Range(0f, 89f)]
    public float backsplashFullAngle = 70f;

    [Tooltip("역류가 최대일 때 초당 줄어드는 갈증. 마시는 게 아니라 얼굴로 받는 것이라 " +
             "물을 마시는 것보다 훨씬 적게 잡습니다.")]
    public float backsplashThirstReliefPerSecond = 0.012f;

    [Tooltip("역류가 최대일 때 청결 악화에 곱해지는 배율. 8이면 평소의 9배로 더러워집니다.")]
    public float backsplashHygieneMultiplier = 8f;

    [Tooltip("역류가 최대일 때 초당 오르는 스트레스. 역류 중에는 시원함(스트레스 해소)이 사라집니다.")]
    public float backsplashStressPerSecond = 0.05f;

    [Header("이벤트")]
    [Tooltip("줄기가 시작될 때")]
    public UnityEvent onStreamStart;

    [Tooltip("줄기가 멈출 때")]
    public UnityEvent onStreamStop;

    [Tooltip("다 비워서 더 나오지 않을 때")]
    public UnityEvent onEmpty;

    // --- Public Properties ---

    /// <summary>현재 출력 비율(0~1)입니다. 사운드 볼륨 등에 쓸 수 있습니다.</summary>
    public float CurrentOutput { get; private set; }

    /// <summary>연타로 쌓인 압력(0~1)입니다.</summary>
    public float Pressure { get { return pressure; } }

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
            if (backsplashFullAngle <= backsplashStartAngle)
            {
                return StreamElevation >= backsplashStartAngle ? 1f : 0f;
            }
            return Mathf.Clamp01(Mathf.InverseLerp(backsplashStartAngle, backsplashFullAngle, StreamElevation));
        }
    }

    // --- Private Member Variables ---

    /// <summary>지금 물줄기의 압력(0~1)입니다. 남은 배뇨량과 누르는 정도에 따라 오르내립니다.</summary>
    private float pressure;
    private float holdAmount;       // 1이면 누르는 중, 놓으면 서서히 0으로
    private float emitAccumulator;  // 한 입자 미만의 방출량을 다음 프레임으로 넘깁니다

    /// <summary>직전 프레임에 배뇨가 비어 있었는지 여부입니다. 비는 순간에만 이벤트를 던지는 데 씁니다.</summary>
    private bool wasEmptyLastFrame;
    private float baseStreamPitch;  // 정면을 볼 때 노즐이 숙이는 각도 (씬 배치에서 읽습니다)

    // --- Unity Event Functions ---

    /// <summary>
    /// 니즈 시스템과 파티클을 찾아 물줄기의 모양·방출 방식을 확정합니다.
    /// 방출은 코드가 직접 하므로 Emission 모듈을 끄고, 시뮬레이션은 월드 공간으로 둡니다.
    /// </summary>
    void Start()
    {
        if (needsSystem == null) needsSystem = FindAnyObjectByType<NeedsSystem>();
        if (needsSystem == null) Debug.LogWarning("UrineRelief: NeedsSystem을 찾지 못했습니다.", this);

        if (stream == null) stream = GetComponentInChildren<ParticleSystem>(true);
        if (stream == null)
        {
            Debug.LogWarning("UrineRelief: 파티클이 없어 물줄기가 보이지 않습니다.", this);
            return;
        }

        // 줄기 모양은 여기서 확정합니다.
        // 방출은 코드가 프레임마다 조금씩 하므로 Emission 모듈은 꺼야 합니다.
        ParticleSystem.EmissionModule emission = stream.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = stream.shape;
        shape.angle = coneAngle;

        // 시뮬레이션은 반드시 월드 공간이어야 합니다.
        // 로컬 공간이면 (1) 조준하려고 노즐을 돌리는 순간 이미 날아간 입자까지 함께 끌려가고,
        // (2) 중력 방향도 노즐을 따라 회전해서 위로 쏴도 땅으로 떨어지지 않습니다.
        ParticleSystem.MainModule main = stream.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        // 씬에 배치된 각도를 "정면을 볼 때의 기본 숙임"으로 삼습니다.
        baseStreamPitch = NormalizeAngle(stream.transform.localEulerAngles.x);
        StreamElevation = -baseStreamPitch;
    }

    /// <summary>
    /// 입력에 따라 물줄기를 켜고 끄며, 압력·조준 각도·방출량을 갱신합니다.
    /// 입력이 막혀 있으면(오버레이 등) 물줄기를 멈춥니다.
    /// </summary>
    void Update()
    {
        if (GameInputGate.Suspended)
        {
            StopStream();
            return;
        }

        float dt = Time.deltaTime;

        UpdateAim();
        UpdatePressure(dt);
        UpdateHold(dt);

        if (holdAmount <= 0.001f)
        {
            StopStream();
            return;
        }

        if (needsSystem == null) { StopStream(); return; }

        float remaining = needsSystem.GetValue(NeedType.Urine);
        if (remaining <= emptyThreshold)
        {
            if (!wasEmptyLastFrame && onEmpty != null) onEmpty.Invoke();
            wasEmptyLastFrame = true;
            StopStream();
            return;
        }
        wasEmptyLastFrame = false;

        float flow = CalculateFlow(remaining);
        CurrentOutput = flow;

        if (!IsStreaming)
        {
            IsStreaming = true;
            if (onStreamStart != null) onStreamStart.Invoke();
        }

        EmitStream(flow, dt);
        ApplyDrain(flow, dt);
    }

    // --- Public Methods ---

    /// <summary>줄기를 즉시 멈춥니다.</summary>
    public void StopStream()
    {
        CurrentOutput = 0f;
        emitAccumulator = 0f;

        if (IsStreaming)
        {
            IsStreaming = false;
            if (onStreamStop != null) onStreamStop.Invoke();
        }
    }

    // --- Private Methods ---

    /// <summary>
    /// 노즐을 시선의 상하 각도에 맞춥니다.
    ///
    /// 파티클은 플레이어 몸통의 자식이라 좌우 회전은 저절로 따라오지만,
    /// 상하는 카메라만 도는 구조라 여기서 직접 옮겨 줘야 합니다.
    /// 몸통은 Y축으로만 도므로 노즐의 로컬 X 회전이 곧 월드 기준 각도가 됩니다.
    /// </summary>
    private void UpdateAim()
    {
        if (stream == null) return;

        if (aimSource == null)
        {
            if (Camera.main == null) return;
            aimSource = Camera.main.transform;
        }

        // 시선이 수평에서 위로 향한 각도. 위를 보면 양수입니다.
        float lookElevation = Mathf.Asin(Mathf.Clamp(aimSource.forward.y, -1f, 1f)) * Mathf.Rad2Deg;

        // Unity의 X 회전은 아래를 볼 때 양수라 부호를 뒤집어 더합니다.
        float pitch = baseStreamPitch - lookElevation * pitchInfluence;
        pitch = Mathf.Clamp(pitch, -maxAimAngle, maxAimAngle);

        stream.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        StreamElevation = -pitch;
    }

    /// <summary>0~360으로 나오는 각도를 -180~180으로 바꿉니다.</summary>
    private static float NormalizeAngle(float degrees)
    {
        degrees %= 360f;
        if (degrees > 180f) degrees -= 360f;
        if (degrees < -180f) degrees += 360f;
        return degrees;
    }

    /// <summary>
    /// 연타로 압력을 쌓고, 시간이 지나면 빠지게 합니다.
    /// </summary>
    private void UpdatePressure(float dt)
    {
        if (Input.GetKeyDown(relieveKey))
        {
            pressure = Mathf.Clamp01(pressure + pressurePerPress);
        }

        pressure = Mathf.MoveTowards(pressure, 0f, pressureDecayPerSecond * dt);
    }

    /// <summary>
    /// 누르고 있으면 1, 놓으면 서서히 0으로 내려갑니다.
    /// 연타할 때 줄기가 뚝뚝 끊기지 않도록 여운을 남깁니다.
    /// </summary>
    private void UpdateHold(float dt)
    {
        if (Input.GetKey(relieveKey))
        {
            holdAmount = 1f;
        }
        else
        {
            holdAmount = Mathf.MoveTowards(holdAmount, 0f, releaseFadeSpeed * dt);
        }
    }

    /// <summary>
    /// 잔뇨에서 나오는 기본 출력에 연타 압력을 더해 최종 출력을 구합니다.
    /// </summary>
    private float CalculateFlow(float remaining)
    {
        // 배뇨는 한계 1.5까지 차오르므로 1을 넘는 부분은 최대로 봅니다.
        float volume = Mathf.Clamp01(remaining);
        float fromVolume = Mathf.Lerp(minFlowFromVolume, 1f, Mathf.Clamp01(volumeCurve.Evaluate(volume)));

        // 연타로 올린 출력은 pressureFlowCeiling(기본 최대의 80%)을 넘지 못합니다.
        // 단, 잔뇨만으로 이미 그보다 세게 나오는 중이라면 압력 때문에 오히려 약해지면 안 되므로
        // 잔뇨 출력을 하한으로 잡습니다.
        float boosted = Mathf.Min(fromVolume + pressure * pressureFlowBonus, pressureFlowCeiling);
        float flow = Mathf.Clamp01(Mathf.Max(fromVolume, boosted));

        return flow * holdAmount;
    }

    /// <summary>
    /// 프레임마다 조금씩 방출합니다.
    /// 한 번에 몰아서 뿜으면 같은 지점에서 원뿔로 퍼져 부채꼴이 되므로,
    /// 방출량을 시간에 나눠 이어지는 한 줄기로 만듭니다.
    /// </summary>
    private void EmitStream(float flow, float dt)
    {
        if (stream == null) return;

        ParticleSystem.MainModule main = stream.main;
        main.startSpeed = Mathf.Lerp(minSpeed, maxSpeed, flow);
        main.startLifetime = Mathf.Lerp(minLifetime, maxLifetime, flow);

        float rate = Mathf.Lerp(minEmissionRate, maxEmissionRate, flow);

        // 소수점 이하 방출량은 누적해 두었다가 1이 넘을 때 내보냅니다.
        emitAccumulator += rate * dt;
        int count = Mathf.FloorToInt(emitAccumulator);
        if (count > 0)
        {
            emitAccumulator -= count;
            stream.Emit(count);
        }
    }

    /// <summary>
    /// 출력에 비례해 배뇨를 줄이고 부수 효과를 적용합니다.
    /// 압력이 높으면 더 빨리 빠집니다.
    ///
    /// 위를 보고 쏘면 되떨어진 만큼을 자기가 받습니다.
    ///  - 청결: 역류에 비례해 급격히 나빠집니다.
    ///  - 갈증: 조금 줄어듭니다. (마시는 게 아니라 얼굴로 받는 것이라 효율이 나쁩니다)
    ///  - 스트레스: 시원함이 사라지고 오히려 오릅니다.
    /// 배뇨가 줄어드는 양 자체는 어디로 쏘든 같습니다. 나간 건 나간 것이니까요.
    /// </summary>
    private void ApplyDrain(float flow, float dt)
    {
        float drain = reliefPerSecond * flow * (1f + pressure * pressureDrainBonus);

        needsSystem.Satisfy(NeedType.Urine, drain * dt);

        float splash = Backsplash;

        if (hygieneCostPerSecond > 0f)
        {
            float dirtiness = hygieneCostPerSecond * (1f + splash * backsplashHygieneMultiplier);
            needsSystem.Add(NeedType.Hygiene, dirtiness * flow * dt);
        }

        if (splash > 0f && backsplashThirstReliefPerSecond > 0f)
        {
            needsSystem.Satisfy(NeedType.Thirst, backsplashThirstReliefPerSecond * splash * flow * dt);
        }

        if (stressReliefPerSecond > 0f)
        {
            needsSystem.Satisfy(NeedType.Stress, stressReliefPerSecond * (1f - splash) * flow * dt);
        }

        if (splash > 0f && backsplashStressPerSecond > 0f)
        {
            needsSystem.Add(NeedType.Stress, backsplashStressPerSecond * splash * flow * dt);
        }
    }
}
