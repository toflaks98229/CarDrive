using System.Collections.Generic;
using UnityEngine;

namespace CarDrive.Systems
{
    /// <summary>
    /// 다음 날씨를 고르는 정책입니다.
    ///
    /// <b>왜 떼어 냈는가.</b> "무엇이 다음에 오는가"는 <see cref="WeatherSystem"/>의 나머지와
    /// 성격이 다릅니다. 전환 진행이나 수치 보간은 물리 법칙처럼 정해진 계산이지만,
    /// 선택은 <b>기획 판단</b>입니다. 밤에 안개가 잦아야 하는지, 폭우 뒤 얼마나 쉬어야 하는지는
    /// 계절이나 지역에 따라 달라질 수 있습니다.
    ///
    /// 그 판단을 한곳에 모아 두면 나중에 <c>SpringWeatherPicker</c>처럼 갈아 끼울 수 있습니다.
    /// 지금은 하나뿐이라 인터페이스를 두지 않았습니다. 둘째가 생길 때 뽑아내면 됩니다.
    ///
    /// 세 가지 규칙이 겹쳐 있습니다.
    ///  1. <b>궂은 날씨 뒤에는 쉬어 갑니다.</b> 폭우가 끝나면 한동안 온화한 것만 옵니다.
    ///  2. <b>같은 날씨가 곧바로 돌아오지 않습니다.</b> 프리셋마다 최소 재등장 간격이 있습니다.
    ///  3. <b>밤에는 무게가 달라집니다.</b> 안개가 새벽에 잦은 것 같은 편향입니다.
    /// </summary>
    public sealed class WeatherPicker
    {
        // --- Private Member Variables ---

        /// <summary>프리셋을 물어볼 곳입니다.</summary>
        private readonly WeatherCatalog _catalog;

        /// <summary>날씨별로 마지막에 끝난 시각입니다. 재등장 간격을 재는 데 씁니다.</summary>
        private readonly Dictionary<WeatherType, float> _lastEndedMinute =
            new Dictionary<WeatherType, float>();

        /// <summary>이 시각까지는 온화한 날씨만 옵니다.</summary>
        private float _calmUntilMinute;

        // --- Public Properties ---

        /// <summary>이 시각까지는 온화한 날씨만 옵니다. 세이브에 담고 되돌립니다.</summary>
        public float CalmUntilMinute
        {
            get { return _calmUntilMinute; }
            set { _calmUntilMinute = value; }
        }

        // --- Constructor ---

        /// <summary>
        /// 프리셋을 물어볼 카탈로그를 받습니다.
        /// </summary>
        /// <param name="catalog">프리셋 조회에 쓸 카탈로그</param>
        public WeatherPicker(WeatherCatalog catalog)
        {
            _catalog = catalog;
        }

        // --- Public Methods ---

        /// <summary>
        /// 지금 조건에서 다음 날씨를 무작위로 고릅니다.
        /// </summary>
        /// <param name="current">지금 날씨. 후보에서 빠집니다.</param>
        /// <param name="nowMinutes">지금까지 흐른 게임 시간(분)</param>
        /// <param name="severeThreshold">이 심각도를 넘으면 '궂은 날씨'로 봅니다.</param>
        /// <param name="isNight">지금이 밤인지 여부</param>
        /// <returns>다음에 올 날씨</returns>
        public WeatherType PickNext(WeatherType current, float nowMinutes, float severeThreshold, bool isNight)
        {
            bool calmOnly = nowMinutes < _calmUntilMinute;

            IReadOnlyList<WeatherPreset> presets = _catalog.Presets;

            float total = 0f;
            for (int i = 0; i < presets.Count; i++)
            {
                total += GetWeight(presets[i], current, nowMinutes, calmOnly, isNight, severeThreshold);
            }

            if (total <= 0f)
            {
                // 전부 걸러졌으면 가장 온화한 날씨로 갑니다.
                return _catalog.FindClosestSeverity(0f, current, current);
            }

            float roll = Random.Range(0f, total);
            for (int i = 0; i < presets.Count; i++)
            {
                float weight = GetWeight(presets[i], current, nowMinutes, calmOnly, isNight, severeThreshold);
                if (roll < weight) return presets[i].type;
                roll -= weight;
            }

            return current;
        }

        /// <summary>
        /// 이 날씨가 방금 끝났음을 기록합니다. 재등장 간격을 재는 기준이 됩니다.
        /// </summary>
        /// <param name="type">끝난 날씨</param>
        /// <param name="nowMinutes">지금까지 흐른 게임 시간(분)</param>
        public void RecordEnded(WeatherType type, float nowMinutes)
        {
            _lastEndedMinute[type] = nowMinutes;
        }

        /// <summary>
        /// 궂은 날씨가 끝났다면 한동안 온화한 날씨만 오도록 잠급니다.
        /// </summary>
        /// <param name="type">방금 자리 잡은 날씨</param>
        /// <param name="nowMinutes">지금까지 흐른 게임 시간(분)</param>
        /// <param name="severeThreshold">이 심각도를 넘으면 '궂은 날씨'로 봅니다.</param>
        /// <param name="calmMinutes">궂은 날씨 뒤 온화하게 유지할 시간(게임 분)</param>
        public void BeginCalmIfSevere(WeatherType type, float nowMinutes,
                                      float severeThreshold, float calmMinutes)
        {
            WeatherPreset preset = _catalog.Get(type);
            if (preset == null || preset.severity < severeThreshold) return;

            _calmUntilMinute = nowMinutes + _catalog.RollDuration(type) + calmMinutes;
        }

        // --- Private Methods ---

        /// <summary>
        /// 이 프리셋이 뽑힐 무게를 구합니다. 0이면 후보에서 빠집니다.
        /// </summary>
        /// <param name="preset">무게를 구할 프리셋</param>
        /// <param name="current">지금 날씨</param>
        /// <param name="nowMinutes">지금까지 흐른 게임 시간(분)</param>
        /// <param name="calmOnly">지금은 온화한 날씨만 허용되는지 여부</param>
        /// <param name="isNight">지금이 밤인지 여부</param>
        /// <param name="severeThreshold">'궂은 날씨'로 보는 심각도 문턱</param>
        /// <returns>뽑힐 무게. 클수록 자주 나옵니다.</returns>
        private float GetWeight(WeatherPreset preset, WeatherType current, float nowMinutes,
                                bool calmOnly, bool isNight, float severeThreshold)
        {
            // 지금과 같은 날씨는 다시 고르지 않습니다.
            if (preset.type == current) return 0f;
            if (preset.weight <= 0f) return 0f;

            // 규칙 1 — 궂은 날씨 직후에는 온화한 것만.
            if (calmOnly && preset.severity >= severeThreshold) return 0f;

            // 규칙 2 — 같은 날씨가 너무 빨리 돌아오지 않게.
            float lastEnded;
            if (_lastEndedMinute.TryGetValue(preset.type, out lastEnded))
            {
                if (nowMinutes - lastEnded < preset.minRepeatGapMinutes) return 0f;
            }

            // 규칙 3 — 밤에는 무게가 달라집니다.
            float weight = preset.weight;
            if (isNight) weight *= Mathf.Max(0f, preset.nightWeightMultiplier);

            return weight;
        }
    }
}
