using UnityEngine;
using UnityEngine.Events;
using VContainer;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 계산대에 놓인 <b>무르는 자리</b>입니다. 조준해 상호작용하면 마지막에 담은 것이
    /// 하나씩 진열대로 돌아갑니다.
    ///
    /// <b>왜 진열대가 아니라 여기인가.</b> 진열대에서 담기와 무르기를 같은 키로 하면
    /// "한 번 더 조준하면 하나 더 담기는가 방금 것이 빠지는가"가 모호해집니다.
    /// 진열대는 담기만, 계산대는 무르기와 계산만 맡으면 <b>자리마다 할 일이 하나</b>입니다.
    ///
    /// <see cref="ShopClerk"/> 과 나란히 두세요. 점원은 계산을, 이 자리는 무르기를 맡습니다.
    /// 상호작용 키가 하나뿐이라 한 오브젝트가 둘을 다 할 수 없기 때문에 나뉘어 있습니다.
    /// </summary>
    public class ShopReturnPoint : MonoBehaviour, IInteractable
    {
        // --- Public Member Variables ---

        [Header("연동")]
        /// <summary>무를 계산대입니다. 비워두면 부모에서 찾고, 그래도 없으면 등록부에서 찾습니다.</summary>
        [Tooltip("무를 계산대. 비워두면 부모에서 찾고, 그래도 없으면 등록부에서 찾습니다.")]
        public ShopCounter counter;

        [Header("문구")]
        /// <summary>무를 때 보여 줄 동사입니다.</summary>
        [Tooltip("무를 때 보여 줄 동사")]
        public string returnLabel = "돌려놓기";

        /// <summary>영업 시간이 아닐 때 보여 줄 문구입니다. {0}에 여는 시각이 들어갑니다.</summary>
        [Tooltip("영업 시간이 아닐 때 보여 줄 문구. {0}에 여는 시각이 들어갑니다.")]
        public string closedFormat = "영업 시간이 아닙니다 ({0} 개점)";

        [Header("이벤트")]
        /// <summary>하나 되돌렸을 때. Feel 의 MMF_Player 를 연결하세요.</summary>
        [Tooltip("하나 되돌렸을 때. Feel 의 MMF_Player 를 연결하세요.")]
        public UnityEvent onReturned;

        // --- Private Member Variables ---

        /// <summary>값을 사람이 읽는 꼴로 적기 위한 지갑입니다. 없으면 숫자만 적습니다.</summary>
        private Wallet wallet;

        // --- Injection ---

        /// <summary>값 표기에 쓸 지갑을 받습니다.</summary>
        /// <param name="playerWallet">플레이어의 지갑</param>
        [Inject]
        public void Construct(Wallet playerWallet)
        {
            wallet = playerWallet;
        }

        // --- Unity Event Functions ---

        /// <summary>무를 계산대를 찾습니다.</summary>
        void Start()
        {
            if (counter == null) counter = GetComponentInParent<ShopCounter>(true);
            if (counter == null) counter = GameContext.Resolve<ShopCounter>(this);

            if (counter == null)
            {
                GameLog.Warn(GameLog.Channel.Player,
                    "ShopReturnPoint: 계산대를 찾지 못해 무를 수 없습니다.", this);
            }
        }

        // --- IInteractable ---

        /// <summary>
        /// 담은 것이 있을 때만 무를 수 있습니다.
        ///
        /// 빈 장바구니에도 "돌려놓기"가 떠 있으면, 눌러도 아무 일이 없어
        /// 고장인지 빈 장바구니인지 구분되지 않습니다. (<see cref="ShopClerk"/> 과 같은 규칙입니다)
        /// </summary>
        /// <returns>무를 것이 있으면 true</returns>
        public bool CanInteract()
        {
            return counter != null && (counter.SelectedCount > 0 || !counter.IsOpen);
        }

        /// <summary>
        /// 조준했을 때 보여 줄 문구입니다. 무엇이 돌아가는지와 몇 개 남는지를 적습니다.
        /// </summary>
        /// <returns>"돌려놓기 — 빵 (2개 담김)" 형태의 문구</returns>
        public string GetInteractionLabel()
        {
            if (!CanInteract()) return "";

            if (!counter.IsOpen)
            {
                ShopSchedule schedule = counter.GetComponent<ShopSchedule>();
                return string.Format(closedFormat, schedule != null ? schedule.OpenTimeText : "");
            }

            string line = returnLabel;

            ShopItem next = NextToReturn();
            if (next != null) line += " — " + next.displayName;

            line += " (" + counter.SelectedCount + "개 담김";

            // 값이 얼마나 줄어드는지 함께 보여 주면 무를지 말지를 그 자리에서 정할 수 있습니다.
            if (next != null)
            {
                string price = wallet != null
                    ? wallet.Format(CurrencyType.Money, next.price)
                    : next.price.ToString();

                line += ", -" + price;
            }

            return line + ")";
        }

        /// <summary>마지막에 담은 것을 하나 되돌립니다.</summary>
        public void Interact()
        {
            if (!CanInteract()) return;
            if (!counter.IsOpen) return;

            if (!counter.ReturnLast()) return;

            if (onReturned != null) onReturned.Invoke();
        }

        // --- Private Methods ---

        /// <summary>
        /// 다음에 되돌아갈 물건의 정의를 돌려줍니다.
        /// </summary>
        /// <returns>마지막에 담은 물건. 담은 것이 없으면 null</returns>
        private ShopItem NextToReturn()
        {
            System.Collections.Generic.IReadOnlyList<ShopShelfItem> cart = counter.Selected;
            if (cart.Count == 0) return null;

            ShopShelfItem last = cart[cart.Count - 1];
            return last != null ? last.item : null;
        }
    }
}
