using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 날씨 종류입니다. 값을 추가하면 WeatherSystem의 프리셋 목록에도 함께 넣어야 합니다.
/// </summary>
public enum WeatherType
{
    Clear,      // 맑음
    Cloudy,     // 흐림
    Overcast,   // 잔뜩 흐림
    Rain,       // 비
    Storm,      // 폭우
    Fog,        // 안개
    Drizzle     // 이슬비
}

/// <summary>
/// 날씨 한 종류의 설정입니다.
///
/// 표현 수치(구름·비·안개…)는 모두 <b>강도 1.0일 때의 값</b>입니다.
/// 실제 적용값은 그날 뽑힌 강도(minIntensity~maxIntensity)를 곱한 것이라,
/// 같은 "비"라도 날마다 굵기가 다릅니다.
/// </summary>
[System.Serializable]
public class WeatherPreset
{
    /// <summary>이 설정이 어떤 날씨에 대한 것인지 나타냅니다.</summary>
    [Tooltip("어떤 날씨에 대한 설정인지")]
    public WeatherType type;

    /// <summary>UI에 표시할 날씨 이름입니다. 비어 있으면 type의 이름을 그대로 씁니다.</summary>
    [Tooltip("UI에 표시할 이름")]
    public string displayName = "";

    /// <summary>
    /// 날씨의 험한 정도입니다. 0이 가장 온화하고 1이 가장 험합니다.
    /// 맑음에서 폭우로 한 번에 건너뛰지 않도록 전환 경로를 계산하는 데 씁니다.
    /// </summary>
    [Header("심각도 / 강도")]
    [Range(0f, 1f)]
    [Tooltip("0이 가장 온화하고 1이 가장 험합니다. 맑음에서 폭우로 한 번에 건너뛰지 않도록 " +
             "전환 경로를 계산하는 데 씁니다.")]
    public float severity = 0f;

    /// <summary>이 날씨가 가질 수 있는 최소 강도입니다.</summary>
    [Range(0f, 1f)]
    [Tooltip("이 날씨가 가질 수 있는 최소 강도")]
    public float minIntensity = 0.5f;

    /// <summary>이 날씨가 가질 수 있는 최대 강도입니다.</summary>
    [Range(0f, 1f)]
    [Tooltip("이 날씨가 가질 수 있는 최대 강도")]
    public float maxIntensity = 1f;

    /// <summary>구름 양입니다. 비보다 먼저 짙어져서 "비가 올 것 같은" 하늘을 만듭니다.</summary>
    [Header("표현 - 강도 1.0일 때의 값")]
    [Range(0f, 1f)]
    [Tooltip("구름 양. 비보다 먼저 짙어져서 '비가 올 것 같은' 하늘을 만듭니다.")]
    public float cloudCover = 0f;

    /// <summary>
    /// 빗줄기 양입니다. 파티클에 원래 설정된 방출량에 곱해지는 배율이라 1을 넘길 수 있습니다.
    /// 1이면 원본 그대로, 4면 네 배로 쏟아집니다.
    /// </summary>
    [Range(0f, 8f)]
    [Tooltip("빗줄기 양. 파티클에 원래 설정된 방출량에 곱해지는 배율이라 1을 넘길 수 있습니다. " +
             "1이면 원본 그대로, 4면 네 배로 쏟아집니다.")]
    public float rainIntensity = 0f;

    /// <summary>안개의 짙기입니다.</summary>
    [Range(0f, 1f)]
    [Tooltip("안개의 짙기")]
    public float fogDensity = 0f;

    /// <summary>바람의 세기입니다. 나무 흔들림과 빗줄기 기울기에 쓰입니다.</summary>
    [Range(0f, 1f)]
    [Tooltip("바람의 세기")]
    public float windStrength = 0f;

    /// <summary>하늘 어둡기입니다. 조명·노출을 낮추는 데 씁니다.</summary>
    [Range(0f, 1f)]
    [Tooltip("하늘 어둡기. 조명·노출을 낮추는 데 씁니다.")]
    public float darkness = 0f;

