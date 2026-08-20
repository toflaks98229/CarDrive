using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.Systems
{
    /// <summary>
    /// 게임 상태를 파일로 저장하고 되돌립니다.
    ///
    /// <b>이 클래스는 어떤 시스템이 있는지 모릅니다.</b> 시간·날씨·니즈·지갑은 각자
    /// <see cref="ISaveable"/>을 구현하고 <see cref="SaveRegistry"/>에 스스로 등록하며,
    /// 여기서는 순서대로 훑어 <c>CaptureInto</c>/<c>RestoreFrom</c>을 부르기만 합니다.
    /// 그래서 새 시스템을 추가할 때 <b>이 파일은 건드리지 않습니다.</b>
    /// (<see cref="SaveData"/>에 담을 자리를 만들고 <see cref="ISaveable"/>을 구현하면 끝입니다)
    ///
    /// 다만 <b>차량과 플레이어는 여기서 직접 다룹니다.</b> 시스템처럼 하나씩 있는 것이 아니라
    /// 씬에 여럿 놓이고, 되돌리는 순서도 서로 얽혀 있기 때문입니다.
    /// (차를 제자리에 놓아야 플레이어가 그 차에 올라탈 수 있습니다)
    ///
    /// <b>지형은 저장하지 않습니다.</b> WorldStreamer가 시드로 배치를 고정하므로
    /// 같은 시드면 언제나 같은 세계가 다시 깔립니다.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>저장 파일 이름입니다. Application.persistentDataPath 아래에 만들어집니다.</summary>
        [Header("파일")]
        [Tooltip("저장 파일 이름. Application.persistentDataPath 아래에 만들어집니다.")]
        public string fileName = "cardrive_save.json";

        /// <summary>사람이 읽기 좋게 줄바꿈해서 저장할지 여부입니다. (디버그용)</summary>
        [Tooltip("체크하면 사람이 읽기 좋게 줄바꿈해서 저장합니다. (디버그용)")]
        public bool prettyPrint = true;

        // 시간·날씨·니즈·지갑은 더 이상 여기서 참조하지 않습니다.
        // 각자 ISaveable 을 구현하고 Awake 에서 SaveRegistry 에 스스로 등록합니다.
        // 이 컴포넌트는 등록부를 순서대로 훑기만 하므로, 어떤 시스템이 있는지 몰라도 됩니다.

        /// <summary>탑승 상태와 도보 위치를 저장·복원할 컨트롤러입니다. 비워두면 Awake에서 씬을 검색합니다.</summary>
        [Header("씬 오브젝트 (비워두면 씬에서 찾습니다)")]
        [Tooltip("탑승 상태와 도보 위치를 저장·복원할 컨트롤러")]
        public PlayerModeController modeController;

        /// <summary>플레이어 체력을 저장·복원할 대상입니다. 비워두면 Awake에서 씬을 검색합니다.</summary>
        [Tooltip("플레이어 체력을 저장·복원할 대상")]
        public PlayerHealth playerHealth;

        /// <summary>저장 단축키입니다. None이면 단축키를 쓰지 않습니다.</summary>
        [Header("단축키 (개발용, 0이면 사용 안 함)")]
        [Tooltip("저장 단축키")]
        public KeyCode saveKey = KeyCode.F5;

        /// <summary>불러오기 단축키입니다. None이면 단축키를 쓰지 않습니다.</summary>
        [Tooltip("불러오기 단축키")]
        public KeyCode loadKey = KeyCode.F9;

        /// <summary>저장에 성공했을 때 한 번 호출됩니다.</summary>
        [Header("이벤트")]
        [Tooltip("저장에 성공했을 때")]
        public UnityEvent onSaved;

        /// <summary>불러오기에 성공했을 때 한 번 호출됩니다.</summary>
        [Tooltip("불러오기에 성공했을 때")]
        public UnityEvent onLoaded;

        // --- Public Properties ---

        /// <summary>씬의 세이브 시스템입니다. 둘 이상이면 나중 것이 스스로 비활성화됩니다.</summary>
        public static SaveSystem Instance { get { return GameContext.Get<SaveSystem>(); } }

        /// <summary>저장 파일의 전체 경로입니다.</summary>
        public string FilePath { get { return Path.Combine(Application.persistentDataPath, fileName); } }

        /// <summary>저장된 파일이 있는지 여부입니다.</summary>
        public bool HasSave { get { return File.Exists(FilePath); } }

        // --- Unity Event Functions ---

        /// <summary>
        /// 자신을 등록합니다. 이미 다른 인스턴스가 있으면 자신을 끕니다.
        /// </summary>
        void Awake()
        {
            // 등록이 거부되면 이미 다른 것이 있다는 뜻입니다. (경고는 GameContext가 남깁니다)
            if (!GameContext.Register(this))
            {
                enabled = false;
                return;
            }
        }

        /// <summary>
        /// 비어 있는 시스템 참조를 채웁니다.
        ///
        /// <b>Awake가 아니라 Start입니다.</b> 등록은 각자의 Awake에서 이뤄지는데
        /// GameObject 사이의 Awake 순서는 정해져 있지 않습니다. Unity는 모든 Awake를
        /// 끝낸 뒤에 Start를 부르므로, 여기서 찾으면 전부 등록이 끝난 뒤입니다.
        /// </summary>
        void Start()
        {
            ResolveReferences();
        }

        /// <summary>
        /// 자신이 전역 인스턴스였다면 그 참조를 비웁니다.
        /// </summary>
        void OnDestroy()
        {
            GameContext.Unregister(this);
        }

        /// <summary>
        /// 저장·불러오기 단축키를 받습니다. 오버레이 등으로 입력이 막혀 있으면 무시합니다.
        /// </summary>
        void Update()
        {
            // 개발용 단축키라 GameInput의 행동 목록에 넣지 않았습니다. 대신 게이트는 지킵니다.
            // (오버레이가 떠 있는 동안 F5가 눌리면 그건 오버레이를 조작하던 손입니다)
            if (GameInput.Suspended) return;

            if (GameInput.GetKeyDownRaw(saveKey)) Save();
            if (GameInput.GetKeyDownRaw(loadKey)) Load();
        }

        // --- Public Methods ---

        /// <summary>
        /// 현재 상태를 파일로 저장합니다.
        /// 쓰기에 실패하면 오류만 남기고 게임 진행에는 영향을 주지 않습니다.
        /// </summary>
        [ContextMenu("저장")]
        public void Save()
        {
            SaveData data = Capture();

            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint);
                File.WriteAllText(FilePath, json);

                Debug.Log("SaveSystem: 저장했습니다. " + FilePath);
                if (onSaved != null) onSaved.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError("SaveSystem: 저장에 실패했습니다. " + e.Message, this);
            }
        }

        /// <summary>
        /// 저장된 파일을 읽어 상태를 되돌립니다.
        /// 파일이 없거나 읽지 못하면 아무것도 바꾸지 않고 로그만 남깁니다.
        /// </summary>
        [ContextMenu("불러오기")]
        public void Load()
        {
            if (!HasSave)
            {
                Debug.Log("SaveSystem: 저장된 파일이 없습니다. " + FilePath);
                return;
            }

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogError("SaveSystem: 저장 파일을 읽지 못했습니다. " + e.Message, this);
                return;
            }

            if (data == null)
            {
                Debug.LogError("SaveSystem: 저장 파일이 비어 있습니다.", this);
                return;
            }

            Restore(data);

            Debug.Log("SaveSystem: 불러왔습니다. (" + data.savedAtUtc + ")");
            if (onLoaded != null) onLoaded.Invoke();
        }

        /// <summary>저장 파일을 지웁니다.</summary>
        [ContextMenu("저장 파일 삭제")]
        public void DeleteSave()
        {
            if (!HasSave) return;

            File.Delete(FilePath);
            Debug.Log("SaveSystem: 저장 파일을 지웠습니다.");
        }

        // --- Private Methods : 모으기 ---

        /// <summary>
        /// 각 시스템에서 현재 상태를 모아 저장 데이터 하나로 만듭니다.
        /// </summary>
        /// <returns>저장 시각과 시간·날씨·니즈·플레이어·차량 상태가 담긴 데이터</returns>
        private SaveData Capture()
        {
            SaveData data = new SaveData();
            data.savedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

            // 등록된 시스템들이 각자 자기 몫을 적습니다.
            // 이 메서드는 무엇이 등록되어 있는지 알 필요가 없습니다.
            List<ISaveable> saveables = SaveRegistry.GetOrdered();
            for (int i = 0; i < saveables.Count; i++)
            {
                saveables[i].CaptureInto(data);
            }

            // 씬에 놓인 것들은 등록부를 쓰지 않습니다.
            // 시스템처럼 하나씩 있는 것이 아니라 여럿이고, 서로 순서가 얽혀 있기 때문입니다.
            CapturePlayer(data.player);
            CaptureVehicles(data);

            return data;
        }

        /// <summary>
        /// 플레이어의 탑승 상태, 도보 위치와 시선 방향, 체력을 담습니다.
        /// </summary>
        /// <param name="save">값을 채워 넣을 플레이어 저장 항목</param>
        private void CapturePlayer(PlayerSave save)
        {
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
        /// 씬에 있는 모든 차량의 위치·자세·연료·시동·내구도를 담습니다.
        /// </summary>
        /// <param name="data">차량 목록을 채워 넣을 저장 데이터</param>
        private void CaptureVehicles(SaveData data)
        {
            for (int i = 0; i < Vehicle.All.Count; i++)
            {
                Vehicle v = Vehicle.All[i];
                if (v == null) continue;

                VehicleSave save = new VehicleSave();
                save.name = v.displayName;
                save.position = v.transform.position;
                save.eulerAngles = v.transform.eulerAngles;

                if (v.controller != null)
                {
                    save.fuel = v.controller.CurrentFuel;
                    save.engineOn = v.controller.IsEngineOn;
                }
                if (v.health != null) save.health = v.health.CurrentHealth;

                data.vehicles.Add(save);
            }
        }

        // --- Private Methods : 되돌리기 ---

        /// <summary>
        /// 저장 데이터를 각 시스템에 되돌립니다.
        /// 니즈와 날씨가 시계를 보고 움직이므로 시계를 가장 먼저 맞춥니다.
        /// </summary>
        /// <param name="data">되돌릴 저장 데이터</param>
        private void Restore(SaveData data)
        {
            // 등록된 시스템들이 선언한 순서대로 자기 몫을 되돌립니다.
            //
            // <b>순서가 코드가 아니라 값으로 표현됩니다.</b> 예전에는 이 메서드의 줄 순서가
            // 곧 복원 순서라, 새 시스템이 어디에 끼어야 하는지 읽어 낼 방법이 없었습니다.
            // 지금은 각 시스템이 SaveOrder 로 자기 자리를 밝힙니다. (SaveOrders 참고)
            List<ISaveable> saveables = SaveRegistry.GetOrdered();
            for (int i = 0; i < saveables.Count; i++)
            {
                saveables[i].RestoreFrom(data);
            }

            // 차량을 먼저 제자리에 놓아야, 플레이어가 그 차에 올라탈 수 있습니다.
            RestoreVehicles(data);
            RestorePlayer(data.player);
        }

        /// <summary>
        /// 저장된 차량들을 원래 자리로 되돌립니다.
        /// 위치를 옮기기 전에 Rigidbody의 속도를 비워, 남아 있던 관성이 튀어나오지 않게 합니다.
        /// </summary>
        /// <param name="data">차량 목록이 담긴 저장 데이터</param>
        private void RestoreVehicles(SaveData data)
        {
            if (data.vehicles == null) return;

            for (int i = 0; i < data.vehicles.Count; i++)
            {
                VehicleSave save = data.vehicles[i];
                Vehicle v = FindVehicle(save.name);
                if (v == null) continue;

                // Rigidbody는 위치를 바꿔도 속도가 남으므로 반드시 함께 비웁니다.
                Rigidbody body = v.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                v.transform.SetPositionAndRotation(save.position, Quaternion.Euler(save.eulerAngles));

                if (v.controller != null) v.controller.RestoreState(save.fuel, save.engineOn);
                if (v.health != null) v.health.Revive(save.health);
            }
        }

        /// <summary>
        /// 플레이어의 체력과 도보 위치를 되돌리고, 저장 당시의 탑승 상태를 재현합니다.
        /// </summary>
        /// <param name="save">되돌릴 플레이어 저장 항목</param>
        private void RestorePlayer(PlayerSave save)
        {
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
                Vehicle target = FindVehicle(save.drivingVehicleName);
                if (target != null) modeController.EnterVehicle(target);
                else modeController.EnterVehicle(true);
            }
            else
            {
                modeController.ExitVehicle(true);
            }
        }

        /// <summary>이름으로 차량을 찾습니다. 이름이 비었으면 첫 번째 차량을 돌려줍니다.</summary>
        /// <param name="name">찾을 차량의 표시 이름</param>
        /// <returns>이름이 일치하는 차량. 없으면 첫 번째 차량, 씬에 차량이 하나도 없으면 null입니다.</returns>
        private Vehicle FindVehicle(string name)
        {
            if (Vehicle.All.Count == 0) return null;

            if (!string.IsNullOrEmpty(name))
            {
                for (int i = 0; i < Vehicle.All.Count; i++)
                {
                    if (Vehicle.All[i] != null && Vehicle.All[i].displayName == name) return Vehicle.All[i];
                }
            }

            return Vehicle.All[0];
        }

        /// <summary>
        /// 인스펙터에서 비워 둔 씬 오브젝트 참조를 찾아 채웁니다.
        ///
        /// <b>시스템은 여기에 없습니다.</b> 시간·날씨·니즈·지갑은 각자
        /// <see cref="SaveRegistry"/>에 스스로 등록하므로 이쪽에서 찾을 이유가 없습니다.
        /// 예전에는 시스템을 하나 늘릴 때마다 이 메서드에도 한 줄이 늘었습니다.
        /// </summary>
        private void ResolveReferences()
        {
            if (modeController == null) modeController = GameContext.Resolve<PlayerModeController>(this);

            // PlayerHealth는 플레이어 전용 타입이라 차량 내구도가 잡힐 일이 없습니다.
            if (playerHealth == null) playerHealth = GameContext.Resolve<PlayerHealth>(this);
        }
    }
}
