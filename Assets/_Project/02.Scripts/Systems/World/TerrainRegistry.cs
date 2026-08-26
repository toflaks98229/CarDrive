using UnityEngine;

namespace CarDrive.Systems
{
    /// <summary>
    /// 씬에 있는 지형 목록을 <b>한 번만 찾아 나눠 씁니다.</b>
    ///
    /// <b>왜 만들었는가.</b> 지형 목록을 필요로 하는 곳이 다섯이었고, 다섯이 각자
    /// <c>FindObjectsByType&lt;Terrain&gt;</c> 을 불렀습니다.
    ///
    ///   TerrainChunkCuller  — 2초마다 (경계 캐시를 다시 짜려고)
    ///   TerrainChunkCuller  — 되돌릴 때
    ///   ViewRangeScaler     — 나무 거리·LOD 를 대입할 때
    ///   TerrainDetailLod    — 속도 단계가 바뀔 때
    ///   GpuGrassRenderer    — 풀을 다시 모을 때
    ///
    /// <c>FindObjectsByType</c> 은 그 타입만 훑는 것이 아니라 <b>로드된 오브젝트 전체</b>를
    /// 훑고, 그때마다 새 배열을 할당합니다. 이 월드는 지형이 103장이라 한 번에 824바이트씩
    /// 쓰레기가 생기고, 그 일이 서로 다른 주기로 겹쳐 일어났습니다.
    ///
    /// <b>지형은 좀처럼 늘거나 줄지 않습니다.</b> 굽거나 다시 깔 때만 바뀝니다.
    /// 그래서 한 번 찾아 두고, 주기가 지나거나 누가 알려 줄 때만 다시 찾습니다.
    ///
    /// <b>돌려주는 배열을 들고 있지 마세요.</b> 다음 갱신 때 <b>같은 배열이 다른 내용</b>으로
    /// 바뀌거나 새 배열로 교체됩니다. 그 자리에서 훑고 버리라고 만든 것입니다.
    /// 목록이 바뀌었는지 알아야 하면 <see cref="Version"/> 을 기억해 두고 비교하세요.
    /// </summary>
    public static class TerrainRegistry
    {
        // --- Constants ---

        /// <summary>목록을 다시 찾는 주기(초)입니다.</summary>
        private const float RefreshSeconds = 2f;

        // --- Private Member Variables ---

        /// <summary>마지막으로 찾아 둔 목록입니다.</summary>
        private static Terrain[] cached = System.Array.Empty<Terrain>();

        /// <summary>다음에 다시 찾을 시각입니다.</summary>
        private static float nextRefresh;

        /// <summary>목록을 다시 찾을 때마다 오르는 번호입니다.</summary>
        private static int version;

        // --- Public Properties ---

        /// <summary>
        /// 씬에 있는 모든 지형입니다. <b>꺼져 있는 것도 담습니다.</b>
        ///
        /// <see cref="Gameplay.WorldStreamer"/> 가 멀어진 타일을 꺼 두는데, 켜진 것만 담으면
        /// 그 타일들이 다시 켜졌을 때 낡은 설정을 안고 있게 됩니다.
        /// (나무 거리가 예전 값인 타일 한 장이 혼자 다르게 보이는 식입니다)
        ///
        /// 파괴된 항목이 섞여 있을 수 있으므로 <b>쓰는 쪽에서 null 을 확인하세요.</b>
        /// 목록을 찾은 뒤에 무언가 파괴되면 그 자리는 비어 있게 됩니다.
        /// </summary>
        public static Terrain[] All
        {
            get
            {
                EnsureFresh();
                return cached;
            }
        }

