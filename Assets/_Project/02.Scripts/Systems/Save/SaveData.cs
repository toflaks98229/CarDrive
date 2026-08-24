using System.Collections.Generic;
using UnityEngine;

namespace CarDrive.Systems
{
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
        /// <summary>
        /// 세이브 형식 번호입니다.
        ///
        /// 나중에 항목이 바뀌면 이 값으로 옛 파일을 구분해 옮겨 실을 수 있습니다.
        /// 항목을 지우거나 뜻을 바꿀 때만 올리면 됩니다.
        /// </summary>
        [Tooltip("세이브 형식 번호. 나중에 항목이 바뀌면 이 값으로 구분합니다.")]
        public int version = 1;

        /// <summary>저장한 실제 시각(UTC)입니다. 표시용이며 복원에는 쓰이지 않습니다.</summary>
        [Tooltip("저장한 실제 시각 (표시용)")]
        public string savedAtUtc = "";

        /// <summary>게임 내 시계 상태입니다.</summary>
        public TimeSave time = new TimeSave();

        /// <summary>날씨 상태입니다. 전환 중간이었다면 그 진행 상황까지 담깁니다.</summary>
        public WeatherSave weather = new WeatherSave();

        /// <summary>니즈 6종의 현재 수치입니다.</summary>
        [Tooltip("니즈 6종의 수치")]
        public List<NeedState> needs = new List<NeedState>();

        /// <summary>보유 재화입니다. 돈과 엑토플라즘이 각각 한 항목으로 들어갑니다.</summary>
        [Tooltip("보유 재화 (돈·엑토플라즘)")]
        public List<CurrencyState> wallet = new List<CurrencyState>();

        /// <summary>플레이어의 위치·체력·탑승 상태입니다.</summary>
        public PlayerSave player = new PlayerSave();

        /// <summary>씬에 있는 차량 전부의 상태입니다. 한 대에 <see cref="VehicleSave"/> 하나가 대응합니다.</summary>
        [Tooltip("씬에 있는 차량들의 상태")]
        public List<VehicleSave> vehicles = new List<VehicleSave>();

