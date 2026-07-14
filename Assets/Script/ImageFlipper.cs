using UnityEngine;

/// <summary>
/// 지정된 시간 간격마다 SpriteRenderer 컴포넌트의 좌우 반전(flipX)을 토글합니다.
/// 이 스크립트는 SpriteRenderer 컴포넌트가 있는 GameObject에 사용되어야 합니다.
/// (참고: 파일명은 ImageFlipper.cs이지만 클래스명은 SpriteFlipper입니다.)
/// </summary>
[RequireComponent(typeof(SpriteRenderer))] // SpriteRenderer 컴포넌트가 필수로 요구됨을 명시
public class SpriteFlipper : MonoBehaviour
{
    // --- Public Member Variables ---

    [Tooltip("스프라이트 좌우 반전을 토글할 시간 간격 (초)")]
    public float flipInterval = 0.5f;

    // --- Private Member Variables ---

    /// <summary>
    /// 좌우 반전을 적용할 대상 SpriteRenderer
    /// </summary>
    private SpriteRenderer targetSprite;

    /// <summary>
    /// 다음 반전까지 남은 시간을 추적하는 타이머
    /// </summary>
    private float timer;

    // --- Unity Event Functions ---

    /// <summary>
    /// 스크립트가 처음 활성화될 때 호출됩니다.
    /// </summary>
    void Start()
    {
        // 같은 GameObject의 SpriteRenderer 컴포넌트 가져오기
        targetSprite = GetComponent<SpriteRenderer>();

        // SpriteRenderer가 없는 경우 오류를 기록하고 스크립트를 비활성화합니다.
        if (targetSprite == null)
        {
            Debug.LogError("SpriteFlipper: SpriteRenderer 컴포넌트를 찾을 수 없습니다! 스크립트가 비활성화됩니다.");
            this.enabled = false;
            return;
        }

        // 타이머를 지정된 간격으로 초기화합니다.
        timer = flipInterval;
    }

    /// <summary>
    /// 매 프레임마다 호출됩니다.
    /// </summary>
    void Update()
    {
        // 타이머에서 경과 시간을 뺍니다.
        timer -= Time.deltaTime;

        // 타이머가 0 이하가 되면
        if (timer <= 0f)
        {
            ToggleFlip(); // 스프라이트를 반전시킵니다.
            timer = flipInterval; // 타이머를 다시 설정합니다.
        }
    }

    // --- Public Methods ---

    /// <summary>
    /// 외부에서 강제로 스프라이트를 원래 상태(반전 없음)로 되돌리고 싶을 때 호출합니다.
    /// </summary>
    public void ResetFlip()
    {
        if (targetSprite == null) return;
        targetSprite.flipX = false;
        timer = flipInterval; // 타이머도 초기화
    }

    /// <summary>
    /// 외부에서 강제로 스프라이트를 반전시키고 싶을 때 호출합니다.
    /// </summary>
    public void ForceFlip()
    {
        if (targetSprite == null) return;
        targetSprite.flipX = true;
        timer = flipInterval; // 타이머도 초기화
    }

    // --- Private Methods ---

    /// <summary>
    /// 스프라이트의 좌우 반전 상태(flipX)를 토글합니다.
    /// (true는 false로, false는 true로 변경)
    /// </summary>
    private void ToggleFlip()
    {
        if (targetSprite == null) return;

        // SpriteRenderer의 flipX 프로퍼티 값을 현재 값의 반대로 설정
        targetSprite.flipX = !targetSprite.flipX;
    }
}
