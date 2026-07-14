using UnityEngine;

/// <summary>
/// 장애물(Obstacle) 등 환경 오브젝트의 사운드를 전담합니다.
/// ObstacleController와 같은 GameObject에 추가해야 합니다.
/// </summary>
[RequireComponent(typeof(ObstacleController), typeof(AudioSource))]
public class EnvironmentSoundController : MonoBehaviour
{
    [Header("오디오 소스 (AudioSource)")]
    [Tooltip("충돌 효과음을 재생할 AudioSource")]
    public AudioSource effectsSource;

    [Header("오디오 클립 (AudioClips)")]
    [Tooltip("충돌 시 재생될 무작위 사운드")]
    public AudioClip[] hitClips;

    /// <summary>
    /// 충돌 사운드를 재생합니다. (ObstacleController가 호출)
    /// </summary>
    /// <param name="impactStrength">충돌 강도 (0.0 ~ 1.0), 볼륨 조절에 사용</param>
    public void PlayHitSound(float impactStrength)
    {
        float volume = Mathf.Clamp(impactStrength, 0.2f, 1.0f);
        AudioUtility.PlayOneShotRandom(effectsSource, hitClips, volume);
    }
}
