using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 길가 등이 <b>죽었는지 살았는지</b>를 담고 되돌립니다.
    ///
    /// <b>왜 필요했는가.</b> <see cref="LampLighter"/> 가 밤새 걸어 다니며 등을
    /// 되살리는데, 그것이 세이브에 없으면 <b>불러오기 한 번에 전부 도로 죽습니다.</b>
    /// 그러면 그 기계가 한 일이 없던 일이 되고, 플레이어가 지켜 준 밤도 없던 일이
    /// 됩니다 — 쓰러뜨린 로봇이 쓰러진 채 남아야 하는 것과 같은 이유입니다.
    ///
    /// <b>자리도 켜짐도 담지 않습니다.</b> 자리는 배치 시드가 정하고 켜짐은 시각이
    /// 정합니다. 담을 것은 <b>죽었는가</b> 하나뿐입니다 — 지형을 담지 않는 것과
    /// 같은 이유로, 스스로 정해지는 것은 저장하지 않습니다.
    ///
    /// <b>불러오기는 씬을 다시 깔지 않습니다.</b> 그래서 되돌리는 일은 "만들기" 가
    /// 아니라 <b>맞추기</b>입니다 — 이미 서 있는 등의 상태만 고쳐 씁니다.
    /// </summary>
    [AddComponentMenu("CarDrive/등 세이브 참여자 (LampSaveParticipant)")]
    public class LampSaveParticipant : MonoBehaviour, ISaveable
    {
        // --- Public Properties ---

        /// <summary>담고 되돌리는 순서입니다.</summary>
        public int SaveOrder { get { return SaveOrders.Lamps; } }

        // --- Unity Event Functions ---

        void Awake()
        {
            SaveRegistry.Register(this);
        }

        void OnDestroy()
        {
            SaveRegistry.Unregister(this);
        }

        // --- Public Methods ---

        /// <summary>세계에 선 등을 전부 적습니다.</summary>
        /// <param name="data">적어 넣을 세이브 자료</param>
        public void CaptureInto(SaveData data)
        {
            if (data == null) return;

            data.lamps.Clear();

            for (int i = 0; i < StreetLamp.All.Count; i++)
            {
                StreetLamp lamp = StreetLamp.All[i];
                if (lamp == null) continue;

                data.lamps.Add(new LampSave
                {
                    name = lamp.name,
                    broken = lamp.broken,
                });
            }
        }

        /// <summary>
        /// 적힌 대로 등을 되돌립니다.
        ///
        /// ⚠ <b>목록에 없는 등은 건드리지 않습니다.</b> 옛 세이브를 불러오거나 배치
        /// 시드를 바꾼 뒤라면 짝이 안 맞는 등이 생기는데, 그것을 전부 죽이거나 전부
        /// 살리면 <b>세이브에 없던 결정</b>을 이쪽이 내리는 셈이 됩니다. 놓인 그대로 둡니다.
        /// </summary>
        /// <param name="data">읽어 올 세이브 자료</param>
        public void RestoreFrom(SaveData data)
        {
            if (data == null || data.lamps == null) return;

            int matched = 0;

            for (int i = 0; i < data.lamps.Count; i++)
            {
                LampSave save = data.lamps[i];
                if (save == null || string.IsNullOrEmpty(save.name)) continue;

                StreetLamp lamp = Find(save.name);
                if (lamp == null) continue;

                matched++;

                // ⚠ <b>Break/Relight 로 넘깁니다.</b> 필드를 직접 쓰면 이미 타고 있던
                // 등이 죽은 채로 계속 빛납니다 — 끄는 일은 그 메서드가 합니다.
                if (save.broken) lamp.Break();
                else lamp.Relight();
            }

            if (matched == data.lamps.Count) return;

            Debug.LogWarning("LampSaveParticipant: 세이브의 등 " + data.lamps.Count
                             + " 개 중 " + matched + " 개만 짝을 찾았습니다. "
                             + "배치 시드가 바뀌었는지 보십시오.");
        }

        // --- Private Methods ---

        /// <summary>그 이름의 등입니다.</summary>
        /// <param name="name">찾을 이름</param>
        private static StreetLamp Find(string name)
        {
            for (int i = 0; i < StreetLamp.All.Count; i++)
            {
                StreetLamp lamp = StreetLamp.All[i];
                if (lamp != null && lamp.name == name) return lamp;
            }

            return null;
        }
    }
}
