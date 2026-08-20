using System.Collections.Generic;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 음료 여러 병을 담고 있는 상자입니다.
    ///
    /// 시작할 때 직계 자식에서 <see cref="Beverage"/>를 찾아 목록을 만들고,
    /// 조준해서 상호작용하면 앞에 있는 병부터 꺼내 마십니다.
    ///
    /// 병은 Rigidbody를 가진 물리 오브젝트라 <b>주행 중 흔들리면 상자 밖으로 굴러 나갑니다.</b>
    /// 그렇게 빠져나간 병까지 목록에 남아 있으면, 상자를 조준했을 때 이미 저 멀리 있는 병을
    /// 마시게 됩니다. 그래서 주기적으로 거리를 확인해 빠져나간 병을 목록에서 뺍니다.
    /// 뺀 병은 사라지지 않고 그 자리에 남아, 직접 조준해서 마실 수 있습니다.
    /// </summary>
    public class BeverageBox : MonoBehaviour, IInteractable
    {
        // --- Public Member Variables ---

        /// <summary>이 상자가 실려 있는 차량입니다. 비워두면 부모에서 찾습니다.</summary>
        [Header("연동")]
        [Tooltip("이 상자가 실려 있는 차량. 비워두면 부모에서 찾습니다. " +
                 "차량 안의 상자는 그 차에 타고 있을 때만 꺼낼 수 있습니다.")]
        public Vehicle vehicle;

        /// <summary>상자에서 이 거리보다 멀어지면 빠져나간 것으로 봅니다.</summary>
        [Header("상자 밖으로 굴러 나간 병")]
        [Tooltip("상자 중심에서 이 거리(m)보다 멀어지면 빠져나간 것으로 보고 목록에서 뺍니다.")]
        public float escapeDistance = 1.2f;

        /// <summary>빠져나갔는지 확인하는 주기(초)입니다.</summary>
        [Tooltip("확인 주기(초). 매 프레임 검사할 필요가 없습니다.")]
        public float escapeCheckInterval = 0.4f;

        /// <summary>시작할 때 찾아 담아 둔 음료 목록입니다.</summary>
        [Header("찾은 음료 목록 (읽기 전용)")]
        [Tooltip("시작할 때 자동으로 채워지는 음료 목록입니다.")]
        public List<Beverage> foundBeverages = new List<Beverage>();

        // --- Private Member Variables ---

        /// <summary>다음 확인까지 남은 시간(초)입니다.</summary>
        private float checkTimer;

        // --- Unity Event Functions ---

        /// <summary>
        /// 직계 자식에서 음료를 찾아 목록을 만들고, 각 병에 자기를 알려 줍니다.
        /// </summary>
        void Awake()
        {
            foundBeverages = new List<Beverage>();

            foreach (Transform child in transform)
            {
                Beverage beverage = child.GetComponent<Beverage>();
                if (beverage == null) continue;

                beverage.SetBox(this);
                foundBeverages.Add(beverage);
            }

            // 이 상자가 차량 안에 실려 있는지 확인해 둡니다. 밖에 놓인 상자라면 null입니다.
            if (vehicle == null) vehicle = GetComponentInParent<Vehicle>(true);
        }

        /// <summary>
        /// 상자 밖으로 굴러 나간 병을 주기적으로 목록에서 뺍니다.
        /// </summary>
        void Update()
        {
            if (foundBeverages.Count == 0) return;

            checkTimer -= Time.deltaTime;
            if (checkTimer > 0f) return;

            checkTimer = Mathf.Max(0.05f, escapeCheckInterval);
            DropEscapedBeverages();
        }

        // --- Public Methods ---

        // --- IInteractable ---

        /// <summary>
        /// 지금 상자에서 음료를 꺼내 마실 수 있는지 확인합니다.
        /// </summary>
        /// <returns>남은 음료가 있고 손이 닿으면 true를 반환합니다.</returns>
        public bool CanInteract()
        {
            return HasBeverage() && IsReachable();
        }

        /// <summary>
        /// 화면에 표시할 동작 이름입니다.
        ///
        /// 상자는 자기 문구를 따로 갖지 않고 <b>내줄 병의 문구</b>를 그대로 씁니다.
        /// 병이 상자 안에 빽빽이 들어 있어 조준점이 상자에 걸릴 때도 있고 병에 걸릴 때도 있는데,
        /// 문구를 양쪽에 따로 두면 겨누는 위치에 따라 안내가 바뀌어 보입니다.
        /// </summary>
        /// <returns>다음에 꺼낼 병의 안내 문구. 남은 병이 없으면 빈 문자열입니다.</returns>
        public string GetInteractionLabel()
        {
            Beverage next = PeekBeverage();
            return next != null ? next.GetInteractionLabel() : "";
        }

        /// <summary>앞에 있는 음료를 하나 꺼내 마십니다.</summary>
        public void Interact()
        {
            Beverage next = PeekBeverage();
            if (next == null) return;

            // 목록에서 빼는 일과 감추는 일은 BeverageConsumer가 처리합니다.
            // (마시는 도중에 상태가 갈리지 않도록 한 곳에서 다룹니다)
            next.Interact();
        }

        /// <summary>
        /// 지금 이 상자에 손이 닿는지 여부입니다.
        ///
        /// 차량 안에 실린 상자는 <b>그 차에 타고 있을 때만</b> 꺼낼 수 있습니다.
        /// 상호작용 레이캐스트는 Interactable 레이어만 보기 때문에 차체를 그대로 통과합니다.
        /// 그래서 예전에는 차 밖에 서서 조준하는 것만으로 안에 있는 음료를 꺼낼 수 있었습니다.
        /// </summary>
        /// <returns>차량 밖에 놓인 상자이거나, 그 차에 타고 있으면 true입니다.</returns>
        public bool IsReachable()
        {
            return vehicle == null || vehicle.IsOccupied;
        }

        /// <summary>
        /// 지금 꺼낼 음료가 남아 있는지 여부입니다.
        /// </summary>
        /// <returns>목록에 음료가 하나라도 있으면 true를 반환합니다.</returns>
        public bool HasBeverage()
        {
            PruneMissing();
            return foundBeverages.Count > 0;
        }

        /// <summary>
        /// 다음에 꺼낼 음료를 꺼내지 않고 확인만 합니다.
        /// </summary>
        /// <returns>목록의 첫 번째 음료. 비어 있으면 null입니다.</returns>
        public Beverage PeekBeverage()
        {
            PruneMissing();
            return foundBeverages.Count > 0 ? foundBeverages[0] : null;
        }

        /// <summary>
        /// 병 하나를 목록에서 뺍니다. 오브젝트는 그대로 둡니다.
        /// 마실 때와 굴러 나갔을 때 모두 이 경로를 씁니다.
        /// </summary>
        /// <param name="beverage">뺄 음료</param>
        public void Release(Beverage beverage)
        {
            if (beverage == null) return;

            foundBeverages.Remove(beverage);
        }

        // --- Private Methods ---

        /// <summary>
        /// 상자에서 멀어진 병을 목록에서 뺍니다.
        /// </summary>
        private void DropEscapedBeverages()
        {
            float limitSqr = escapeDistance * escapeDistance;

            for (int i = foundBeverages.Count - 1; i >= 0; i--)
            {
                Beverage beverage = foundBeverages[i];
                if (beverage == null)
                {
                    foundBeverages.RemoveAt(i);
                    continue;
                }

                if ((beverage.transform.position - transform.position).sqrMagnitude <= limitSqr) continue;

                // 상자를 따라다니지 않도록 부모에서도 떼어 냅니다.
                beverage.transform.SetParent(null, true);
                beverage.LeaveBox();
            }
        }

        /// <summary>
        /// 파괴되었거나 사라진 항목을 목록에서 걷어 냅니다.
        /// </summary>
        private void PruneMissing()
        {
            for (int i = foundBeverages.Count - 1; i >= 0; i--)
            {
                if (foundBeverages[i] == null) foundBeverages.RemoveAt(i);
            }
        }
    }
}
