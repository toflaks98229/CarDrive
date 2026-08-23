using UnityEngine;
using UnityEngine.Events;
using VContainer;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 마트 진열장에 놓인 상품 하나입니다. 조준해 고르면 계산대의 값이 올라갑니다.
    ///
    /// <b>고르는 것과 사는 것은 다릅니다.</b> 여기서는 실물이 움직이지 않습니다 —
    /// 진열장의 물건은 그대로 있고, <see cref="ShopCounter"/>에 "이것을 살 것"이라고
    /// 표시만 됩니다. 실물이 생기는 것은 계산을 마치고 봉투에서 꺼낼 때입니다.
    ///
    /// 다시 고르면 무릅니다. 장바구니를 여닫는 UI 없이 <b>진열장 자체가 장바구니</b>가 되도록
    /// 한 것이라, 잘못 담았을 때 되돌리는 길이 담을 때와 같습니다.
    /// </summary>
    public class ShopShelfItem : MonoBehaviour, IInteractable
    {
        // --- Public Member Variables ---

        [Header("상품")]
        /// <summary>이 자리에 놓인 물건의 정의입니다.</summary>
        [Tooltip("이 자리에 놓인 물건의 정의")]
        public ShopItem item;

        [Header("연동")]
        /// <summary>값을 올릴 계산대입니다. 비워두면 부모에서 찾고, 그래도 없으면 씬에서 찾습니다.</summary>
        [Tooltip("값을 올릴 계산대. 비워두면 부모에서 찾고, 그래도 없으면 씬에서 찾습니다.")]
        public ShopCounter counter;

        [Header("문구")]
        /// <summary>고르지 않았을 때 보여 줄 동사입니다.</summary>
        [Tooltip("고르지 않았을 때 보여 줄 동사")]
        public string pickLabel = "담기";

        /// <summary>이미 골랐을 때 보여 줄 동사입니다.</summary>
        [Tooltip("이미 골랐을 때 보여 줄 동사")]
        public string dropLabel = "빼기";

        [Header("고른 표시")]
        /// <summary>골랐을 때 켤 표식입니다. 비워두면 아무것도 켜지 않습니다.</summary>
        [Tooltip("골랐을 때 켤 표식. 비워두면 아무것도 켜지 않습니다.")]
        public GameObject selectedMarker;

        [Header("이벤트")]
        /// <summary>골랐을 때. Feel 의 MMF_Player 를 연결하세요.</summary>
        [Tooltip("골랐을 때. Feel 의 MMF_Player 를 연결하세요.")]
        public UnityEvent onSelected;

        /// <summary>물렀을 때.</summary>
        [Tooltip("물렀을 때")]
        public UnityEvent onDeselected;

        // --- Public Properties ---

        /// <summary>지금 골라져 있는지 여부입니다.</summary>
        public bool IsSelected { get { return counter != null && counter.Contains(this); } }

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
            if (counter == null) counter = FindAnyObjectByType<ShopCounter>(FindObjectsInactive.Include);

            if (counter == null)
            {
                GameLog.Warn(GameLog.Channel.Player,
                    "ShopShelfItem: 계산대를 찾지 못해 " + name + " 을(를) 고를 수 없습니다.", this);
            }

            if (item == null)
            {
                GameLog.Warn(GameLog.Channel.Player,
                    "ShopShelfItem: " + name + " 에 상품이 지정되지 않았습니다.", this);
            }

            ApplyMarker(false);
        }

        // --- IInteractable ---

        /// <summary>계산대와 상품이 모두 갖춰졌을 때만 고를 수 있습니다.</summary>
        /// <returns>고르거나 무를 수 있으면 true</returns>
        public bool CanInteract()
        {
            return counter != null && item != null;
        }

        /// <summary>
        /// 조준했을 때 보여 줄 문구입니다. 이름과 값을 함께 보여 주어
        /// 진열장 앞에서 값을 확인할 수 있게 합니다.
        /// </summary>
        /// <returns>"담기 — 소시지 ₩1,200" 형태의 문구</returns>
        public string GetInteractionLabel()
        {
            if (!CanInteract()) return "";

            string verb = IsSelected ? dropLabel : pickLabel;
            return verb + " — " + item.ToLine(FormatPrice());
        }

        /// <summary>고르거나 무릅니다.</summary>
        public void Interact()
        {
            if (!CanInteract()) return;

            counter.Toggle(this);

            bool nowSelected = IsSelected;
            ApplyMarker(nowSelected);

            UnityEvent raised = nowSelected ? onSelected : onDeselected;
            if (raised != null) raised.Invoke();
        }

        // --- Public Methods ---

        /// <summary>
        /// 계산대가 장바구니를 비웠음을 알려 옵니다. 표식을 되돌립니다.
        ///
        /// <b>계산대가 부릅니다.</b> 계산을 마치면 고른 것이 한꺼번에 빠지는데, 그때
        /// 진열품이 스스로 알 방법이 없기 때문입니다.
        /// </summary>
        public void NotifyDeselected()
        {
            ApplyMarker(false);
            if (onDeselected != null) onDeselected.Invoke();
        }

        // --- Private Methods ---

        /// <summary>고른 표식을 켜거나 끕니다.</summary>
        /// <param name="on">켤지 여부</param>
        private void ApplyMarker(bool on)
        {
            if (selectedMarker != null) selectedMarker.SetActive(on);
        }

        /// <summary>값을 사람이 읽는 꼴로 적습니다. 지갑이 없으면 숫자만 적습니다.</summary>
        /// <returns>표기된 값</returns>
        private string FormatPrice()
        {
            if (wallet == null) return item.price.ToString();
            return wallet.Format(CurrencyType.Money, item.price);
        }
    }
}