    /// <summary>시야 배율입니다. 1이 평소이고 낮을수록 멀리 못 봅니다.</summary>
    [Header("불이익")]
    [Tooltip("시야 배율. 1이 평소이고 낮을수록 멀리 못 봅니다.")]
    public float visibilityMultiplier = 1f;

    /// <summary>노면 미끄러움 배율입니다. 1이 평소입니다.</summary>
    [Tooltip("노면 미끄러움 배율. 1이 평소입니다.")]
    public float roadSlipperiness = 1f;

    /// <summary>연료 소모 배율입니다. 맞바람·젖은 노면에서 더 먹습니다.</summary>
    [Tooltip("연료 소모 배율. 맞바람·젖은 노면에서 더 먹습니다.")]
    public float fuelConsumptionMultiplier = 1f;

    /// <summary>
    /// 하늘 아래 있을 때 초당 청결 변화입니다.
    /// 양수면 더러워지고, 음수면 씻겨 나갑니다. 비를 맞으면 몸이 씻기므로 비 계열은 음수입니다.
    /// </summary>
    [Tooltip("하늘 아래 있을 때 초당 청결 변화. " +
             "양수면 더러워지고, 음수면 씻겨 나갑니다. " +
             "비를 맞으면 몸이 씻기므로 비 계열은 음수입니다.")]
    public float hygieneChangePerSecond = 0f;

    /// <summary>밖에 있을 때 초당 오르는 스트레스입니다.</summary>
    [Tooltip("밖에 있을 때 초당 오르는 스트레스")]
    public float stressPerSecond = 0f;

    /// <summary>귀신 활동량 배율입니다. 스폰 빈도·공격성에 씁니다.</summary>
    [Tooltip("귀신 활동량 배율. 스폰 빈도·공격성에 씁니다.")]
    public float ghostActivity = 1f;

    /// <summary>귀신이 플레이어를 알아채는 정도입니다. 낮을수록 잘 숨을 수 있습니다.</summary>
    [Header("이익")]
    [Tooltip("귀신이 플레이어를 알아채는 정도. 낮을수록 잘 숨을 수 있습니다.")]
    public float ghostDetectionMultiplier = 1f;

    /// <summary>수면 회복 배율입니다. 빗소리를 들으며 자면 더 잘 쉽니다.</summary>
    [Tooltip("수면 회복 배율. 빗소리를 들으며 자면 더 잘 쉽니다.")]
    public float sleepQualityMultiplier = 1f;

    /// <summary>밖에서 비를 맞을 때 초당 줄어드는 갈증입니다.</summary>
    [Tooltip("밖에 있을 때 초당 줄어드는 갈증 (비를 맞을 때)")]
    public float thirstReliefPerSecond = 0f;

    /// <summary>좋은 날씨에 밖에 있을 때 초당 줄어드는 스트레스입니다.</summary>
    [Tooltip("밖에 있을 때 초당 줄어드는 스트레스 (좋은 날씨)")]
    public float stressReliefPerSecond = 0f;

    /// <summary>이 날씨가 유지되는 최소 시간(게임 분)입니다.</summary>
    [Header("지속 시간 (게임 내 분)")]
    [Tooltip("이 날씨가 유지되는 최소 시간(게임 분)")]
    public float minDurationMinutes = 90f;

    /// <summary>이 날씨가 유지되는 최대 시간(게임 분)입니다.</summary>
    [Tooltip("이 날씨가 유지되는 최대 시간(게임 분)")]
    public float maxDurationMinutes = 300f;

    /// <summary>이 날씨가 끝난 뒤 다시 오기까지 필요한 최소 게임 시간(분)입니다.</summary>
    [Header("간격")]
    [Tooltip("이 날씨가 끝난 뒤 다시 오기까지 필요한 최소 게임 시간(분)")]
    public float minRepeatGapMinutes = 240f;

