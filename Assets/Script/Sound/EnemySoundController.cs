using UnityEngine;

/// <summary>
/// EnemyController(추적형 적)의 사운드를 전담합니다.
/// EnemyController와 같은 GameObject에 추가해야 합니다.
/// </summary>
[RequireComponent(typeof(EnemyController), typeof(AudioSource))]
public class EnemySoundController : MonoBehaviour
{
    [Header("오디오 소스 (AudioSources)")]
    [Tooltip("추적 중 루프 사운드를 재생할 AudioSource")]
    public AudioSource loopSource;
    [Tooltip("피격 등 효과음을 재생할 AudioSource")]
    public AudioSource effectsSource;

    [Header("오디오 클립 (AudioClips)")]
    [Tooltip("스폰(활성화) 시 사운드")]
    public AudioClip spawnSound;
    [Tooltip("추적 중 루프 사운드")]
    public AudioClip chaseLoop;
    [Tooltip("피격 시 재생될 무작위 사운드")]
    public AudioClip[] takeDamageClips;
    [Tooltip("죽을 때 사운드 (오브젝트가 파괴되어도 재생됨)")]
    public AudioClip deathClip;

    private EnemyController enemyController;

    void Start()
    {
        enemyController = GetComponent<EnemyController>();

        // 스폰 사운드 재생
        AudioUtility.PlayOneShot(effectsSource, spawnSound);

        // 추적 루프 시작
        AudioUtility.StartLoop(loopSource, chaseLoop, 1.0f, 1.0f);
    }

    /// <summary>
    /// 피격 사운드를 재생합니다. (EnemyController가 호출)
    /// </summary>
    public void PlayTakeDamageSound()
    {
        AudioUtility.PlayOneShotRandom(effectsSource, takeDamageClips);
    }

    /// <summary>
    /// 죽음 사운드를 재생합니다. (EnemyController가 호출)
    /// </summary>
    public void PlayDeathSound()
    {
        // 루프 사운드 중지
        AudioUtility.StopLoop(loopSource);

        // 오브젝트가 파괴될 것이므로, AudioUtility를 사용해 위치 기반으로 재생
        AudioUtility.PlayClipAtPoint(deathClip, transform.position);
    }
}
