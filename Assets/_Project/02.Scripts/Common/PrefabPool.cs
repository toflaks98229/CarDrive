using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace CarDrive.Common
{
    /// <summary>
    /// 프리팹별로 인스턴스를 재사용하는 오브젝트 풀입니다.
    ///
    /// 귀신은 15~30초마다 생기고 죽을 때마다 파괴되었습니다. 여기까지는 견딜 만하지만
    /// GhostSpawner는 날씨·시간 활동량에 따라 <b>스폰 간격을 최대 3배까지 좁히도록</b>
    /// 설계되어 있습니다. 폭우가 쏟아지는 밤, 즉 게임이 가장 몰아치는 구간이 하필
    /// 할당과 GC가 가장 바쁜 구간이 됩니다. 픽셀 룩이라 프레임이 튀면 특히 눈에 띕니다.
    ///
    /// 그래서 Instantiate/Destroy를 Get/Release로 바꿉니다.
    /// Unity 2021부터 <see cref="ObjectPool{T}"/>가 내장되어 있어 직접 만들 것은 거의 없습니다.
    ///
    /// <b>재사용되는 오브젝트는 상태를 스스로 되돌려야 합니다.</b> 파괴되지 않으므로
    /// 체력·타이머·연출이 지난번 값 그대로 남아 있습니다. (AttachedGhostController 참고)
    ///
    /// 주의: 풀은 씬에 놓인 보관용 오브젝트 아래에 인스턴스를 모아 둡니다.
    /// 씬을 새로 불러오면 그 보관함도 함께 사라지므로 <see cref="Clear"/>로 비워야 합니다.
    /// 지금 이 게임은 씬을 다시 불러오지 않으므로 플레이 시작 시 한 번만 비웁니다.
    /// </summary>
    public static class PrefabPool
    {
        // --- Private Member Variables ---

        /// <summary>프리팹별 풀입니다. 프리팹 하나가 풀 하나를 가집니다.</summary>
        private static readonly Dictionary<GameObject, ObjectPool<GameObject>> pools =
            new Dictionary<GameObject, ObjectPool<GameObject>>();

        /// <summary>쉬고 있는 인스턴스를 모아 두는 곳입니다. 하이어라키가 어지러워지지 않게 묶어 둡니다.</summary>
        private static Transform parking;

        // --- Public Methods ---

        /// <summary>
        /// 풀에서 인스턴스를 하나 꺼내 배치합니다. 풀이 비어 있으면 새로 만듭니다.
        /// </summary>
        /// <param name="prefab">꺼낼 프리팹. null이면 아무것도 하지 않습니다.</param>
        /// <param name="position">배치할 월드 좌표</param>
        /// <param name="rotation">배치할 회전</param>
        /// <param name="parent">붙일 부모. null이면 보관함에서 떼어 최상위에 둡니다.</param>
        /// <returns>활성화된 인스턴스. 프리팹이 null이면 null입니다.</returns>
        public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (prefab == null) return null;

            GameObject instance = GetPool(prefab).Get();
            if (instance == null) return null;

            // 위치를 먼저 잡고 마지막에 켭니다.
            // 그래야 OnEnable이 도는 시점에 이미 제자리에 있습니다.
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            return instance;
        }

        /// <summary>
        /// 인스턴스를 풀로 돌려보냅니다.
        ///
        /// 풀에서 나온 것이 아니면 예전처럼 파괴합니다. 그래야 호출부가
        /// "이게 풀에서 온 것인지"를 따로 기억하지 않아도 됩니다.
        /// </summary>
        /// <param name="instance">돌려보낼 인스턴스</param>
        /// <returns>풀로 돌아갔으면 true, 파괴했으면 false입니다.</returns>
        public static bool Release(GameObject instance)
        {
            if (instance == null) return false;

            PooledObject tag = instance.GetComponent<PooledObject>();
            ObjectPool<GameObject> pool;

            if (tag == null || tag.SourcePrefab == null || !pools.TryGetValue(tag.SourcePrefab, out pool))
            {
                Object.Destroy(instance);
                return false;
            }

            pool.Release(instance);
            return true;
        }

        /// <summary>
        /// 모든 풀을 비웁니다. 씬을 다시 불러들일 때처럼 보관함이 사라지는 경우에 씁니다.
        /// </summary>
        public static void Clear()
        {
            foreach (KeyValuePair<GameObject, ObjectPool<GameObject>> entry in pools)
            {
                if (entry.Value != null) entry.Value.Clear();
            }
            pools.Clear();

            if (parking != null) Object.Destroy(parking.gameObject);
            parking = null;
        }

        // --- Private Methods ---

        /// <summary>
        /// 플레이 모드에 들어갈 때 정적 상태를 비웁니다.
        /// 에디터에서 도메인 리로드를 꺼 두면 static 값이 지난 실행에서 그대로 남기 때문입니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pools.Clear();
            parking = null;
        }

        /// <summary>
        /// 프리팹에 해당하는 풀을 돌려줍니다. 없으면 만듭니다.
        /// </summary>
        /// <param name="prefab">풀을 찾을 프리팹</param>
        /// <returns>이 프리팹의 풀</returns>
        private static ObjectPool<GameObject> GetPool(GameObject prefab)
        {
            ObjectPool<GameObject> pool;
            if (pools.TryGetValue(prefab, out pool)) return pool;

            pool = new ObjectPool<GameObject>(
                createFunc: () => Create(prefab),
                actionOnGet: null,          // 위치를 잡은 뒤 Get에서 직접 켭니다.
                actionOnRelease: Park,
                actionOnDestroy: OnDestroyInstance,
                collectionCheck: true,      // 같은 인스턴스를 두 번 반납하면 바로 알려 줍니다.
                defaultCapacity: 4,
                maxSize: 32);

            pools.Add(prefab, pool);
            return pool;
        }

        /// <summary>
        /// 새 인스턴스를 만들어 출처를 적어 두고 꺼진 상태로 보관함에 넣습니다.
        /// </summary>
        /// <param name="prefab">복제할 프리팹</param>
        /// <returns>비활성 상태의 새 인스턴스</returns>
        private static GameObject Create(GameObject prefab)
        {
            // 보관함 아래에 바로 만듭니다. 보관함 자체가 꺼져 있으므로 이 인스턴스는
            // <b>한 번도 활성 상태가 되지 않고</b>, 따라서 Awake·OnEnable이 여기서 돌지 않습니다.
            //
            // 예전에는 최상위에 만든 뒤 껐습니다. 그 사이 한 프레임도 지나지 않지만
            // OnEnable은 실행되어, 스폰 사운드나 파티클처럼 "꺼내질 때 한 번" 해야 하는 일이
            // 자리를 잡기도 전에 원점에서 한 번 헛돌았습니다.
            GameObject instance = Object.Instantiate(prefab, GetParking());

            // activeSelf를 꺼 둡니다. 이것이 없으면 Get에서 부모를 옮기는 순간
            // 위치를 잡기도 전에 켜집니다.
            instance.SetActive(false);

            // 반납할 때 어느 풀로 돌아가야 하는지 알기 위한 표식입니다.
            PooledObject tag = instance.AddComponent<PooledObject>();
            tag.SourcePrefab = prefab;

            return instance;
        }

        /// <summary>
        /// 인스턴스를 꺼진 상태로 보관함에 되돌립니다.
        /// </summary>
        /// <param name="instance">보관할 인스턴스</param>
        private static void Park(GameObject instance)
        {
            if (instance == null) return;

            instance.SetActive(false);

            // 부모를 반드시 떼어 놓습니다. 귀신은 차량의 자식으로 붙어 있어서,
            // 그대로 두면 쉬는 동안에도 차를 따라다니고 차가 사라질 때 함께 파괴됩니다.
            instance.transform.SetParent(GetParking(), false);
        }

        /// <summary>
        /// 풀이 넘칠 때 인스턴스를 실제로 파괴합니다.
        /// </summary>
        /// <param name="instance">파괴할 인스턴스</param>
        private static void OnDestroyInstance(GameObject instance)
        {
            if (instance != null) Object.Destroy(instance);
        }

        /// <summary>
        /// 보관함 Transform을 돌려줍니다. 없으면 만듭니다.
        /// </summary>
        /// <returns>쉬고 있는 인스턴스를 담아 둘 Transform</returns>
        private static Transform GetParking()
        {
            if (parking != null) return parking;

            GameObject root = new GameObject("PrefabPool (Inactive)");
            root.SetActive(false);
            parking = root.transform;

            return parking;
        }
    }
}