        /// <summary>
        /// 월드에 놓였거나 손에 들렸거나 차에 실린 물건들입니다.
        ///
        /// <b>목록이 무한히 길어지지 않습니다.</b> 놓인 물건은 하루가 지나면 사라지므로
        /// (<c>ItemDecay</c>) 여기 담기는 것은 <b>최근 하루치</b>와 보관 중인 것뿐입니다.
        /// 지형을 저장하지 않는 것과 같은 이유로, 스스로 정리되는 것은 저장하지 않아도 됩니다.
        /// </summary>
        [Tooltip("월드·손·차에 있던 물건들. 놓인 것은 하루가 지나면 사라지므로 목록이 무한히 늘지 않습니다.")]
        public List<ItemSave> items = new List<ItemSave>();
    }

    /// <summary>물건 하나의 상태입니다.</summary>
    [System.Serializable]
    public class ItemSave
    {
        /// <summary>무엇이었는지 알려 주는 이름표입니다. <c>ItemCatalog</c> 가 이것으로 프리팹을 되찾습니다.</summary>
        [Tooltip("무엇이었는지 알려 주는 이름표. ItemCatalog 의 항목과 짝입니다.")]
        public string id = "";

        /// <summary>월드 위치입니다.</summary>
        public Vector3 position;

        /// <summary>회전입니다. 쿼터니언 대신 오일러 각으로 담아 파일에서 읽기 쉽게 둡니다.</summary>
        public Vector3 eulerAngles;

        /// <summary>
        /// 어디에 있었는지입니다. 0=놓임, 1=들고 있음, 2=차에 실림.
        ///
        /// <b>왜 숫자인가.</b> 이 뜻을 담은 열거형(<c>ItemPlacement</c>)은 Gameplay 층에 있고,
        /// 이 파일은 Systems 층이라 그것을 알 수 없습니다. <see cref="SaveData"/> 가
        /// 순수 DTO 로 남으려면 여기서는 숫자여야 합니다.
        /// </summary>
        [Tooltip("0=놓임, 1=들고 있음, 2=차에 실림")]
        public int placement;

        /// <summary>차에 실려 있었다면 그 차의 이름입니다. 아니면 빈 문자열입니다.</summary>
        [Tooltip("차에 실려 있었다면 그 차의 이름")]
        public string vehicleName = "";

        /// <summary>
        /// 월드에 놓인 시각(첫날 0시부터의 게임 내 분)입니다. 놓여 있지 않았으면 -1입니다.
        /// 불러온 뒤에도 <b>수명이 이어서</b> 흐르게 하는 값입니다.
        /// </summary>
        [Tooltip("월드에 놓인 시각(게임 내 분). 놓여 있지 않았으면 -1")]
        public float looseSinceMinute = -1f;

        /// <summary>봉투라면 그 안에 담긴 것들의 이름표입니다. 담은 차례 그대로입니다.</summary>
        [Tooltip("봉투라면 담긴 것들의 이름표. 담은 차례 그대로입니다.")]
        public List<string> bagContents = new List<string>();

        /// <summary>
        /// 상자라면 그 안에 남은 병의 수입니다. 상자가 아니면 -1입니다.
        ///
        /// <b>왜 개수만 적는가.</b> 상자 속 병은 전부 같은 것이고 자리도 프리팹이 정합니다.
        /// 하나하나 적으면 파일만 길어지고 되돌릴 때 달라지는 것이 없습니다.
        /// (봉투는 서로 다른 물건이 섞이므로 <see cref="bagContents"/> 로 종류를 적습니다)
        /// </summary>
        [Tooltip("상자라면 남은 병의 수. 상자가 아니면 -1")]
        public int containedCount = -1;
    }

    /// <summary>시계 상태입니다.</summary>
    [System.Serializable]
    public class TimeSave
    {
        /// <summary>1일차부터 세는 경과 일수입니다.</summary>
        public int day = 1;

        /// <summary>
        /// 자정부터 흐른 분입니다. 0이 00:00, 1440이 하루입니다.
        ///
        /// 기본값 480은 아침 8시입니다. 새 게임이 시작되는 시각입니다.
        /// </summary>
        public float minuteOfDay = 480f;
    }

    /// <summary>
    /// 날씨 상태입니다. 전환 중간에 저장해도 이어서 진행되도록
    /// 진행도와 남은 유지 시간까지 담습니다.
    /// </summary>
    [System.Serializable]
    public class WeatherSave
    {
        /// <summary>전환의 <b>출발</b> 날씨입니다. 전환 중이 아니면 지금 날씨 그 자체입니다.</summary>
        public WeatherType current = WeatherType.Clear;

        /// <summary>전환의 <b>도착</b> 날씨입니다. 지금 섞여 들어오고 있는 쪽입니다.</summary>
        public WeatherType target = WeatherType.Clear;

        /// <summary>
        /// 여러 단계를 거쳐 최종적으로 가려는 날씨입니다.
        ///
        /// 맑음에서 폭풍으로 한 번에 건너뛰지 않고 흐림을 거칠 때,
        /// <see cref="target"/>은 흐림이지만 이 값은 폭풍으로 남습니다.
        /// </summary>
        public WeatherType finalTarget = WeatherType.Clear;

        /// <summary><see cref="current"/>에서 <see cref="target"/>으로 섞인 정도입니다. 0~1입니다.</summary>
        public float blend;

        /// <summary><see cref="current"/> 날씨의 세기입니다. 같은 비라도 이 값으로 굵기가 달라집니다.</summary>
        public float currentIntensity = 1f;

        /// <summary><see cref="target"/> 날씨의 세기입니다.</summary>
        public float targetIntensity = 1f;

        /// <summary>이번 전환에 배정된 전체 시간(게임 내 분)입니다.</summary>
        public float transitionMinutes;

        /// <summary>이번 전환이 지금까지 흐른 시간(게임 내 분)입니다.</summary>
        public float transitionElapsed;

        /// <summary>전환이 끝난 뒤 이 날씨를 유지할 남은 시간(게임 내 분)입니다.</summary>
        public float holdRemaining;

        /// <summary>
        /// 이 시각(게임 내 분)까지는 궂은 날씨를 뽑지 않습니다.
        ///
        /// 게임을 막 시작했거나 폭풍이 지나간 직후에 연달아 폭풍이 오지 않도록 두는 유예입니다.
        /// </summary>
        public float calmUntilMinute;
    }

    /// <summary>플레이어 상태입니다.</summary>
    [System.Serializable]
    public class PlayerSave
    {
        /// <summary>
        /// 도보 리그의 위치입니다.
        ///
        /// 차에 타고 있었어도 마지막으로 서 있던 자리를 남겨 둡니다.
        /// 불러온 뒤 차가 사라져 있어도 설 자리가 있어야 하기 때문입니다.
        /// </summary>
        [Tooltip("도보 리그의 위치·회전. 차에 타고 있었어도 마지막 위치를 남겨 둡니다.")]
        public Vector3 footPosition;

        /// <summary>도보 리그가 바라보던 방향(Y축 회전, 도)입니다.</summary>
        public float footYaw;

        /// <summary>저장 시점에 차에 타고 있었는지 여부입니다.</summary>
        [Tooltip("차에 타고 있었는지")]
        public bool wasDriving;

        /// <summary>타고 있던 차량의 이름입니다. 차가 여러 대일 때 어느 차였는지 구분합니다.</summary>
        [Tooltip("타고 있던 차량의 이름 (여러 대일 때 어느 차였는지 구분)")]
        public string drivingVehicleName = "";

        /// <summary>플레이어의 남은 체력입니다.</summary>
        public float health = 100f;
    }

    /// <summary>차량 한 대의 상태입니다.</summary>
    [System.Serializable]
    public class VehicleSave
    {
        /// <summary>
        /// 차량을 구분할 이름입니다. <c>Vehicle.displayName</c>을 씁니다.
        ///
        /// 불러올 때 이 이름으로 씬의 차량과 짝을 맞춥니다.
        /// </summary>
        [Tooltip("차량을 구분할 이름. Vehicle.displayName을 씁니다.")]
        public string name = "";

        /// <summary>차량의 월드 위치입니다.</summary>
        public Vector3 position;

        /// <summary>차량의 회전입니다. 쿼터니언 대신 오일러 각으로 담아 파일에서 읽기 쉽게 둡니다.</summary>
        public Vector3 eulerAngles;

        /// <summary>연료통에 남은 연료입니다.</summary>
        public float fuel;

        /// <summary>차량의 남은 내구도입니다.</summary>
        public float health;

        /// <summary>저장 시점에 시동이 걸려 있었는지 여부입니다.</summary>
        public bool engineOn;
    }
}
