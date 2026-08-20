using UnityEngine;

namespace CarDrive.Systems
{
    /// <summary>
    /// 두 날씨 사이의 표현·영향 수치를 섞습니다.
    ///
    /// <b>왜 떼어 냈는가.</b> 이 계산에는 상태가 없습니다. 두 프리셋과 진행도를 넣으면
    /// 열여섯 개의 수치가 나올 뿐입니다. 그런데 <see cref="WeatherSystem"/> 안에 있으면
    /// 전환 타이머·선택 규칙과 뒤엉켜, "지금 비의 세기가 왜 이 값인가"를 따라가기 어려웠습니다.
    ///
    /// <b>세 가지 속도로 섞습니다.</b> 전부 같은 속도로 변하면 하늘이 통째로 스위치처럼 바뀝니다.
    ///  - <b>구름·어두워짐·바람</b>은 앞쪽에서 먼저 끝납니다. 하늘이 먼저 무거워져야 합니다.
    ///  - <b>비·씻김</b>은 뒤늦게 시작합니다. 흐려진 다음에야 빗방울이 떨어집니다.
    ///  - <b>나머지</b>는 고르게 섞입니다.
    /// </summary>
    public sealed class WeatherValueBlender
    {
        // --- Public Properties : 표현 ---

        /// <summary>구름이 하늘을 덮은 정도(0~1)입니다.</summary>
        public float CloudCover { get; private set; }

        /// <summary>비의 세기입니다. 0이면 비가 오지 않습니다.</summary>
        public float RainIntensity { get; private set; }

        /// <summary>안개의 짙기(0~1)입니다.</summary>
        public float FogDensity { get; private set; }

        /// <summary>바람의 세기(0~1)입니다.</summary>
        public float WindStrength { get; private set; }

        /// <summary>날씨 때문에 어두워진 정도(0~1)입니다.</summary>
        public float Darkness { get; private set; }

        /// <summary>지금 날씨의 강도(0~1)입니다.</summary>
        public float Intensity { get; private set; }

        // --- Public Properties : 불이익 ---

        /// <summary>시야 배율입니다. 1보다 작으면 잘 보이지 않습니다.</summary>
        public float VisibilityMultiplier { get; private set; }

        /// <summary>노면 미끄러움입니다. 1보다 크면 접지력이 떨어집니다.</summary>
        public float RoadSlipperiness { get; private set; }

        /// <summary>연료 소모 배율입니다.</summary>
        public float FuelConsumptionMultiplier { get; private set; }

        /// <summary>초당 더러움 변화입니다. 음수면 비에 씻깁니다.</summary>
        public float HygieneChangePerSecond { get; private set; }

        /// <summary>초당 오르는 스트레스입니다.</summary>
        public float StressPerSecond { get; private set; }

        /// <summary>귀신 활동량 배율입니다.</summary>
        public float GhostActivity { get; private set; }

        // --- Public Properties : 이익 ---

        /// <summary>귀신을 알아채는 거리 배율입니다.</summary>
        public float GhostDetectionMultiplier { get; private set; }

        /// <summary>수면 회복 배율입니다. 빗소리를 들으면 더 잘 쉽니다.</summary>
        public float SleepQualityMultiplier { get; private set; }

        /// <summary>초당 줄어드는 갈증입니다. 비를 맞으면 조금 해소됩니다.</summary>
        public float ThirstReliefPerSecond { get; private set; }

        /// <summary>초당 줄어드는 스트레스입니다.</summary>
        public float StressReliefPerSecond { get; private set; }

        // --- Public Methods ---

        /// <summary>
        /// 두 날씨를 섞어 모든 수치를 다시 계산합니다.
        /// </summary>
        /// <param name="from">지금 날씨의 프리셋. null이면 아무것도 하지 않습니다.</param>
        /// <param name="fromIntensity">지금 날씨의 강도</param>
        /// <param name="to">전환 중인 목표 날씨의 프리셋. null이면 <paramref name="from"/>을 씁니다.</param>
        /// <param name="toIntensity">목표 날씨의 강도</param>
        /// <param name="blend">전환 진행도(0~1)</param>
        /// <param name="cloudLeadPortion">구름이 다 끼는 시점. 0.45면 전환의 45% 지점입니다.</param>
        /// <param name="rainStartPortion">비가 시작되는 시점. 0.35면 35% 지점부터 내립니다.</param>
        public void Blend(WeatherPreset from, float fromIntensity,
                          WeatherPreset to, float toIntensity,
                          float blend, float cloudLeadPortion, float rainStartPortion)
        {
            if (from == null) return;
            if (to == null) to = from;

            // 앞서 끝나는 것 / 늦게 시작하는 것 / 고르게 가는 것, 세 가지 진행도를 만듭니다.
            float cloudT = Mathf.SmoothStep(
                0f, 1f, Mathf.InverseLerp(0f, Mathf.Max(0.01f, cloudLeadPortion), blend));
            float rainT = Mathf.SmoothStep(
                0f, 1f, Mathf.InverseLerp(Mathf.Min(0.99f, rainStartPortion), 1f, blend));
            float midT = Mathf.SmoothStep(0f, 1f, blend);

            BlendAppearance(from, fromIntensity, to, toIntensity, cloudT, rainT, midT);
            BlendPenalties(from, fromIntensity, to, toIntensity, rainT, midT);
            BlendBenefits(from, fromIntensity, to, toIntensity, rainT, midT);

            Intensity = Mathf.Lerp(fromIntensity, toIntensity, midT);
        }

