using UnityEngine;
using System.Collections;
using UnityEngine.UI; // Image 컴포넌트를 사용하기 위해 필요합니다.

/// <summary>
/// UI RectTransform을 제어하여 앙크 사용 애니메이션을 재생합니다.
/// Show/Hide 메서드를 통해 제어하며, 이전 애니메이션을 중지시켜 부드러운 전환을 보장합니다.
/// 앙크 UI Image의 머티리얼 인텐시티를 '서서히' 제어합니다.
/// [추가됨] bool 기반으로 떨리는 애니메이션을 추가하여 PlayerCameraController에서 제어합니다.
/// </summary>
[RequireComponent(typeof(RectTransform))] // 이 컴포넌트는 RectTransform이 반드시 필요합니다.
public class AnkhAnimation : MonoBehaviour
{
    // --- Public Member Variables ---

    [Header("애니메이션 설정")]
    [Tooltip("애니메이션(올라오거나 내려가는)에 걸리는 시간 (초)")]
    public float animationTime = 0.3f;

    [Tooltip("숨겨질 위치의 Y축 오프셋 (현재 위치 기준). 음수 값이어야 아래로 내려갑니다.")]
    public float hiddenYOffset = -500f; // 화면 밖으로 충분히 나갈 수 있는 값

    [Header("머티리얼 설정")]
    [Tooltip("인텐시티를 조절할 앙크의 UI Image 컴포넌트")]
    public Image ankhImage;

    [Tooltip("머티리얼에서 사용할 HDR Color 속성의 이름 (예: _Color)")]
    public string materialColorName = "Color"; // 사용자가 요청한 "Color"를 기본값으로 설정

    [Tooltip("기본 상태의 인텐시티 (밝기)")]
    public float baseIntensity = 1.0f;

    [Tooltip("최대 충전 상태의 인텐시티 (밝기)")]
    public float chargedIntensity = 3.0f;

    [Tooltip("인텐시티가 목표 값으로 변경되는 속도")]
    public float intensityChangeSpeed = 5.0f; // 값이 클수록 빨리 변합니다.

    [Header("떨림 애니메이션 설정")] // [추가됨]
    [Tooltip("떨림 애니메이션이 지속될 시간 (초). PlayerCameraController에서 재설정됩니다.")]
    public float shakeDuration = 0.1f;

    [Tooltip("떨림의 강도 (RectTransform의 위치 변화 폭)")] // [추가됨]
    public float shakeMagnitude = 5.0f;

    [Tooltip("떨림의 속도 (초당 진동 횟수)")] // [추가됨]
    public float shakeSpeed = 50.0f;


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
    /// 현재 실행 중인 애니메이션 코루틴의 참조. (중복 실행 및 중지 용도)
    /// </summary>
    private Coroutine animationCoroutine;

    /// <summary>
    /// 앙크 이미지 머티리얼의 인스턴스. (원본 에셋을 변경하지 않기 위함)
    /// </summary>
    private Material ankhMaterialInstance;

    /// <summary>
    /// 머티리얼의 원본(기본) 색상 값
    /// </summary>
    private Color originalColor;

    /// <summary>
    /// 현재 머티리얼에 적용된 인텐시티 값
    /// </summary>
    private float currentIntensity;

    /// <summary>
    /// 도달해야 할 목표 인텐시티 값
    /// </summary>
    private float targetIntensity;

    /// <summary>
    /// [추가됨] 현재 떨림 애니메이션이 활성화되어 있는지 여부
    /// </summary>
    private bool isShaking = false;

    /// <summary>
    /// [추가됨] 떨림 애니메이션의 남은 시간
    /// </summary>
    private float shakeTimer = 0f;

    /// <summary>
    /// [추가됨] 떨림 애니메이션 시작 시의 RectTransform 원래 위치
    /// </summary>
    private Vector2 originalRectTransformPosition;


    // --- Unity Event Functions ---

