using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 세이브 파일에 담기는 내용 전체입니다.
///
/// 지형은 담지 않습니다. WorldStreamer가 시드로 배치를 고정하므로
/// 같은 시드면 언제나 같은 세계가 다시 깔립니다. 저장할 이유가 없습니다.
/// (그래서 이 스키마가 이만큼 작습니다)
/// </summary>
[System.Serializable]
public class SaveData
{
    [Tooltip("세이브 형식 번호. 나중에 항목이 바뀌면 이 값으로 구분합니다.")]
    public int version = 1;

    [Tooltip("저장한 실제 시각 (표시용)")]
    public string savedAtUtc = "";

    public TimeSave time = new TimeSave();
    public WeatherSave weather = new WeatherSave();

    [Tooltip("니즈 6종의 수치")]
    public List<NeedState> needs = new List<NeedState>();

    public PlayerSave player = new PlayerSave();

    [Tooltip("씬에 있는 차량들의 상태")]
    public List<VehicleSave> vehicles = new List<VehicleSave>();
}

/// <summary>시계 상태입니다.</summary>
[System.Serializable]
public class TimeSave
{
    public int day = 1;
    public float minuteOfDay = 480f;
}

/// <summary>
/// 날씨 상태입니다. 전환 중간에 저장해도 이어서 진행되도록
/// 진행도와 남은 유지 시간까지 담습니다.
/// </summary>
[System.Serializable]
public class WeatherSave
{
    public WeatherType current = WeatherType.Clear;
    public WeatherType target = WeatherType.Clear;
    public WeatherType finalTarget = WeatherType.Clear;
    public float blend;
    public float currentIntensity = 1f;
    public float targetIntensity = 1f;
    public float transitionMinutes;
    public float transitionElapsed;
    public float holdRemaining;
    public float calmUntilMinute;
}

/// <summary>플레이어 상태입니다.</summary>
[System.Serializable]
public class PlayerSave
{
    [Tooltip("도보 리그의 위치·회전. 차에 타고 있었어도 마지막 위치를 남겨 둡니다.")]
    public Vector3 footPosition;
    public float footYaw;

    [Tooltip("차에 타고 있었는지")]
    public bool wasDriving;

    [Tooltip("타고 있던 차량의 이름 (여러 대일 때 어느 차였는지 구분)")]
    public string drivingVehicleName = "";

    public float health = 100f;
}

/// <summary>차량 한 대의 상태입니다.</summary>
[System.Serializable]
public class VehicleSave
{
    [Tooltip("차량을 구분할 이름. Vehicle.displayName을 씁니다.")]
    public string name = "";

    public Vector3 position;
    public Vector3 eulerAngles;

    public float fuel;
    public float health;
    public bool engineOn;
}
