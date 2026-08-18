using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 별도의 UI 구성 없이 바로 니즈 상태를 확인할 수 있는 디버그 오버레이입니다.
/// 정식 HUD(NeedsUI)를 만들기 전까지 테스트용으로 씁니다.
/// NeedsSystem과 같은 GameObject에 붙여 두면 됩니다.
/// </summary>
[RequireComponent(typeof(NeedsSystem))]
public class NeedsDebugOverlay : MonoBehaviour
{
    // --- Public Member Variables ---

    /// <summary>오버레이를 화면에 그릴지 여부입니다. toggleKey로도 전환할 수 있습니다.</summary>
    [Header("표시")]
    [Tooltip("오버레이를 켜고 끕니다.")]
    public bool showOverlay = true;

    /// <summary>오버레이 표시를 전환하는 키입니다.</summary>
    [Tooltip("오버레이 표시를 토글하는 키")]
    public KeyCode toggleKey = KeyCode.F1;

    /// <summary>화면 좌상단으로부터의 여백입니다.</summary>
    [Tooltip("화면 좌상단으로부터의 여백")]
    public Vector2 margin = new Vector2(16f, 16f);

    /// <summary>게이지 하나의 가로·세로 크기입니다.</summary>
    [Tooltip("게이지 하나의 크기")]
    public Vector2 barSize = new Vector2(220f, 18f);

    /// <summary>숫자키로 니즈를 강제로 올리는 테스트 조작을 허용할지 여부입니다.</summary>
    [Header("테스트 조작")]
    [Tooltip("체크하면 숫자키 1~6으로 해당 니즈를 0.1씩 올릴 수 있습니다.")]
    public bool enableTestKeys = true;

    // --- Private Member Variables ---

    /// <summary>값을 읽어올 니즈 시스템입니다. 같은 GameObject에서 가져옵니다.</summary>
    private NeedsSystem needs;

    /// <summary>게이지를 그릴 1x1 흰색 텍스처입니다. 색은 GUI.color로 입힙니다.</summary>
    private Texture2D barTexture;

    /// <summary>글자 크기를 줄인 라벨 스타일입니다. OnGUI에서 처음 필요할 때 만듭니다.</summary>
    private GUIStyle labelStyle;

    // --- Unity Event Functions ---

    /// <summary>
    /// 니즈 시스템 참조를 가져오고 게이지용 텍스처를 만듭니다.
    /// </summary>
    void Awake()
    {
        needs = GetComponent<NeedsSystem>();

        // 게이지를 그릴 1x1 흰색 텍스처 (색은 GUI.color로 입힙니다)
        barTexture = new Texture2D(1, 1);
        barTexture.SetPixel(0, 0, Color.white);
        barTexture.Apply();
    }

    /// <summary>
    /// 코드로 만든 텍스처를 해제합니다. 두지 않으면 씬을 오갈 때마다 쌓입니다.
    /// </summary>
    void OnDestroy()
    {
        if (barTexture != null) Destroy(barTexture);
    }

    /// <summary>
    /// 표시 전환 키와 테스트 조작 키 입력을 받습니다.
    /// </summary>
    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            showOverlay = !showOverlay;
        }

        if (enableTestKeys) HandleTestKeys();
    }

    /// <summary>
    /// 니즈 게이지 목록과 테스트 조작 안내를 화면 좌상단에 그립니다.
    /// </summary>
    void OnGUI()
    {
        if (!showOverlay || needs == null) return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 12;
        }

        List<NeedSetting> all = needs.GetAllSettings();
        if (all == null) return;

        float y = margin.y;

        GUI.color = Color.white;
        GUI.Label(new Rect(margin.x, y, 320f, 18f), "니즈 상태  (" + toggleKey + " 로 표시 전환)", labelStyle);
        y += 20f;

        for (int i = 0; i < all.Count; i++)
        {
            NeedSetting setting = all[i];
            DrawNeedBar(setting, new Rect(margin.x, y, barSize.x, barSize.y));
            y += barSize.y + 6f;
        }

        if (enableTestKeys)
        {
            y += 6f;
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            GUI.Label(new Rect(margin.x, y, 400f, 18f), "테스트: 1 허기 · 2 갈증 · 3 피로 · 4 스트레스 · 5 배뇨 · 6 청결 (+0.1)", labelStyle);
            y += 18f;
            GUI.Label(new Rect(margin.x, y, 400f, 18f), "0: 모두 초기화", labelStyle);
        }

        GUI.color = Color.white;
    }

    // --- Private Methods ---

    /// <summary>
    /// 니즈 하나의 게이지와 수치를 그립니다.
    /// </summary>
    /// <param name="setting">그릴 니즈의 설정 (이름·색상·경고 임계)</param>
    /// <param name="rect">게이지를 그릴 화면 영역</param>
    private void DrawNeedBar(NeedSetting setting, Rect rect)
    {
        float raw = needs.GetValue(setting.type);
        float fill = needs.GetDisplayFill(setting.type);

        // 배경
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(rect, barTexture);

        // 채움
        Rect fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height);
        GUI.color = needs.IsCritical(setting.type)
            ? Color.red
            : (needs.IsWarning(setting.type) ? new Color(1f, 0.6f, 0.1f) : setting.barColor);
        GUI.DrawTexture(fillRect, barTexture);

        // 경고 임계선
        float markerX = rect.x + rect.width * Mathf.Clamp01(setting.invertDisplay ? 1f - setting.warnThreshold : setting.warnThreshold);
        GUI.color = new Color(1f, 1f, 1f, 0.5f);
        GUI.DrawTexture(new Rect(markerX, rect.y, 1f, rect.height), barTexture);

        // 글자
        GUI.color = Color.white;
        string label = setting.displayName + "  " + raw.ToString("0.00");
        if (needs.IsCritical(setting.type)) label += "  [한계 초과]";
        GUI.Label(new Rect(rect.x + 6f, rect.y + 1f, rect.width, rect.height), label, labelStyle);
    }

    /// <summary>
    /// 숫자키로 니즈를 강제로 올려 임계 동작을 확인합니다.
    /// 1~6은 각 니즈를 0.1씩 올리고, 0은 모든 니즈를 초기화합니다.
    /// </summary>
    private void HandleTestKeys()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) needs.Add(NeedType.Hunger, 0.1f);
        if (Input.GetKeyDown(KeyCode.Alpha2)) needs.Add(NeedType.Thirst, 0.1f);
        if (Input.GetKeyDown(KeyCode.Alpha3)) needs.Add(NeedType.Fatigue, 0.1f);
        if (Input.GetKeyDown(KeyCode.Alpha4)) needs.Add(NeedType.Stress, 0.1f);
        if (Input.GetKeyDown(KeyCode.Alpha5)) needs.Add(NeedType.Urine, 0.1f);
        if (Input.GetKeyDown(KeyCode.Alpha6)) needs.Add(NeedType.Hygiene, 0.1f);
        if (Input.GetKeyDown(KeyCode.Alpha0)) needs.ResetAll();
    }
}
