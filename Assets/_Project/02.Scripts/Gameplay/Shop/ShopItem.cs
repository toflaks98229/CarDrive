using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 마트에서 파는 물건 한 종류의 정의입니다.
    ///
    /// <b>왜 에셋인가.</b> 값과 프리팹이 코드 밖에 있어야 물건을 늘릴 때 스크립트를 고치지
    /// 않습니다. 이 프로젝트가 <see cref="CarData"/>·<c>NeedsProfile</c>에서 쓰는 방식과 같습니다.
    ///
    /// <b>봉투에 담기는 것은 "데이터"입니다.</b> 계산을 마치면 물건이 그 자리에서 생겨나는 것이
    /// 아니라, 무엇을 샀는지가 봉투에 적힙니다. 실물은 봉투에서 꺼낼 때 비로소 만들어집니다.
    /// 그래서 장바구니와 봉투가 들고 다니는 것은 이 정의이고, <see cref="prefab"/>은
    /// 꺼내는 순간에만 쓰입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "New Shop Item", menuName = "CarDrive/Shop Item")]
    public class ShopItem : ScriptableObject
    {
        // --- Public Member Variables ---

        [Header("표시")]
        /// <summary>가격표와 안내 문구에 쓸 이름입니다.</summary>
        [Tooltip("가격표와 안내 문구에 쓸 이름")]
        public string displayName = "물건";

        [Header("값")]
        /// <summary>이 물건 하나의 값입니다.</summary>
        [Tooltip("이 물건 하나의 값")]
        public int price = 1000;

        [Header("실물")]
        /// <summary>
        /// 봉투에서 꺼내거나 계산대에 올릴 때 만들어질 프리팹입니다.
        ///
        /// 비어 있으면 살 수는 있지만 꺼낼 것이 없습니다. 값만 있는 물건을 시험할 때 쓰세요.
        /// </summary>
        [Tooltip("꺼낼 때 만들어질 프리팹. 비어 있으면 꺼내도 아무것도 나오지 않습니다.")]
        public GameObject prefab;

        /// <summary>
        /// 봉투에 담을 수 있는 크기인지 여부입니다.
        ///
        /// <b>맥주 상자처럼 큰 것은 담기지 않습니다.</b> 그런 물건은 계산을 마치면 봉투 대신
        /// 계산대 옆에 그대로 놓이고, 플레이어가 직접 들어 옮겨야 합니다.
        /// 봉투 하나에 상자가 들어가는 것이 이상해 보이는 것을 막는 규칙입니다.
        /// </summary>
        [Tooltip("체크를 해제하면 봉투에 담기지 않고 계산대 옆에 그대로 놓입니다. (맥주 상자 같은 큰 물건)")]
        public bool fitsInBag = true;

        // --- Public Methods ---

        /// <summary>
        /// 이름과 값을 한 줄로 적어 돌려줍니다. 가격표에 그대로 쓸 수 있습니다.
        /// </summary>
        /// <param name="formattedPrice">이미 재화 표기를 입힌 값</param>
        /// <returns>"소시지  ₩1,200" 형태의 한 줄</returns>
        public string ToLine(string formattedPrice)
        {
            return displayName + "  " + formattedPrice;
        }
    }
}
