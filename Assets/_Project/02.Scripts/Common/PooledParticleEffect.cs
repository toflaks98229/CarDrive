using System.Collections;
using UnityEngine;

namespace CarDrive.Common
{
    /// <summary>
    /// 한 번 재생하고 스스로 <see cref="PrefabPool"/>로 돌아가는 일회성 파티클입니다.
    ///
    /// 사망 파티클은 지금까지 <c>Instantiate</c>로 만들어지고 프리팹의
    /// Stop Action(Destroy)으로 사라졌습니다. 전투가 벌어지는 내내 GameObject가
    /// 만들어지고 버려진다는 뜻이라, 귀신 스폰이 최대 3배까지 좁아지는
    /// 폭우·야간 구간에 정확히 이 경로가 열립니다.
    ///
    /// <b>파티클을 풀에 넣을 때 조심할 것이 하나 있습니다.</b> 프리팹이 Stop Action을
    /// Destroy로 두고 있으면 풀에서 꺼낸 인스턴스가 파괴되어 풀에 빈 자리(파괴된 참조)가
    /// 남습니다. 그래서 <see cref="Spawn"/>은 꺼낼 때마다 Stop Action을 Callback으로
    /// 덮어쓰고, 회수는 반드시 이 컴포넌트가 합니다.
    ///
    /// 실행 중에 붙이므로 인스펙터에서 직접 추가할 일은 없습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PooledParticleEffect : MonoBehaviour
    {
        // --- Private Member Variables ---

        /// <summary>재생할 파티클 시스템입니다. 이 오브젝트에서 가져옵니다.</summary>
        private ParticleSystem system;

        /// <summary>이미 풀로 돌려보냈는지 여부입니다. 콜백과 안전장치가 겹쳐도 한 번만 반납합니다.</summary>
        private bool released;

        // --- Public Methods ---

        /// <summary>
        /// 파티클을 풀에서 꺼내 지정한 자리에서 한 번 재생합니다.
        /// 재생이 끝나면 스스로 풀로 돌아갑니다.
        /// </summary>
        /// <param name="prefab">재생할 파티클 프리팹. null이면 아무것도 하지 않습니다.</param>
        /// <param name="position">재생할 월드 좌표</param>
        /// <param name="rotation">재생할 회전</param>
        public static void Spawn(ParticleSystem prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return;

            // 부모를 붙이지 않습니다. 귀신은 차량의 자식으로 붙어 있어서,
            // 부모를 물려받으면 사망 파티클이 차를 따라다닙니다.
            GameObject instance = PrefabPool.Get(prefab.gameObject, position, rotation, null);
            if (instance == null) return;

            PooledParticleEffect effect = instance.GetComponent<PooledParticleEffect>();
            if (effect == null) effect = instance.AddComponent<PooledParticleEffect>();

            effect.PlayOnce();
        }

        // --- Unity Event Functions ---

        /// <summary>
        /// 파티클이 완전히 멈추면 Unity가 불러 줍니다. (Stop Action이 Callback일 때)
        /// </summary>
        private void OnParticleSystemStopped()
        {
            Release();
        }

        // --- Private Methods ---

        /// <summary>
        /// 이번 한 번의 재생을 시작합니다. 풀에서 나온 인스턴스는 지난번 상태가
        /// 그대로 남아 있으므로 반드시 지우고 다시 재생합니다.
        /// </summary>
        private void PlayOnce()
        {
            if (system == null) system = GetComponent<ParticleSystem>();
            if (system == null)
            {
                Debug.LogWarning("PooledParticleEffect: ParticleSystem이 없어 재생할 수 없습니다.", this);
                PrefabPool.Release(gameObject);
                return;
            }

            released = false;

            ParticleSystem.MainModule main = system.main;
            main.stopAction = ParticleSystemStopAction.Callback;
            main.loop = false;

            system.Clear(true);
            system.Play(true);

            // 하위 파티클 구성에 따라 콜백이 오지 않는 경우가 있습니다.
            // 그대로 두면 인스턴스가 영영 풀로 돌아오지 않으므로 시간으로도 회수합니다.
            float lifetime = main.duration + Mathf.Max(2f, main.startLifetime.constantMax) + 0.5f;
            StartCoroutine(ReleaseAfter(lifetime));
        }

        /// <summary>
        /// 지정한 시간이 지나면 회수합니다. 콜백이 먼저 오면 이 코루틴은 버려집니다.
        /// </summary>
        /// <param name="seconds">기다릴 시간(초)</param>
        private IEnumerator ReleaseAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            Release();
        }

        /// <summary>
        /// 풀로 돌려보냅니다. 두 번 불려도 한 번만 반납합니다.
        /// </summary>
        private void Release()
        {
            if (released) return;
            released = true;

            StopAllCoroutines();
            PrefabPool.Release(gameObject);
        }
    }
}
