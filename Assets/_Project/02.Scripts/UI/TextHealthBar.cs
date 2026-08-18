using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// 체력을 TextMeshPro 문자열 게이지("■■■■□□□□□□")로 그립니다.
///
/// <b>이 컴포넌트는 이제 표시만 합니다.</b> 상태는 Health(PlayerHealth / VehicleHealth)가
/// 소유하며 여기서는 읽기만 합니다. 예전에는 이 클래스가 상태까지 갖고 있어서
/// 차량 내구도와 플레이어 체력이 같은 타입이 되는 문제가 있었습니다.
///
/// 갱신은 Health의 Revision 번호가 달라졌을 때만 합니다.
/// 그래야 매 프레임 문자열을 다시 만들지 않습니다.
/// </summary>
public class TextHealthBar : MonoBehaviour
{
    // --- Public Member Variables ---

    /// <summary>체력을 읽어올 대상입니다. 비워두면 Awake에서 이 오브젝트와 부모를 검색합니다.</summary>
    [Header("연동")]
    [Tooltip("체력을 읽어올 대상. 비워두면 이 오브젝트와 부모에서 찾습니다.")]
    public Health source;

    /// <summary>문자열 게이지를 그릴 TextMeshPro입니다.</summary>
    [Tooltip("게이지를 그릴 TextMeshPro")]
    public TextMeshPro textMeshPro;

    /// <summary>체력바를 몇 칸으로 나눠 그릴지 정합니다.</summary>
    [Header("표시")]
    [Tooltip("체력바를 몇 칸으로 나눠 그릴지")]
    [Range(4, 40)]
    public int segmentCount = 10;

    /// <summary>
    /// 채워진 칸에 쓸 문자입니다.
    /// 폰트에 없는 문자를 넣으면 빈칸으로 보이므로, 현재 폰트(korean SDF)에 있는 U+25A0을 기본값으로 씁니다.
    /// </summary>
    [Tooltip("채워진 칸에 쓸 문자. 폰트에 없는 문자를 넣으면 빈칸으로 보입니다. " +
             "현재 폰트(korean SDF)에는 U+2588(FULL BLOCK)이 없어 U+25A0(BLACK SQUARE)을 씁니다.")]
    public string filledCharacter = "■";

    /// <summary>빈 칸에 쓸 문자입니다.</summary>
    [Tooltip("빈 칸에 쓸 문자")]
    public string emptyCharacter = "□";

    /// <summary>체력이 조금이라도 남아 있으면 최소 한 칸은 채울지 여부입니다. (0과 구분하기 위해)</summary>
    [Tooltip("체력이 조금이라도 남아 있으면 최소 한 칸은 채웁니다. (0과 구분하기 위해)")]
    public bool keepLastSegment = true;

    /// <summary>자동 줄바꿈을 막을지 여부입니다. 칸 수를 늘려도 한 줄로 유지됩니다.</summary>
    [Tooltip("줄바꿈을 막습니다. 칸 수를 늘려도 한 줄로 유지됩니다.")]
    public bool preventWrap = true;

    // --- Private Member Variables ---

    // 매 갱신마다 문자열을 이어 붙이면 쓰레기가 쌓이므로 버퍼를 재사용합니다.
    private readonly StringBuilder builder = new StringBuilder(64);

    // 마지막으로 그린 시점의 상태. 이 값이 그대로면 다시 그리지 않습니다.
    private int lastRevision = -1;

    // --- Unity Event Functions ---

    /// <summary>
    /// 표시 대상을 찾고 줄바꿈 방지 설정을 적용합니다.
    /// </summary>
    void Awake()
    {
        ResolveSource();
        ApplyTextSettings();
    }

    /// <summary>
    /// 체력이 바뀐 프레임에만 게이지 문자열을 다시 만듭니다.
    /// 체력 변화가 반영된 뒤에 그려야 하므로 LateUpdate에서 처리합니다.
    /// </summary>
    void LateUpdate()
    {
        if (source == null) return;

        // 값이 바뀌었을 때만 문자열을 다시 만듭니다.
        if (source.Revision == lastRevision) return;

        lastRevision = source.Revision;
        UpdateHealthText();
    }

    // --- Private Methods ---

    /// <summary>
    /// 표시할 대상을 찾습니다. 보통 같은 오브젝트나 부모(차량)에 붙어 있습니다.
    /// </summary>
    private void ResolveSource()
    {
        if (source != null) return;

        source = GetComponent<Health>();
        if (source == null) source = GetComponentInParent<Health>();

        if (source == null)
        {
            Debug.LogWarning("TextHealthBar: 읽어올 Health를 찾지 못했습니다.", this);
        }
    }

    /// <summary>
    /// 체력바가 한 줄로 유지되도록 텍스트 설정을 잡습니다.
    /// 칸이 많아지면 자동 줄바꿈으로 두 줄이 되어 계기판을 벗어나기 때문입니다.
    /// </summary>
    private void ApplyTextSettings()
    {
        if (textMeshPro == null || !preventWrap) return;

        textMeshPro.textWrappingMode = TextWrappingModes.NoWrap;
        textMeshPro.overflowMode = TextOverflowModes.Overflow;
    }

    /// <summary>
    /// 현재 체력 비율만큼 칸을 채운 게이지 문자열을 만들어 TextMeshPro에 넣습니다.
    /// </summary>
    private void UpdateHealthText()
    {
        if (textMeshPro == null) return;

        int segments = Mathf.Max(1, segmentCount);
        int filled = Mathf.RoundToInt(Mathf.Clamp01(source.HealthNormalized) * segments);

        // 반올림 때문에 체력이 남았는데 0칸이 되는 것을 막습니다.
        if (keepLastSegment && filled == 0 && source.CurrentHealth > 0f) filled = 1;

        builder.Length = 0;
        for (int i = 0; i < segments; i++)
        {
            builder.Append(i < filled ? filledCharacter : emptyCharacter);
        }

        textMeshPro.text = builder.ToString();
    }
}
