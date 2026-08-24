using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VContainer;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 마트의 계산대입니다. <b>고른 물건과 값</b>을 소유합니다.
    ///
    /// 흐름은 셋으로 나뉘고, 각자 주인이 다릅니다.
    ///  1. <see cref="ShopShelfItem"/> — 진열장에서 고르고 무르기
    ///  2. <b>여기</b> — 무엇이 골라졌고 얼마인지
    ///  3. <see cref="ShopClerk"/> — 계산해 달라고 말 걸기
    ///
    /// <b>왜 점원이 아니라 계산대가 장바구니를 갖는가.</b> 점원은 자리를 비울 수 있고
    /// 나중에 여럿이 될 수도 있지만, 고른 물건은 <b>그 계산대에 쌓인 것</b>입니다.
    /// 가격표를 그리는 UI 도 사람이 아니라 계산대를 봅니다.
    /// </summary>
    public class ShopCounter : MonoBehaviour
    {
        // --- Public Member Variables ---

        [Header("놓을 자리")]
        /// <summary>계산을 마친 봉투가 놓일 자리입니다. 비워두면 계산대 위쪽을 씁니다.</summary>
        [Tooltip("계산을 마친 봉투가 놓일 자리. 비워두면 계산대 바로 위를 씁니다.")]
        public Transform bagPlacement;

        /// <summary>
        /// 봉투에 들어가지 않는 큰 물건이 놓일 자리입니다. 비워두면 봉투 자리를 씁니다.
        /// </summary>
        [Tooltip("맥주 상자처럼 봉투에 안 들어가는 것이 놓일 자리. 비워두면 봉투 자리를 씁니다.")]
        public Transform bulkyPlacement;

        [Header("봉투")]
        /// <summary>계산을 마쳤을 때 만들어질 봉투 프리팹입니다. <see cref="ShoppingBag"/>이 붙어 있어야 합니다.</summary>
        [Tooltip("계산을 마쳤을 때 만들어질 봉투 프리팹. ShoppingBag 이 붙어 있어야 합니다.")]
        public GameObject bagPrefab;

        /// <summary>
        /// 큰 물건을 놓을 때 서로 겹치지 않게 벌리는 간격(m)입니다.
        /// </summary>
        [Tooltip("큰 물건 여러 개를 놓을 때 벌리는 간격(m)")]
        public float bulkySpacing = 0.6f;

        [Header("이벤트")]
        /// <summary>고른 물건이 바뀔 때마다 발생합니다. 가격표가 이것을 듣습니다.</summary>
        [Tooltip("고른 물건이 바뀔 때마다. 가격표 UI 가 이것을 듣습니다.")]
        public UnityEvent onCartChanged;

        /// <summary>계산에 성공했을 때 발생합니다.</summary>
        [Tooltip("계산에 성공했을 때")]
        public UnityEvent onPurchased;

        /// <summary>돈이 모자라 계산이 거절되었을 때 발생합니다.</summary>
        [Tooltip("돈이 모자라 거절되었을 때")]
        public UnityEvent onRefused;

        // --- Public Properties ---

        /// <summary>지금 고른 물건의 수입니다.</summary>
        public int SelectedCount { get { Prune(); return selected.Count; } }

        /// <summary>지금 고른 물건들의 값을 모두 더한 값입니다.</summary>
        public int TotalPrice
        {
            get
            {
                Prune();

                int sum = 0;
                for (int i = 0; i < selected.Count; i++)
                {
                    ShopItem item = selected[i].item;
                    if (item != null) sum += item.price;
                }
                return sum;
            }
        }

        /// <summary>지금 고른 물건들입니다. 고른 순서 그대로입니다.</summary>
        public IReadOnlyList<ShopShelfItem> Selected { get { Prune(); return selected; } }

        /// <summary>
        /// 지금 영업 중인지 여부입니다.
        ///
        /// <b>기본값이 열림입니다.</b> <see cref="ShopSchedule"/> 을 붙이지 않은 씬에서는
        /// 마트가 늘 열려 있어야 예전처럼 돌아갑니다. 시간을 붙이는 것은 선택이지
        /// 전제가 아닙니다.
        /// </summary>
        public bool IsOpen { get { return isOpen; } }

        /// <summary>이 계산대에 딸린 진열 칸들입니다. 입고할 때 훑습니다.</summary>
        public IReadOnlyList<ShopShelfItem> Shelves { get { return shelves; } }

        // --- Private Member Variables ---

        /// <summary>
        /// 고른 진열품들입니다. <b>순서를 지킵니다.</b>
        ///
        /// 봉투에서 꺼낼 때 "가장 마지막에 산 것부터" 나와야 하므로, 담긴 차례가 곧 규칙입니다.
        /// 진열품(<see cref="ShopShelfItem"/>)을 담아 두는 이유는 계산을 마친 뒤
        /// <b>그것들의 고른 표시를 되돌려야</b> 하기 때문입니다. 값과 프리팹만 필요했다면
        /// <see cref="ShopItem"/>만 담아도 됐습니다.
        /// </summary>
        private readonly List<ShopShelfItem> selected = new List<ShopShelfItem>();

        /// <summary>값을 치를 지갑입니다. 주입되지 않으면 계산이 되지 않습니다.</summary>
        private Wallet wallet;

        /// <summary>
        /// 이 계산대에 딸린 진열 칸들입니다. 진열 칸이 <c>Start</c> 에서 스스로 등록합니다.
        ///
        /// 등록을 받는 이유는 입고 때문입니다 — 채울 대상을 씬에서 찾지 않으려면
        /// 누가 내 것인지 알고 있어야 합니다.
        /// </summary>
        private readonly List<ShopShelfItem> shelves = new List<ShopShelfItem>();

        /// <summary>지금 영업 중인지입니다. 일정표가 없으면 늘 열려 있습니다.</summary>
        private bool isOpen = true;

        // --- Injection ---

        /// <summary>값을 치를 지갑을 받습니다.</summary>
        /// <param name="playerWallet">플레이어의 지갑</param>
        [Inject]
        public void Construct(Wallet playerWallet)
        {
            wallet = playerWallet;
        }

        // --- Unity Event Functions ---

        /// <summary>
        /// 자신을 레지스트리에 등록합니다. 진열 칸·점원·가격표가 <c>Start</c> 에서 찾아 씁니다.
        ///
        /// 예전에는 그 셋이 각자 <c>FindAnyObjectByType</c> 으로 씬을 훑었습니다.
        /// 진열 칸이 수십 개면 시작 프레임에 그만큼 씬을 훑습니다.
        /// </summary>
        void Awake()
        {
            GameContext.Register(this);
        }

        /// <summary>등록을 해제합니다.</summary>
        void OnDestroy()
        {
            GameContext.Unregister(this);
        }

        // --- Public Methods : 장바구니 ---

        // --- Public Methods : 영업과 진열 ---

        /// <summary>
        /// 문을 열거나 닫습니다. <see cref="ShopSchedule"/> 이 시각을 보고 부릅니다.
        ///
        /// <b>왜 계산대가 상태를 갖는가.</b> 문이 열렸는지 물어야 하는 쪽이 넷입니다 —
        /// 진열 칸·점원·무르는 자리, 그리고 가격표. 그 넷이 각자 일정표를 찾아가면
        /// 배선이 넷으로 늘어납니다. 계산대는 이미 그 넷이 모두 알고 있는 곳입니다.
        /// </summary>
        /// <param name="open">열려 있어야 하면 true</param>
        internal void SetOpen(bool open)
        {
            isOpen = open;
        }

        /// <summary>
        /// 진열 칸을 등록합니다. 진열 칸이 <c>Start</c> 에서 스스로 부릅니다.
        /// </summary>
        /// <param name="shelfItem">등록할 진열 칸</param>
        public void RegisterShelf(ShopShelfItem shelfItem)
        {
            if (shelfItem == null || shelves.Contains(shelfItem)) return;
            shelves.Add(shelfItem);
        }

        /// <summary>
        /// 진열 칸의 등록을 해제합니다.
        /// </summary>
        /// <param name="shelfItem">해제할 진열 칸</param>
        public void UnregisterShelf(ShopShelfItem shelfItem)
        {
            if (shelfItem == null) return;
            shelves.Remove(shelfItem);
        }

        /// <summary>
        /// 모든 진열 칸을 처음 상태로 채웁니다.
        ///
        /// <b>담아 둔 것을 먼저 되돌립니다.</b> 그러지 않으면 진열대는 가득 찼는데
        /// 장바구니에는 어제 담은 것이 남아, 실물과 값이 어긋납니다.
        /// </summary>
        /// <returns>다시 채운 진열 칸의 수</returns>
        public int RestockAll()
        {
            ReturnAll();

            int count = 0;
            for (int i = shelves.Count - 1; i >= 0; i--)
            {
                if (shelves[i] == null) { shelves.RemoveAt(i); continue; }

                shelves[i].Restock();
                count++;
            }

            return count;
        }

        // --- Public Methods : 장바구니 ---

        /// <summary>이 진열 칸에서 담아 온 것이 있는지 확인합니다.</summary>
        /// <param name="shelfItem">확인할 진열 칸</param>
        /// <returns>하나라도 담겨 있으면 true</returns>
        public bool Contains(ShopShelfItem shelfItem)
        {
            return shelfItem != null && selected.Contains(shelfItem);
        }

        /// <summary>
        /// 진열 칸에서 하나를 담습니다.
        ///
        /// <b>같은 칸을 여러 번 담을 수 있습니다.</b> 목록의 항목 하나가 <b>물건 한 개</b>입니다.
        /// 진열대에서 실물이 하나 사라지는 것과 짝을 이룹니다.
        /// (실물을 감추는 일은 <see cref="ShopShelfItem.Interact"/> 가 먼저 합니다)
        /// </summary>
        /// <param name="shelfItem">담아 온 진열 칸</param>
        public void Add(ShopShelfItem shelfItem)
        {
            if (shelfItem == null || shelfItem.item == null) return;

            selected.Add(shelfItem);
            RaiseCartChanged();
        }

        /// <summary>
        /// <b>가장 마지막에 담은 것</b>을 하나 진열대로 되돌립니다.
        ///
        /// 마지막부터 되돌리는 것은 봉투에서 꺼내는 순서와 같은 규칙입니다 —
        /// 방금 한 일을 무르는 것이 가장 자연스럽습니다.
        /// </summary>
        /// <returns>되돌렸으면 true. 담은 것이 없으면 false</returns>
        public bool ReturnLast()
        {
            Prune();

            if (selected.Count == 0) return false;

            int last = selected.Count - 1;
            ShopShelfItem shelfItem = selected[last];
            selected.RemoveAt(last);

            // 진열대가 사라졌어도 장바구니에서는 빠져야 합니다. 값이 남아 있으면
            // 되돌릴 수 없는 물건 값을 계속 치르게 됩니다.
            if (shelfItem != null) shelfItem.ReturnOne();

            RaiseCartChanged();
            return true;
        }

        /// <summary>
        /// 담은 것을 <b>모두</b> 진열대로 되돌립니다.
        /// </summary>
        /// <returns>되돌린 개수</returns>
        public int ReturnAll()
        {
            int count = 0;
            while (ReturnLast()) count++;
            return count;
        }

        /// <summary>
        /// 장바구니를 비웁니다. <b>진열대로 되돌리지 않습니다.</b>
        ///
        /// 계산을 마친 뒤에 부르는 것이라, 담겼던 물건은 <b>팔린 것</b>입니다.
        /// 무르려는 것이라면 <see cref="ReturnAll"/> 를 쓰세요.
        /// </summary>
        public void ClearCart()
        {
            if (selected.Count == 0) return;

            selected.Clear();
            RaiseCartChanged();
        }

        // --- Public Methods : 계산 ---

        /// <summary>
        /// 고른 물건들의 값을 치르고, 봉투와 큰 물건을 계산대에 올립니다.
        /// </summary>
        /// <returns>계산에 성공했으면 true. 고른 것이 없거나 돈이 모자라면 false</returns>
        public bool TryCheckout()
        {
            Prune();

            if (selected.Count == 0) return false;

            int total = TotalPrice;

            if (wallet == null)
            {
                GameLog.Warn(GameLog.Channel.Player,
                    "ShopCounter: 지갑이 주입되지 않아 계산할 수 없습니다.", this);
                return false;
            }

            // <b>내줄 수 있는지를 값을 받기 전에 확인합니다.</b>
            //
            // 예전에는 순서가 반대였습니다. TrySpend 로 돈을 먼저 빼고 그 다음에 봉투를
            // 만들다가 실패하면 경고만 남겼습니다. 그러면 <b>돈은 나갔는데 물건은 없는</b>
            // 상태가 되고, 세이브에 소지품이 없어 불러오기로도 되돌릴 수 없습니다.
            // (당시 주석이 "값은 치러졌으므로 다시 사야 합니다" 라고 그 결과를 인정하고 있었습니다)
            //
            // 확인 대상은 <b>계산대 자신의 배선</b>뿐입니다. 개별 상품의 프리팹이 비어 있는 것은
            // <see cref="ShopItem.prefab"/> 이 "값만 있는 물건" 용도로 허용하는 상태이므로
            // 여기서 막지 않습니다. 그쪽은 꺼낼 때 경고가 남습니다.
            string blocker;
            if (!CanFulfill(out blocker))
            {
                GameLog.Error(GameLog.Channel.Player,
                    "ShopCounter: " + blocker + " 계산을 진행하지 않았습니다. 돈은 그대로입니다.", this);

                if (onRefused != null) onRefused.Invoke();
                return false;
            }

            if (!wallet.TrySpend(CurrencyType.Money, total))
            {
                // 지갑이 이미 onInsufficientFunds 를 냅니다. 여기서는 계산대 쪽 연출만 겁니다.
                if (onRefused != null) onRefused.Invoke();
                return false;
            }

            PackAndPlace();

            ClearCart();

            if (onPurchased != null) onPurchased.Invoke();
            return true;
        }

        // --- Private Methods ---

        /// <summary>
        /// 지금 고른 것을 <b>실제로 내줄 수 있는지</b> 확인합니다. 값을 받기 전에 부릅니다.
        ///
        /// 봉투가 필요 없는 장바구니(큰 물건만 고른 경우)라면 봉투 배선을 보지 않습니다.
        /// 없는 것을 이유로 거절하면 살 수 있는 것도 못 사게 되기 때문입니다.
        /// </summary>
        /// <param name="blocker">막힌 이유가 담깁니다. 내줄 수 있으면 null입니다.</param>
        /// <returns>내줄 수 있으면 true</returns>
        private bool CanFulfill(out string blocker)
        {
            blocker = null;

            // 봉투에 담길 것이 하나라도 있어야 봉투 배선이 문제가 됩니다.
            bool needsBag = false;
            for (int i = 0; i < selected.Count; i++)
            {
                ShopItem item = selected[i] != null ? selected[i].item : null;
                if (item != null && item.fitsInBag) { needsBag = true; break; }
            }

            if (!needsBag) return true;

            if (bagPrefab == null)
            {
                blocker = "봉투 프리팹이 연결되지 않아 담아 줄 봉투가 없습니다.";
                return false;
            }

            // 프리팹은 인스턴스를 만들지 않고도 컴포넌트를 확인할 수 있습니다.
            // 만들어 보고 되돌리는 방식은 Awake·OnEnable 이 한 번 헛돌기 때문에 쓰지 않습니다.
            if (bagPrefab.GetComponent<ShoppingBag>() == null)
            {
                blocker = "봉투 프리팹에 ShoppingBag 이 없어 담아도 꺼낼 수 없습니다.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 산 것을 봉투와 계산대 위로 나눠 놓습니다.
        ///
        /// 봉투에 들어가는 것은 <b>데이터로</b> 봉투에 담기고, 큰 것은 <b>실물로</b> 그 자리에 놓입니다.
        /// </summary>
        private void PackAndPlace()
        {
            List<ShopItem> packable = new List<ShopItem>();
            int bulkyIndex = 0;

            for (int i = 0; i < selected.Count; i++)
            {
                ShopItem item = selected[i] != null ? selected[i].item : null;
                if (item == null) continue;

                if (item.fitsInBag)
                {
                    packable.Add(item);
                }
                else
                {
                    PlaceBulky(item, bulkyIndex);
                    bulkyIndex++;
                }
            }

            if (packable.Count > 0) CreateBag(packable);
        }

        /// <summary>
        /// 담은 것들을 넣은 봉투를 계산대 위에 올립니다.
        /// </summary>
        /// <param name="contents">봉투에 넣을 물건들. 고른 순서 그대로입니다.</param>
        private void CreateBag(List<ShopItem> contents)
        {
            // 여기까지 왔다면 <see cref="CanFulfill"/> 이 봉투 배선을 이미 확인했습니다.
            // 아래 검사가 걸린다면 그 사이에 프리팹이 바뀌었다는 뜻이므로 오류로 남깁니다.
            Transform spot = ResolveBagPlacement();
            GameObject go = Instantiate(bagPrefab, spot.position, spot.rotation);

            ShoppingBag bag = go.GetComponent<ShoppingBag>();
            if (bag == null)
            {
                GameLog.Error(GameLog.Channel.Player,
                    "ShopCounter: 봉투 프리팹에 ShoppingBag 이 없어 꺼낼 수 없습니다. " +
                    "계산 직전 확인을 통과했는데 여기서 걸렸다면 그 사이에 프리팹이 바뀐 것입니다.", this);

                Destroy(go);
                return;
            }

            bag.Fill(contents);
        }

        /// <summary>
        /// 봉투에 들어가지 않는 물건을 계산대 옆에 그대로 놓습니다.
        /// </summary>
        /// <param name="item">놓을 물건</param>
        /// <param name="index">몇 번째 큰 물건인지. 서로 겹치지 않게 벌리는 데 씁니다.</param>
        private void PlaceBulky(ShopItem item, int index)
        {
            if (item.prefab == null)
            {
                GameLog.Warn(GameLog.Channel.Player,
                    "ShopCounter: " + item.displayName + " 에 프리팹이 없어 놓지 못했습니다.", this);
                return;
            }

            Transform spot = bulkyPlacement != null ? bulkyPlacement : ResolveBagPlacement();
            Vector3 offset = spot.right * (index * bulkySpacing);

            Instantiate(item.prefab, spot.position + offset, spot.rotation);
        }

        /// <summary>봉투를 놓을 자리를 정합니다. 지정하지 않았으면 계산대 바로 위입니다.</summary>
        /// <returns>봉투를 놓을 기준</returns>
        private Transform ResolveBagPlacement()
        {
            return bagPlacement != null ? bagPlacement : transform;
        }

        /// <summary>고른 것이 바뀌었음을 알립니다.</summary>
        private void RaiseCartChanged()
        {
            if (onCartChanged != null) onCartChanged.Invoke();
        }

        /// <summary>사라진 진열품을 목록에서 걷어 냅니다.</summary>
        private void Prune()
        {
            for (int i = selected.Count - 1; i >= 0; i--)
            {
                if (selected[i] == null) selected.RemoveAt(i);
            }
        }
    }
}
