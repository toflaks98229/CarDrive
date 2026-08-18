using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 니즈 게이지를 화면에 표시하는 HUD입니다.
/// 상태는 NeedsSystem이 소유하고 이 클래스는 읽어서 그리기만 합니다.
///
/// 사용법: 니즈마다 Image(Fill Method: Horizontal) 하나와 선택적으로 라벨 텍스트를 만들고,
/// 아래 bars 리스트에 연결하세요. 연결하지 않은 니즈는 그냥 무시됩니다.
/// </summary>
public class NeedsUI : MonoBehaviour
{
    /// <summary>니즈 하나에 대응하는 UI 묶음입니다.</summary>
    [System.Serializable]
    public class NeedBar
    {
        /// <summary>이 묶음이 표시할 니즈 종류입니다.</summary>
        [Tooltip("어떤 니즈를 표시할지")]
        public NeedType type;

        /// <summary>니즈 값을 채움으로 표현할 Image입니다. Image Type을 Filled / Horizontal로 설정하세요.</summary>
        [Tooltip("Image Type을 Filled / Horizontal로 설정한 게이지")]
        public Image fillImage;

        /// <summary>니즈 이름을 표시할 텍스트입니다. 시작할 때 한 번만 채웁니다.</summary>
        [Tooltip("니즈 이름을 표시할 텍스트 (선택)")]
        public TextMeshProUGUI labelText;

        /// <summary>니즈 수치를 퍼센트로 표시할 텍스트입니다. 비워 두어도 됩니다.</summary>
        [Tooltip("수치를 퍼센트로 표시할 텍스트 (선택)")]
        public TextMeshProUGUI valueText;

        /// <summary>경고·한계 상태에서 켜거나 깜빡일 오브젝트입니다. 비워 두어도 됩니다.</summary>
        [Tooltip("경고 상태에서 깜빡일 오브젝트 (선택)")]
        public GameObject warningIcon;
    }

    // --- Public Member Variables ---

    /// <summary>니즈 값을 읽어올 대상입니다. 비워두면 Start에서 씬을 검색합니다.</summary>
    [Header("연동")]
    [Tooltip("표시할 대상. 비워두면 씬에서 자동으로 찾습니다.")]
    public NeedsSystem needsSystem;

    /// <summary>표시할 니즈 게이지 목록입니다. 여기에 없는 니즈는 그리지 않습니다.</summary>
    [Header("게이지")]
    [Tooltip("표시할 니즈 게이지 목록")]
    public List<NeedBar> bars = new List<NeedBar>();

    /// <summary>경고 임계를 넘었을 때 쓰는 게이지 색상입니다.</summary>
    [Header("색상")]
    [Tooltip("경고 임계를 넘었을 때 게이지 색상")]
    public Color warningColor = new Color(1f, 0.6f, 0.1f);

    /// <summary>한계를 넘었을 때 쓰는 게이지 색상입니다.</summary>
    [Tooltip("한계를 넘었을 때 게이지 색상")]
    public Color criticalColor = new Color(0.9f, 0.2f, 0.15f);

    /// <summary>경고 아이콘이 깜빡이는 속도입니다. (초당 횟수)</summary>
    [Tooltip("경고 상태에서 깜빡이는 속도 (초당 횟수)")]
    public float warningBlinkSpeed = 2f;

    // --- Unity Event Functions ---

    /// <summary>
    /// 표시 대상을 확인하고 니즈 이름 라벨을 한 번만 채웁니다.
    /// 대상이 없으면 경고를 남기고 이 컴포넌트를 끕니다.
    /// </summary>
    void Start()
    {
        if (needsSystem == null)
        {
            needsSystem = FindAnyObjectByType<NeedsSystem>();
        }

        if (needsSystem == null)
        {
            Debug.LogWarning("NeedsUI: NeedsSystem을 찾을 수 없어 게이지를 표시하지 않습니다.", this);
            enabled = false;
            return;
        }

        // 이름 라벨은 매 프레임 갱신할 필요가 없으므로 한 번만 채웁니다.
        for (int i = 0; i < bars.Count; i++)
        {
            NeedSetting setting = needsSystem.GetSetting(bars[i].type);
            if (setting != null && bars[i].labelText != null)
            {
                bars[i].labelText.text = setting.displayName;
            }
        }
    }

    /// <summary>
    /// 매 프레임 깜빡임 위상을 계산하고 모든 게이지를 갱신합니다.
    /// </summary>
    void Update()
    {
        if (needsSystem == null) return;

        bool blinkOn = Mathf.Repeat(Time.unscaledTime * warningBlinkSpeed, 1f) < 0.5f;

        for (int i = 0; i < bars.Count; i++)
        {
            UpdateBar(bars[i], blinkOn);
        }
    }

    // --- Private Methods ---

    /// <summary>
    /// 게이지 하나를 현재 상태에 맞춰 갱신합니다.
    /// </summary>
    /// <param name="bar">갱신할 게이지 묶음</param>
    /// <param name="blinkOn">이번 프레임의 깜빡임이 켜짐 위상인지 여부</param>
    private void UpdateBar(NeedBar bar, bool blinkOn)
    {
        NeedSetting setting = needsSystem.GetSetting(bar.type);
        if (setting == null) return;

        bool isCritical = needsSystem.IsCritical(bar.type);
        bool isWarning = needsSystem.IsWarning(bar.type);

        if (bar.fillImage != null)
        {
            bar.fillImage.fillAmount = needsSystem.GetDisplayFill(bar.type);
            bar.fillImage.color = isCritical ? criticalColor : (isWarning ? warningColor : setting.barColor);
        }

        if (bar.valueText != null)
        {
            // 게이지와 숫자가 항상 같은 값을 가리키도록 표시용 수치를 씁니다.
            // (청결처럼 반전 표시하는 니즈는 숫자도 같이 반전됩니다)
            bar.valueText.text = Mathf.RoundToInt(needsSystem.GetDisplayFill(bar.type) * 100f) + "%";
        }

        if (bar.warningIcon != null)
        {
            // 한계를 넘으면 계속 켜 두고, 경고 구간에서는 깜빡입니다.
            bar.warningIcon.SetActive(isCritical || (isWarning && blinkOn));
        }
    }
}
