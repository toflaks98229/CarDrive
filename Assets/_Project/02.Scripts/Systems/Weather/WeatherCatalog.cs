using System.Collections.Generic;
using UnityEngine;

namespace CarDrive.Systems
{
    /// <summary>
    /// 날씨 프리셋에 대해 <b>묻는 곳</b>입니다. 상태를 갖지 않고 표만 들고 있습니다.
    ///
    /// <b>왜 떼어 냈는가.</b> "이 날씨의 심각도는?", "이 심각도에 가장 가까운 날씨는?",
    /// "이번 강도는 얼마?" 같은 물음이 <see cref="WeatherSystem"/> 안에 흩어져 있었습니다.
    /// 셋 다 프리셋 표만 있으면 답할 수 있는 것인데, 전환·선택 로직과 뒤섞여 있어서
    /// 어느 것이 상태를 바꾸고 어느 것이 그냥 조회인지 구분되지 않았습니다.
    ///
    /// 이 클래스는 <b>아무것도 바꾸지 않습니다.</b> 전환기와 선택기가 함께 씁니다.
    /// </summary>
    public sealed class WeatherCatalog
    {
        // --- Private Member Variables ---

        /// <summary>날씨 종류로 프리셋을 바로 찾기 위한 표입니다.</summary>
        private readonly Dictionary<WeatherType, WeatherPreset> _lookup =
            new Dictionary<WeatherType, WeatherPreset>();

        /// <summary>이번 실행에 쓸 프리셋 목록입니다. 빠진 것은 기본값으로 메워집니다.</summary>
        private List<WeatherPreset> _presets;

        // --- Public Properties ---

        /// <summary>이번 실행에 쓸 프리셋 목록입니다.</summary>
        public IReadOnlyList<WeatherPreset> Presets { get { return _presets; } }

        // --- Public Methods ---

        /// <summary>
        /// 프리셋 표를 짭니다. 비어 있으면 기본값을 쓰고, 빠진 날씨는 기본값으로 메웁니다.
        /// </summary>
        /// <param name="authored">인스펙터에 적어 둔 프리셋. 비어 있으면 기본값만 씁니다.</param>
        /// <param name="context">경고를 클릭했을 때 선택될 대상</param>
        /// <returns>메워진 뒤의 프리셋 목록. 호출부가 인스펙터에 돌려주면 됩니다.</returns>
        public List<WeatherPreset> Build(List<WeatherPreset> authored, Object context)
        {
            _presets = (authored != null && authored.Count > 0)
                ? authored
                : WeatherDefaults.CreatePresets();

            _lookup.Clear();
            for (int i = 0; i < _presets.Count; i++)
            {
                _lookup[_presets[i].type] = _presets[i];
            }

            List<WeatherPreset> fallback = WeatherDefaults.CreatePresets();
            for (int i = 0; i < fallback.Count; i++)
            {
                if (_lookup.ContainsKey(fallback[i].type)) continue;

                Debug.LogWarning("WeatherSystem: " + fallback[i].type + " 프리셋이 없어 기본값을 씁니다.", context);
                _presets.Add(fallback[i]);
                _lookup[fallback[i].type] = fallback[i];
            }

            return _presets;
        }

        /// <summary>
        /// 날씨 종류에 해당하는 프리셋을 돌려줍니다.
        /// </summary>
        /// <param name="type">찾을 날씨</param>
        /// <returns>프리셋. 없으면 null입니다.</returns>
        public WeatherPreset Get(WeatherType type)
        {
            WeatherPreset preset;
            return _lookup.TryGetValue(type, out preset) ? preset : null;
        }

        /// <summary>
        /// 이 날씨의 심각도를 돌려줍니다. 전환 경로를 계산하는 기준입니다.
        /// </summary>
        /// <param name="type">확인할 날씨</param>
        /// <returns>0(맑음)에서 1(폭우) 사이의 심각도. 프리셋이 없으면 0입니다.</returns>
        public float GetSeverity(WeatherType type)
        {
            WeatherPreset preset = Get(type);
            return preset != null ? preset.severity : 0f;
        }

        /// <summary>
        /// 원하는 심각도에 가장 가까운 날씨를 찾습니다. 중간 기착지를 고를 때 씁니다.
        /// </summary>
        /// <param name="wanted">원하는 심각도</param>
        /// <param name="exclude">제외할 날씨. 지금 날씨를 다시 고르지 않기 위해서입니다.</param>
        /// <param name="allowAnyway">제외 대상이어도 허용할 날씨. 후보가 없을 때의 답이기도 합니다.</param>
        /// <returns>가장 가까운 심각도의 날씨</returns>
        public WeatherType FindClosestSeverity(float wanted, WeatherType exclude, WeatherType allowAnyway)
        {
            WeatherType best = allowAnyway;
            float bestDiff = float.MaxValue;

            for (int i = 0; i < _presets.Count; i++)
            {
                WeatherPreset preset = _presets[i];
                if (preset.type == exclude && preset.type != allowAnyway) continue;

                float diff = Mathf.Abs(preset.severity - wanted);
                if (diff >= bestDiff) continue;

                bestDiff = diff;
                best = preset.type;
            }

            return best;
        }

        /// <summary>
        /// 이번에 쓸 강도를 무작위로 뽑습니다. 같은 비라도 올 때마다 세기가 다릅니다.
        /// </summary>
        /// <param name="type">강도를 뽑을 날씨</param>
        /// <returns>프리셋이 정한 범위 안의 강도. 프리셋이 없으면 1입니다.</returns>
        public float RollIntensity(WeatherType type)
        {
            WeatherPreset preset = Get(type);
            if (preset == null) return 1f;

            float min = Mathf.Min(preset.minIntensity, preset.maxIntensity);
            float max = Mathf.Max(preset.minIntensity, preset.maxIntensity);
            return Random.Range(min, max);
        }

        /// <summary>
        /// 이번에 이 날씨가 얼마나 이어질지 무작위로 뽑습니다.
        /// </summary>
        /// <param name="type">지속 시간을 뽑을 날씨</param>
        /// <returns>게임 내 분. 프리셋이 없으면 120분입니다.</returns>
        public float RollDuration(WeatherType type)
        {
            WeatherPreset preset = Get(type);
            return preset != null
                ? Random.Range(preset.minDurationMinutes, preset.maxDurationMinutes)
                : 120f;
        }
    }
}
