using DG.Tweening;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.UI
{
    /// <summary>
    /// 음료를 마실 때 UI를 올렸다가, 잠시 두었다가, 다시 내리는 연출입니다.
    ///
    /// 예전에는 코루틴 하나가 세 구간(올라옴 → 머무름 → 내려감)을 <c>Vector2.Lerp</c> 로
    /// 직접 돌렸습니다. 지금은 DOTween 시퀀스가 같은 일을 합니다. 바뀐 것은 셋입니다.
    ///  - 중간에 다시 마시면 <b>이전 트윈을 죽이고</b> 처음부터 다시 시작합니다.
    ///    코루틴 시절에는 <c>isAnimating</c> 을 보고 그냥 무시했습니다.
    ///  - <c>Time.timeScale</c> 을 무시합니다. Feel 의 프리즈 프레임이 걸려도 UI는 계속 움직입니다.
    ///  - 이징을 줄 수 있습니다. 등속 Lerp 보다 훨씬 자연스럽습니다.
    ///
    /// 상태는 여전히 <see cref="Gameplay.BeverageConsumer"/> 가 소유합니다.
    /// 이 클래스는 <b>보여 주기만</b> 합니다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DrinkAnimation : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>올라오거나 내려가는 데 걸리는 시간(초)입니다.</summary>
        [Header("애니메이션")]
        [Tooltip("올라오거나 내려가는 데 걸리는 시간(초)")]
        public float animationTime = 0.3f;

        /// <summary>화면에 머무는 시간(초)입니다.</summary>
        [Tooltip("화면에 머무는 시간(초)")]
        public float visibleDuration = 2f;

        /// <summary>숨겨질 위치의 Y 오프셋입니다. 음수여야 아래로 내려갑니다.</summary>
        [Tooltip("숨겨질 위치의 Y 오프셋. 음수여야 아래로 내려갑니다.")]
        public float hiddenYOffset = -600f;

        /// <summary>올라올 때의 이징입니다. 뒤로 살짝 넘겼다 오면 경쾌합니다.</summary>
        [Header("이징")]
        [Tooltip("올라올 때의 이징")]
        public Ease riseEase = Ease.OutBack;

        /// <summary>내려갈 때의 이징입니다.</summary>
        [Tooltip("내려갈 때의 이징")]
        public Ease fallEase = Ease.InQuad;

        // --- Public Properties ---

        /// <summary>
        /// 연출 전체에 걸리는 시간입니다.
        /// <see cref="Gameplay.BeverageConsumer"/> 가 이만큼 기다린 뒤 빈 병을 던집니다.
        /// </summary>
        public float TotalDuration { get { return animationTime * 2f + visibleDuration; } }

        /// <summary>지금 재생 중인지 여부입니다.</summary>
        public bool IsAnimating { get { return sequence != null && sequence.IsActive() && sequence.IsPlaying(); } }

        // --- Private Member Variables ---

        /// <summary>연출을 적용할 RectTransform 입니다.</summary>
        private RectTransform rectTransform;

        /// <summary>화면에 보일 때의 위치입니다. 에디터에서 배치한 자리를 씁니다.</summary>
        private Vector2 visiblePosition;

        /// <summary>화면 밖으로 숨었을 때의 위치입니다.</summary>
        private Vector2 hiddenPosition;

        /// <summary>지금 돌고 있는 시퀀스입니다. 다시 재생할 때 죽이고 새로 만듭니다.</summary>
        private Sequence sequence;

        // --- Unity Event Functions ---

        /// <summary>
        /// 위치를 계산하고 숨긴 상태로 시작합니다.
        /// </summary>
        void Awake()
        {
            // BeverageConsumer 가 Start 에서 이걸 찾습니다. (등록은 Awake, 조회는 Start)
            GameContext.Register(this);

            rectTransform = GetComponent<RectTransform>();

            visiblePosition = rectTransform.anchoredPosition;
            hiddenPosition = new Vector2(visiblePosition.x, visiblePosition.y + hiddenYOffset);

            rectTransform.anchoredPosition = hiddenPosition;
        }

        /// <summary>등록을 해제합니다.</summary>
        void OnDestroy()
        {
            GameContext.Unregister(this);

            // SetLink 가 대신해 주지만, 명시해 두는 편이 읽기에 분명합니다.
            KillSequence();
        }

        // --- Public Methods ---

        /// <summary>
        /// 연출을 재생합니다. 이미 재생 중이면 <b>처음부터 다시</b> 시작합니다.
        ///
        /// 연속으로 마실 때 UI가 어중간한 자리에 멈춰 있다가 이어지는 것보다,
        /// 매번 아래에서 올라오는 편이 무슨 일이 일어났는지 분명합니다.
        /// </summary>
        public void PlayDrinkAnimation()
        {
            KillSequence();

            rectTransform.anchoredPosition = hiddenPosition;

            sequence = DOTween.Sequence()
                .Append(rectTransform.DOAnchorPos(visiblePosition, animationTime).SetEase(riseEase))
                .AppendInterval(visibleDuration)
                .Append(rectTransform.DOAnchorPos(hiddenPosition, animationTime).SetEase(fallEase))
                .ForUI(this);
        }

        /// <summary>
        /// 연출을 즉시 멈추고 숨긴 자리로 되돌립니다.
        /// </summary>
        public void StopDrinkAnimation()
        {
            KillSequence();
            rectTransform.anchoredPosition = hiddenPosition;
        }

        // --- Private Methods ---

        /// <summary>
        /// 돌고 있는 시퀀스를 정리합니다. 두 번 불려도 안전합니다.
        /// </summary>
        private void KillSequence()
        {
            if (sequence == null) return;

            if (sequence.IsActive()) sequence.Kill();
            sequence = null;
        }
    }
}
