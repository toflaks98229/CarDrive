using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 차량을 조종하는 상태입니다.
    ///
    /// 카메라를 운전석 피벗에 붙이고 차량 입력·추종 카메라·주행 진동을 켭니다.
    /// 어느 차량인지는 <see cref="PlayerModeController.CurrentVehicle"/>이 알고 있으므로,
    /// 다른 차로 갈아탈 때는 <b>이 상태로 다시 들어오기만 하면 됩니다.</b>
    /// </summary>
    public class DrivingState : IPlayerState
    {
        /// <summary>이 상태가 나타내는 모드입니다.</summary>
        public PlayerMode Mode { get { return PlayerMode.Driving; } }

        /// <summary>
        /// 운전석에 앉힙니다. 도보 조작을 끄고 차량 조작을 켠 뒤 카메라를 옮깁니다.
        /// </summary>
        /// <param name="player">참조와 공용 동작을 들고 있는 컨트롤러</param>
        public void Enter(PlayerModeController player)
        {
            Vehicle vehicle = player.CurrentVehicle;
            if (vehicle == null)
            {
                Debug.LogWarning("DrivingState: 탑승할 차량이 없습니다.", player);
                return;
            }

            Vehicle.SetCurrent(vehicle);

            // 1. 도보 조작을 끕니다.
            //    차량 입력과 같은 축을 쓰므로 반드시 한쪽만 켜져 있어야 합니다.
            if (player.footRig != null) player.footRig.SetActive(false);

            // 2. 카메라를 운전석 피벗으로 옮깁니다.
            Transform pivot = player.GetDriverPivot();
            player.AttachCamera(pivot);

            // 3. 차량 조작을 켭니다.
            if (player.carCameraFollow != null)
            {
                player.carCameraFollow.target = vehicle.DriverAnchor;
                player.carCameraFollow.enabled = true;
            }
            if (vehicle.input != null) vehicle.input.enabled = true;

            // 이 차량의 계기판 체력 표시를 켭니다. (차마다 자기 것을 가집니다)
            if (vehicle.seat != null) vehicle.seat.SetHealthDisplayVisible(true);

            // 4. 주행 진동을 켜고, 어느 차의 진동인지 알려 줍니다.
            if (player.carCameraEffects != null)
            {
                player.carCameraEffects.SetVehicle(vehicle);
                player.carCameraEffects.enabled = true;
            }

            // 5. 마우스 좌우 회전은 운전석 피벗을 돌립니다. (차 안에서 두리번거리기)
            if (player.lookController != null) player.lookController.SetPlayerBody(pivot);

            Debug.Log("PlayerModeController: " + vehicle.displayName + "에 탑승했습니다.");
        }

        /// <summary>
        /// 차량 조작을 내려놓습니다.
        /// 입력 값을 비우지 않으면 마지막 값이 남아 차가 계속 달립니다.
        /// </summary>
        /// <param name="player">참조와 공용 동작을 들고 있는 컨트롤러</param>
        public void Exit(PlayerModeController player)
        {
            player.ReleaseVehicleControl(player.CurrentVehicle);

            if (player.carCameraFollow != null) player.carCameraFollow.enabled = false;

            // 주행 진동은 반드시 꺼야 합니다.
            // LateUpdate에서 카메라의 localPosition을 계속 덮어쓰기 때문입니다.
            if (player.carCameraEffects != null) player.carCameraEffects.enabled = false;
        }
    }
}
