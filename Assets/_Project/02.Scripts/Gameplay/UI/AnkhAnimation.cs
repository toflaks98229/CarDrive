using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using CarDrive.Common;

namespace CarDrive.UI
{
    /// <summary>
    /// 앙크를 화면에 올리고 내리고, 적중하는 동안 떨고, 충전할수록 밝아지게 합니다.
    ///
    /// 예전에는 셋을 각자 다른 방식으로 처리했습니다 — 슬라이드는 코루틴,
    /// 떨림은 Update 안의 사인파, 밝기는 Update 안의 Lerp. 지금은 전부 DOTween 입니다.
    ///
    /// <b>주의할 점 하나.</b> 슬라이드와 떨림은 둘 다 <c>anchoredPosition</c> 을 건드립니다.
    /// 그래서 떨림은 <b>제자리에서만</b> 돌게 하고(<c>DOShakeAnchorPos</c> 는 끝나면 원래 자리로
    /// 되돌립니다), 슬라이드를 시작할 때는 떨림을 먼저 죽입니다.
    /// 이 규칙을 지키지 않으면 두 트윈이 같은 값을 두고 다퉈 앙크가 엉뚱한 곳에 남습니다.
    ///
    /// 밝기는 재질 색이라 위치와 겹치지 않습니다. 그래서 독립적으로 돌아도 안전합니다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class AnkhAnimation : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>올라오거나 내려가는 데 걸리는 시간(초)입니다.</summary>
        [Header("슬라이드")]
        [Tooltip("올라오거나 내려가는 데 걸리는 시간(초)")]
        public float animationTime = 0.3f;

        /// <summary>숨겨질 위치의 Y 오프셋입니다. 음수여야 아래로 내려갑니다.</summary>
        [Tooltip("숨겨질 위치의 Y 오프셋. 음수여야 아래로 내려갑니다.")]
        public float hiddenYOffset = -500f;

        /// <summary>올라올 때의 이징입니다.</summary>
        [Tooltip("올라올 때의 이징")]
        public Ease showEase = Ease.OutBack;

        /// <summary>내려갈 때의 이징입니다.</summary>
        [Tooltip("내려갈 때의 이징")]
        public Ease hideEase = Ease.InQuad;

        /// <summary>밝기를 적용할 앙크 이미지입니다.</summary>
        [Header("충전 밝기")]
        [Tooltip("밝기를 적용할 앙크 이미지")]
        public Image ankhImage;

        /// <summary>재질에서 색을 담고 있는 속성 이름입니다.</summary>
        [Tooltip("재질에서 색을 담고 있는 속성 이름")]
        public string materialColorName = "Color";

        /// <summary>충전이 0일 때의 밝기입니다.</summary>
        [Tooltip("충전이 0일 때의 밝기")]
        public float baseIntensity = 1.0f;

        /// <summary>충전이 가득일 때의 밝기입니다.</summary>
        [Tooltip("충전이 가득일 때의 밝기")]
        public float chargedIntensity = 3.0f;

        /// <summary>목표 밝기까지 따라가는 데 걸리는 시간(초)입니다.</summary>
        [Tooltip("목표 밝기까지 따라가는 데 걸리는 시간(초)")]
        public float intensityTweenTime = 0.2f;

        /// <summary>떨림의 세기입니다.</summary>
        [Header("적중 떨림")]
        [Tooltip("떨림의 세기(픽셀)")]
        public float shakeStrength = 5f;

        /// <summary>떨림 한 번의 길이(초)입니다. 적중하는 동안 이어서 반복합니다.</summary>
        [Tooltip("떨림 한 번의 길이(초). 적중하는 동안 이어서 반복합니다.")]
        public float shakeDuration = 0.12f;

        /// <summary>떨림의 잔진동 횟수입니다.</summary>
        [Tooltip("떨림의 잔진동 횟수")]
        public int shakeVibrato = 20;

        // --- Private Member Variables ---

        /// <summary>연출을 적용할 RectTransform 입니다.</summary>
        private RectTransform rectTransform;

        /// <summary>화면에 보일 때의 위치입니다.</summary>
        private Vector2 visiblePosition;

        /// <summary>화면 밖으로 숨었을 때의 위치입니다.</summary>
        private Vector2 hiddenPosition;

        /// <summary>지금 돌고 있는 슬라이드 트윈입니다.</summary>
        private Tween slideTween;

        /// <summary>지금 돌고 있는 떨림 트윈입니다.</summary>
        private Tween shakeTween;

        /// <summary>지금 돌고 있는 밝기 트윈입니다.</summary>
        private Tween intensityTween;

        /// <summary>이 인스턴스만의 재질입니다. 공유 재질을 고치면 에셋이 바뀝니다.</summary>
        private Material ankhMaterialInstance;

        /// <summary>재질의 원래 색입니다. 여기에 밝기를 곱해 씁니다.</summary>
        private Color originalColor;

        /// <summary>지금 적용 중인 밝기입니다. 트윈이 이 값을 움직입니다.</summary>
        private float currentIntensity;

        /// <summary>재질에 그 속성이 실제로 있는지 여부입니다. 없으면 조용히 넘어갑니다.</summary>
        private bool hasColorProperty;

        // --- Unity Event Functions ---

        /// <summary>
        /// 위치를 계산하고 숨긴 상태로 시작합니다. 재질 사본도 여기서 만듭니다.
        /// </summary>
        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();

            visiblePosition = rectTransform.anchoredPosition;
            hiddenPosition = new Vector2(visiblePosition.x, visiblePosition.y + hiddenYOffset);
            rectTransform.anchoredPosition = hiddenPosition;

