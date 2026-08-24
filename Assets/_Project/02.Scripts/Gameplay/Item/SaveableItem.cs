using System.Collections.Generic;
using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>물건이 지금 어디에 있는지입니다.</summary>
    public enum ItemPlacement
    {
        /// <summary>월드에 그냥 놓여 있습니다. <b>하루가 지나면 사라집니다.</b></summary>
        Loose,

        /// <summary>플레이어가 들고 있습니다. 사라지지 않습니다.</summary>
        Held,

        /// <summary>차나 상자 안에 있습니다. 사라지지 않습니다.</summary>
        Stored
    }

    /// <summary>
    /// 세이브에 담기고 <b>하루가 지나면 사라지는</b> 물건에 붙입니다.
    ///
    /// <b>왜 이름표가 필요한가.</b> 불러올 때 물건을 다시 만들려면 "이것이 무엇이었는지"를
    /// 알아야 하는데, <c>GameObject</c> 는 그 정보를 갖고 있지 않습니다. (이름은 "(Clone)"이
    /// 붙고 사람이 바꿀 수도 있어 신원이 못 됩니다) 그래서 프리팹에 <see cref="itemId"/> 를
    /// 적어 두고, <see cref="ItemCatalog"/> 가 그 이름표로 프리팹을 되찾습니다.
    ///
    /// <b>수명은 이벤트가 아니라 상태로 셉니다.</b> 들었다·놓았다를 갈고리로 붙잡는 대신
    /// <see cref="ItemDecay"/> 가 주기적으로 <see cref="Placement"/> 를 물어봅니다.
    /// 물건이 손에서 놓이는 경로는 여럿이고(내려놓기·던지기·차에서 떨어짐·봉투에서 꺼내기)
    /// 하나라도 빠뜨리면 그 경로로 나온 물건만 영원히 남기 때문입니다.
    /// <b>상태를 물어보면 빠뜨릴 경로가 없습니다.</b>
    /// </summary>
    public class SaveableItem : MonoBehaviour
    {
        // --- Constants ---

        /// <summary>놓여 있지 않다는 뜻의 시각 값입니다.</summary>
        public const float NotLoose = -1f;

        // --- Static Registry ---

        /// <summary>
        /// 살아 있는 모든 물건입니다. <see cref="Vehicle"/> 의 등록부와 같은 방식으로,
        /// 스스로 넣고 스스로 빠지므로 세이브와 수명 관리가 씬을 뒤지지 않아도 됩니다.
        /// </summary>
        private static readonly List<SaveableItem> all = new List<SaveableItem>();

        /// <summary>살아 있는 모든 물건입니다.</summary>
        public static IReadOnlyList<SaveableItem> All { get { return all; } }

        // --- Public Member Variables ---

        /// <summary>
        /// 세이브에 적힐 이름표입니다. <see cref="ItemCatalog"/> 의 항목과 같아야 합니다.
        ///
        /// 비어 있으면 이 물건은 <b>저장되지 않습니다.</b> 되살릴 방법이 없는 것을
        /// 저장하면 불러올 때 조용히 사라지기 때문입니다.
        /// </summary>
        [Header("신원")]
        [Tooltip("세이브에 적힐 이름표. ItemCatalog 의 항목과 같아야 합니다. " +
                 "비어 있으면 이 물건은 저장되지 않습니다.")]
        public string itemId = "";

        // --- Public Properties ---

        /// <summary>이 물건이 저장될 수 있는지 여부입니다.</summary>
        public bool HasIdentity { get { return !string.IsNullOrEmpty(itemId); } }

        /// <summary>
        /// 이 물건이 월드에 놓인 시각(게임 내 분)입니다.
        /// 놓여 있지 않으면 <see cref="NotLoose"/> 입니다.
        /// </summary>
        public float LooseSinceMinute { get { return looseSinceMinute; } }

        /// <summary>
        /// 이 물건이 지금 어디에 있는지입니다.
        ///
        /// 차나 상자의 <b>자식</b>인지로 판단합니다. 실려 있는 것과 바닥에 굴러다니는 것을
        /// 가르는 기준이 계층 구조이기 때문입니다. (<see cref="BeverageBox"/> 는 상자를
        /// 벗어난 병을 <c>SetParent(null)</c> 로 떼어 냅니다)
        /// </summary>
        public ItemPlacement Placement
        {
            get
            {
                // Awake 가 돌지 않은 상태(에디터 테스트)에서도 옳게 답해야 합니다.
                if (carryable == null) carryable = GetComponent<Carryable>();

                if (carryable != null && carryable.IsHeld) return ItemPlacement.Held;

                if (GetComponentInParent<Vehicle>(true) != null) return ItemPlacement.Stored;
                if (ContainingBox != null) return ItemPlacement.Stored;

                return ItemPlacement.Loose;
            }
        }

        /// <summary>
        /// 이 물건이 상자 안에 들어 있는지 여부입니다.
        ///
        /// <b>상자 안의 병은 저장하지 않습니다.</b> 상자는 씬에 놓인 붙박이라 불러오기가
        /// 그것을 되돌리지 않고, 그런데도 병만 다시 만들면 상자에 병이 겹쳐 쌓입니다.
        /// 병이 상자를 벗어나는 순간부터 저장 대상이 됩니다.
        /// </summary>
        public bool IsInsideBox { get { return ContainingBox != null; } }

        /// <summary>
        /// 이 물건을 담고 있는 상자입니다. 담겨 있지 않으면 null 입니다.
        ///
        /// <b>자기 자신에 붙은 상자는 세지 않습니다.</b> 맥주 상자는 스스로가
        /// <see cref="BeverageBox"/> 인데, 그것까지 세면 <b>상자가 자기 안에 들어 있는 것</b>이
        /// 되어 저장 대상에서 빠지고 수명도 세지 않게 됩니다.
        /// 부모부터 거슬러 올라가면 그 함정이 없습니다.
        /// </summary>
        private BeverageBox ContainingBox
        {
            get
            {
                Transform parent = transform.parent;
                return parent != null ? parent.GetComponentInParent<BeverageBox>(true) : null;
            }
        }

        // --- Private Member Variables ---

        /// <summary>들 수 있는 물건이라면 그 컴포넌트입니다. 없으면 null입니다.</summary>
        private Carryable carryable;

        /// <summary>월드에 놓인 시각(게임 내 분)입니다.</summary>
        private float looseSinceMinute = NotLoose;

        // --- Unity Event Functions ---

        /// <summary>들 수 있는 물건인지 확인해 둡니다.</summary>
        void Awake()
        {
            carryable = GetComponent<Carryable>();
        }

        /// <summary>등록부에 자신을 넣습니다.</summary>
        void OnEnable()
        {
            EnsureRegistered();
        }

        /// <summary>등록부에서 자신을 뺍니다.</summary>
        void OnDisable()
        {
            all.Remove(this);
        }

        // --- Public Methods ---

        /// <summary>
        /// 등록부에 자신이 들어 있는지 확인하고, 아직이면 넣습니다.
        ///
        /// <b><c>OnEnable</c> 에만 맡기지 않는 이유가 있습니다.</b> 에디터 테스트에서는
        /// <c>OnEnable</c> 이 아예 돌지 않아, 만들어 둔 물건이 등록부에 나타나지 않습니다.
        /// (<c>NeedsSystem.EnsureInitialized</c> 와 같은 사정입니다)
        /// 두 번 불려도 안전합니다.
        /// </summary>
        public void EnsureRegistered()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        /// <summary>
        /// 등록부를 비웁니다. 씬을 다시 불러들일 때처럼 상태가 꼬였을 때, 그리고
        /// 테스트 사이에 씁니다. (<c>GameContext.Clear</c>·<c>SaveRegistry.Clear</c> 와 같은 자리입니다)
        /// </summary>
        public static void Clear()
        {
            all.Clear();
        }

        /// <summary>
        /// 놓여 있는 시각을 기록합니다. <see cref="ItemDecay"/> 가 부릅니다.
        /// </summary>
        /// <param name="minute">지금 게임 시각(분). 놓여 있지 않으면 <see cref="NotLoose"/></param>
        public void SetLooseSince(float minute)
        {
            looseSinceMinute = minute;
        }

        /// <summary>
        /// 이 물건이 얼마나 오래 놓여 있었는지입니다.
        /// </summary>
        /// <param name="nowMinute">지금 게임 시각(분)</param>
        /// <returns>놓여 있은 시간(게임 내 분). 놓여 있지 않으면 0</returns>
        public float LooseFor(float nowMinute)
        {
            if (looseSinceMinute <= NotLoose) return 0f;
            return Mathf.Max(0f, nowMinute - looseSinceMinute);
        }

        // --- Private Methods ---

        /// <summary>
        /// 플레이 모드에 들어갈 때 등록부를 비웁니다.
        /// 도메인 리로드를 꺼 두면 지난 실행의 항목이 유령으로 남습니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
        }
    }
}
