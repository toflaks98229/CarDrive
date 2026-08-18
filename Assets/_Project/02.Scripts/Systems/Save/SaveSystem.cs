using System;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 게임 상태를 파일로 저장하고 되돌립니다.
///
/// 각 시스템이 자기 상태를 Capture/Restore로 내주고 받으므로,
/// 이 클래스는 그것들을 모아 JSON으로 쓰고 읽는 일만 합니다.
/// 새 시스템이 생기면 여기에 두 줄만 추가하면 됩니다.
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

    /// <summary>날짜와 시각을 저장·복원할 시간 시스템입니다. 비워두면 Awake에서 씬을 검색합니다.</summary>
    [Header("연동 (비워두면 씬에서 찾습니다)")]
    [Tooltip("날짜와 시각을 저장·복원할 시간 시스템")]
    public TimeSystem timeSystem;

    /// <summary>날씨와 전환 진행도를 저장·복원할 날씨 시스템입니다. 비워두면 Awake에서 씬을 검색합니다.</summary>
    [Tooltip("날씨 상태를 저장·복원할 날씨 시스템")]
    public WeatherSystem weatherSystem;

    /// <summary>니즈 수치를 저장·복원할 니즈 시스템입니다. 비워두면 Awake에서 씬을 검색합니다.</summary>
    [Tooltip("니즈 수치를 저장·복원할 니즈 시스템")]
    public NeedsSystem needsSystem;

    /// <summary>탑승 상태와 도보 위치를 저장·복원할 컨트롤러입니다. 비워두면 Awake에서 씬을 검색합니다.</summary>
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
    public static SaveSystem Instance { get; private set; }

    /// <summary>저장 파일의 전체 경로입니다.</summary>
    public string FilePath { get { return Path.Combine(Application.persistentDataPath, fileName); } }

    /// <summary>저장된 파일이 있는지 여부입니다.</summary>
    public bool HasSave { get { return File.Exists(FilePath); } }

    // --- Unity Event Functions ---

    /// <summary>
    /// 자신을 전역 인스턴스로 등록하고 비어 있는 시스템 참조를 채웁니다.
    /// 이미 다른 인스턴스가 있으면 경고를 남기고 자신을 끕니다.
    /// </summary>
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("SaveSystem: 씬에 두 개 이상 존재합니다. 나중 것을 비활성화합니다.", this);
            enabled = false;
            return;
        }
        Instance = this;

        ResolveReferences();
    }

    /// <summary>
    /// 자신이 전역 인스턴스였다면 그 참조를 비웁니다.
    /// </summary>
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 저장·불러오기 단축키를 받습니다. 오버레이 등으로 입력이 막혀 있으면 무시합니다.
    /// </summary>
    void Update()
    {
        if (GameInputGate.Suspended) return;

        if (saveKey != KeyCode.None && Input.GetKeyDown(saveKey)) Save();
        if (loadKey != KeyCode.None && Input.GetKeyDown(loadKey)) Load();
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

        if (timeSystem != null)
        {
            data.time.day = timeSystem.Day;
            data.time.minuteOfDay = timeSystem.MinuteOfDay;
        }

        if (weatherSystem != null) data.weather = weatherSystem.CaptureState();
        if (needsSystem != null) data.needs = needsSystem.CaptureState();

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
                save.fuel = v.controller.GetCurrentFuel();
                save.engineOn = v.controller.IsEngineOn();
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
        // 시계를 먼저 맞춥니다. 니즈와 날씨가 이 시계를 보고 움직이기 때문입니다.
        if (timeSystem != null) timeSystem.RestoreClock(data.time.day, data.time.minuteOfDay);
        if (weatherSystem != null) weatherSystem.RestoreState(data.weather);
        if (needsSystem != null) needsSystem.RestoreState(data.needs);

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
    /// 인스펙터에서 비워 둔 시스템 참조를 씬에서 찾아 채웁니다.
    /// </summary>
    private void ResolveReferences()
    {
        if (timeSystem == null) timeSystem = FindAnyObjectByType<TimeSystem>();
        if (weatherSystem == null) weatherSystem = FindAnyObjectByType<WeatherSystem>();
        if (needsSystem == null) needsSystem = FindAnyObjectByType<NeedsSystem>();
        if (modeController == null) modeController = FindAnyObjectByType<PlayerModeController>();

        // PlayerHealth는 플레이어 전용 타입이라 차량 내구도가 잡힐 일이 없습니다.
        if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();
    }
}
