using System.Text;
using TMPro;
using UnityEngine;
using VContainer;
using CarDrive.Common;
using CarDrive.Gameplay;
using CarDrive.Systems;

namespace CarDrive.UI
{
    /// <summary>
    /// 계산대의 가격표입니다. 고른 물건이 늘어날 때마다 값이 올라갑니다.
    ///
    /// <b>이 컴포넌트는 상태를 갖지 않습니다.</b> 무엇이 골라졌는지는 <see cref="ShopCounter"/>가
    /// 알고 있고, 여기서는 그것을 읽어 글자로 옮길 뿐입니다. UI 층의 규칙 그대로입니다.
    ///
    /// <b>매 프레임 다시 그리지 않습니다.</b> 값은 물건을 고를 때만 바뀌므로, 계산대가 내는
    /// 신호를 듣고 그때만 고쳐 씁니다. 진열장 앞에 서 있는 내내 문자열을 새로 만들 이유가 없습니다.
    /// </summary>
    public class ShopPriceTagUI : MonoBehaviour
    {
        // --- Public Member Variables ---

        [Header("연동")]
        /// <summary>값을 읽어 올 계산대입니다. 비워두면 부모에서 찾고, 그래도 없으면 씬에서 찾습니다.</summary>
        [Tooltip("값을 읽어 올 계산대. 비워두면 부모에서 찾고, 그래도 없으면 씬에서 찾습니다.")]
        public ShopCounter counter;

        [Header("표시")]
        /// <summary>합계를 적을 글자입니다.</summary>
        [Tooltip("합계를 적을 글자")]
        public TMP_Text totalText;

        /// <summary>고른 물건을 줄줄이 적을 글자입니다. 비워두면 합계만 보여 줍니다.</summary>
        [Tooltip("고른 물건을 줄줄이 적을 글자. 비워두면 합계만 보여 줍니다.")]
        public TMP_Text itemListText;

        [Header("문구")]
        /// <summary>아무것도 고르지 않았을 때 보여 줄 글자입니다.</summary>
        [Tooltip("아무것도 고르지 않았을 때 보여 줄 글자")]
        public string emptyLabel = "고른 물건 없음";

        /// <summary>줄 수가 이보다 많으면 나머지를 "…외 N개"로 줄입니다.</summary>
        [Tooltip("줄 수가 이보다 많으면 나머지를 '…외 N개'로 줄입니다.")]
        public int maxLines = 6;

        // --- Private Member Variables ---

        /// <summary>값 표기 규칙을 가져올 지갑입니다. 없으면 숫자만 적습니다.</summary>
        private ICurrencyBalance wallet;

        /// <summary>줄을 이어 붙일 때 쓰는 버퍼입니다. 매번 새로 만들지 않습니다.</summary>
        private readonly StringBuilder builder = new StringBuilder(128);

        /// <summary>신호를 듣고 있는 계산대입니다. 꺼질 때 정확히 이것에서 떼어 냅니다.</summary>
        private ShopCounter listening;

        // --- Injection ---

        /// <summary>값 표기에 쓸 지갑을 받습니다.</summary>
        /// <param name="playerWallet">플레이어의 지갑</param>
        [Inject]
        public void Construct(ICurrencyBalance playerWallet)
        {
            wallet = playerWallet;
        }

        // --- Unity Event Functions ---

        void OnEnable()
        {
            ResolveCounter();
            Subscribe();
            Redraw();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        // --- Public Methods ---

        /// <summary>
        /// 가격표를 다시 씁니다. 계산대가 바뀌었다고 알려 올 때 불립니다.
        /// </summary>
        public void Redraw()
        {
            if (counter == null) return;

            int count = counter.SelectedCount;

            if (totalText != null)
            {
                totalText.text = count == 0 ? emptyLabel : FormatPrice(counter.TotalPrice);
            }

            if (itemListText != null) itemListText.text = BuildItemList(count);
        }

        // --- Private Methods ---

        /// <summary>고른 물건을 줄줄이 적습니다.</summary>
        /// <param name="count">고른 물건의 수</param>
        /// <returns>여러 줄로 이어 붙인 글자</returns>
        private string BuildItemList(int count)
        {
            if (count == 0) return "";

            builder.Clear();

            var selected = counter.Selected;
            int shown = 0;

            for (int i = 0; i < selected.Count; i++)
            {
                ShopShelfItem shelf = selected[i];
                if (shelf == null || shelf.item == null) continue;

                if (shown >= maxLines)
                {
                    builder.Append("…외 ").Append(count - shown).Append("개");
                    break;
                }

                if (shown > 0) builder.Append('\n');
                builder.Append(shelf.item.ToLine(FormatPrice(shelf.item.price)));
                shown++;
            }

            return builder.ToString();
        }

        /// <summary>값을 사람이 읽는 꼴로 적습니다.</summary>
        /// <param name="amount">적을 값</param>
        /// <returns>표기된 값</returns>
        private string FormatPrice(int amount)
        {
            return wallet != null ? wallet.Format(CurrencyType.Money, amount) : amount.ToString();
        }

        /// <summary>볼 계산대를 정합니다.</summary>
        private void ResolveCounter()
        {
            if (counter != null) return;

            counter = GetComponentInParent<ShopCounter>(true);
            if (counter == null) counter = GameContext.Resolve<ShopCounter>(this);

            if (counter == null)
            {
                GameLog.Warn(GameLog.Channel.UI,
                    "ShopPriceTagUI: 계산대를 찾지 못해 가격표를 그릴 수 없습니다.", this);
            }
        }

        /// <summary>계산대의 신호를 듣기 시작합니다.</summary>
        private void Subscribe()
        {
            if (counter == null || counter.onCartChanged == null) return;

            counter.onCartChanged.AddListener(Redraw);
            listening = counter;
        }

        /// <summary>듣던 신호를 뗍니다.</summary>
        private void Unsubscribe()
        {
            if (listening == null || listening.onCartChanged == null) return;

            listening.onCartChanged.RemoveListener(Redraw);
            listening = null;
        }
    }
}
