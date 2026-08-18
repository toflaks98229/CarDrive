using UnityEngine;

/// <summary>
/// 하늘과 주변광을 시간·날씨에 맞춰 몰아 줍니다.
///
/// <see cref="TimeSystem"/>은 태양을 돌리고 밝기(DaylightFactor)를 계산하지만,
/// 그 값을 <b>하늘에 반영하는 곳이 없었습니다.</b> 그래서 기본 프로시저럴 스카이박스가
/// 그대로 보였고 밤에도 별이 없었습니다.
///
/// 이 컴포넌트가 하는 일은 셋입니다.
///  1. 하늘 머티리얼에 낮 정도·해 방향·구름 가림을 넣습니다. (CarDrive/Sky 셰이더)
///  2. 주변광을 하늘색에서 뽑아 밤에는 어둡고 낮에는 밝게 맞춥니다.
///  3. 해의 색과 세기를 시간대에 맞춰 바꿉니다. 밤에는 달빛으로 넘깁니다.
///
/// 밤에 태양광을 완전히 끄면 지형이 새까매져 아무것도 보이지 않습니다.
/// 그래서 <b>밤에는 방향을 유지한 채 차갑고 약한 달빛으로 바꿉니다.</b>
/// </summary>
[ExecuteAlways]
public class SkyController : MonoBehaviour
{
    // --- Public Member Variables ---

    /// <summary>하늘에 쓸 머티리얼입니다. 비워두면 RenderSettings의 스카이박스를 씁니다.</summary>
    [Header("하늘")]
    [Tooltip("CarDrive/Sky 셰이더를 쓰는 머티리얼. 비워두면 RenderSettings의 스카이박스를 씁니다.")]
    public Material skyMaterial;

    /// <summary>해 방향을 읽어 올 조명입니다. 비워두면 TimeSystem의 태양광을 씁니다.</summary>
    [Tooltip("해 방향을 읽어 올 조명. 비워두면 TimeSystem의 태양광을 씁니다.")]
    public Light sun;

    /// <summary>구름이 짙을수록 별을 가릴지 여부입니다.</summary>
    [Tooltip("체크하면 구름이 짙을수록 별이 가려집니다.")]
    public bool cloudsHideStars = true;

    /// <summary>주변광(Ambient)을 시간에 맞춰 조절할지 여부입니다.</summary>
    [Header("주변광")]
    [Tooltip("체크하면 주변광을 시간대에 맞춰 조절합니다. RenderSettings의 Ambient Mode를 Gradient로 바꿉니다.")]
    public bool driveAmbient = true;

    /// <summary>한낮의 하늘 방향 주변광입니다.</summary>
    [Tooltip("한낮의 하늘 쪽 주변광")]
    public Color dayAmbientSky = new Color(0.42f, 0.47f, 0.55f);

    /// <summary>한밤의 하늘 방향 주변광입니다. 너무 어두우면 아무것도 안 보입니다.</summary>
    [Tooltip("한밤의 하늘 쪽 주변광. 너무 어둡게 두면 헤드라이트 밖이 완전히 검어집니다.")]
    public Color nightAmbientSky = new Color(0.055f, 0.065f, 0.095f);

    /// <summary>지면 쪽 주변광입니다. 위아래 대비를 만들어 입체감을 남깁니다.</summary>
    [Tooltip("지면 쪽 주변광. 하늘색보다 어두워야 위아래 대비가 생깁니다.")]
    [Range(0f, 1f)]
    public float groundAmbientScale = 0.35f;

    /// <summary>가장 궂은 날씨에서 주변광이 낮아지는 하한 배율입니다.</summary>
    [Tooltip("가장 궂은 날씨일 때 주변광에 곱할 배율. 0.5면 절반까지 어두워집니다.")]
    [Range(0.1f, 1f)]
    public float weatherDarkFloor = 0.55f;

    /// <summary>한낮 태양광의 색입니다.</summary>
    [Header("해와 달")]
    [Tooltip("한낮 태양광의 색")]
    public Color dayLightColor = new Color(1f, 0.96f, 0.87f);

    /// <summary>해 뜰 무렵·해 질 무렵의 색입니다.</summary>
    [Tooltip("해 뜰 무렵과 해 질 무렵의 색")]
    public Color duskLightColor = new Color(1f, 0.66f, 0.38f);

    /// <summary>밤에 쓸 달빛 색입니다.</summary>
    [Tooltip("밤에 쓸 달빛 색. 차가운 색이어야 밤처럼 보입니다.")]
    public Color moonLightColor = new Color(0.55f, 0.66f, 0.95f);

    /// <summary>달빛의 세기입니다. 0이면 밤에 조명이 완전히 꺼집니다.</summary>
    [Tooltip("달빛의 세기. 0이면 밤에 조명이 꺼져 헤드라이트 밖이 완전히 검어집니다.")]
    public float moonIntensity = 0.12f;

    // --- Private Member Variables ---

    /// <summary>지금 조작 중인 하늘 머티리얼입니다.</summary>
    private Material activeSky;

