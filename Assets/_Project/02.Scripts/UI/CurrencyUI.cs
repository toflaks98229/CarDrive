using System.Collections.Generic;
using TMPro;
using UnityEngine;
using VContainer;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.UI
{
    /// <summary>
    /// 보유 재화를 숫자로 표시하는 HUD입니다.
    ///
    /// 상태는 <see cref="Wallet"/>이 소유하고 이 클래스는 <b>읽어서 그리기만</b> 합니다.
    /// (<see cref="NeedsUI"/>와 같은 규칙입니다)
    ///
    /// 값이 바뀐 프레임에만 다시 그립니다. TMP의 text 대입은 레이아웃을 다시 계산하므로
    /// 매 프레임 같은 글자를 넣으면 그만큼 낭비입니다.
    ///
    /// 사용법: 재화마다 TextMeshProUGUI 하나를 만들고 아래 <see cref="entries"/>에 연결하세요.
    /// 연결하지 않은 재화는 그냥 무시됩니다.
    /// </summary>
    public class CurrencyUI : MonoBehaviour
    {
        /// <summary>재화 하나에 대응하는 UI 묶음입니다.</summary>
        [System.Serializable]
        public class CurrencyEntry
        {
            /// <summary>이 묶음이 표시할 재화 종류입니다.</summary>
            [Tooltip("어떤 재화를 표시할지")]
            public CurrencyType type;

            /// <summary>보유량을 그릴 텍스트입니다.</summary>
            [Tooltip("보유량을 숫자로 그릴 텍스트")]
            public TextMeshProUGUI valueText;

            /// <summary>재화 이름을 그릴 텍스트입니다. 시작할 때 한 번만 채웁니다.</summary>
            [Tooltip("재화 이름을 그릴 텍스트 (선택)")]
            public TextMeshProUGUI labelText;

            /// <summary>보유량이 0일 때 통째로 숨길 오브젝트입니다.</summary>
            [Tooltip("보유량이 0일 때 숨길 오브젝트 (선택)")]
            public GameObject hideWhenZero;

            /// <summary>마지막으로 그린 값입니다. 달라졌을 때만 다시 그립니다.</summary>
            [System.NonSerialized]
            public int lastDrawn = int.MinValue;
        }

        // --- Public Member Variables ---

        /// <summary>표시할 재화 목록입니다. 여기에 없는 재화는 그리지 않습니다.</summary>
        [Header("표시")]
        [Tooltip("표시할 재화 목록")]
        public List<CurrencyEntry> entries = new List<CurrencyEntry>();

        /// <summary>재화 색을 텍스트에도 적용할지 여부입니다.</summary>
        [Tooltip("체크하면 설정에 있는 재화 색을 숫자에도 적용합니다.")]
        public bool applyCurrencyColor = true;

        // --- Injection ---

        /// <summary>값을 읽어 올 지갑입니다. 주입되지 않으면 그릴 것이 없어 스스로 꺼집니다.</summary>
        private ICurrencyBalance wallet;

        /// <summary>
        /// 값을 읽어 올 지갑을 받습니다.
        ///
        /// <b>잔액 계약이면 충분합니다.</b> UI 는 읽기만 하므로 지출까지 할 수 있는
        /// <see cref="ICurrencyStore"/>를 받을 이유가 없습니다.
        /// 예전에는 인스펙터 칸도 함께 두었는데, 같은 것을 잇는 길이 둘이면
        /// <b>어느 쪽이 실제로 쓰였는지 코드만 보고는 알 수 없습니다.</b>
        /// </summary>
        /// <param name="playerWallet">값을 읽어 올 지갑</param>
        [Inject]
        public void Construct(ICurrencyBalance playerWallet)
        {
            wallet = playerWallet;
        }

        // --- Unity Event Functions ---

        /// <summary>
        /// 이름 라벨과 색을 한 번만 채웁니다.
        /// 지갑이 없으면 경고를 남기고 이 컴포넌트를 끕니다.
        /// </summary>
        void Start()
        {

            if (wallet == null)
            {
                GameLog.Warn(GameLog.Channel.UI, "CurrencyUI: 지갑이 주입되지 않아 재화를 표시하지 않습니다.", this);
                enabled = false;
                return;
            }

            // 이름과 색은 매 프레임 갱신할 필요가 없으므로 한 번만 채웁니다.
            for (int i = 0; i < entries.Count; i++)
            {
                CurrencyEntry entry = entries[i];
                CurrencySetting setting = wallet.GetSetting(entry.type);
                if (setting == null) continue;

                if (entry.labelText != null) entry.labelText.text = setting.displayName;

                if (applyCurrencyColor)
                {
                    if (entry.valueText != null) entry.valueText.color = setting.displayColor;
                    if (entry.labelText != null) entry.labelText.color = setting.displayColor;
                }
            }
        }

        /// <summary>
        /// 값이 바뀐 것만 다시 그립니다.
        /// </summary>
        void Update()
        {
            if (wallet == null) return;

            for (int i = 0; i < entries.Count; i++)
            {
                Redraw(entries[i]);
            }
        }

        // --- Private Methods ---

        /// <summary>
        /// 재화 하나를 그립니다. 값이 그대로면 아무것도 하지 않습니다.
        /// </summary>
        /// <param name="entry">그릴 UI 묶음</param>
        private void Redraw(CurrencyEntry entry)
        {
            int amount = wallet.Get(entry.type);
            if (amount == entry.lastDrawn) return;

            entry.lastDrawn = amount;

            // 접두·접미와 천 단위 표기는 지갑이 정합니다. UI가 따로 정하면 규칙이 갈라집니다.
            if (entry.valueText != null) entry.valueText.text = wallet.Format(entry.type);

            if (entry.hideWhenZero != null) entry.hideWhenZero.SetActive(amount > 0);
        }
    }
}
