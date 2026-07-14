using UnityEngine;

/// <summary>
/// 차량의 모든 사운드(엔진, 충돌, 시동)를 전담하는 컨트롤러입니다.
/// CarController와 같은 GameObject에 추가해야 합니다.
/// </summary>
[RequireComponent(typeof(CarController))]
public class CarSoundController : MonoBehaviour
{
    [Header("오디오 소스 (AudioSources)")]
    [Tooltip("엔진 루프 사운드를 재생할 AudioSource")]
    public AudioSource engineSource;
    [Tooltip("시동, 충돌 등 효과음을 재생할 AudioSource")]
    public AudioSource effectsSource;

    [Header("오디오 클립 (AudioClips)")]
    [Tooltip("시동 거는 소리")]
    public AudioClip engineStartClip;
    [Tooltip("엔진 작동 루프 (RPM에 따라 피치 조절됨)")]
    public AudioClip engineLoopClip;
    [Tooltip("시동 끄는 소리")]
    public AudioClip engineStopClip;
    [Tooltip("적 또는 장애물과 충돌 시 재생될 무작위 소리")]
    public AudioClip[] collisionClips;

    [Header("엔진 사운드 설정")]
    [Tooltip("엔진 소리가 나지 않는 최소 RPM")]
    public float minRpm = 800f;
    [Tooltip("엔진 피치가 최대가 되는 RPM")]
    public float maxRpm = 6000f;
    [Tooltip("최소 RPM일 때의 엔진 피치")]
    public float minPitch = 0.8f;
    [Tooltip("최대 RPM일 때의 엔진 피치")]
    public float maxPitch = 2.5f;

    private CarController carController;

    void Start()
    {
        carController = GetComponent<CarController>();
        if (engineSource != null)
        {
            engineSource.clip = engineLoopClip;
        }
    }

    void Update()
    {
        UpdateEngineSound();
    }

    /// <summary>
    /// CarController의 현재 RPM을 기반으로 엔진 사운드를 조절합니다.
    /// </summary>
    private void UpdateEngineSound()
    {
        if (engineSource == null || !carController.IsEngineOn())
        {
            if (engineSource != null && engineSource.isPlaying)
            {
                engineSource.Stop();
            }
            return;
        }

        if (!engineSource.isPlaying)
        {
            engineSource.Play();
        }

        // RPM 비율 계산 (0.0 ~ 1.0)
        float rpmRatio = Mathf.InverseLerp(minRpm, maxRpm, carController.GetCurrentRPM());

        // 피치 조절
        engineSource.pitch = Mathf.Lerp(minPitch, maxPitch, rpmRatio);

        // 볼륨 조절 (스로틀 입력에 따라)
        float throttleInput = Mathf.Abs(Input.GetAxis("Vertical"));
        engineSource.volume = Mathf.Lerp(0.5f, 1.0f, throttleInput);
    }

    /// <summary>
    /// 시동 사운드를 재생합니다. (CarController에서 호출)
    /// </summary>
    public void PlayEngineStart()
    {
        AudioUtility.PlayOneShot(effectsSource, engineStartClip);
    }

    /// <summary>
    /// 시동 끄는 사운드를 재생합니다. (CarController에서 호출)
    /// </summary>
    public void PlayEngineStop()
    {
        AudioUtility.PlayOneShot(effectsSource, engineStopClip);
    }

    /// <summary>
    /// 충돌 사운드를 재생합니다. (CarCollisionHandler에서 호출)
    /// </summary>
    /// <param name="impactStrength">충돌 강도 (0.0 ~ 1.0), 볼륨 조절에 사용</param>
    public void PlayCollisionSound(float impactStrength)
    {
        float volume = Mathf.Clamp(impactStrength, 0.3f, 1.0f);
        AudioUtility.PlayOneShotRandom(effectsSource, collisionClips, volume);
    }
}
