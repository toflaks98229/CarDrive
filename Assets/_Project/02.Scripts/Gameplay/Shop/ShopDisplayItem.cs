using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 진열대에 <b>실제로 놓인 물건 하나</b>입니다. 조준해 담으면 이것이 사라집니다.
    ///
    /// <b>왜 칸이 아니라 물건마다 두는가.</b> 예전에는 칸 전체가 조준 대상이라
    /// 어디를 겨누든 <b>맨 앞의 것</b>만 담겼습니다. 물건이 여섯 개 놓여 있어도
    /// 담는 자리는 하나로 고정되어 있었고, 뒤쪽 물건을 집으려 해도 앞엣것이 갔습니다.
    ///
    /// 이제 물건마다 콜라이더가 있어 <b>보고 있는 그것</b>이 담깁니다.
    /// 담는 순서를 사람이 정할 수 있게 되는 것이 요점입니다.
    ///
    /// <b>규칙은 전부 칸이 갖고 있습니다.</b> 값·영업 시간·장바구니는
    /// <see cref="ShopShelfItem"/> 의 것이고, 이 컴포넌트는 "나를 담아 달라"고
    /// 자기를 지목해 넘기기만 합니다.
    /// </summary>
    public class ShopDisplayItem : MonoBehaviour, IInteractable
    {
        // --- Public Member Variables ---

        /// <summary>이 물건이 놓인 칸입니다. 비워두면 부모에서 찾습니다.</summary>
        [Header("연동")]
        [Tooltip("이 물건이 놓인 진열 칸. 비워두면 부모에서 찾습니다.")]
        public ShopShelfItem shelf;

        // --- Unity Event Functions ---

        /// <summary>놓인 칸을 찾습니다.</summary>
        void Start()
        {
            if (shelf == null) shelf = GetComponentInParent<ShopShelfItem>(true);

            if (shelf == null)
            {
                GameLog.Warn(GameLog.Channel.Player,
                    "ShopDisplayItem: " + name + " 이(가) 어느 진열 칸의 것인지 찾지 못해 담을 수 없습니다.", this);
            }
        }

        // --- IInteractable ---

        /// <summary>칸이 안내할 말이 있으면 이 물건도 조준 대상입니다.</summary>
        /// <returns>담거나 안내할 수 있으면 true</returns>
        public bool CanInteract()
        {
            return shelf != null && shelf.CanInteract();
        }

        /// <summary>
        /// 조준했을 때 보여 줄 문구입니다. 칸의 규칙을 그대로 씁니다 —
        /// 값도 영업 시간도 칸이 압니다.
        /// </summary>
        /// <returns>칸이 만든 안내 문구</returns>
        public string GetInteractionLabel()
        {
            return shelf != null ? shelf.GetInteractionLabel() : "";
        }

        /// <summary><b>이 물건</b>을 담습니다.</summary>
        public void Interact()
        {
            if (shelf == null) return;
            shelf.Take(gameObject);
        }
    }
}
