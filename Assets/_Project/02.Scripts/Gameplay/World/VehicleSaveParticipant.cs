using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 씬에 놓인 모든 차량의 위치·자세·연료·시동·내구도를 세이브에 담고 되돌립니다.
    ///
    /// <b>왜 SaveSystem에서 떼어 냈는가.</b> <see cref="SaveSystem"/>은 "등록된 것을 순서대로 훑기만"
    /// 하도록 만들어졌고, 그 덕분에 시스템을 늘려도 그 파일을 고치지 않습니다. 그런데
    /// <b>차량과 플레이어만은 예외로 그 안에 직접 박혀 있었습니다.</b> 그래서 두 가지가 걸렸습니다.
    ///
    ///  1. Systems 계층이 <see cref="Vehicle"/>(Gameplay)을 알아야 했습니다. 어셈블리를
    ///     나누는 순간 그 참조가 역방향 순환이 됩니다.
    ///  2. 개방-폐쇄가 절반만 이뤄져 있었습니다. "차량은 여럿이라 다르다"는 이유였지만,
    ///     <b>여럿을 한 참여자가 담당하면</b> 그 예외가 필요 없습니다.
    ///
    /// 이제 <see cref="SaveSystem"/>은 차량을 모릅니다.
    ///
    /// <b>순서가 중요합니다.</b> <see cref="SaveOrders.Vehicles"/>는 플레이어보다 앞입니다.
    /// 차를 제자리에 놓아야 플레이어가 그 차에 올라탈 수 있습니다.
    /// </summary>
    public class VehicleSaveParticipant : MonoBehaviour, ISaveable
    {
        // --- Unity Event Functions ---

        /// <summary>세이브 등록부에 자신을 넣습니다.</summary>
        void Awake()
        {
            SaveRegistry.Register(this);
        }

        /// <summary>세이브 등록부에서 자신을 뺍니다.</summary>
        void OnDestroy()
        {
            SaveRegistry.Unregister(this);
        }

        // --- ISaveable ---

        /// <summary>지갑 다음, 플레이어보다 먼저입니다.</summary>
        public int SaveOrder { get { return SaveOrders.Vehicles; } }

        /// <summary>
        /// 씬에 있는 모든 차량의 상태를 담습니다.
        /// </summary>
        /// <param name="data">차량 목록을 채워 넣을 세이브 자료</param>
        public void CaptureInto(SaveData data)
        {
            data.vehicles.Clear();

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

        /// <summary>
        /// 저장된 차량들을 원래 자리로 되돌립니다.
        /// 위치를 옮기기 전에 Rigidbody의 속도를 비워, 남아 있던 관성이 튀어나오지 않게 합니다.
        /// </summary>
        /// <param name="data">차량 목록이 담긴 세이브 자료</param>
        public void RestoreFrom(SaveData data)
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

        // --- Public Methods ---

        /// <summary>
        /// 이름으로 차량을 찾습니다.
        ///
        /// <b>이름이 안 맞아도 첫 번째 차량으로 물러섭니다.</b> 차 이름을 바꾼 뒤 옛 세이브를
        /// 열었을 때 불러오기가 통째로 실패하는 것보다, 엉뚱한 차라도 제자리에 놓이는 편이 낫습니다.
        /// </summary>
        /// <param name="name">찾을 차량의 표시 이름</param>
        /// <returns>이름이 같은 차량. 없으면 첫 번째 차량, 씬에 차량이 없으면 null입니다.</returns>
        public static Vehicle FindVehicle(string name)
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
    }
}