        // --- Private Methods ---

        /// <summary>
        /// 눈에 보이는 것들을 섞습니다. 구름이 먼저, 비가 나중입니다.
        /// </summary>
        private void BlendAppearance(WeatherPreset from, float fromIntensity,
                                     WeatherPreset to, float toIntensity,
                                     float cloudT, float rainT, float midT)
        {
            CloudCover = Mathf.Lerp(from.cloudCover * fromIntensity, to.cloudCover * toIntensity, cloudT);
            Darkness = Mathf.Lerp(from.darkness * fromIntensity, to.darkness * toIntensity, cloudT);
            WindStrength = Mathf.Lerp(from.windStrength * fromIntensity, to.windStrength * toIntensity, cloudT);
            FogDensity = Mathf.Lerp(from.fogDensity * fromIntensity, to.fogDensity * toIntensity, midT);
            RainIntensity = Mathf.Lerp(from.rainIntensity * fromIntensity, to.rainIntensity * toIntensity, rainT);
        }

        /// <summary>
        /// 불이익을 섞습니다.
        /// </summary>
        private void BlendPenalties(WeatherPreset from, float fromIntensity,
                                    WeatherPreset to, float toIntensity,
                                    float rainT, float midT)
        {
            VisibilityMultiplier = LerpScaled(
                from.visibilityMultiplier, fromIntensity, to.visibilityMultiplier, toIntensity, midT);
            RoadSlipperiness = LerpScaled(
                from.roadSlipperiness, fromIntensity, to.roadSlipperiness, toIntensity, midT);
            FuelConsumptionMultiplier = LerpScaled(
                from.fuelConsumptionMultiplier, fromIntensity, to.fuelConsumptionMultiplier, toIntensity, midT);
            GhostActivity = LerpScaled(
                from.ghostActivity, fromIntensity, to.ghostActivity, toIntensity, midT);

            // 씻김은 비를 따라갑니다. 하늘만 흐린데 몸이 씻기면 이상합니다.
            HygieneChangePerSecond = Mathf.Lerp(
                from.hygieneChangePerSecond * fromIntensity, to.hygieneChangePerSecond * toIntensity, rainT);
            StressPerSecond = Mathf.Lerp(
                from.stressPerSecond * fromIntensity, to.stressPerSecond * toIntensity, midT);
        }

        /// <summary>
        /// 이익을 섞습니다.
        /// </summary>
        private void BlendBenefits(WeatherPreset from, float fromIntensity,
                                   WeatherPreset to, float toIntensity,
                                   float rainT, float midT)
        {
            GhostDetectionMultiplier = LerpScaled(
                from.ghostDetectionMultiplier, fromIntensity, to.ghostDetectionMultiplier, toIntensity, midT);
            SleepQualityMultiplier = LerpScaled(
                from.sleepQualityMultiplier, fromIntensity, to.sleepQualityMultiplier, toIntensity, midT);
            ThirstReliefPerSecond = Mathf.Lerp(
                from.thirstReliefPerSecond * fromIntensity, to.thirstReliefPerSecond * toIntensity, rainT);
            StressReliefPerSecond = Mathf.Lerp(
                from.stressReliefPerSecond * fromIntensity, to.stressReliefPerSecond * toIntensity, midT);
        }

        /// <summary>
        /// 배율을 강도만큼 벌려서 섞습니다.
        ///
        /// <b>배율은 그냥 곱하면 안 됩니다.</b> 미끄러움 1.9를 강도 0.5로 곱하면 0.95가 되어
        /// <em>평소보다 덜 미끄러워집니다.</em> 배율의 기준점은 0이 아니라 1이므로,
        /// 1에서 얼마나 벌어졌는지를 강도로 조절해야 합니다.
        /// </summary>
        /// <param name="valueA">앞쪽 배율</param>
        /// <param name="intensityA">앞쪽 강도</param>
        /// <param name="valueB">뒤쪽 배율</param>
        /// <param name="intensityB">뒤쪽 강도</param>
        /// <param name="t">진행도</param>
        /// <returns>섞인 배율</returns>
        private static float LerpScaled(float valueA, float intensityA, float valueB, float intensityB, float t)
        {
            float a = 1f + (valueA - 1f) * intensityA;
            float b = 1f + (valueB - 1f) * intensityB;
            return Mathf.Lerp(a, b, t);
        }
    }
}
