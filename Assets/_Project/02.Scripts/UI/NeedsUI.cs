using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CarDrive.Systems;
using CarDrive.Common;

namespace CarDrive.UI
{
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

            /// <summary>
            /// 마지막으로 글자에 써 넣은 퍼센트입니다. 이 값이 그대로면 다시 쓰지 않습니다.
            ///
            /// <c>int.MinValue</c>로 시작해 <b>첫 프레임에는 반드시 한 번 그립니다.</b>
            /// 0으로 두면 "이미 0%를 그렸다"는 뜻이 되어, 실제로 0%인 니즈가 빈 칸으로 남습니다.
            /// (<see cref="CurrencyUI"/>가 쓰는 것과 같은 방식입니다)
            /// </summary>
            [System.NonSerialized]
            public int LastDrawnPercent = int.MinValue;

            /// <summary>
            /// Feel 의 게이지입니다. 연결하면 채움을 이쪽에 맡깁니다.
            ///
            /// <see cref="MMProgressBar"/> 는 값이 바뀔 때 부드럽게 따라가고, 뒤늦게 줄어드는
            /// '지연 바'를 함께 그려 줍니다. 얼마나 줄었는지가 눈에 남는 그 연출입니다.
            /// (Feel 데모의 FeelMMProgressBar 씬을 참고했습니다)
            /// </summary>
            [Tooltip("Feel 게이지 (선택). 연결하면 채움과 지연 바를 이쪽이 담당합니다.")]
            public MMProgressBar progressBar;

            /// <summary>이 니즈가 나빠졌을 때 재생할 피드백입니다.</summary>
            [Tooltip("이 니즈가 눈에 띄게 나빠졌을 때 재생할 피드백 (선택)")]
            public MMF_Player worsenedFeedback;

            /// <summary>이 니즈가 해소되었을 때 재생할 피드백입니다.</summary>
            [Tooltip("이 니즈가 눈에 띄게 해소되었을 때 재생할 피드백 (선택)")]
            public MMF_Player relievedFeedback;
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

        // --- Private Member Variables ---

        /// <summary>
        /// "0%"부터 "100%"까지 미리 만들어 둔 글자입니다.
        ///
        /// 퍼센트는 <b>101가지뿐</b>이라 처음에 한 번 만들어 두면 그 뒤로는 만들 일이 없습니다.
        /// 값이 바뀔 때조차 새 문자열이 생기지 않으므로, 이 화면의 숫자 표시는 <b>할당이 0</b>입니다.
        /// </summary>
        private static readonly string[] PercentLabels = CreatePercentLabels();

        // --- Unity Event Functions ---