    /// <summary>무작위로 뽑을 때의 가중치입니다. 0이면 저절로는 오지 않습니다.</summary>
    [Tooltip("무작위로 뽑을 때의 가중치. 0이면 저절로는 오지 않습니다.")]
    [Range(0f, 5f)]
    public float weight = 1f;

    /// <summary>밤에 뽑힐 확률 배율입니다. 안개처럼 밤에 잦은 날씨는 1보다 크게 둡니다.</summary>
    [Tooltip("밤에 뽑힐 확률 배율. 안개처럼 밤에 잦은 날씨는 1보다 크게 둡니다.")]
    public float nightWeightMultiplier = 1f;
}

/// <summary>
/// 프리셋을 만들지 않아도 바로 돌아가도록 기본값을 제공합니다.
/// severity 순서: 맑음 0 → 흐림 0.25 → 잔뜩 흐림 0.4 → 이슬비 0.5 → 비 0.7 → 폭우 1.0
/// (안개는 계열이 달라 0.35로 따로 둡니다)
/// </summary>
public static class WeatherDefaults
{
    // --- Public Methods ---

    /// <summary>
    /// 모든 날씨 종류의 기본 프리셋을 새로 만들어 돌려줍니다.
    /// 호출할 때마다 새 인스턴스를 만들므로, 받은 쪽에서 값을 고쳐도 다음 호출에 영향을 주지 않습니다.
    /// </summary>
    /// <returns>severity 순서대로 정렬된 기본 날씨 프리셋 목록</returns>
    public static List<WeatherPreset> CreatePresets()
    {
        return new List<WeatherPreset>
        {
            new WeatherPreset {
                type = WeatherType.Clear, displayName = "맑음", severity = 0f,
                minIntensity = 0.6f, maxIntensity = 1f,
                cloudCover = 0.05f, rainIntensity = 0f, fogDensity = 0f, windStrength = 0.1f, darkness = 0f,
                visibilityMultiplier = 1f, roadSlipperiness = 1f, fuelConsumptionMultiplier = 1f,
                ghostActivity = 0.8f, ghostDetectionMultiplier = 1.15f,
                sleepQualityMultiplier = 1f, stressReliefPerSecond = 0.0008f,
                minDurationMinutes = 240f, maxDurationMinutes = 600f,
                minRepeatGapMinutes = 0f, weight = 3f, nightWeightMultiplier = 1f
            },
            new WeatherPreset {
                type = WeatherType.Cloudy, displayName = "흐림", severity = 0.25f,
                minIntensity = 0.4f, maxIntensity = 1f,
                cloudCover = 0.5f, rainIntensity = 0f, fogDensity = 0.08f, windStrength = 0.3f, darkness = 0.2f,
                visibilityMultiplier = 0.95f, roadSlipperiness = 1f, fuelConsumptionMultiplier = 1f,
                ghostActivity = 1.1f, ghostDetectionMultiplier = 1f,
                sleepQualityMultiplier = 1f,
                minDurationMinutes = 120f, maxDurationMinutes = 360f,
                minRepeatGapMinutes = 60f, weight = 2.5f, nightWeightMultiplier = 1f
            },
            new WeatherPreset {
                type = WeatherType.Overcast, displayName = "잔뜩 흐림", severity = 0.4f,
                minIntensity = 0.5f, maxIntensity = 1f,
                cloudCover = 0.85f, rainIntensity = 0f, fogDensity = 0.15f, windStrength = 0.4f, darkness = 0.4f,
                visibilityMultiplier = 0.9f, roadSlipperiness = 1f, fuelConsumptionMultiplier = 1f,
                stressPerSecond = 0.0004f,
                ghostActivity = 1.25f, ghostDetectionMultiplier = 0.95f,
                minDurationMinutes = 90f, maxDurationMinutes = 240f,
                minRepeatGapMinutes = 90f, weight = 1.6f, nightWeightMultiplier = 1.1f
            },
            new WeatherPreset {
                type = WeatherType.Fog, displayName = "안개", severity = 0.35f,
                minIntensity = 0.4f, maxIntensity = 1f,
                cloudCover = 0.4f, rainIntensity = 0f, fogDensity = 1f, windStrength = 0.03f, darkness = 0.3f,
                visibilityMultiplier = 0.35f, roadSlipperiness = 1.1f, fuelConsumptionMultiplier = 1f,
                stressPerSecond = 0.0012f,
                ghostActivity = 1.6f, ghostDetectionMultiplier = 0.55f,   // 잘 안 보이는 만큼 잘 숨습니다
                sleepQualityMultiplier = 1f,
                minDurationMinutes = 60f, maxDurationMinutes = 180f,
                minRepeatGapMinutes = 300f, weight = 1.2f, nightWeightMultiplier = 2.2f
            },
            new WeatherPreset {
                type = WeatherType.Drizzle, displayName = "이슬비", severity = 0.5f,
                minIntensity = 0.3f, maxIntensity = 0.8f,
                cloudCover = 0.75f, rainIntensity = 0.3f, fogDensity = 0.2f, windStrength = 0.3f, darkness = 0.35f,
                visibilityMultiplier = 0.85f, roadSlipperiness = 1.2f, fuelConsumptionMultiplier = 1.03f,
                // 이슬비로도 씻기긴 하지만 한참 서 있어야 합니다. (약 5분 30초에 완전히 씻김)
                hygieneChangePerSecond = -0.003f,
                ghostActivity = 1.2f, ghostDetectionMultiplier = 0.9f,
                sleepQualityMultiplier = 1.15f, thirstReliefPerSecond = 0.0004f,
                minDurationMinutes = 60f, maxDurationMinutes = 180f,
                minRepeatGapMinutes = 120f, weight = 1.5f, nightWeightMultiplier = 1f
            },
            new WeatherPreset {
                type = WeatherType.Rain, displayName = "비", severity = 0.7f,
                minIntensity = 0.4f, maxIntensity = 1f,
                cloudCover = 0.9f, rainIntensity = 0.7f, fogDensity = 0.25f, windStrength = 0.5f, darkness = 0.5f,
                visibilityMultiplier = 0.7f, roadSlipperiness = 1.4f, fuelConsumptionMultiplier = 1.08f,
                // 비를 맞으면 씻깁니다. 대신 스트레스를 받으니 공짜 목욕은 아닙니다. (약 2분)
                hygieneChangePerSecond = -0.008f, stressPerSecond = 0.001f,
                ghostActivity = 1.4f, ghostDetectionMultiplier = 0.75f,
                sleepQualityMultiplier = 1.25f, thirstReliefPerSecond = 0.001f,
                minDurationMinutes = 90f, maxDurationMinutes = 240f,
                minRepeatGapMinutes = 180f, weight = 2f, nightWeightMultiplier = 1.2f
            },
            new WeatherPreset {
                type = WeatherType.Storm, displayName = "폭우", severity = 1f,
                minIntensity = 0.6f, maxIntensity = 1f,
                // 비 양은 원본 방출량의 4배까지 쏟아집니다. (다른 날씨와 확실히 구분되도록)
                cloudCover = 1f, rainIntensity = 4f, fogDensity = 0.35f, windStrength = 1f, darkness = 0.75f,
                visibilityMultiplier = 0.45f, roadSlipperiness = 1.9f, fuelConsumptionMultiplier = 1.18f,
                // 퍼붓는 만큼 가장 빨리 씻기지만 스트레스도 가장 비쌉니다. (약 1분 5초)
                hygieneChangePerSecond = -0.015f, stressPerSecond = 0.004f,
                ghostActivity = 1.9f, ghostDetectionMultiplier = 0.6f,
                sleepQualityMultiplier = 0.85f, thirstReliefPerSecond = 0.0015f,
                minDurationMinutes = 45f, maxDurationMinutes = 120f,
                minRepeatGapMinutes = 480f, weight = 0.7f, nightWeightMultiplier = 1.4f
            }
        };
    }
}
