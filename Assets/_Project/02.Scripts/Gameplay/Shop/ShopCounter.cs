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

        // --- Injection ---

        /// <summary>값을 치를 지갑을 받습니다.</summary>
        /// <param name="playerWallet">플레이어의 지갑</param>
        [Inject]
        public void Construct(Wallet playerWallet)
        {
            wallet = playerWallet;
        }

        // --- Public Methods : 장바구니 ---

        /// <summary>이 진열품이 이미 골라져 있는지 확인합니다.</summary>
        /// <param name="shelfItem">확인할 진열품</param>
        /// <returns>골라져 있으면 true</returns>
        public bool Contains(ShopShelfItem shelfItem)
        {
            return shelfItem != null && selected.Contains(shelfItem);
        }

        /// <summary>
        /// 진열품을 고르거나 무릅니다. 이미 골랐다면 빠집니다.
        /// </summary>
        /// <param name="shelfItem">고르거나 무를 진열품</param>
        public void Toggle(ShopShelfItem shelfItem)
        {
            if (shelfItem == null || shelfItem.item == null) return;

            if (!selected.Remove(shelfItem)) selected.Add(shelfItem);

            RaiseCartChanged();
        }

        /// <summary>고른 것을 모두 무릅니다.</summary>
        public void ClearCart()
        {
            if (selected.Count == 0) return;

            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] != null) selected[i].NotifyDeselected();
            }

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
            if (bagPrefab == null)
            {
                GameLog.Warn(GameLog.Channel.Player,
                    "ShopCounter: 봉투 프리팹이 없어 산 물건을 담지 못했습니다. " +
                    "값은 치러졌으므로 프리팹을 연결하고 다시 사야 합니다.", this);
                return;
            }

            Transform spot = ResolveBagPlacement();
            GameObject go = Instantiate(bagPrefab, spot.position, spot.rotation);

            ShoppingBag bag = go.GetComponent<ShoppingBag>();
            if (bag == null)
            {
                GameLog.Error(GameLog.Channel.Player,
                    "ShopCounter: 봉투 프리팹에 ShoppingBag 이 없어 꺼낼 수 없습니다.", this);
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
