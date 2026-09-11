using UnityEngine;

namespace CarDrive.Common
{
    /// <summary>
    /// 하루의 <b>시각 구간</b>을 다룹니다.
    ///
    /// <b>왜 한 곳에 모았는가.</b> 같은 규칙이 세 곳에 따로 적혀 있었습니다 —
    /// 가게의 영업 시간(<c>ShopSchedule</c>), 순찰기의 근무 시간(<c>RobotPatrol</c>),
    /// 그리고 점등기의 근무 시간. 셋이 글자까지 같았고, 그런 것은 <b>한쪽만
    /// 고쳐지는 날</b>이 옵니다.
    ///
    /// 시각은 0~24 입니다. 시작이 끝보다 늦으면 <b>자정을 넘긴 구간</b>으로 봅니다 —
    /// 20시에 열어 4시에 닫는 가게가 그렇고, 이 게임은 밤 운전이 본편이라
    /// 그 경우가 오히려 기본입니다.
    /// </summary>
    public static class DayHours
    {
        // --- Public Methods ---

        /// <summary>
        /// 그 시각이 구간 안인가.
        ///
        /// <b>시작과 끝이 같으면 하루 종일입니다.</b> "쉬지 않는다" 를 적는 방법이
        /// 그것뿐이고, 실제로 24시간 영업하는 가게와 쉬지 않는 기계가 그렇게 씁니다.
        ///
        /// <b>시작은 넣고 끝은 뺍니다.</b> 8~18 시 근무라면 18시 정각에는 이미
        /// 퇴근한 것입니다. 그래야 붙어 있는 두 구간이 겹치지 않습니다.
        /// </summary>
        /// <param name="hour">지금 시각 (0~24)</param>
        /// <param name="from">구간이 시작하는 시각</param>
        /// <param name="until">구간이 끝나는 시각</param>
        /// <returns>구간 안이면 true</returns>
        public static bool Within(float hour, float from, float until)
        {
            if (Mathf.Approximately(from, until)) return true;

            if (from < until) return hour >= from && hour < until;

            // 자정을 넘긴 구간입니다.
            return hour >= from || hour < until;
        }
    }
}
