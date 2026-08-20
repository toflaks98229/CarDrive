using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 차량 문에 붙여 탑승과 하차를 모두 처리합니다.
    /// 조준점이 이 문에 걸려 있을 때만 동작하므로, 밖에서는 문을 봐야 타고
    /// 안에서도 문을 봐야 내립니다.
    ///
    /// 이 문은 <b>자기 차량이 무엇인지</b>를 알고 있고, 탑승할 때 그 차량을 넘겨 줍니다.
    /// 그래서 차가 여러 대여도 "조준한 문의 차"에 정확히 탑니다.
    /// </summary>
    public class VehicleDoorInteractable : MonoBehaviour, IInteractable
    {
        // --- Public Member Variables ---

        /// <summary>이 문이 속한 차량입니다. 비워두면 Start에서 부모를 거슬러 찾습니다.</summary>
        [Header("연동")]
        [Tooltip("이 문이 속한 차량. 비워두면 부모에서 찾습니다.")]
        public Vehicle vehicle;

        /// <summary>탑승·하차를 실제로 수행할 컨트롤러입니다. 비워두면 Start에서 씬을 검색합니다.</summary>
        [Tooltip("탑승을 처리할 컨트롤러. 비워두면 씬에서 자동으로 찾습니다.")]
        public PlayerModeController modeController;

        /// <summary>밖에서 문을 조준했을 때 표시할 안내 문구입니다.</summary>
        [Header("문구 (다국어 대응)")]
        [Tooltip("밖에서 문을 조준했을 때")]
        public string enterLabel = "탑승";

        /// <summary>차 안에서 문을 조준했을 때 표시할 안내 문구입니다.</summary>
        [Tooltip("안에서 문을 조준했을 때")]
        public string exitLabel = "하차";

        /// <summary>속도가 너무 빨라 내릴 수 없을 때 표시할 안내 문구입니다.</summary>
        [Tooltip("너무 빨라 내릴 수 없을 때")]
        public string tooFastLabel = "속도를 줄이세요";

        // --- Unity Event Functions ---

        /// <summary>
        /// 비어 있는 차량·컨트롤러 참조를 채웁니다. 끝내 찾지 못한 것이 있으면 각각 경고를 남깁니다.
        /// </summary>
        void Start()
        {
            if (vehicle == null) vehicle = GetComponentInParent<Vehicle>();
            if (modeController == null) modeController = GameContext.Resolve<PlayerModeController>(this);

            if (vehicle == null)
            {
                Debug.LogWarning("VehicleDoorInteractable: 이 문이 속한 Vehicle을 찾지 못했습니다.", this);
            }
            if (modeController == null)
            {
                Debug.LogWarning("VehicleDoorInteractable: PlayerModeController를 찾지 못해 탑승할 수 없습니다.", this);
            }
        }

        // --- Public Methods ---

        // --- IInteractable ---

        /// <summary>
        /// 문은 탑승·하차 양쪽에 쓰이므로 컨트롤러와 차량만 있으면 언제든 동작합니다.
        /// (달리는 중 하차 차단은 PlayerModeController.ExitVehicle이 판단합니다)
        /// </summary>
        /// <returns>컨트롤러와 차량 참조가 모두 있으면 true를 반환합니다.</returns>
        public bool CanInteract()
        {
            return modeController != null && vehicle != null;
        }

        /// <summary>
        /// 지금 이 문으로 무엇을 할 수 있는지 안내 문구를 만듭니다.
        /// 이 차를 타고 있는 중이면 하차 문구를, 너무 빠르면 감속을 요구하는 문구를 돌려줍니다.
        /// </summary>
        /// <returns>상황에 맞는 안내 문구. 상호작용할 수 없으면 빈 문자열입니다.</returns>
        public string GetInteractionLabel()
        {
            if (!CanInteract()) return "";

            // 이 차를 타고 있는 중이라면 '하차'입니다.
            if (modeController.Mode == PlayerMode.Driving && vehicle.IsOccupied)
            {
                // 왜 못 내리는지 알려 줍니다. 예전에는 조용히 실패하고 로그만 남았습니다.
                // 속도 판정은 PlayerModeController가 소유합니다. 여기서 따로 계산하면
                // 안내 문구와 실제 하차 조건이 서로 어긋날 수 있습니다.
                if (!modeController.CanExitVehicle()) return tooFastLabel;
                return exitLabel;
            }

            return enterLabel;
        }

        /// <summary>
        /// 이 문의 차량에 타거나, 타고 있었다면 내립니다.
        /// 다른 차를 타고 있었다면 그대로 이 차량으로 옮겨 탑니다.
        /// </summary>
        public void Interact()
        {
            if (!CanInteract()) return;

            if (modeController.Mode == PlayerMode.Driving && vehicle.IsOccupied)
            {
                modeController.ExitVehicle(false);
            }
            else
            {
                // 자기 차량을 넘겨 줍니다. 다른 차를 타고 있었다면 그대로 옮겨 탑니다.
                modeController.EnterVehicle(vehicle);
            }
        }
    }
}