            currentIntensity = baseIntensity;
            SetupMaterial();
        }

        /// <summary>돌고 있는 트윈을 모두 정리합니다.</summary>
        void OnDestroy()
        {
            KillAll();
        }

        // --- Public Methods ---

        /// <summary>
        /// 앙크를 화면에 올립니다. 내려가는 중이었다면 그 자리에서 방향을 바꿉니다.
        /// </summary>
        public void ShowAnkh()
        {
            KillShake();
            KillSlide();

            slideTween = rectTransform.DOAnchorPos(visiblePosition, animationTime)
                                      .SetEase(showEase)
                                      .ForUI(this);
        }

        /// <summary>
        /// 앙크를 화면 밖으로 내립니다. 떨림도 함께 멈춥니다.
        /// </summary>
        public void HideAnkh()
        {
            KillShake();
            KillSlide();

            slideTween = rectTransform.DOAnchorPos(hiddenPosition, animationTime)
                                      .SetEase(hideEase)
                                      .ForUI(this);
        }

        /// <summary>
        /// 충전 진행률을 넘깁니다. 밝기가 그 목표까지 부드럽게 따라갑니다.
        /// </summary>
        /// <param name="progress">충전 진행률 (0~1). 범위를 벗어나면 잘립니다.</param>
        public void SetTargetChargeProgress(float progress)
        {
            if (!hasColorProperty) return;

            float target = Mathf.Lerp(baseIntensity, chargedIntensity, Mathf.Clamp01(progress));
            if (Mathf.Approximately(currentIntensity, target)) return;

            if (intensityTween != null && intensityTween.IsActive()) intensityTween.Kill();

            intensityTween = DOTween.To(() => currentIntensity,
                                        v => { currentIntensity = v; ApplyIntensity(); },
                                        target, intensityTweenTime)
                                    .ForUI(this);
        }

        /// <summary>
        /// 적중하는 동안 떱니다. 이미 떨고 있으면 그대로 둡니다.
        ///
        /// <b>슬라이드가 도는 중에는 떨지 않습니다.</b> 둘 다 같은 위치를 건드리기 때문입니다.
        /// </summary>
        public void StartShake()
        {
            if (shakeTween != null && shakeTween.IsActive()) return;
            if (slideTween != null && slideTween.IsActive()) return;

            // 무한 루프로 돌리지 않습니다. 반복되는 셰이크는 시작 위치가 조금씩 밀립니다.
            // 대신 짧게 한 번 떨고 끝내면, 계속 적중하는 동안 PlayerAttacker 가
            // 매 프레임 다시 불러 주므로 자연스럽게 이어집니다.
            shakeTween = rectTransform.DOShakeAnchorPos(shakeDuration, shakeStrength, shakeVibrato)
                                      .OnComplete(() => shakeTween = null)
                                      .ForUI(this);
        }

        /// <summary>
        /// 떨림을 멈추고 제자리로 되돌립니다.
        /// </summary>
        public void StopShake()
        {
            if (!KillShake()) return;

            // 반복 떨림은 중간에 끊기면 어긋난 자리에 남습니다. 보이는 자리로 되돌립니다.
            if (slideTween == null || !slideTween.IsActive())
            {
                rectTransform.anchoredPosition = visiblePosition;
            }
        }

        // --- Private Methods ---

        /// <summary>
        /// 자기만의 재질 사본을 만들고 원래 색을 기억해 둡니다.
        /// </summary>
        private void SetupMaterial()
        {
            if (ankhImage == null) ankhImage = GetComponent<Image>();
            if (ankhImage == null || ankhImage.material == null) return;

            ankhMaterialInstance = new Material(ankhImage.material);
            ankhImage.material = ankhMaterialInstance;

            hasColorProperty = !string.IsNullOrEmpty(materialColorName)
                               && ankhMaterialInstance.HasProperty(materialColorName);

            if (!hasColorProperty)
            {
                Debug.LogWarning("AnkhAnimation: 재질에 '" + materialColorName + "' 속성이 없어 " +
                                 "충전 밝기를 표현하지 않습니다.", this);
                return;
            }

            originalColor = ankhMaterialInstance.GetColor(materialColorName);
            ApplyIntensity();
        }

        /// <summary>
        /// 지금 밝기를 재질에 반영합니다. 알파는 건드리지 않습니다.
        /// </summary>
        private void ApplyIntensity()
        {
            if (!hasColorProperty) return;

            Color tinted = originalColor * currentIntensity;
            tinted.a = originalColor.a;
            ankhMaterialInstance.SetColor(materialColorName, tinted);
        }

        /// <summary>슬라이드 트윈을 정리합니다.</summary>
        private void KillSlide()
        {
            if (slideTween != null && slideTween.IsActive()) slideTween.Kill();
            slideTween = null;
        }

        /// <summary>떨림 트윈을 정리합니다.</summary>
        /// <returns>실제로 떨고 있었으면 true 입니다.</returns>
        private bool KillShake()
        {
            if (shakeTween == null) return false;

            bool wasActive = shakeTween.IsActive();
            if (wasActive) shakeTween.Kill();
            shakeTween = null;

            return wasActive;
        }

        /// <summary>모든 트윈을 정리합니다.</summary>
        private void KillAll()
        {
            KillSlide();
            KillShake();

            if (intensityTween != null && intensityTween.IsActive()) intensityTween.Kill();
            intensityTween = null;
        }
    }
}
