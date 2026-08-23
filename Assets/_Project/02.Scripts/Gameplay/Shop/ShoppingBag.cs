using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 계산을 마친 물건들이 담긴 봉투입니다. 상호작용할 때마다 하나씩 꺼냅니다.
    ///
    /// <b>봉투가 들고 있는 것은 실물이 아니라 목록입니다.</b> 산 순간에 물건을 전부 만들어
    /// 봉투 안에 쑤셔 넣으면, 보이지도 않는 리지드바디 여럿이 물리 계산을 받고 봉투를 밀어냅니다.
    /// 그래서 무엇을 샀는지만 적어 두고, <b>꺼낼 때 그 자리에서 만듭니다.</b>
    ///
    /// <b>마지막에 산 것부터 나옵니다.</b> 봉투에 차곡차곡 담았으니 위에 있는 것이
    /// 먼저 잡히는 것이 자연스럽습니다. 목록의 끝에서부터 꺼내는 것이 그 규칙입니다.
    ///
    /// 봉투 자체를 들고 옮기고 싶다면 프리팹에 <see cref="Carryable"/>을 함께 붙이세요.
    /// 드는 것은 좌클릭, 꺼내는 것은 상호작용 키라 서로 부딪히지 않습니다.
    /// </summary>
    public class ShoppingBag : MonoBehaviour, IInteractable
    {
        // --- Public Member Variables ---

        [Header("꺼내는 자리")]
        /// <summary>물건이 나올 봉투 입구입니다. 비워두면 봉투 위쪽에서 나옵니다.</summary>
        [Tooltip("물건이 나올 봉투 입구. 비워두면 봉투 위쪽에서 나옵니다.")]
        public Transform mouth;

        /// <summary>입구를 지정하지 않았을 때 봉투 중심에서 위로 띄울 높이(m)입니다.</summary>
        [Tooltip("입구를 지정하지 않았을 때 봉투 중심에서 위로 띄울 높이(m)")]
        public float fallbackMouthHeight = 0.35f;

        /// <summary>꺼낼 때 위로 살짝 띄우는 속도(m/s)입니다. 0이면 그냥 떨어집니다.</summary>
        [Tooltip("꺼낼 때 위로 살짝 띄우는 속도(m/s). 0이면 그냥 떨어집니다.")]
        public float popSpeed = 1.2f;

        [Header("문구")]
        /// <summary>꺼낼 것이 남았을 때 보여 줄 동사입니다.</summary>
        [Tooltip("꺼낼 것이 남았을 때 보여 줄 동사")]
        public string takeLabel = "꺼내기";

        [Header("빈 봉투")]
        /// <summary>모두 꺼내면 봉투를 없앨지 여부입니다. 끄면 빈 봉투가 그 자리에 남습니다.</summary>
        [Tooltip("모두 꺼내면 봉투를 없앱니다. 끄면 빈 봉투가 남습니다.")]
        public bool destroyWhenEmpty = true;

        [Header("이벤트")]
        /// <summary>물건을 하나 꺼냈을 때.</summary>
        [Tooltip("물건을 하나 꺼냈을 때. Feel 의 MMF_Player 를 연결하세요.")]
        public UnityEvent onItemTaken;

        /// <summary>마지막 물건까지 꺼냈을 때.</summary>
        [Tooltip("마지막 물건까지 꺼냈을 때")]
        public UnityEvent onEmptied;

        // --- Public Properties ---

        /// <summary>봉투에 남은 물건의 수입니다.</summary>
        public int RemainingCount { get { return contents.Count; } }

        /// <summary>다음에 나올 물건입니다. 비었으면 null입니다.</summary>
        public ShopItem Next
        {
            get { return contents.Count > 0 ? contents[contents.Count - 1] : null; }
        }

        // --- Private Member Variables ---

        /// <summary>
        /// 담긴 물건들입니다. <b>담은 차례 그대로</b>이고, 꺼낼 때는 끝에서부터 뺍니다.
        /// </summary>
        private readonly List<ShopItem> contents = new List<ShopItem>();

        // --- Public Methods ---

        /// <summary>
        /// 봉투에 물건을 담습니다. 계산대가 계산을 마치고 한 번 부릅니다.
        /// </summary>
        /// <param name="items">담을 물건들. 산 순서 그대로 넘기세요.</param>
        public void Fill(IReadOnlyList<ShopItem> items)
        {
            contents.Clear();
            if (items == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null) contents.Add(items[i]);
            }
        }

        // --- IInteractable ---

        /// <summary>남은 것이 있을 때만 꺼낼 수 있습니다.</summary>
        /// <returns>꺼낼 것이 남았으면 true</returns>
        public bool CanInteract()
        {
            return contents.Count > 0;
        }

        /// <summary>
        /// 조준했을 때 보여 줄 문구입니다. 다음에 무엇이 나오는지와 몇 개 남았는지를 함께 적습니다.
        /// </summary>
        /// <returns>"꺼내기 — 소시지 (3개 남음)" 형태의 문구</returns>
        public string GetInteractionLabel()
        {
            ShopItem next = Next;
            if (next == null) return "";

            return takeLabel + " — " + next.displayName + " (" + contents.Count + "개 남음)";
        }

        /// <summary>마지막에 담긴 물건을 하나 꺼냅니다.</summary>
        public void Interact()
        {
            ShopItem next = Next;
            if (next == null) return;

            contents.RemoveAt(contents.Count - 1);

            Spawn(next);

            if (onItemTaken != null) onItemTaken.Invoke();

            if (contents.Count > 0) return;

            if (onEmptied != null) onEmptied.Invoke();
            if (destroyWhenEmpty) Destroy(gameObject);
        }

        // --- Private Methods ---

        /// <summary>
        /// 물건의 실물을 봉투 입구에 만듭니다.
        /// </summary>
        /// <param name="item">만들 물건</param>
        private void Spawn(ShopItem item)
        {
            if (item.prefab == null)
            {
                GameLog.Warn(GameLog.Channel.Player,
                    "ShoppingBag: " + item.displayName + " 에 프리팹이 없어 꺼낼 실물이 없습니다.", this);
                return;
            }

            Transform spot = mouth;
            Vector3 position = spot != null
                ? spot.position
                : transform.position + Vector3.up * fallbackMouthHeight;
            Quaternion rotation = spot != null ? spot.rotation : transform.rotation;

            GameObject spawned = Instantiate(item.prefab, position, rotation);

            // 입구에서 살짝 솟아오르게 해, 봉투 안에서 나온 것처럼 보이게 합니다.
            if (popSpeed <= 0f) return;

            Rigidbody body = spawned.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic) body.linearVelocity = Vector3.up * popSpeed;
        }
    }
}
