using UnityEngine;
using System.Collections; // Coroutine을 사용하기 위해 필요합니다.

/// <summary>
/// UI RectTransform을 제어하여 음료수 마시기 애니메이션을 재생합니다.
/// 이 스크립트는 애니메이션을 적용할 UI Image에 부착해야 합니다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DrinkAnimation : MonoBehaviour
{
    // --- Public Member Variables ---

    [Header("애니메이션 설정")]
    [Tooltip("애니메이션(올라오거나 내려가는)에 걸리는 시간 (초)")]
    public float animationTime = 0.3f;

    [Tooltip("음료가 화면에 머무르는 시간 (초)")]
    public float visibleDuration = 1.0f;

    [Tooltip("숨겨질 위치의 Y축 오프셋 (현재 위치 기준). 음수 값이어야 아래로 내려갑니다.")]
    public float hiddenYOffset = -500f; // 화면 밖으로 충분히 나갈 수 있는 값

    // --- Private Member Variables ---

    /// <summary>
    /// 애니메이션을 적용할 UI의 RectTransform 컴포넌트
    /// </summary>
    private RectTransform rectTransform;

    /// <summary>
    /// UI가 화면에 보여질 때의 위치 (에디터에서 설정한 초기 위치)
    /// </summary>
    private Vector2 visiblePosition;

    /// <summary>
    /// UI가 화면 밖에 숨겨질 때의 위치 (visiblePosition + hiddenYOffset)
    /// </summary>
    private Vector2 hiddenPosition;

    /// <summary>
    /// 현재 애니메이션이 실행 중인지 여부 (중복 실행 방지 플래그)
    /// </summary>
    private bool isAnimating = false;

    // --- Unity Event Functions ---

    /// <summary>
    /// Start보다 먼저, 스크립트 인스턴스가 로드될 때 호출됩니다.
    /// </summary>
    void Awake()
    {
        // RectTransform 컴포넌트를 가져옵니다.
        rectTransform = GetComponent<RectTransform>();

        // 1. 현재 UI의 위치를 '보이는 위치'로 저장합니다.
        // (Unity 에디터에서 UI를 최종 위치에 배치한 상태로 시작해야 함)
        visiblePosition = rectTransform.anchoredPosition;

        // 2. '숨겨진 위치'를 계산합니다. (Y축에만 오프셋 적용)
        hiddenPosition = new Vector2(visiblePosition.x, visiblePosition.y + hiddenYOffset);

        // 3. 게임 시작 시 '숨겨진 위치'로 즉시 이동시킵니다.
        rectTransform.anchoredPosition = hiddenPosition;
    }

    // --- Public Methods ---

    /// <summary>
    /// 외부(예: PlayerCameraController)에서 호출하여 애니메이션을 시작합니다.
    /// </summary>
    public void PlayDrinkAnimation()
    {
        // 이미 애니메이션이 실행 중이면 다시 실행하지 않습니다.
        if (!isAnimating)
        {
            // 애니메이션 코루틴을 시작합니다.
            StartCoroutine(AnimateDrink());
        }
    }

    // --- Coroutines ---

    /// <summary>
    /// UI를 올리고, 기다렸다가, 내리는 전체 애니메이션을 처리하는 코루틴입니다.
    /// </summary>
    private IEnumerator AnimateDrink()
    {
        // 애니메이션 시작, 중복 방지 플래그 설정
        isAnimating = true;
        float timer = 0f;

        // --- 1. 올라오는 애니메이션 (숨겨진 위치 -> 보이는 위치) ---
        while (timer < animationTime)
        {
            // Lerp(시작, 끝, 비율)을 사용하여 부드럽게 위치를 이동시킵니다.
            rectTransform.anchoredPosition = Vector2.Lerp(hiddenPosition, visiblePosition, timer / animationTime);
            timer += Time.deltaTime; // 경과 시간 추가
            yield return null; // 1프레임 대기
        }
        // 애니메이션 종료 후 정확한 '보이는 위치'로 보정합니다.
        rectTransform.anchoredPosition = visiblePosition;

        // --- 2. 화면에서 대기 ---
        // 지정된 시간(visibleDuration)만큼 화면에 머무릅니다.
        yield return new WaitForSeconds(visibleDuration);

        // --- 3. 내려가는 애니메이션 (보이는 위치 -> 숨겨진 위치) ---
        timer = 0f; // 타이머 초기화
        while (timer < animationTime)
        {
            // Lerp를 사용하여 '숨겨진 위치'로 부드럽게 이동합니다.
            rectTransform.anchoredPosition = Vector2.Lerp(visiblePosition, hiddenPosition, timer / animationTime);
            timer += Time.deltaTime;
            yield return null; // 1프레임 대기
        }
        // 애니메이션 종료 후 정확한 '숨겨진 위치'로 보정합니다.
        rectTransform.anchoredPosition = hiddenPosition;

        // 애니메이션 종료, 플래그 해제
        isAnimating = false;
    }
}
