using UnityEngine;

/// <summary>
/// 오디오 재생을 위한 정적(static) 헬퍼 클래스입니다.
/// 파괴되는 오브젝트의 사운드(예: 죽음 효과음)를 재생하는 데 유용합니다.
/// </summary>
public static class AudioUtility
{
    /// <summary>
    /// 지정된 위치에 임시 AudioSource를 생성하여 클립을 한 번 재생합니다.
    /// 오브젝트가 파괴된 후에도 사운드가 계속 재생되도록 보장합니다.
    /// </summary>
    /// <param name="clip">재생할 AudioClip</param>
    /// <param name="position">재생할 월드 좌표</param>
    /// <param name="volume">볼륨 (0.0f ~ 1.0f)</param>
    public static void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume = 1.0f)
    {
        if (clip == null) return;

        // 임시 게임 오브젝트 생성
        GameObject tempAudioPlayer = new GameObject($"OneShotAudio_{clip.name}");
        tempAudioPlayer.transform.position = position;

        // AudioSource 컴포넌트 추가 및 설정
        AudioSource audioSource = tempAudioPlayer.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.spatialBlend = 1.0f; // 3D 사운드로 설정
        audioSource.Play();

        // 클립 재생이 끝나면 임시 오브젝트 파괴
        Object.Destroy(tempAudioPlayer, clip.length);
    }

    /// <summary>
    /// AudioSource가 null이거나 클립이 null인지 확인하고 안전하게 클립을 한 번 재생합니다.
    /// </summary>
    public static void PlayOneShot(AudioSource source, AudioClip clip, float volumeScale = 1.0f)
    {
        if (source != null && clip != null)
        {
            source.PlayOneShot(clip, volumeScale);
        }
    }

    /// <summary>
    /// 클립 배열에서 무작위 클립을 선택하여 안전하게 한 번 재생합니다.
    /// </summary>
    public static void PlayOneShotRandom(AudioSource source, AudioClip[] clips, float volumeScale = 1.0f)
    {
        if (source != null && clips != null && clips.Length > 0)
        {
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            source.PlayOneShot(clip, volumeScale);
        }
    }

    /// <summary>
    /// 루프 사운드를 안전하게 시작하거나 교체합니다.
    /// </summary>
    public static void StartLoop(AudioSource source, AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
    {
        if (source == null || clip == null) return;

        if (source.clip != clip)
        {
            source.clip = clip;
            source.Play();
        }

        source.volume = volume;
        source.pitch = pitch;
        source.loop = true;
    }

    /// <summary>
    /// 루프 사운드를 안전하게 중지합니다.
    /// </summary>
    public static void StopLoop(AudioSource source)
    {
        if (source != null)
        {
            source.clip = null;
            source.Stop();
        }
    }
}
