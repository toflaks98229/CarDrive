using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 걸어 다니는 상태입니다.
    ///
    /// 도보 리그를 하차 지점에 놓고 카메라를 눈높이로 옮깁니다.
    /// 차량 쪽 정리는 <see cref="DrivingState.Exit"/>가 담당하므로 여기서 다시 하지 않습니다.
    /// (게임을 도보로 시작하는 경우에도 전환을 태우기 때문에 그 정리가 반드시 한 번 실행됩니다.
    ///  PlayerModeController.Start의 주석을 참고하세요)
    /// </summary>
    public class OnFootState : IPlayerState
    {
        /// <summary>이 상태가 나타내는 모드입니다.</summary>
        public PlayerMode Mode { get { return PlayerMode.OnFoot; } }

        /// <summary>
        /// 차 밖에 내려섭니다. 도보 리그를 하차 지점에 놓고 카메라를 머리 위치로 옮깁니다.
        /// </summary>
        /// <param name="player">참조와 공용 동작을 들고 있는 컨트롤러</param>
        public void Enter(PlayerModeController player)
        {
            Vehicle.SetCurrent(null);

            // 1. 도보 리그를 하차 지점에 놓습니다.
            player.PlaceFootRig();

            // 2. 카메라를 머리 위치로 옮깁니다.
            player.AttachCamera(player.headMount);

            // 3. 마우스 좌우 회전은 도보 리그 본체를 돌립니다.
            if (player.lookController != null && player.footRig != null)
            {
                player.lookController.SetPlayerBody(player.footRig.transform);
            }

            Debug.Log("PlayerModeController: 차량에서 내렸습니다.");
        }

        /// <summary>
        /// 도보 상태에서 나갈 때 따로 정리할 것은 없습니다.
        /// 도보 리그는 <see cref="DrivingState.Enter"/>가 끄고, 들고 있던 물건은
        /// 리그가 꺼질 때 PlayerCarrier.OnDisable이 내려놓습니다.
        /// </summary>
        /// <param name="player">참조와 공용 동작을 들고 있는 컨트롤러</param>
        public void Exit(PlayerModeController player)
        {
        }
    }
}
