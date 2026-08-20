using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using CarDrive.Gameplay;
using UnityEngine.Serialization;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>NeedType 하나를 인자로 넘기는 인스펙터 연결용 이벤트입니다.</summary>
    [System.Serializable]
    public class NeedTypeEvent : UnityEvent<NeedType> { }

    /// <summary>
    /// 허기·갈증·피로·스트레스·배뇨·청결 6종 니즈를 관리하는 핵심 시스템입니다.
    ///
    /// 설계 규칙:
    ///  - 모든 니즈는 0에서 시작해 1로 차오르며, 값이 클수록 나쁩니다.
    ///  - 1을 넘어도 즉시 죽지 않고 overflowLimit(기본 1.5)까지 유예가 있습니다.
    ///    이 "경고 후 악화" 구조가 마이 썸머 카의 니즈 처리 방식입니다.
    ///  - 게임 상태는 이 컴포넌트가 소유하며, UI는 읽기만 합니다. (NeedsUI 참고)
    /// </summary>
    public class NeedsSystem : MonoBehaviour, ISaveable
    {
        // --- Static Access ---

        /// <summary>
        /// 씬에 존재하는 NeedsSystem입니다. 런타임에 생성되는 오브젝트(귀신 등)가
        /// 인스펙터 연결 없이 스트레스를 올릴 수 있도록 열어 두었습니다.
        /// </summary>
        public static NeedsSystem Instance { get { return GameContext.Get<NeedsSystem>(); } }

        // --- Public Member Variables ---

        [Header("설정")]
        [Tooltip("니즈 설정 에셋. 비워두면 NeedDefaults의 기본값이 사용됩니다.")]
        public NeedsProfile profile;

        [Tooltip("실제 1초당 흐르는 게임 시간(분). 1이면 실제 24분이 게임 내 하루입니다.")]
        public float gameMinutesPerRealSecond = 1f;

        [Tooltip("체크를 해제하면 니즈가 더 이상 차오르지 않습니다. (디버그용)")]
        public bool needsEnabled = true;

        [Header("연동 컴포넌트")]
        [Tooltip("한계 초과 시 체력을 깎을 대상. 보통 플레이어의 PlayerHealth입니다. " +
                 "비워두면 체력은 줄지 않고 이벤트만 발생합니다.")]
        public Health healthBar;

        [Header("기절 설정")]
        [Tooltip("피로가 한계를 넘어 기절했을 때 회복되는 피로 수치")]
        public float blackoutFatigueReset = 0.35f;

        [Tooltip("기절해 있는 동안 흐르는 게임 시간(분). 그동안 다른 니즈는 계속 차오릅니다.")]
        public float blackoutGameMinutes = 240f;

        [Header("이벤트")]
        [Tooltip("경고 임계를 넘었을 때")]
        public NeedTypeEvent onNeedWarning;

        [Tooltip("한계(overflowLimit)를 넘었을 때")]
        public NeedTypeEvent onNeedCritical;

        [Tooltip("경고 임계 아래로 회복되었을 때")]
        public NeedTypeEvent onNeedRelieved;

        [Tooltip("피로 한계 초과로 기절했을 때")]
        public UnityEvent onBlackout;

        /// <summary>상호작용으로 니즈가 눈에 띄게 나빠졌을 때 호출됩니다.</summary>
        [Tooltip("상호작용으로 니즈가 눈에 띄게 나빠졌을 때. Feel 의 MMF_Player 를 연결하세요.")]
        public NeedTypeEvent onNeedWorsened;

        /// <summary>상호작용으로 니즈가 눈에 띄게 해소되었을 때 호출됩니다.</summary>
        [Tooltip("상호작용으로 니즈가 눈에 띄게 해소되었을 때. Feel 의 MMF_Player 를 연결하세요.")]
        public NeedTypeEvent onNeedRelievedStep;

        /// <summary>
        /// 변화 이벤트를 낼 최소 변화량입니다.
        ///
        /// <b>시간에 따른 상시 증가는 이 이벤트를 내지 않습니다.</b> (그쪽은 Tick 이 직접 값을 씁니다)
        /// 여기서 거르는 것은 아주 작은 상호작용입니다. 이 값이 없으면 비를 맞는 동안
        /// 매 프레임 이벤트가 터져 연출이 도배됩니다.
        /// </summary>
        [Tooltip("변화 이벤트를 낼 최소 변화량. 이보다 작은 변화는 조용히 넘어갑니다.")]
        public float changeEventThreshold = 0.02f;

        /// <summary>
        /// 실행 중 니즈 수치입니다. 인스펙터에서 확인하고 손으로 고칠 수 있습니다.
        ///
        /// <b>public 이 아닙니다.</b> 예전에는 공개 필드라 바깥 코드가
        /// <see cref="Add"/>를 거치지 않고 값을 직접 쓸 수 있었고, 그 경로로 들어온 변경은
        /// <b>임계 판정과 이벤트를 건너뛰었습니다.</b> 게이지가 빨개지지도, 기절하지도 않은 채
        /// 수치만 한계를 넘는 종류의 버그입니다.
        ///
        /// <c>[SerializeField]</c>라 인스펙터 표시와 디버그용 수정은 그대로 됩니다.
        /// 읽기만 할 곳은 <see cref="States"/>를 쓰세요.
        /// </summary>
        [Header("현재 상태 (읽기 전용)")]
        [Tooltip("실행 중 니즈 수치입니다. 인스펙터에서 확인 및 강제 수정이 가능합니다.")]
        [SerializeField, FormerlySerializedAs("states")]
        private List<NeedState> _states = new List<NeedState>();

        // --- Public Properties : 상태 ---

        /// <summary>
        /// 실행 중 니즈 수치입니다. 값을 바꾸려면 <see cref="Add"/>나 <see cref="Satisfy"/>를 쓰세요.
        /// </summary>
        public IReadOnlyList<NeedState> States { get { EnsureInitialized(); return _states; } }

        // --- Private Member Variables ---

        /// <summary>
        /// 설정과 상태를 짝지어 관리하는 표입니다.
        /// 프로파일 복사·빠진 항목 메우기·조회·세이브 담기를 전부 여기가 합니다.
        /// (예전에는 이 골격을 <see cref="Wallet"/>과 각각 손으로 썼습니다)
        /// </summary>
        private readonly DefinitionTable<NeedType, NeedSetting, NeedState> table =
            new DefinitionTable<NeedType, NeedSetting, NeedState>();

        /// <summary>실제로 쓰는 니즈 설정 목록입니다. 표가 메운 뒤의 것을 가리킵니다.</summary>
        private List<NeedSetting> settings;

        /// <summary>니즈끼리 서로 영향을 주는 연쇄 규칙 목록입니다.</summary>
        private List<NeedCoupling> couplings;

        /// <summary>설정과 상태를 이미 만들었는지 여부입니다.</summary>
        private bool initialized;

        // 이번 프레임에 연쇄 규칙으로 추가될 증가량 (매 프레임 재계산)
        private readonly Dictionary<NeedType, float> couplingBuffer = new Dictionary<NeedType, float>();

        // --- Unity Event Functions ---

        /// <summary>
        /// 자신을 전역 인스턴스로 등록하고 니즈 설정과 초기 상태를 만듭니다.
        /// 이미 다른 인스턴스가 있으면 경고를 남기고 자신을 끕니다.
        /// </summary>
        void Awake()
        {
            // 등록이 거부되면 이미 다른 것이 있다는 뜻입니다. (경고는 GameContext가 남깁니다)
            if (!GameContext.Register(this))
            {
                enabled = false;
                return;
            }

            SaveRegistry.Register(this);

            EnsureInitialized();
        }

        /// <summary>
        /// 자신이 전역 인스턴스였다면 그 참조를 비웁니다.
        /// </summary>
        void OnDestroy()
        {
            GameContext.Unregister(this);
            SaveRegistry.Unregister(this);
        }

        // --- ISaveable ---

        /// <summary>시계와 날씨 다음입니다.</summary>
        public int SaveOrder { get { return SaveOrders.Needs; } }

        /// <summary>니즈 수치를 세이브에 적습니다.</summary>
        /// <param name="data">적어 넣을 세이브 자료</param>
        public void CaptureInto(SaveData data)
        {
            data.needs = CaptureState();
        }

        /// <summary>세이브에서 니즈 수치를 되돌립니다.</summary>
        /// <param name="data">읽어 올 세이브 자료</param>
        public void RestoreFrom(SaveData data)
        {
            RestoreState(data.needs);
        }

        /// <summary>
        /// 이번 프레임만큼 니즈를 진행시킵니다. needsEnabled가 꺼져 있으면 아무 일도 하지 않습니다.
        /// </summary>
        void Update()
        {
            if (!needsEnabled) return;
            Tick(Time.deltaTime);
        }

        // --- Public Methods ---

        /// <summary>
        /// 설정과 상태가 준비되어 있는지 확인하고, 아직이면 만듭니다.
        ///
        /// <b>Awake 에만 맡기지 않는 이유가 있습니다.</b> 다른 컴포넌트가 자기 Awake 에서
        /// 니즈를 건드리면 순서에 따라 아직 준비되지 않은 것을 만날 수 있습니다.
        /// 또 에디터 테스트에서는 Awake 가 아예 돌지 않습니다.
        /// 그래서 <b>쓰기 직전에 스스로 확인</b>합니다. 두 번 불려도 안전합니다.
        /// </summary>
        public void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            // 프로파일 에셋이 있으면 그것을, 없으면 기본값을 씁니다.
            // 복사본 만들기·빠진 니즈 메우기·조회표 짓기는 전부 표가 합니다.
            bool hasProfileSettings = profile != null && profile.settings != null && profile.settings.Count > 0;

            table.Build(
                authored: hasProfileSettings ? profile.settings : null,
                fallback: NeedDefaults.CreateSettings(),
                stateList: _states,
                createState: setting => new NeedState { type = setting.type, value = 0f },
                ownerName: "NeedsSystem",
                context: this);

            settings = table.Settings;
            _states = table.States;

            // 연쇄 규칙은 설정과 짝이 맞을 필요가 없어 표를 쓰지 않습니다. 그대로 골라 씁니다.
            bool hasProfileCouplings = profile != null && profile.couplings != null && profile.couplings.Count > 0;
            couplings = hasProfileCouplings
                ? new List<NeedCoupling>(profile.couplings)
                : NeedDefaults.CreateCouplings();
        }

        /// <summary>
        /// 니즈를 한 스텝 진행시킵니다. 평소에는 Update가 알아서 호출합니다.
        /// 나중에 별도의 시간 시스템이 니즈를 직접 구동하고 싶을 때, 또는
        /// 테스트에서 프레임 없이 진행시키고 싶을 때 이 메서드를 씁니다.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            EnsureInitialized();

            if (deltaSeconds <= 0f) return;

            // 씬에 TimeSystem이 있으면 그쪽 시간 배율을 따릅니다.
            // 그래야 니즈와 날씨가 같은 시계를 보고 움직입니다.
            float rate = TimeSystem.GetMinutesPerSecond(gameMinutesPerRealSecond);

            TickNeeds(deltaSeconds * rate);
            ApplyConsequences(deltaSeconds);
        }

        /// <summary>
        /// 니즈 수치를 직접 더합니다. 양수면 악화, 음수면 해소입니다.
        /// </summary>
        public void Add(NeedType type, float amount)
        {
            EnsureInitialized();

            NeedState state = GetState(type);
            if (state == null) return;

            NeedSetting setting = GetSetting(type);
            float limit = setting != null ? setting.overflowLimit : 1.5f;

            float before = state.value;
            state.value = Mathf.Clamp(state.value + amount, 0f, limit);
            EvaluateThresholds(state, setting);

            RaiseChanged(type, state.value - before);
        }

        /// <summary>
        /// 니즈를 해소합니다. (Add의 반대 방향 편의 메서드)
        /// </summary>
        public void Satisfy(NeedType type, float amount)
        {
            Add(type, -amount);
        }

        /// <summary>
        /// 현재 수치를 그대로 돌려줍니다. (0 ~ overflowLimit)
        /// </summary>
        public float GetValue(NeedType type)
        {
            NeedState state = GetState(type);
            return state != null ? state.value : 0f;
        }

        /// <summary>
        /// 게이지 표시에 쓸 0~1 값입니다. invertDisplay가 켜진 니즈는 뒤집어서 돌려줍니다.
        /// </summary>
        public float GetDisplayFill(NeedType type)
        {
            NeedSetting setting = GetSetting(type);
            float raw = Mathf.Clamp01(GetValue(type));
            return (setting != null && setting.invertDisplay) ? 1f - raw : raw;
        }

        /// <summary>경고 임계를 넘었는지 여부입니다.</summary>
        public bool IsWarning(NeedType type)
        {
            NeedState state = GetState(type);
            return state != null && state.isWarning;
        }

        /// <summary>한계를 넘어 실제 피해가 발생하는 상태인지 여부입니다.</summary>
        public bool IsCritical(NeedType type)
        {
            NeedState state = GetState(type);
            return state != null && state.isCritical;
        }

        /// <summary>해당 니즈의 설정값을 돌려줍니다. (UI에서 이름·색상을 읽을 때 사용)</summary>
        public NeedSetting GetSetting(NeedType type)
        {
            EnsureInitialized();
            return table.GetSetting(type);
        }

        /// <summary>모든 니즈 설정을 순서대로 돌려줍니다.</summary>
        public IReadOnlyList<NeedSetting> GetAllSettings()
        {
            EnsureInitialized();

            return settings;
        }

        /// <summary>
        /// 상호작용 하나가 정의한 효과를 한꺼번에 적용합니다.
        /// </summary>
        public void ApplyEffects(List<NeedEffect> effects)
        {
            ApplyEffects(effects, 1f);
        }

        /// <summary>
        /// 상호작용 하나가 정의한 효과를 적용하되, 해소되는 양에만 배율을 곱합니다.
        ///
        /// 배율을 <b>해소 쪽에만</b> 거는 이유가 있습니다. 예를 들어 잠자리가 나빠
        /// 수면 회복이 0.85배가 되더라도, 자는 동안 배가 고파지는 양까지 줄어들면 안 됩니다.
        /// 8시간은 어느 쪽이든 똑같이 흘렀기 때문입니다.
        /// </summary>
        /// <param name="reliefScale">해소량에 곱할 배율. 1이면 원래대로입니다.</param>
        public void ApplyEffects(List<NeedEffect> effects, float reliefScale)
        {
            if (effects == null) return;

            for (int i = 0; i < effects.Count; i++)
            {
                // relief가 양수면 해소, 음수면 악화
                float relief = effects[i].relief;
                if (relief > 0f) relief *= reliefScale;

                Satisfy(effects[i].type, relief);
            }
        }

        /// <summary>
        /// 지정한 게임 시간만큼을 한 번에 흘려보냅니다. (수면·이동 등 시간 건너뛰기용)
        /// 체력 감소는 적용하지 않습니다.
        /// </summary>
        public void AdvanceGameMinutes(float gameMinutes)
        {
            if (gameMinutes <= 0f) return;

            TickNeeds(gameMinutes);

            // 수면처럼 시간을 건너뛸 때는 시계도 함께 돌려야 날이 밝습니다.
            if (TimeSystem.Instance != null) TimeSystem.Instance.AdvanceMinutes(gameMinutes);
        }

        /// <summary>
        /// 런타임에 생성되는 오브젝트가 인스펙터 연결 없이 니즈를 올릴 때 씁니다.
        /// NeedsSystem이 씬에 없으면 조용히 무시합니다.
        /// </summary>
        public static void Report(NeedType type, float amount)
        {
            if (Instance == null) return;
            Instance.Add(type, amount);
        }

        /// <summary>
        /// 세이브용으로 현재 수치를 복사해 돌려줍니다.
        /// </summary>
        public List<NeedState> CaptureState()
        {
            EnsureInitialized();
            return table.Capture();
        }

        /// <summary>
        /// 세이브에서 읽은 수치를 되돌립니다.
        /// </summary>
        public void RestoreState(List<NeedState> saved)
        {
            EnsureInitialized();
            table.Restore(saved);
        }

        /// <summary>모든 니즈를 0으로 되돌립니다.</summary>
        [ContextMenu("모든 니즈 초기화")]
        public void ResetAll()
        {
            EnsureInitialized();

            for (int i = 0; i < _states.Count; i++)
            {
                _states[i].value = 0f;
                _states[i].isWarning = false;
                _states[i].isCritical = false;
            }
        }

        // --- Private Methods ---

        /// <summary>
        /// 니즈 종류에 해당하는 현재 상태를 돌려줍니다.
        /// </summary>
        /// <param name="type">찾을 니즈 종류</param>
        /// <returns>해당 니즈의 상태. 등록되어 있지 않으면 null입니다.</returns>
        private NeedState GetState(NeedType type)
        {
            EnsureInitialized();
            return table.GetState(type);
        }

        /// <summary>
        /// 기본 증가량과 연쇄 규칙을 적용해 모든 니즈를 진행시킵니다.
        /// </summary>
        private void TickNeeds(float gameMinutes)
        {
            // 1. 연쇄 규칙으로 인한 추가 증가량을 먼저 모읍니다.
            //    (이번 틱의 시작 시점 값을 기준으로 계산해야 적용 순서에 따라 결과가 달라지지 않습니다.)
            couplingBuffer.Clear();
            for (int i = 0; i < couplings.Count; i++)
            {
                NeedCoupling rule = couplings[i];
                NeedState source = GetState(rule.source);
                if (source == null || source.value < rule.sourceThreshold) continue;

                float acc;
                couplingBuffer.TryGetValue(rule.target, out acc);
                couplingBuffer[rule.target] = acc + rule.extraFillPerGameMinute;
            }

            // 2. 기본 증가량 + 연쇄 증가량을 적용합니다.
            for (int i = 0; i < settings.Count; i++)
            {
                NeedSetting setting = settings[i];
                NeedState state = GetState(setting.type);
                if (state == null) continue;

                float rate = setting.fillPerGameMinute;

                float extra;
                if (couplingBuffer.TryGetValue(setting.type, out extra)) rate += extra;

                state.value = Mathf.Clamp(state.value + rate * gameMinutes, 0f, setting.overflowLimit);
                EvaluateThresholds(state, setting);
            }
        }

        /// <summary>
        /// 눈에 띄는 변화가 있었으면 알립니다.
        ///
        /// 잘린 뒤의 <b>실제</b> 변화량으로 판단합니다. 이미 가득 찬 니즈에 더 넣으면
        /// 값이 바뀌지 않으므로 연출도 나오지 않아야 합니다.
        /// </summary>
        /// <param name="type">바뀐 니즈</param>
        /// <param name="delta">실제로 바뀐 양. 양수면 악화, 음수면 해소입니다.</param>
        private void RaiseChanged(NeedType type, float delta)
        {
            if (Mathf.Abs(delta) < changeEventThreshold) return;

            if (delta > 0f)
            {
                if (onNeedWorsened != null) onNeedWorsened.Invoke(type);
            }
            else
            {
                if (onNeedRelievedStep != null) onNeedRelievedStep.Invoke(type);
            }
        }

        /// <summary>
        /// 경고·한계 임계를 넘나들 때 이벤트를 발생시킵니다.
        /// </summary>
        private void EvaluateThresholds(NeedState state, NeedSetting setting)
        {
            if (setting == null) return;

            // 한계(overflowLimit) 도달 여부
            bool nowCritical = state.value >= setting.overflowLimit;
            if (nowCritical && !state.isCritical)
            {
                state.isCritical = true;
                Debug.Log("NeedsSystem: " + setting.displayName + " 한계 초과!");
                if (onNeedCritical != null) onNeedCritical.Invoke(state.type);
            }
            else if (!nowCritical && state.isCritical)
            {
                state.isCritical = false;
            }

            // 경고 임계 도달 여부
            bool nowWarning = state.value >= setting.warnThreshold;
            if (nowWarning && !state.isWarning)
            {
                state.isWarning = true;
                if (onNeedWarning != null) onNeedWarning.Invoke(state.type);
            }
            else if (!nowWarning && state.isWarning)
            {
                state.isWarning = false;
                if (onNeedRelieved != null) onNeedRelieved.Invoke(state.type);
            }
        }

        /// <summary>
        /// 한계를 넘은 니즈의 실제 처벌(체력 감소·기절)을 적용합니다.
        /// </summary>
        private void ApplyConsequences(float deltaTime)
        {
            float totalDrain = 0f;

            for (int i = 0; i < settings.Count; i++)
            {
                NeedSetting setting = settings[i];
                NeedState state = GetState(setting.type);
                if (state == null || !state.isCritical) continue;

                if (setting.consequence == NeedConsequence.Lethal)
                {
                    totalDrain += setting.criticalHealthDrainPerSecond;
                }
                else if (setting.consequence == NeedConsequence.Blackout)
                {
                    TriggerBlackout(state, setting);
                }
                // Nuisance는 직접 처벌이 없습니다. (연쇄 규칙으로만 영향을 줍니다)
            }

            if (totalDrain > 0f && healthBar != null)
            {
                healthBar.TakeDamage(totalDrain * deltaTime);
            }
        }

        /// <summary>
        /// 피로가 한계를 넘었을 때 기절 처리합니다.
        /// 기절해 있는 동안의 시간이 흐르므로 다른 니즈는 더 나빠집니다.
        /// </summary>
        private void TriggerBlackout(NeedState fatigueState, NeedSetting setting)
        {
            Debug.Log("NeedsSystem: 피로 한계 초과 - 기절합니다.");

            // 먼저 피로를 낮춰 두어야 시간을 흘릴 때 다시 기절 판정이 나지 않습니다.
            fatigueState.value = Mathf.Clamp(blackoutFatigueReset, 0f, setting.overflowLimit);
            fatigueState.isCritical = false;
            fatigueState.isWarning = fatigueState.value >= setting.warnThreshold;

            AdvanceGameMinutes(blackoutGameMinutes);

            if (onBlackout != null) onBlackout.Invoke();
        }
    }
}
