using System.Collections.Generic;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// 플레이어가 모으는 재화의 종류입니다.
    ///
    /// 값을 추가하면 <see cref="CurrencyDefaults.CreateSettings"/>에도 함께 넣어야 합니다.
    /// (넣지 않으면 <see cref="Wallet"/>이 경고를 남기고 기본값으로 메웁니다)
    /// </summary>
    public enum CurrencyType
    {
        /// <summary>돈. 주유·수리·물건 구입에 씁니다.</summary>
        Money,

        /// <summary>엑토플라즘. 귀신을 쓰러뜨리면 떨어집니다.</summary>
        Ectoplasm
    }

    /// <summary>
    /// 재화 한 종류의 설정입니다.
    /// </summary>
    [System.Serializable]
    public class CurrencySetting : IDefinition<CurrencyType>
    {
        /// <summary>어떤 재화에 대한 설정인지 나타냅니다.</summary>
        [Tooltip("어떤 재화에 대한 설정인지")]
        public CurrencyType type;

        /// <summary>표가 이 설정을 찾는 열쇠입니다.</summary>
        public CurrencyType Key { get { return type; } }

        /// <summary>빠진 항목을 메웠다고 알릴 때 쓸 이름입니다.</summary>
        public string DisplayName { get { return displayName; } }

        /// <summary>UI에 표시할 이름입니다.</summary>
        [Tooltip("UI에 표시할 이름")]
        public string displayName = "";

        /// <summary>게임을 시작할 때 가지고 있는 양입니다.</summary>
        [Tooltip("시작할 때 가지고 있는 양")]
        public int startingAmount = 0;

        /// <summary>가질 수 있는 최대치입니다. 0이면 제한이 없습니다.</summary>
        [Tooltip("가질 수 있는 최대치. 0이면 제한 없음")]
        public int maxAmount = 0;

        /// <summary>숫자 앞에 붙일 글자입니다. (예: ₩)</summary>
        [Tooltip("숫자 앞에 붙일 글자 (예: ₩)")]
        public string prefix = "";

        /// <summary>숫자 뒤에 붙일 글자입니다. (예: ml)</summary>
        [Tooltip("숫자 뒤에 붙일 글자 (예: ml)")]
        public string suffix = "";

        /// <summary>
        /// 숫자 표기 형식입니다. "N0"이면 1,250처럼 천 단위 쉼표가 들어갑니다.
        /// 비워 두면 쉼표 없이 그대로 씁니다.
        /// </summary>
        [Tooltip("숫자 표기 형식. N0이면 1,250 처럼 천 단위 쉼표가 들어갑니다. 비우면 그대로 표기")]
        public string numberFormat = "N0";

        /// <summary>UI에서 이 재화를 나타낼 색입니다.</summary>
        [Tooltip("UI 색상")]
        public Color displayColor = Color.white;
    }

    /// <summary>
    /// 재화 한 종류의 실행 중 상태입니다. 세이브 대상이라 직렬화 가능하게 두었습니다.
    /// </summary>
    [System.Serializable]
    public class CurrencyState : IDefinitionState<CurrencyType, CurrencyState>
    {
        /// <summary>어떤 재화인지 나타냅니다.</summary>
        public CurrencyType type;

        /// <summary>지금 가지고 있는 양입니다.</summary>
        public int amount;

        /// <summary>표가 이 상태를 찾는 열쇠입니다.</summary>
        public CurrencyType Key { get { return type; } }

        /// <summary>세이브에 담을 사본을 만듭니다.</summary>
        /// <returns>값이 같은 새 인스턴스</returns>
        public CurrencyState Clone()
        {
            return new CurrencyState { type = type, amount = amount };
        }

        /// <summary>
        /// 불러온 값을 받아 옵니다. 종류(<see cref="type"/>)는 이미 제자리이므로 옮기지 않습니다.
        /// </summary>
        /// <param name="other">값을 가져올 상태</param>
        public void CopyFrom(CurrencyState other)
        {
            if (other == null) return;
            amount = other.amount;
        }
    }

    /// <summary>
    /// 설정 에셋을 만들지 않아도 바로 돌아가도록 기본값을 제공합니다.
    /// <see cref="NeedDefaults"/>와 같은 방식입니다.
    /// </summary>
    public static class CurrencyDefaults
    {
        /// <summary>
        /// 두 재화의 기본 설정을 새로 만들어 돌려줍니다.
        /// 호출할 때마다 새 인스턴스를 만들므로, 받은 쪽에서 값을 고쳐도 다음 호출에 영향을 주지 않습니다.
        /// </summary>
        /// <returns>돈과 엑토플라즘의 기본 설정</returns>
        public static List<CurrencySetting> CreateSettings()
        {
            return new List<CurrencySetting>
            {
                new CurrencySetting {
                    type = CurrencyType.Money, displayName = "돈",
                    startingAmount = 0, maxAmount = 0,
                    prefix = "₩", numberFormat = "N0",
                    displayColor = new Color(0.95f, 0.85f, 0.35f)
                },
                new CurrencySetting {
                    type = CurrencyType.Ectoplasm, displayName = "엑토플라즘",
                    startingAmount = 0, maxAmount = 0,
                    prefix = "", numberFormat = "N0",
                    displayColor = new Color(0.55f, 0.95f, 0.75f)
                }
            };
        }
    }
}
