using UnityEngine;
using UnityEngine.Events;
using VContainer;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 계산대의 점원입니다. 말을 걸면 고른 물건의 값을 치르고 봉투를 내줍니다.
    ///
    /// <b>점원은 상태를 갖지 않습니다.</b> 무엇을 골랐고 얼마인지는 <see cref="ShopCounter"/>가
    /// 알고, 여기는 "계산해 주세요"라고 말을 거는 창구일 뿐입니다. 나중에 점원이 자리를 비우거나
    /// 여럿이 되어도 계산대에 쌓인 것은 그대로 남습니다.
    /// </summary>
    public class ShopClerk : MonoBehaviour, IInteractable
    {
        // --- Public Member Variables ---

        [Header("연동")]
        /// <summary>이 점원이 보는 계산대입니다. 비워두면 부모에서 찾고, 그래도 없으면 씬에서 찾습니다.</summary>
        [Tooltip("이 점원이 보는 계산대. 비워두면 부모에서 찾고, 그래도 없으면 씬에서 찾습니다.")]
        public ShopCounter counter;

        [Header("문구")]
        /// <summary>계산할 수 있을 때 보여 줄 동사입니다.</summary>
        [Tooltip("계산할 수 있을 때 보여 줄 동사")]
        public string checkoutLabel = "계산하기";

        /// <summary>영업 시간이 아닐 때 보여 줄 문구입니다. {0}에 여는 시각이 들어갑니다.</summary>
        [Tooltip("영업 시간이 아닐 때 보여 줄 문구. {0}에 여는 시각이 들어갑니다.")]
        public string closedFormat = "영업 시간이 아닙니다 ({0} 개점)";

        [Header("이벤트")]
        /// <summary>계산에 성공했을 때. 인사말이나 소리를 여기에 거세요.</summary>
        [Tooltip("계산에 성공했을 때. 인사말이나 소리를 거세요.")]
        public UnityEvent onServed;

        /// <summary>돈이 모자라 거절했을 때.</summary>
        [Tooltip("돈이 모자라 거절했을 때")]
        public UnityEvent onRefused;

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

        void Start()
        {
            if (counter == null) counter = GetComponentInParent<ShopCounter>(true);
            if (counter == null) counter = GameContext.Resolve<ShopCounter>(this);

            if (counter == null)
            {
                GameLog.Warn(GameLog.Channel.Player,
                    "ShopClerk: 계산대를 찾지 못해 계산할 수 없습니다.", this);
            }
        }

        // --- IInteractable ---

        /// <summary>
        /// 고른 것이 있을 때만 말을 걸 수 있습니다.
        ///
        /// 빈손으로 말을 걸 수 있게 두면 "계산하기"가 떠 있는데 눌러도 아무 일이 없어,
        /// 고장인지 빈 장바구니인지 구분되지 않습니다.
        /// </summary>
        /// <returns>계산할 것이 있으면 true</returns>
        public bool CanInteract()
        {
            return counter != null && (counter.SelectedCount > 0 || !counter.IsOpen);
        }

        /// <summary>조준했을 때 보여 줄 문구입니다. 치를 값을 함께 적습니다.</summary>
        /// <returns>"계산하기 — ₩12,300 (3개)" 형태의 문구</returns>
        public string GetInteractionLabel()
        {
            if (!CanInteract()) return "";

            // 문이 닫혔으면 계산해 줄 수 없습니다. 왜 안 되는지는 알려 줍니다.
            if (!counter.IsOpen)
            {
                ShopSchedule schedule = counter.GetComponent<ShopSchedule>();
                return string.Format(closedFormat, schedule != null ? schedule.OpenTimeText : "");
            }

            int total = counter.TotalPrice;
            string price = wallet != null
                ? wallet.Format(CurrencyType.Money, total)
                : total.ToString();

            return checkoutLabel + " — " + price + " (" + counter.SelectedCount + "개)";
        }

        /// <summary>값을 치르고 봉투를 내줍니다.</summary>
        public void Interact()
        {
            if (!CanInteract()) return;
            if (!counter.IsOpen) return;

            bool served = counter.TryCheckout();

            UnityEvent raised = served ? onServed : onRefused;
            if (raised != null) raised.Invoke();
        }
    }
}
