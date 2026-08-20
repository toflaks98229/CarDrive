using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>재화 종류 하나를 인자로 넘기는 인스펙터 연결용 이벤트입니다.</summary>
    [System.Serializable]
    public class CurrencyEvent : UnityEvent<CurrencyType> { }

    /// <summary>
    /// 플레이어가 가진 재화를 소유합니다.
    ///
    /// 설계 규칙은 <see cref="NeedsSystem"/>과 같습니다.
    ///  - 게임 상태는 이 컴포넌트가 소유하고, UI는 <b>읽기만</b> 합니다. (CurrencyUI)
    ///  - 값은 정수입니다. 돈에 소수점이 생기면 표시와 계산이 어긋나기 시작합니다.
    ///  - 쓰는 쪽은 <see cref="TrySpend"/>로 <b>성공 여부를 받아</b> 처리합니다.
    ///    잔액을 먼저 묻고 빼는 방식은 두 호출 사이에 값이 바뀌면 음수가 됩니다.
    ///
    /// 런타임에 생성되는 것(풀에서 나온 엑토플라즘 등)은 인스펙터 연결이 없으므로
    /// <see cref="Report"/>로 지갑을 찾지 않고 넣습니다.
    /// </summary>
    public class Wallet : MonoBehaviour, ISaveable
    {
        // --- Static Access ---

        /// <summary>씬의 지갑입니다. 없으면 재화 관련 호출은 조용히 무시됩니다.</summary>
        public static Wallet Instance { get { return GameContext.Get<Wallet>(); } }

        // --- Public Properties : 상태 ---

        /// <summary>
        /// 실행 중 보유량입니다. 값을 바꾸려면 <see cref="Add"/>나 <see cref="TrySpend"/>를 쓰세요.
        /// </summary>
        public IReadOnlyList<CurrencyState> States { get { EnsureInitialized(); return _states; } }

        // --- Public Member Variables ---

        /// <summary>재화별 설정입니다. 비워두면 <see cref="CurrencyDefaults"/>의 기본값을 씁니다.</summary>
        [Header("설정")]
        [Tooltip("재화별 설정. 비워두면 기본값이 사용됩니다.")]
        [SerializeField, FormerlySerializedAs("settings")]
        private List<CurrencySetting> _settings = new List<CurrencySetting>();

        /// <summary>
        /// 실행 중 보유량입니다. 인스펙터에서 확인하고 손으로 고칠 수 있습니다.
        ///
        /// <b>public 이 아닙니다.</b> 예전에는 공개 필드라 바깥 코드가
        /// <see cref="TrySpend"/>를 거치지 않고 값을 직접 뺄 수 있었고, 그러면
        /// <b>잔액 검사와 이벤트를 건너뜁니다.</b> 지갑이 음수가 되어도 아무도 모르는 상태가 됩니다.
        ///
        /// <c>[SerializeField]</c>라 인스펙터 표시와 디버그용 수정은 그대로 됩니다.
        /// 읽기만 할 곳은 <see cref="States"/>를 쓰세요.
        /// </summary>
        [Header("현재 상태 (읽기 전용)")]
        [Tooltip("실행 중 보유량입니다. 인스펙터에서 확인 및 강제 수정이 가능합니다.")]
        [SerializeField, FormerlySerializedAs("states")]
        private List<CurrencyState> _states = new List<CurrencyState>();

        /// <summary>재화가 늘었을 때 호출됩니다.</summary>
        [Header("이벤트")]
        [Tooltip("재화가 늘었을 때 (Feel 의 MMF_Player 를 여기에 연결하세요)")]
        public CurrencyEvent onGained;

        /// <summary>재화를 썼을 때 호출됩니다.</summary>
        [Tooltip("재화를 썼을 때")]
        public CurrencyEvent onSpent;

        /// <summary>잔액이 모자라 지불에 실패했을 때 호출됩니다.</summary>
        [Tooltip("잔액이 모자라 지불에 실패했을 때 (거절음·흔들림 연출용)")]
        public CurrencyEvent onInsufficientFunds;

        // --- Private Member Variables ---

        /// <summary>
        /// 설정과 상태를 짝지어 관리하는 표입니다.
        /// 복사본 만들기·빠진 항목 메우기·조회·세이브 담기를 전부 여기가 합니다.
        /// (예전에는 이 골격을 NeedsSystem과 각각 손으로 썼습니다)
        /// </summary>
        private readonly DefinitionTable<CurrencyType, CurrencySetting, CurrencyState> table =
            new DefinitionTable<CurrencyType, CurrencySetting, CurrencyState>();

        /// <summary>설정과 상태를 이미 만들었는지 여부입니다.</summary>
        private bool initialized;

        // --- Unity Event Functions ---

        /// <summary>
        /// 자신을 등록하고 설정·초기 보유량을 만듭니다.
        /// 이미 다른 지갑이 있으면 자신을 끕니다.
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

        /// <summary>등록을 해제합니다.</summary>
        void OnDestroy()
        {
            GameContext.Unregister(this);
            SaveRegistry.Unregister(this);
        }

        // --- ISaveable ---

        /// <summary>다른 것에 기대지 않아 마지막이어도 됩니다.</summary>
        public int SaveOrder { get { return SaveOrders.Wallet; } }

        /// <summary>보유 재화를 세이브에 적습니다.</summary>
        /// <param name="data">적어 넣을 세이브 자료</param>
        public void CaptureInto(SaveData data)
        {
            data.wallet = CaptureState();
        }

        /// <summary>세이브에서 보유 재화를 되돌립니다.</summary>
        /// <param name="data">읽어 올 세이브 자료</param>
        public void RestoreFrom(SaveData data)
        {
            RestoreState(data.wallet);
        }

        // --- Public Methods ---

        /// <summary>
        /// 설정과 보유량이 준비되어 있는지 확인하고, 아직이면 만듭니다.
        ///
        /// <b>Awake 에만 맡기지 않는 이유가 있습니다.</b> 다른 컴포넌트가 자기 Awake 에서
        /// 지갑을 건드리면 순서에 따라 아직 준비되지 않은 지갑을 만날 수 있습니다.
        /// 또 에디터 테스트에서는 Awake 가 아예 돌지 않습니다.
        /// 그래서 <b>쓰기 직전에 스스로 확인</b>합니다. 두 번 불려도 안전합니다.
        /// </summary>
        public void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            // 인스펙터에 적어 둔 설정이 있으면 그것을, 없으면 기본값을 씁니다.
            // 빠진 재화를 메우고 조회표를 짜는 것까지 표가 알아서 합니다.
            table.Build(
                authored: _settings,
                fallback: CurrencyDefaults.CreateSettings(),
                stateList: _states,
                createState: setting => new CurrencyState { type = setting.type, amount = setting.startingAmount },
                ownerName: "Wallet",
                context: this);

            // 메워진 뒤의 목록을 인스펙터에도 돌려줍니다.
            _settings = table.Settings;
            _states = table.States;
        }

        /// <summary>
        /// 재화를 넣습니다. 0 이하를 넣으면 아무 일도 하지 않습니다.
        /// (빼려면 <see cref="TrySpend"/>를 쓰세요)
        /// </summary>
        /// <param name="type">넣을 재화 종류</param>
        /// <param name="amount">넣을 양. 양수여야 합니다.</param>
        /// <returns>실제로 늘어난 양. 최대치에 걸리면 요청한 것보다 적을 수 있습니다.</returns>
        public int Add(CurrencyType type, int amount)
        {
            EnsureInitialized();

            if (amount <= 0) return 0;

            CurrencyState state = GetState(type);
            if (state == null) return 0;

            int before = state.amount;
            int max = GetMax(type);

            state.amount = max > 0 ? Mathf.Min(state.amount + amount, max) : state.amount + amount;

            int gained = state.amount - before;
            if (gained > 0 && onGained != null) onGained.Invoke(type);

            return gained;
        }

        /// <summary>
        /// 재화를 씁니다. <b>모자라면 아무것도 빼지 않고 false를 돌려줍니다.</b>
        ///
        /// 잔액을 먼저 묻고 따로 빼는 방식은 두 호출 사이에 값이 바뀌면 음수가 됩니다.
        /// 그래서 확인과 차감을 한 번에 합니다.
        /// </summary>
        /// <param name="type">쓸 재화 종류</param>
        /// <param name="amount">쓸 양. 0 이하면 아무 일도 하지 않고 true입니다.</param>
        /// <returns>지불에 성공했으면 true입니다.</returns>
        public bool TrySpend(CurrencyType type, int amount)
        {
            EnsureInitialized();

            if (amount <= 0) return true;

            CurrencyState state = GetState(type);
            if (state == null) return false;

            if (state.amount < amount)
            {
                if (onInsufficientFunds != null) onInsufficientFunds.Invoke(type);
                return false;
            }

            state.amount -= amount;
            if (onSpent != null) onSpent.Invoke(type);

            return true;
        }

        /// <summary>지금 가진 양입니다.</summary>
        /// <param name="type">확인할 재화 종류</param>
        public int Get(CurrencyType type)
        {
            EnsureInitialized();

            CurrencyState state = GetState(type);
            return state != null ? state.amount : 0;
        }

        /// <summary>지불할 수 있는지 확인만 합니다. 실제로 쓸 때는 <see cref="TrySpend"/>를 쓰세요.</summary>
        /// <param name="type">확인할 재화 종류</param>
        /// <param name="amount">필요한 양</param>
        public bool CanAfford(CurrencyType type, int amount)
        {
            return Get(type) >= amount;
        }

        /// <summary>해당 재화의 설정을 돌려줍니다. (UI에서 이름·색·표기 형식을 읽을 때 사용)</summary>
        /// <param name="type">찾을 재화 종류</param>
        /// <returns>설정. 등록되어 있지 않으면 null입니다.</returns>
        public CurrencySetting GetSetting(CurrencyType type)
        {
            EnsureInitialized();
            return table.GetSetting(type);
        }

        /// <summary>모든 재화 설정을 순서대로 돌려줍니다.</summary>
        public IReadOnlyList<CurrencySetting> GetAllSettings()
        {
            EnsureInitialized();

            return _settings;
        }

        /// <summary>
        /// 설정의 표기 규칙에 맞춰 숫자를 글자로 만듭니다. UI가 같은 규칙을 쓰도록 여기에 둡니다.
        /// </summary>
        /// <param name="type">표기할 재화 종류</param>
        /// <returns>접두·접미가 붙은 문자열. 예) "₩1,250"</returns>
        public string Format(CurrencyType type)
        {
            int amount = Get(type);
            CurrencySetting setting = GetSetting(type);
            if (setting == null) return amount.ToString();

            string number = string.IsNullOrEmpty(setting.numberFormat)
                ? amount.ToString()
                : amount.ToString(setting.numberFormat);

            return setting.prefix + number + setting.suffix;
        }

        /// <summary>
        /// 런타임에 생성된 것이 인스펙터 연결 없이 재화를 넣을 때 씁니다.
        /// 지갑이 씬에 없으면 조용히 무시합니다.
        /// </summary>
        /// <param name="type">넣을 재화 종류</param>
        /// <param name="amount">넣을 양</param>
        /// <returns>실제로 늘어난 양. 지갑이 없으면 0입니다.</returns>
        public static int Report(CurrencyType type, int amount)
        {
            Wallet wallet = Instance;
            return wallet != null ? wallet.Add(type, amount) : 0;
        }

        /// <summary>세이브용으로 현재 보유량을 복사해 돌려줍니다.</summary>
        public List<CurrencyState> CaptureState()
        {
            EnsureInitialized();
            return table.Capture();
        }

        /// <summary>세이브에서 읽은 보유량을 되돌립니다.</summary>
        /// <param name="saved">되돌릴 보유량 목록. null이면 아무것도 하지 않습니다.</param>
        public void RestoreState(List<CurrencyState> saved)
        {
            EnsureInitialized();
            table.Restore(saved);
        }

        /// <summary>모든 재화를 시작값으로 되돌립니다.</summary>
        [ContextMenu("지갑 초기화")]
        public void ResetAll()
        {
            EnsureInitialized();

            for (int i = 0; i < _states.Count; i++)
            {
                CurrencySetting setting = GetSetting(_states[i].type);
                _states[i].amount = setting != null ? setting.startingAmount : 0;
            }
        }

        // --- Private Methods ---

        /// <summary>재화 종류에 해당하는 현재 상태를 돌려줍니다.</summary>
        /// <param name="type">찾을 재화 종류</param>
        /// <returns>상태. 등록되어 있지 않으면 null입니다.</returns>
        private CurrencyState GetState(CurrencyType type)
        {
            return table.GetState(type);
        }

        /// <summary>해당 재화의 최대치입니다. 0이면 제한이 없습니다.</summary>
        /// <param name="type">확인할 재화 종류</param>
        private int GetMax(CurrencyType type)
        {
            CurrencySetting setting = GetSetting(type);
            return setting != null ? setting.maxAmount : 0;
        }
    }
}