    // 셰이더 프로퍼티 이름은 문자열로 매번 찾으면 낭비라 한 번만 해석해 둡니다.
    private static readonly int DayFactorId = Shader.PropertyToID("_DayFactor");
    private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
    private static readonly int StarFadeId = Shader.PropertyToID("_StarFade");

    // --- Unity Event Functions ---

    /// <summary>
    /// 하늘 머티리얼과 해를 찾아 둡니다.
    /// </summary>
    void OnEnable()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 매 프레임 하늘·주변광·해를 지금 시각에 맞춥니다.
    /// 편집 중에도 돌게 두어 인스펙터에서 값을 바꾸면 바로 보이게 합니다.
    /// </summary>
    void LateUpdate()
    {
        if (activeSky == null) ResolveReferences();

        float daylight = TimeSystem.GetDaylight();

        ApplySky(daylight);
        ApplySun(daylight);
        ApplyAmbient(daylight);
    }

    // --- Private Methods ---

    /// <summary>
    /// 비어 있는 참조를 채웁니다.
    /// </summary>
    private void ResolveReferences()
    {
        activeSky = skyMaterial != null ? skyMaterial : RenderSettings.skybox;

        if (sun == null && TimeSystem.Instance != null) sun = TimeSystem.Instance.sunLight;
        if (sun == null) sun = RenderSettings.sun;
    }

    /// <summary>
    /// 하늘 머티리얼에 낮 정도와 해 방향, 구름 가림을 넣습니다.
    /// </summary>
    /// <param name="daylight">0이면 한밤, 1이면 한낮</param>
    private void ApplySky(float daylight)
    {
        if (activeSky == null || !activeSky.HasProperty(DayFactorId)) return;

        activeSky.SetFloat(DayFactorId, daylight);

        // 해가 없으면 위쪽을 향한 것으로 둡니다. (여명과 해가 그려지지 않습니다)
        Vector3 toSun = sun != null ? -sun.transform.forward : Vector3.up;
        activeSky.SetVector(SunDirectionId, toSun);

        if (!activeSky.HasProperty(StarFadeId)) return;

        // 구름이 짙을수록 별을 가립니다. 날씨 시스템이 없으면 0이라 아무 일도 없습니다.
        float clouds = 0f;
        if (cloudsHideStars && WeatherSystem.Instance != null)
        {
            clouds = Mathf.Clamp01(WeatherSystem.Instance.CloudCover);
        }
        activeSky.SetFloat(StarFadeId, clouds);
    }

    /// <summary>
    /// 해의 색과 세기를 시간대에 맞춥니다. 밤에는 달빛으로 넘깁니다.
    /// </summary>
    /// <param name="daylight">0이면 한밤, 1이면 한낮</param>
    private void ApplySun(float daylight)
    {
        if (sun == null) return;

        // TimeSystem이 이미 세기를 DaylightFactor에 비례해 낮춰 둡니다.
        // 여기서는 그 위에 색과 밤의 하한만 얹습니다.
        float dusk = Mathf.Clamp01(1f - Mathf.Abs(daylight - 0.5f) * 2.2f);

        Color lit = Color.Lerp(moonLightColor, dayLightColor, daylight);
        sun.color = Color.Lerp(lit, duskLightColor, dusk * daylight);

        // 밤에 완전히 꺼 버리면 헤드라이트 밖이 아무것도 보이지 않습니다.
        // 방향은 그대로 두고 약한 달빛만 남깁니다.
        float max = TimeSystem.Instance != null ? TimeSystem.Instance.sunMaxIntensity : 1f;
        sun.intensity = Mathf.Max(max * daylight, moonIntensity);
        sun.enabled = true;
    }

    /// <summary>
    /// 주변광을 시간대에 맞춥니다.
    /// </summary>
    /// <param name="daylight">0이면 한밤, 1이면 한낮</param>
    private void ApplyAmbient(float daylight)
    {
        if (!driveAmbient) return;

        // 하늘색을 그대로 주변광으로 쓰면(Skybox 모드) 별빛까지 섞여 밤이 이상하게 밝아집니다.
        // 위아래를 직접 정하는 Gradient가 다루기 쉽습니다.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

        Color sky = Color.Lerp(nightAmbientSky, dayAmbientSky, daylight);

        // 궂은 날씨는 낮에도 하늘을 덮습니다.
        //
        // 이 처리를 여기서 하는 이유가 있습니다. WeatherRig에도 주변광을 낮추는 기능이
        // 있지만(controlAmbient), 둘 다 켜면 매 프레임 서로의 값을 덮어써서 실행 순서에 따라
        // 결과가 달라집니다. 그래서 <b>주변광의 주인은 이 컴포넌트 하나</b>로 정하고
        // WeatherRig의 controlAmbient는 꺼 둡니다.
        if (WeatherSystem.Instance != null)
        {
            float darkness = Mathf.Clamp01(WeatherSystem.Instance.Darkness);
            sky *= Mathf.Lerp(1f, weatherDarkFloor, darkness);
        }

        RenderSettings.ambientSkyColor = sky;
        RenderSettings.ambientEquatorColor = sky * 0.7f;
        RenderSettings.ambientGroundColor = sky * groundAmbientScale;
    }
}
