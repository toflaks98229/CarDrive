using UnityEngine;

/// <summary>
/// AttachedGhostController(부착형 적)의 사운드를 전담합니다.
/// AttachedGhostController와 같은 GameObject에 추가해야 합니다.
/// </summary>
[RequireComponent(typeof(AttachedGhostController), typeof(AudioSource))]
public class AttachedGhostSoundController : MonoBehaviour
{
    [Header("오디오 소스 (AudioSources)")]
    [Tooltip("공격/속삭임 루프 사운드")]
    public AudioSource loopSource;
    [Tooltip("피격, 스폰 등 효과음")]
    public AudioSource effectsSource;

    [Header("오디오 클립 (AudioClips)")]
    [Tooltip("스폰 시 사운드")]
    public AudioClip spawnSound;
    [Tooltip("차량에 부착 후 공격(지속) 루프 사운드")]
    public AudioClip attackLoop;
    [Tooltip("주기적 공격 시 효과음")]
    public AudioClip[] attackImpactClips;
    [Tooltip("피격 시 재생될 무작위 사운드")]
    public AudioClip[] takeDamageClips;
    [Tooltip("죽을 때 사운드 (오브젝트가 파괴되어도 재생됨)")]
    public AudioClip deathClip;

    private AttachedGhostController ghostController;

    void Start()
    {
        ghostController = GetComponent<AttachedGhostController>();

        // 스폰 사운드 재생
        AudioUtility.PlayOneShot(effectsSource, spawnSound);
    }

    /// <summary>
    /// 차량에 부착되었을 때 공격 루프를 시작합니다. (AttachedGhostController가 호출)
    /// </summary>
    public void StartAttackLoop()
    {
        AudioUtility.StartLoop(loopSource, attackLoop, 1.0f, 1.0f);
    }

    /// <summary>
    /// 주기적 데미지 효과음을 재생합니다. (AttachedGhostController가 호출)
    /// </summary>
    public void PlayAttackImpact()
    {
        AudioUtility.PlayOneShotRandom(effectsSource, attackImpactClips);
    }

    /// <summary>
    /// 피격 사운드를 재생합니다. (AttachedGhostController가 호출)
    /// </summary>
    public void PlayTakeDamageSound()
    {
        AudioUtility.PlayOneShotRandom(effectsSource, takeDamageClips);
    }

    /// <summary>
    /// 죽음 사운드를 재생합니다. (AttachedGhostController가 호출)
    /// </summary>
    public void PlayDeathSound()
    {
        // 루프 사운드 중지
        AudioUtility.StopLoop(loopSource);

        // 오브젝트가 파괴될 것이므로, AudioUtility를 사용해 위치 기반으로 재생
        AudioUtility.PlayClipAtPoint(deathClip, transform.position);
    }
}
