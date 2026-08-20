using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace CarDrive.Systems
{
    /// <summary>
    /// 위치 기반 일회성 사운드를 재생할 AudioSource를 재사용합니다.
    ///
    /// 예전에는 <c>AudioUtility.PlayClipAtPoint</c>가 소리 <b>한 번마다</b> GameObject를
    /// 만들고 클립 길이만큼 뒤에 파괴했습니다. 그런데 이 게임에서 그 경로를 타는 소리는
    /// 드물지 않습니다. 앙크 피격음이 0.25초 간격, 귀신 공격음이 1초 간격입니다.
    /// 즉 전투가 벌어지는 내내 GameObject가 만들어지고 버려지고 있었습니다.
    ///
    /// 그래서 AudioSource 몇 개를 돌려 씁니다. 재생이 끝난 것을 매 프레임 확인해
    /// 풀로 돌려보내므로, 소리가 겹치는 만큼만 늘어나고 그 이상은 만들지 않습니다.
    ///
    /// 이 컴포넌트는 처음 필요할 때 스스로 만들어집니다. 씬에 미리 놓을 필요가 없습니다.
    /// </summary>
    public class OneShotAudioPool : MonoBehaviour
    {
        // --- Private Member Variables ---

        /// <summary>실행 중 단 하나뿐인 풀입니다. 처음 재생할 때 만들어집니다.</summary>
        private static OneShotAudioPool instance;

        /// <summary>쉬고 있는 AudioSource를 담아 두는 풀입니다.</summary>
        private ObjectPool<AudioSource> pool;

        /// <summary>지금 소리를 내고 있는 것들입니다. 다 끝나면 풀로 돌려보냅니다.</summary>
        private readonly List<AudioSource> playing = new List<AudioSource>();

        // --- Public Methods ---

        /// <summary>
        /// 지정한 위치에서 클립을 한 번 재생합니다.
        /// 소리를 낸 오브젝트가 곧바로 사라져도 끝까지 들립니다.
        /// </summary>
        /// <param name="clip">재생할 클립. null이면 아무것도 하지 않습니다.</param>
        /// <param name="position">재생할 월드 좌표</param>
        /// <param name="volume">볼륨 (0~1)</param>
        public static void Play(AudioClip clip, Vector3 position, float volume)
        {
            if (clip == null) return;

            EnsureInstance();
            if (instance == null) return;

            instance.PlayPooled(clip, position, volume);
        }

        // --- Unity Event Functions ---

        /// <summary>
        /// AudioSource 풀을 준비합니다.
        /// </summary>
        void Awake()
        {
            pool = new ObjectPool<AudioSource>(
                createFunc: CreateSource,
                actionOnGet: OnGetSource,
                actionOnRelease: OnReleaseSource,
                actionOnDestroy: OnDestroySource,
                collectionCheck: true,
                defaultCapacity: 8,
                maxSize: 32);
        }

        /// <summary>
        /// 재생이 끝난 AudioSource를 찾아 풀로 돌려보냅니다.
        /// 클립 길이로 시간을 재지 않고 실제 재생 상태를 보는 이유는,
        /// 오디오가 Time.timeScale의 영향을 받지 않아 타이머와 어긋날 수 있기 때문입니다.
        /// </summary>
        void Update()
        {
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                AudioSource source = playing[i];

                if (source == null)
                {
                    playing.RemoveAt(i);
                    continue;
                }

                if (source.isPlaying) continue;

                playing.RemoveAt(i);
                pool.Release(source);
            }
        }

        /// <summary>
        /// 풀이 파괴되면 전역 참조도 비웁니다.
        /// </summary>
        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // --- Private Methods ---

        /// <summary>
        /// 플레이 모드에 들어갈 때 정적 상태를 비웁니다.
        /// 에디터에서 도메인 리로드를 꺼 두면 static 값이 지난 실행에서 그대로 남기 때문입니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        /// <summary>
        /// 풀이 아직 없으면 만듭니다.
        /// </summary>
        private static void EnsureInstance()
        {
            if (instance != null) return;

            GameObject host = new GameObject("OneShotAudioPool");
            instance = host.AddComponent<OneShotAudioPool>();
        }

        /// <summary>
        /// 풀에서 AudioSource를 하나 꺼내 재생을 시작합니다.
        /// </summary>
        /// <param name="clip">재생할 클립</param>
        /// <param name="position">재생할 월드 좌표</param>
        /// <param name="volume">볼륨</param>
        private void PlayPooled(AudioClip clip, Vector3 position, float volume)
        {
            AudioSource source = pool.Get();
            if (source == null) return;

            source.transform.position = position;

            // 돌려 쓰는 것이라 지난번 설정이 남아 있습니다. 매번 전부 다시 잡습니다.
            source.clip = clip;
            source.volume = volume;
            source.pitch = 1f;
            source.loop = false;
            source.spatialBlend = 1f;   // 3D 사운드
            source.Play();

            playing.Add(source);
        }

        /// <summary>
        /// 풀이 쓸 새 AudioSource를 만듭니다. 꺼진 상태로 시작합니다.
        /// </summary>
        /// <returns>재생 준비가 된 AudioSource</returns>
        private AudioSource CreateSource()
        {
            GameObject go = new GameObject("PooledOneShot");
            go.transform.SetParent(transform, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;

            go.SetActive(false);
            return source;
        }

        /// <summary>
        /// 꺼내 쓸 때 오브젝트를 켭니다. 꺼져 있으면 소리가 나지 않습니다.
        /// </summary>
        /// <param name="source">꺼낸 AudioSource</param>
        private void OnGetSource(AudioSource source)
        {
            if (source != null) source.gameObject.SetActive(true);
        }

        /// <summary>
        /// 돌려받을 때 재생을 멈추고 클립 참조를 놓습니다.
        /// (참조를 쥐고 있으면 쓰지도 않는 오디오 클립이 메모리에 남습니다)
        /// </summary>
        /// <param name="source">돌려받은 AudioSource</param>
        private void OnReleaseSource(AudioSource source)
        {
            if (source == null) return;

            source.Stop();
            source.clip = null;
            source.gameObject.SetActive(false);
        }

        /// <summary>
        /// 풀이 넘칠 때 실제로 파괴합니다.
        /// </summary>
        /// <param name="source">파괴할 AudioSource</param>
        private void OnDestroySource(AudioSource source)
        {
            if (source != null) Destroy(source.gameObject);
        }
    }
}
