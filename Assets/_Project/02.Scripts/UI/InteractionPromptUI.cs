using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CarDrive.Gameplay;
using CarDrive.Common;

namespace CarDrive.UI
{
    /// <summary>
    /// 지금 무엇을 할 수 있는지 화면 하단에 안내합니다.
    /// 배경은 Image, 글자는 TextMeshPro를 씁니다.
    ///
    /// 안내 문구는 <b>조준점에 걸린 대상</b>에서 나옵니다. 아무것도 조준하지 않으면
    /// (주행 중 하차 안내를 빼고) 아무것도 표시되지 않습니다.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>조준점에 걸린 대상의 안내 문구를 읽어올 컴포넌트입니다. 비워두면 Start에서 씬을 검색합니다.</summary>
        [Header("연동")]
        [Tooltip("조준점에 걸린 대상을 읽어올 컴포넌트. 비워두면 자동으로 찾습니다.")]
        public PlayerInteractor interactor;

        /// <summary>탑승·하차 상태를 읽어올 컴포넌트입니다. 비워두면 Start에서 씬을 검색합니다.</summary>
        [Tooltip("탑승/하차 상태를 읽어올 컴포넌트. 비워두면 자동으로 찾습니다.")]
        public PlayerModeController modeController;

        /// <summary>물건 들기·내려놓기 안내를 읽어올 컴포넌트입니다. 비워두면 Start에서 씬을 검색합니다.</summary>
        [Tooltip("물건 들기 안내를 읽어올 컴포넌트. 비워두면 자동으로 찾습니다.")]
        public PlayerCarrier carrier;

        /// <summary>안내 배경 Image입니다. 표시할 내용이 없으면 통째로 숨깁니다.</summary>
        [Header("UI")]
        [Tooltip("안내 배경 Image. 표시할 내용이 없으면 통째로 숨깁니다.")]
        public Image panelImage;

        /// <summary>안내 문구를 그릴 텍스트입니다. 없으면 이 컴포넌트를 끕니다.</summary>
        [Tooltip("안내 문구")]
        public TextMeshProUGUI promptText;

        /// <summary>표시할 내용이 없을 때 배경까지 숨길지 여부입니다.</summary>
        [Header("표시")]
        [Tooltip("내용이 없을 때 배경까지 숨길지 여부")]
        public bool hideWhenEmpty = true;

        // --- Private Member Variables ---

        /// <summary>안내 문구를 조립할 버퍼입니다. 매 프레임 새 문자열을 만들지 않도록 재사용합니다.</summary>
        private readonly StringBuilder builder = new StringBuilder(96);

        /// <summary>마지막으로 표시한 문구입니다. 내용이 그대로면 UI를 건드리지 않습니다.</summary>
        private string lastText = null;

        // --- Unity Event Functions ---

        /// <summary>
        /// 비어 있는 참조를 씬에서 채웁니다. 문구를 그릴 텍스트가 없으면 이 컴포넌트를 끕니다.
        /// </summary>
        void Start()
        {
            if (interactor == null) interactor = GameContext.Resolve<PlayerInteractor>(this);
            if (modeController == null) modeController = GameContext.Resolve<PlayerModeController>(this);
            if (carrier == null) carrier = GameContext.Resolve<PlayerCarrier>(this);

            if (promptText == null)
            {
                Debug.LogWarning("InteractionPromptUI: promptText가 없습니다.", this);
                enabled = false;
            }
        }

        /// <summary>
        /// 매 프레임 안내 문구를 만들고, 내용이 달라졌을 때만 텍스트와 배경 표시를 갱신합니다.
        /// </summary>
        void Update()
        {
            string text = BuildPrompt();

            // 문자열이 바뀔 때만 갱신해 불필요한 레이아웃 재계산을 피합니다.
            if (text != lastText)
            {
                lastText = text;
                promptText.text = text;

                bool visible = !hideWhenEmpty || !string.IsNullOrEmpty(text);
                if (panelImage != null) panelImage.enabled = visible;
                promptText.enabled = visible;
            }
        }

        // --- Private Methods ---

        /// <summary>
        /// 현재 상태에 맞는 안내 문구를 만듭니다.
        /// </summary>
        /// <returns>조준 대상과 들기 안내를 줄바꿈으로 이어 붙인 문구. 안내할 것이 없으면 빈 문자열입니다.</returns>
        private string BuildPrompt()
        {
            builder.Length = 0;

            // 조준점에 걸린 대상의 안내만 표시합니다.
            // (문 = 탑승/하차, 운전대 = 시동, 침대·화장실 = 니즈 해소, 음료 상자 = 마시기)
            string interaction = interactor != null ? interactor.GetInteractionPrompt() : "";
            if (!string.IsNullOrEmpty(interaction)) Append(interaction);

            // 들기/내려놓기 안내 (좌클릭)
            string carry = carrier != null ? carrier.GetPrompt() : "";
            if (!string.IsNullOrEmpty(carry)) Append(carry);

            return builder.ToString();
        }

        /// <summary>
        /// 항목을 줄바꿈으로 이어 붙입니다.
        /// </summary>
        /// <param name="line">이어 붙일 안내 한 줄</param>
        private void Append(string line)
        {
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(line);
        }
    }
}
