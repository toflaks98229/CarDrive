using UnityEngine;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 플레이어의 도보 위치·시선·체력과 <b>저장 당시의 탑승 상태</b>를 담고 되돌립니다.
    ///
    /// <see cref="VehicleSaveParticipant"/>와 같은 이유로 <see cref="SaveSystem"/>에서 떼어 냈습니다.
    /// 그쪽 주석을 참고하세요.
    ///
    /// <b>순서가 중요합니다.</b> <see cref="SaveOrders.Player"/>는 차량보다 뒤입니다.
    /// 차가 제자리에 놓인 뒤에야 그 차에 올라탈 수 있습니다.
    /// </summary>
    public class PlayerSaveParticipant : MonoBehaviour, ISaveable
    {
        // --- Public Member Variables ---

        /// <summary>탑승 상태와 도보 위치를 다룰 컨트롤러입니다. 비워두면 Start에서 찾습니다.</summary>
        [Header("연동 (비워두면 실행할 때 찾습니다)")]
        [Tooltip("탑승 상태와 도보 위치를 저장·복원할 컨트롤러")]
        public PlayerModeController modeController;

        /// <summary>
        /// 플레이어 체력입니다. 비워두면 Start에서 찾습니다.
        ///
        /// 타입이 <see cref="PlayerHealth"/>인 것이 요점입니다. 차량 내구도를 여기에
        /// 끌어다 놓는 실수가 <b>컴파일 단계에서</b> 막힙니다.
        /// </summary>
        [Tooltip("플레이어 체력을 저장·복원할 대상")]
        public PlayerHealth playerHealth;

        // --- Unity Event Functions ---

        /// <summary>세이브 등록부에 자신을 넣습니다.</summary>
        void Awake()
        {
            SaveRegistry.Register(this);
        }

        /// <summary>
        /// 비어 있는 참조를 채웁니다.
        ///
        /// <b>Awake가 아니라 Start입니다.</b> 등록은 각자의 Awake에서 이뤄지는데
        /// GameObject 사이의 Awake 순서는 정해져 있지 않습니다.
        /// </summary>
        void Start()
        {
            if (modeController == null) modeController = GameContext.Resolve<PlayerModeController>(this);
            if (playerHealth == null) playerHealth = GameContext.Resolve<PlayerHealth>(this);
        }

        /// <summary>세이브 등록부에서 자신을 뺍니다.</summary>
        void OnDestroy()
        {
            SaveRegistry.Unregister(this);
        }

        // --- ISaveable ---

        /// <summary>차량이 제자리에 놓인 뒤입니다.</summary>
        public int SaveOrder { get { return SaveOrders.Player; } }

        /// <summary>
        /// 플레이어의 탑승 상태, 도보 위치와 시선 방향, 체력을 담습니다.
        /// </summary>
        /// <param name="data">플레이어 항목을 채워 넣을 세이브 자료</param>
        public void CaptureInto(SaveData data)
        {
            PlayerSave save = data.player;

            if (modeController != null)
            {
                save.wasDriving = modeController.Mode == PlayerMode.Driving;

                Vehicle driving = modeController.CurrentVehicle;
                save.drivingVehicleName = (save.wasDriving && driving != null) ? driving.displayName : "";

                if (modeController.footRig != null)
                {
                    Transform foot = modeController.footRig.transform;
                    save.footPosition = foot.position;
                    save.footYaw = foot.eulerAngles.y;
                }
            }

            if (playerHealth != null) save.health = playerHealth.CurrentHealth;
        }

        /// <summary>
        /// 플레이어의 체력과 도보 위치를 되돌리고, 저장 당시의 탑승 상태를 재현합니다.
        /// </summary>
        /// <param name="data">플레이어 항목이 담긴 세이브 자료</param>
        public void RestoreFrom(SaveData data)
        {
            PlayerSave save = data.player;

            if (playerHealth != null) playerHealth.Revive(save.health);
            if (modeController == null) return;

            // 도보 위치를 먼저 되돌려 둡니다. 차에서 내릴 때 이 자리에서 시작하게 됩니다.
            if (modeController.footRig != null)
            {
                GameObject rig = modeController.footRig;
                CharacterController cc = rig.GetComponent<CharacterController>();

                // CharacterController는 켜져 있으면 위치 대입을 무시합니다.
                if (cc != null) cc.enabled = false;
                rig.transform.SetPositionAndRotation(save.footPosition, Quaternion.Euler(0f, save.footYaw, 0f));
                if (cc != null) cc.enabled = true;
            }

            if (save.wasDriving)
            {
                Vehicle target = VehicleSaveParticipant.FindVehicle(save.drivingVehicleName);
                if (target != null) modeController.EnterVehicle(target);
                else modeController.EnterVehicle(true);
            }
            else
            {
                modeController.ExitVehicle(true);
            }
        }
    }
}
