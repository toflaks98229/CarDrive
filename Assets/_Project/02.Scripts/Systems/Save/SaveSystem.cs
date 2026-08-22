using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using CarDrive.Common;

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
    /// <b>차량과 플레이어도 더 이상 예외가 아닙니다.</b> 예전에는 "씬에 여럿 놓이고 순서가
    /// 얽혀 있다"는 이유로 이 클래스가 직접 다뤘는데, 그 때문에 Systems 계층이 Gameplay 계층을
    /// 거꾸로 참조했습니다. 지금은 <c>VehicleSaveParticipant</c>·<c>PlayerSaveParticipant</c>가
    /// 각각 <see cref="ISaveable"/>로 참여하고, 순서는 <see cref="SaveOrders"/>의 값이 정합니다.
    /// <b>이 파일은 이제 어떤 게임플레이 타입도 알지 못합니다.</b>
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

        // --- Private Methods ---

        /// <summary>
        /// 등록된 참여자들에게서 현재 상태를 모아 세이브 자료 하나로 만듭니다.
        ///
        /// <b>이 메서드는 무엇이 등록되어 있는지 알 필요가 없습니다.</b>
        /// </summary>
        /// <returns>저장 시각과 참여자들이 적어 넣은 상태가 담긴 자료</returns>
        private SaveData Capture()
        {
            SaveData data = new SaveData();
            data.savedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

            List<ISaveable> saveables = SaveRegistry.GetOrdered();
            for (int i = 0; i < saveables.Count; i++)
            {
                saveables[i].CaptureInto(data);
            }

            return data;
        }

        /// <summary>
        /// 세이브 자료를 참여자들에게 되돌립니다.
        ///
        /// <b>순서가 코드가 아니라 값으로 표현됩니다.</b> 예전에는 이 메서드의 줄 순서가
        /// 곧 복원 순서라, 새 참여자가 어디에 끼어야 하는지 읽어 낼 방법이 없었습니다.
        /// 지금은 각자가 <see cref="ISaveable.SaveOrder"/>로 자기 자리를 밝힙니다.
        /// (시계 → 날씨 → 니즈 → 지갑 → 차량 → 플레이어. <see cref="SaveOrders"/> 참고)
        /// </summary>
        /// <param name="data">되돌릴 세이브 자료</param>
        private void Restore(SaveData data)
        {
            List<ISaveable> saveables = SaveRegistry.GetOrdered();
            for (int i = 0; i < saveables.Count; i++)
            {
                saveables[i].RestoreFrom(data);
            }
        }
    }
}