    /// <summary>
    /// 스크립트 인스턴스가 로드될 때 호출됩니다. (Start보다 먼저)
    /// 컴포넌트 참조, 위치 계산, 머티리얼 인스턴스화 등 초기 설정을 담당합니다.
    /// </summary>
    void Awake()
    {
        // 1. RectTransform 컴포넌트를 가져옵니다.
        rectTransform = GetComponent<RectTransform>();

        // 2. 현재 UI의 위치를 '보이는 위치'로 저장합니다.
        visiblePosition = rectTransform.anchoredPosition;
        originalRectTransformPosition = rectTransform.anchoredPosition; // [추가됨] 떨림을 위한 초기 위치 저장

        // 3. '숨겨진 위치'를 계산합니다. (Y축에만 오프셋 적용)
        hiddenPosition = new Vector2(visiblePosition.x, visiblePosition.y + hiddenYOffset);

        // 4. 게임 시작 시 '숨겨진 위치'로 즉시 이동시킵니다.
        rectTransform.anchoredPosition = hiddenPosition;

        // 5. 머티리얼 인스턴스 생성 및 초기화
        if (ankhImage != null)
        {
            // 5-1. 머티리얼 인스턴스를 생성하여 원본 에셋이 아닌 복사본을 사용합니다.
            ankhMaterialInstance = new Material(ankhImage.material);
            ankhImage.material = ankhMaterialInstance; // Image가 이 인스턴스를 사용하도록 설정

            // 5-2. 쉐이더에 해당 이름의 속성이 있는지 확인합니다.
            if (ankhMaterialInstance.HasProperty(materialColorName))
            {
                // 5-3. 원본 색상 값을 저장해 둡니다.
                originalColor = ankhMaterialInstance.GetColor(materialColorName);

                // 5-4. 현재 값과 목표 값을 기본 인텐시티로 초기화합니다.
                currentIntensity = baseIntensity;
                targetIntensity = baseIntensity;

                // 5-5. 초기 값을 바로 적용합니다.
                ApplyIntensityToMaterial(currentIntensity);
            }
            else
            {
                // 쉐이더에 해당 속성이 없으면 경고를 출력합니다.
                Debug.LogWarning($"AnkhAnimation: 머티리얼에 '{materialColorName}' 속성이 없습니다. 인텐시티 조절이 작동하지 않습니다.", this);
                ankhMaterialInstance = null; // 작동하지 않으므로 null 처리
            }
        }
        else
        {
            // 앙크 이미지가 할당되지 않았으면 경고를 출력합니다.
            Debug.LogWarning("AnkhAnimation: 'Ankh Image'가 할당되지 않았습니다. 인텐시티 조절이 작동하지 않습니다.", this);
        }
    }

    /// <summary>
    /// 매 프레임마다 호출됩니다.
    /// 머티리얼 인텐시티의 서서한 변경과 떨림 애니메이션을 처리합니다.
    /// </summary>
    void Update()
    {
        // 1. 머티리얼 인텐시티 업데이트
        UpdateMaterialIntensity();

        // 2. 떨림 애니메이션 업데이트
        UpdateShakeAnimation();
    }

    // --- Public Methods ---