        /// <summary>
        /// 목록의 <b>내용이 바뀔 때마다</b> 오르는 번호입니다.
        ///
        /// 지형마다 무언가를 미리 구해 두는 쪽(<see cref="TerrainChunkCuller"/> 의 경계 캐시)이
        /// <b>다시 구해야 하는지</b>를 이 번호로 판단합니다. 자기 주기를 따로 두면
        /// 목록과 캐시의 갱신 시점이 어긋나, 캐시가 이미 사라진 지형을 가리키는 순간이 생깁니다.
        ///
        /// <b>"다시 찾을 때"가 아니라 "바뀔 때"입니다.</b> 예전에는 2초 주기로 다시 찾을 때마다
        /// 무조건 올랐습니다. 그런데 이 월드의 지형은 <b>굽거나 다시 깔 때만</b> 바뀌므로,
        /// 실제로는 내용이 같은데도 번호가 계속 올랐고 그때마다 컬러가 타일 103장의
        /// 경계 캐시를 통째로 다시 짰습니다. <b>대기 중이던 지면 켜기 신청도 함께 버려졌습니다.</b>
        /// 캐시를 두는 이유가 그 재구성을 없애는 것이었으므로, 2초마다 하는 재구성은
        /// 캐시가 없는 것과 다르지 않았습니다.
        /// </summary>
        public static int Version
        {
            get
            {
                EnsureFresh();
                return version;
            }
        }

        // --- Public Methods ---

        /// <summary>
        /// 다음 조회 때 목록을 <b>반드시</b> 다시 찾게 합니다.
        ///
        /// 지형을 새로 굽거나 월드를 다시 깐 뒤에 부르세요. 주기를 기다리면 그 사이
        /// 사라진 지형을 가리키거나 새 지형을 빠뜨립니다.
        /// </summary>
        public static void Invalidate()
        {
            nextRefresh = 0f;
        }

        // --- Private Methods ---

        /// <summary>
        /// 주기가 지났으면 목록을 다시 찾습니다.
        ///
        /// <b>찾는 것과 번호를 올리는 것은 다른 일입니다.</b> 찾는 것은 2초마다 하지만,
        /// 번호는 <b>내용이 실제로 달라졌을 때만</b> 올립니다. 지형은 굽거나 다시 깔 때만
        /// 바뀌므로 대개는 같은 목록이 나오고, 그때는 찾아 둔 배열도 <b>그대로 둡니다</b> —
        /// 쓰는 쪽이 색인으로 가리키고 있어서 같은 내용이라도 배열이 바뀌면 곤란합니다.
        /// </summary>
        private static void EnsureFresh()
        {
            if (Time.realtimeSinceStartup < nextRefresh) return;

            nextRefresh = Time.realtimeSinceStartup + RefreshSeconds;

            Terrain[] found = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

            WorldProfiler.Count(WorldProfiler.Counter.TerrainScanned);

            // 같은 목록이면 새로 찾은 배열을 버리고 쓰던 것을 그대로 둡니다.
            if (SameAsCached(found)) return;

            cached = found;
            version++;
        }

        /// <summary>
        /// 새로 찾은 목록이 쓰고 있던 것과 같은지 봅니다.
        ///
        /// <b>순서까지 같아야 같다고 봅니다.</b> <c>FindObjectsByType</c> 의 순서는 보장되지
        /// 않으므로, 순서가 흔들리는 환경에서는 이 판정이 늘 거짓이 됩니다. 그래도 <b>손해는
        /// 없습니다</b> — 그때의 동작이 예전과 같아지는 것뿐입니다. 순서가 안정적인 동안에는
        /// 2초마다 일어나던 경계 캐시 재구성이 통째로 사라집니다.
        ///
        /// <c>ReferenceEquals</c> 를 쓰는 것은 파괴된 지형을 <b>파괴되기 전과 같은 것으로
        /// 보지 않기 위해서</b>입니다. 유니티의 <c>==</c> 는 파괴된 객체를 null 처럼 다루므로
        /// 여기서는 답을 흐립니다.
        /// </summary>
        /// <param name="found">방금 찾은 목록</param>
        /// <returns>내용과 순서가 모두 같으면 참</returns>
        private static bool SameAsCached(Terrain[] found)
        {
            if (found.Length != cached.Length) return false;

            for (int i = 0; i < found.Length; i++)
            {
                if (!ReferenceEquals(found[i], cached[i])) return false;
            }

            return true;
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때 찾아 둔 것을 비웁니다.
        /// 에디터에서 도메인 리로드를 꺼 두면 지난 실행의 지형이 그대로 남습니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cached = System.Array.Empty<Terrain>();
            nextRefresh = 0f;
            version = 0;
        }
    }
}