        /// <summary>
        /// 표시 대상을 확인하고 니즈 이름 라벨을 한 번만 채웁니다.
        /// 대상이 없으면 경고를 남기고 이 컴포넌트를 끕니다.
        /// </summary>
        void Start()
        {
            if (needsSystem == null)
            {
                needsSystem = GameContext.Resolve<NeedsSystem>(this);
            }

            if (needsSystem == null)
            {
                Debug.LogWarning("NeedsUI: NeedsSystem을 찾을 수 없어 게이지를 표시하지 않습니다.", this);
                enabled = false;
                return;
            }

            // 변화 연출은 폴링으로 알 수 없습니다. 니즈가 바뀐 그 순간을 시스템이 알려 줍니다.
            needsSystem.onNeedWorsened.AddListener(PlayWorsened);
            needsSystem.onNeedRelievedStep.AddListener(PlayRelieved);

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

        /// <summary>
        /// 구독을 해제합니다. 남겨 두면 씬을 다시 불러올 때 죽은 대상을 부릅니다.
        /// </summary>
        void OnDestroy()
        {
            if (needsSystem == null) return;

            needsSystem.onNeedWorsened.RemoveListener(PlayWorsened);
            needsSystem.onNeedRelievedStep.RemoveListener(PlayRelieved);
        }

        // --- Private Methods ---

        /// <summary>
        /// 니즈가 나빠졌을 때의 연출을 재생합니다.
        /// </summary>
        /// <param name="type">나빠진 니즈</param>
        private void PlayWorsened(NeedType type)
        {
            NeedBar bar = FindBar(type);
            if (bar == null) return;

            if (bar.progressBar != null) bar.progressBar.Bump();
            if (bar.worsenedFeedback != null) bar.worsenedFeedback.PlayFeedbacks();
        }

        /// <summary>
        /// 니즈가 해소되었을 때의 연출을 재생합니다.
        /// </summary>
        /// <param name="type">해소된 니즈</param>
        private void PlayRelieved(NeedType type)
        {
            NeedBar bar = FindBar(type);
            if (bar == null) return;

            if (bar.progressBar != null) bar.progressBar.Bump();
            if (bar.relievedFeedback != null) bar.relievedFeedback.PlayFeedbacks();
        }

        /// <summary>
        /// 니즈 종류에 해당하는 게이지 묶음을 찾습니다.
        /// </summary>
        /// <param name="type">찾을 니즈 종류</param>
        /// <returns>해당 묶음. 연결되어 있지 않으면 null입니다.</returns>
        private NeedBar FindBar(NeedType type)
        {
            for (int i = 0; i < bars.Count; i++)
            {
                if (bars[i].type == type) return bars[i];
            }
            return null;
        }

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

            float fill = needsSystem.GetDisplayFill(bar.type);

            // Feel 게이지를 연결했으면 채움을 그쪽에 맡깁니다.
            // 부드러운 추종과 지연 바가 함께 따라오므로 여기서 직접 쓰면 서로 밀어냅니다.
            if (bar.progressBar != null)
            {
                bar.progressBar.UpdateBar01(fill);
            }
            else if (bar.fillImage != null)
            {
                bar.fillImage.fillAmount = fill;
            }

            if (bar.fillImage != null)
            {
                bar.fillImage.color = isCritical ? criticalColor : (isWarning ? warningColor : setting.barColor);
            }

            // 게이지와 숫자가 항상 같은 값을 가리키도록 위에서 구한 표시용 수치를 그대로 씁니다.
            // (청결처럼 반전 표시하는 니즈는 숫자도 같이 반전됩니다)
            UpdateValueText(bar, fill);

            if (bar.warningIcon != null)
            {
                // 한계를 넘으면 계속 켜 두고, 경고 구간에서는 깜빡입니다.
                bar.warningIcon.SetActive(isCritical || (isWarning && blinkOn));
            }
        }

        /// <summary>
        /// 퍼센트 글자를 갱신합니다. <b>값이 바뀌었을 때만 씁니다.</b>
        ///
        /// 예전에는 매 프레임 <c>int + "%"</c>를 새로 만들어 넣었습니다. 게이지가 여섯 개니
        /// <b>프레임당 문자열 여섯 개</b>가 쓰레기로 쌓였고, 값이 그대로여도 마찬가지였습니다.
        /// 픽셀 룩에서는 GC가 한 번 돌 때의 프레임 튐이 특히 눈에 띕니다.
        /// </summary>
        /// <param name="bar">갱신할 게이지</param>
        /// <param name="fill">표시용 수치(0~1)</param>
        private static void UpdateValueText(NeedBar bar, float fill)
        {
            if (bar.valueText == null) return;

            int percent = Mathf.Clamp(Mathf.RoundToInt(fill * 100f), 0, 100);
            if (percent == bar.LastDrawnPercent) return;

            bar.LastDrawnPercent = percent;
            bar.valueText.text = PercentLabels[percent];
        }

        /// <summary>
        /// 0%부터 100%까지의 글자를 미리 만듭니다.
        /// </summary>
        /// <returns>색인이 곧 퍼센트인 101칸짜리 배열</returns>
        private static string[] CreatePercentLabels()
        {
            string[] labels = new string[101];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = i + "%";
            }
            return labels;
        }
    }
}
