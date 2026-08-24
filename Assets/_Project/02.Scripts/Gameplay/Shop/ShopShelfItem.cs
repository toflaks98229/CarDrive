using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VContainer;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 마트 진열장의 한 칸입니다. <b>같은 물건이 여러 개 쌓여 있고</b>, 하나 담을 때마다
    /// 앞에 있는 것부터 하나씩 사라집니다.
    ///
    /// <b>왜 실물이 사라지는가.</b> 예전에는 진열장이 곧 장바구니였습니다 — 조준해 고르면
    /// 표식만 켜지고 물건은 그대로 있었고, 다시 조준하면 물러졌습니다. 담은 것을 세는 데는
    /// 성립했지만 <b>몇 개나 남았는지, 몇 개나 담았는지가 화면에 나타나지 않았습니다.</b>
    /// 지금은 진열대에서 물건이 줄어드는 것이 곧 장바구니가 차는 것입니다.
    ///
    /// <b>여기서는 무를 수 없습니다.</b> 담기와 무르기가 같은 자리에 있으면 "한 번 더
    /// 조준하면 빠지는가 하나 더 담기는가"가 모호해집니다. 무르는 일은
    /// <see cref="ShopReturnPoint"/> 가 계산대에서 맡습니다.
    ///
    /// 앞에서부터 하나씩 내주는 규칙은 <see cref="BeverageBox"/> 와 같습니다.
    /// </summary>
    public class ShopShelfItem : MonoBehaviour, IInteractable
    {
        // --- Public Member Variables ---

        [Header("상품")]
        /// <summary>이 칸에 놓인 물건의 정의입니다.</summary>
        [Tooltip("이 칸에 놓인 물건의 정의")]
        public ShopItem item;

        [Header("연동")]
        /// <summary>값을 올릴 계산대입니다. 비워두면 부모에서 찾고, 그래도 없으면 씬에서 찾습니다.</summary>
        [Tooltip("값을 올릴 계산대. 비워두면 부모에서 찾고, 그래도 없으면 씬에서 찾습니다.")]
        public ShopCounter counter;

        [Header("진열")]
        /// <summary>
        /// 진열된 실물들입니다. <b>앞(0번)부터 사라집니다.</b>
        ///
        /// 비워 두면 <c>Awake</c> 에서 <b>직계 자식</b>을 순서대로 채웁니다.
        /// 손으로 채우면 그 순서가 사라지는 순서가 됩니다.
        /// </summary>
        [Tooltip("진열된 실물들. 앞(0번)부터 사라집니다. 비워 두면 직계 자식을 순서대로 채웁니다.")]
        public List<GameObject> displays = new List<GameObject>();

        [Header("문구")]
        /// <summary>담을 때 보여 줄 동사입니다.</summary>
        [Tooltip("담을 때 보여 줄 동사")]
        public string pickLabel = "담기";

        /// <summary>남은 수를 적을 형식입니다. {0}에 남은 개수가 들어갑니다.</summary>
        [Tooltip("남은 수를 적을 형식. {0}에 남은 개수가 들어갑니다. 비우면 적지 않습니다.")]
        public string remainFormat = "({0}개 남음)";

        /// <summary>다 팔렸을 때 보여 줄 문구입니다.</summary>
        [Tooltip("다 팔렸을 때 보여 줄 문구")]
        public string soldOutLabel = "다 팔렸습니다";

        /// <summary>영업 시간이 아닐 때 보여 줄 문구입니다. {0}에 여는 시각이 들어갑니다.</summary>
        [Tooltip("영업 시간이 아닐 때 보여 줄 문구. {0}에 여는 시각이 들어갑니다.")]
        public string closedFormat = "영업 시간이 아닙니다 ({0} 개점)";

        [Header("이벤트")]
        /// <summary>하나 담았을 때. Feel 의 MMF_Player 를 연결하세요.</summary>
        [Tooltip("하나 담았을 때. Feel 의 MMF_Player 를 연결하세요.")]
        public UnityEvent onPicked;

        /// <summary>하나 되돌려 놓았을 때.</summary>
        [Tooltip("하나 되돌려 놓았을 때")]
        public UnityEvent onReturned;

        /// <summary>마지막 하나까지 담겨 진열이 비었을 때.</summary>
        [Tooltip("마지막 하나까지 담겨 진열이 비었을 때")]
        public UnityEvent onEmptied;

        // --- Public Properties ---

        /// <summary>아직 진열대에 남아 있는 수입니다.</summary>
        public int RemainingCount { get { return Mathf.Max(0, displays.Count - taken.Count); } }

        /// <summary>이 칸에서 지금까지 담은 수입니다.</summary>
        public int TakenCount { get { return taken.Count; } }

        // --- Private Member Variables ---

        /// <summary>값을 사람이 읽는 꼴로 적기 위한 지갑입니다. 없으면 숫자만 적습니다.</summary>
        private Wallet wallet;

        /// <summary>
        /// 담아 간 물건들입니다. <b>담은 차례 그대로</b>라 되돌릴 때는 끝에서부터 꺼냅니다.
        ///
        /// <b>수만 세지 않는 이유가 있습니다.</b> 손님이 조준한 물건이 담기므로
        /// 반드시 앞에서부터 사라지지 않습니다. 세 번째 것을 먼저 집을 수 있고,
        /// 그러면 "앞에서 몇 개" 라는 셈으로는 어느 것이 꺼져 있는지 알 수 없습니다.
        /// </summary>
        private readonly List<GameObject> taken = new List<GameObject>();

        // --- Injection ---

        /// <summary>값 표기에 쓸 지갑을 받습니다.</summary>
        /// <param name="playerWallet">플레이어의 지갑</param>
        [Inject]
        public void Construct(Wallet playerWallet)
        {
            wallet = playerWallet;
        }

        // --- Unity Event Functions ---

        /// <summary>진열 목록이 비어 있으면 직계 자식으로 채웁니다.</summary>
        void Awake()
        {
            EnsureDisplays();
        }

        /// <summary>협력자를 찾고 배선을 확인합니다.</summary>
        void Start()
        {
            if (counter == null) counter = GetComponentInParent<ShopCounter>(true);
            if (counter == null) counter = GameContext.Resolve<ShopCounter>(this);

            if (counter != null) counter.RegisterShelf(this);
            else
            {
                GameLog.Warn(GameLog.Channel.Player,
                    "ShopShelfItem: 계산대를 찾지 못해 " + name + " 을(를) 담을 수 없습니다.", this);
            }

            if (item == null)
            {
                GameLog.Warn(GameLog.Channel.Player,
                    "ShopShelfItem: " + name + " 에 상품이 지정되지 않았습니다.", this);
            }

            if (displays.Count == 0)
            {
                GameLog.Warn(GameLog.Channel.Player,
                    "ShopShelfItem: " + name + " 에 진열된 실물이 없어 담을 것이 없습니다. " +
                    "자식으로 물건을 놓거나 displays 를 채우세요.", this);
            }
        }

        // --- IInteractable ---

        /// <summary>
        /// 계산대와 상품이 갖춰져 있으면 <b>안내할 말이 있습니다.</b>
        ///
        /// <b>왜 재고와 영업 시간을 여기서 막지 않는가.</b> 막으면 조준해도 아무 문구가
        /// 뜨지 않아 <b>왜 안 되는지</b>를 알 수 없습니다. 다 팔렸는지 문이 닫혔는지
        /// 아니면 고장인지 구분되지 않습니다. <see cref="VehicleDoorInteractable"/> 이
        /// "속도를 줄이세요"를 띄우는 것과 같은 규칙입니다 — <b>거절도 안내합니다.</b>
        /// 실제로 막는 것은 <see cref="Interact"/> 입니다.
        /// </summary>
        /// <returns>안내할 것이 있으면 true</returns>
        public bool CanInteract()
        {
            return counter != null && item != null;
        }

        /// <summary>
        /// 조준했을 때 보여 줄 문구입니다. 담을 수 없다면 <b>그 이유</b>를 적습니다.
        /// </summary>
        /// <returns>"담기 — 빵 ₩1,200 (3개 남음)" 또는 거절 사유</returns>
        public string GetInteractionLabel()
        {
            if (!CanInteract()) return "";

            if (!counter.IsOpen) return string.Format(closedFormat, OpenTimeText());
            if (RemainingCount <= 0) return soldOutLabel;

            string line = pickLabel + " — " + item.ToLine(FormatPrice());

            if (!string.IsNullOrEmpty(remainFormat))
            {
                line += " " + string.Format(remainFormat, RemainingCount);
            }

            return line;
        }

        /// <summary>
        /// 앞에 있는 것을 하나 담습니다. 칸 자체를 조준했을 때의 경로입니다.
        /// 물건 하나하나는 <see cref="ShopDisplayItem"/> 이 <see cref="Take"/> 로 지목합니다.
        /// </summary>
        public void Interact()
        {
            Take(FirstAvailable());
        }

        // --- Public Methods ---

        /// <summary>
        /// <b>지목한 물건</b>을 담습니다. 조준한 것이 담기도록 <see cref="ShopDisplayItem"/> 이 부릅니다.
        /// </summary>
        /// <param name="display">담을 실물. 이 칸의 것이 아니거나 이미 담겼으면 아무 일도 하지 않습니다.</param>
        /// <returns>담았으면 true</returns>
        public bool Take(GameObject display)
        {
            if (!CanInteract()) return false;
            if (!counter.IsOpen) return false;

            if (display == null || !displays.Contains(display)) return false;
            if (taken.Contains(display)) return false;

            // 진열에서 먼저 치우고, 그것이 성공했을 때만 장바구니에 올립니다.
            // 순서를 뒤집으면 진열은 그대로인데 값만 오르는 상태가 생길 수 있습니다.
            display.SetActive(false);
            taken.Add(display);

            counter.Add(this);

            if (onPicked != null) onPicked.Invoke();

            if (RemainingCount == 0 && onEmptied != null) onEmptied.Invoke();
            return true;
        }

        // --- Public Methods ---

        /// <summary>
        /// 진열대를 처음 상태로 되돌립니다. 입고할 때 <see cref="ShopCounter"/> 가 부릅니다.
        ///
        /// <b>담아 간 수를 0으로 되돌리고 전부 다시 켭니다.</b> 장바구니를 비우는 일은
        /// 계산대가 먼저 하므로 여기서는 진열만 봅니다.
        /// </summary>
        /// <returns>다시 채워진 수</returns>
        public int Restock()
        {
            taken.Clear();

            for (int i = 0; i < displays.Count; i++)
            {
                if (displays[i] != null) displays[i].SetActive(true);
            }

            return displays.Count;
        }

        /// <summary>
        /// 담아 갔던 것을 하나 진열대에 되돌립니다. <see cref="ShopCounter"/> 가 부릅니다.
        /// </summary>
        /// <returns>되돌렸으면 true. 담아 간 것이 없으면 false</returns>
        public bool ReturnOne()
        {
            if (taken.Count == 0) return false;

            // 마지막에 담은 것부터 돌아갑니다. 방금 한 일을 무르는 것이 가장 자연스럽습니다.
            int last = taken.Count - 1;
            GameObject display = taken[last];
            taken.RemoveAt(last);

            if (display != null) display.SetActive(true);

            if (onReturned != null) onReturned.Invoke();
            return true;
        }

        // --- Private Methods ---

        /// <summary>
        /// 아직 담기지 않은 것 중 <b>맨 앞</b>의 실물을 돌려줍니다.
        /// 칸 자체를 조준했을 때 무엇을 담을지 정하는 데 씁니다.
        /// </summary>
        /// <returns>담을 수 있는 첫 실물. 남은 것이 없으면 null</returns>
        private GameObject FirstAvailable()
        {
            for (int i = 0; i < displays.Count; i++)
            {
                GameObject display = displays[i];
                if (display == null) continue;
                if (taken.Contains(display)) continue;

                return display;
            }

            return null;
        }

        /// <summary>
        /// 진열 목록이 비어 있으면 직계 자식으로 채웁니다.
        ///
        /// <see cref="BeverageBox"/> 가 자식에서 병을 찾는 것과 같은 방식입니다 —
        /// 씬에 물건을 늘어놓는 것만으로 재고가 되도록 하기 위해서입니다.
        /// </summary>
        private void EnsureDisplays()
        {
            if (displays.Count > 0) return;

            foreach (Transform child in transform)
            {
                displays.Add(child.gameObject);
            }
        }

        /// <summary>등록을 해제합니다.</summary>
        void OnDestroy()
        {
            if (counter != null) counter.UnregisterShelf(this);
        }

        /// <summary>
        /// 문을 여는 시각을 적습니다. 일정표가 없으면 빈 문자열입니다.
        /// </summary>
        /// <returns>"08:00" 꼴의 시각</returns>
        private string OpenTimeText()
        {
            ShopSchedule schedule = counter != null ? counter.GetComponent<ShopSchedule>() : null;
            return schedule != null ? schedule.OpenTimeText : "";
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