    /// <summary>
    /// 앙크를 화면에 보이도록 올립니다. (PlayerCameraController에서 호출)
    /// </summary>
    public void ShowAnkh()
    {
        // 이전에 실행 중인 애니메이션이 있다면 (내려가고 있었다면) 즉시 중지합니다.
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }
        // '보이는 위치'로 이동하는 애니메이션을 시작하고 코루틴 참조를 저장합니다.
        animationCoroutine = StartCoroutine(AnimateToPosition(visiblePosition));
    }

    /// <summary>
    /// 앙크를 화면 밖으로 내립니다. (PlayerCameraController에서 호출)
    /// </summary>
    public void HideAnkh()
    {
        // 이전에 실행 중인 애니메이션이 있다면 (올라오고 있었다면) 즉시 중지합니다.
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }
        // '숨겨진 위치'로 이동하는 애니메이션을 시작하고 코루틴 참조를 저장합니다.
        animationCoroutine = StartCoroutine(AnimateToPosition(hiddenPosition));

        // 앙크를 숨길 때, '목표' 인텐시티가 0이 되도록 설정합니다. (PlayerCameraController에서 호출)
        // 이 스크립트의 Update() 루프가 인텐시티를 서서히 낮추게 됩니다.
    }

    /// <summary>
    /// 앙크 머티리얼의 '목표' 인텐시티를 설정합니다.
    /// 실제 적용은 Update()에서 서서히 일어납니다.
    /// </summary>
    /// <param name="progress">충전 진행률 (0.0f ~ 1.0f)</param>
    public void SetTargetChargeProgress(float progress)
    {
        // LERP(선형 보간)를 사용하여 현재 진행률(progress)에 해당하는 '목표' 인텐시티 값을 계산합니다.
        // progress가 0이면 baseIntensity, 1이면 chargedIntensity가 됩니다.
        targetIntensity = Mathf.Lerp(baseIntensity, chargedIntensity, progress);
    }

    /// <summary>
    /// [추가됨] 떨림 애니메이션을 시작합니다.
    /// </summary>
    public void StartShake()
    {
        // 떨림이 시작될 때 현재 RectTransform의 위치를 원본 위치로 저장합니다.
        originalRectTransformPosition = rectTransform.anchoredPosition;
        isShaking = true; // 떨림 활성화
        shakeTimer = shakeDuration; // 타이머 초기화
    }

    /// <summary>
    /// [추가됨] 떨림 애니메이션을 즉시 중지하고 위치를 원래대로 되돌립니다.
    /// </summary>
    public void StopShake()
    {
        isShaking = false; // 떨림 비활성화
        shakeTimer = 0f; // 타이머 초기화
        // 떨림 중지 시 RectTransform의 위치를 원본 위치로 되돌립니다.
        rectTransform.anchoredPosition = originalRectTransformPosition;
    }

    // --- Private Methods ---

    /// <summary>
    /// [추가됨] 머티리얼 인텐시티를 목표 값으로 서서히 업데이트하는 로직을 처리합니다.
    /// </summary>
    private void UpdateMaterialIntensity()
    {
        // 머티리얼 인스턴스가 없으면 아무것도 실행하지 않습니다.
        if (ankhMaterialInstance == null) return;

        // 현재 인텐시티와 목표 인텐시티가 거의 같으면 (0.01f 차이 이내) 추가 계산을 중지합니다.
        if (Mathf.Abs(currentIntensity - targetIntensity) < 0.01f)
        {
            // 값이 거의 같으면 정확한 목표 값으로 설정하고 종료
            if (currentIntensity != targetIntensity)
            {
                currentIntensity = targetIntensity;
                ApplyIntensityToMaterial(currentIntensity);
            }
            return; // 변경이 필요 없으므로 함수 종료
        }

        // MoveTowards를 사용하여 일정한 속도로 인텐시티를 변경합니다.
        currentIntensity = Mathf.MoveTowards(
            currentIntensity,       // 현재 값
            targetIntensity,        // 목표 값
            intensityChangeSpeed * Time.deltaTime // 1초에 intensityChangeSpeed 만큼 이동
        );

        // 계산된 현재 인텐시티 값을 머티리얼에 적용합니다.
        ApplyIntensityToMaterial(currentIntensity);
    }

    /// <summary>
    /// [추가됨] 떨림 애니메이션을 매 프레임 업데이트합니다.
    /// </summary>
    private void UpdateShakeAnimation()
    {
        if (!isShaking) return; // 떨림이 활성화되지 않았다면 아무것도 안 함

        // 떨림 타이머 감소
        shakeTimer -= Time.deltaTime;

        // 떨림이 지속될 시간 동안
        if (shakeTimer > 0)
        {
            // Sin 함수를 사용하여 위치를 주기적으로 변경합니다.
            // Time.time * shakeSpeed는 시간에 따라 값이 계속 변하며, Sin 함수는 -1에서 1 사이의 값을 반환합니다.
            // 이 값에 shakeMagnitude를 곱하여 떨림의 강도를 조절합니다.
            float offsetX = Mathf.PerlinNoise(Time.time * shakeSpeed, 0f) * 2f - 1f; // -1에서 1 사이의 랜덤 값
            float offsetY = Mathf.PerlinNoise(0f, Time.time * shakeSpeed) * 2f - 1f; // -1에서 1 사이의 랜덤 값
            // 또는 Mathf.Sin을 사용한 더 규칙적인 떨림:
            // float offsetX = Mathf.Sin(Time.time * shakeSpeed) * shakeMagnitude;
            // float offsetY = Mathf.Cos(Time.time * shakeSpeed * 1.2f) * shakeMagnitude;

            // RectTransform의 현재 위치에 떨림 값을 더해줍니다.
            // 주의: originalRectTransformPosition을 기준으로 떨리게 하는 것이 더 좋습니다.
            rectTransform.anchoredPosition = visiblePosition + new Vector2(offsetX, offsetY) * shakeMagnitude;
        }
        else
        {
            // 떨림 타이머가 끝나면 떨림을 중지합니다.
            StopShake();
        }
    }

    /// <summary>
    /// 계산된 인텐시티 값을 실제 머티리얼에 적용하는 도우미 함수
    /// </summary>
    /// <param name="intensity">적용할 인텐시티 값</param>
    private void ApplyIntensityToMaterial(float intensity)
    {
        // 머티리얼 인스턴스가 유효한지 다시 한번 확인합니다.
        if (ankhMaterialInstance == null) return;

        // 원본 색상(originalColor)에 계산된 인텐시티(밝기)를 곱하여 HDR 컬러를 설정합니다.
        // (Color * float 연산은 각 채널(R, G, B)에 float 값을 곱합니다)
        ankhMaterialInstance.SetColor(materialColorName, originalColor * intensity);
    }


    // --- Coroutines ---

    /// <summary>
    /// 현재 위치에서 목표 위치로 UI를 부드럽게 이동시키는 코루틴입니다.
    /// </summary>
    /// <param name="targetPosition">도착할 최종 위치 (visiblePosition 또는 hiddenPosition)</param>
    private IEnumerator AnimateToPosition(Vector2 targetPosition)
    {
        float timer = 0f; // 경과 시간
        Vector2 startPosition = rectTransform.anchoredPosition; // 애니메이션 시작 위치

        // 현재 위치와 목표 위치가 거의 같다면 (0.01f 이내) 애니메이션을 실행할 필요가 없습니다.
        if (Vector2.Distance(startPosition, targetPosition) < 0.01f)
        {
            rectTransform.anchoredPosition = targetPosition; // 위치 보정
            animationCoroutine = null; // 코루틴 참조 비우기
            yield break; // 코루틴 즉시 종료
        }

        // 지정된 animationTime 동안
        while (timer < animationTime)
        {
            // Lerp(시작, 끝, 비율)을 사용하여 부드럽게 위치를 이동시킵니다.
            // 비율 = (경과 시간 / 총 시간)
            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, timer / animationTime);

            timer += Time.deltaTime; // 경과 시간 추가
            yield return null; // 1프레임 대기
        }

        // 애니메이션 종료 후 정확한 '목표 위치'로 보정합니다.
        rectTransform.anchoredPosition = targetPosition;
        animationCoroutine = null; // 애니메이션 완료, 코루틴 참조 비우기
    }
}

