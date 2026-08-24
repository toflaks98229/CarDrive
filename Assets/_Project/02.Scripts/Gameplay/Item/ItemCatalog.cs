using System.Collections.Generic;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 이름표 하나에 대응하는 물건 한 종류입니다.
    /// </summary>
    [System.Serializable]
    public class ItemCatalogEntry
    {
        /// <summary>세이브에 적히는 이름표입니다. <see cref="SaveableItem.itemId"/> 와 같아야 합니다.</summary>
        [Tooltip("세이브에 적히는 이름표. 프리팹의 SaveableItem.itemId 와 같아야 합니다.")]
        public string id = "";

        /// <summary>불러올 때 이 이름표로 다시 만들 프리팹입니다.</summary>
        [Tooltip("불러올 때 다시 만들 프리팹")]
        public GameObject prefab;

        /// <summary>
        /// 마트에서 파는 물건이라면 그 정의입니다. 봉투 안에 담긴 것을 되돌릴 때 씁니다.
        /// 마트에서 팔지 않는 것(맥주병 등)은 비워 둡니다.
        /// </summary>
        [Tooltip("마트에서 파는 물건이라면 그 정의. 봉투 속 내용물을 되돌릴 때 씁니다.")]
        public ShopItem shopItem;
    }

    /// <summary>
    /// 이름표로 물건을 되찾는 표입니다.
    ///
    /// <b>왜 필요한가.</b> 세이브에 담기는 것은 <c>GameObject</c> 가 아니라 <b>무엇이 어디에
    /// 있었는지</b>뿐입니다. 불러올 때 그 이름표로 프리팹을 찾아 다시 만들어야 하는데,
    /// 문자열에서 프리팹으로 가는 길이 어딘가에는 있어야 합니다. 그 길이 여기입니다.
    ///
    /// <b>왜 <c>Resources</c> 인가.</b> 세이브 참여자는 <see cref="Composition.GameBootstrap"/> 이
    /// 필요할 때 만들어 주므로 인스펙터 칸이 없습니다. 이 프로젝트는 같은 사정을
    /// <see cref="Systems.CarDriveWorldSettings"/> 에서 이미 <c>Resources.Load</c> 로 풀었고,
    /// 없을 때 예외를 던지지 않고 빈 표로 물러서는 성질까지 그대로 따릅니다.
    /// (표가 없으면 물건이 저장되지 않을 뿐 게임은 돕니다)
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "CarDrive/물건 목록")]
    public class ItemCatalog : ScriptableObject
    {
        // --- Constants ---

        /// <summary><c>Resources</c> 에서 찾을 이름입니다.</summary>
        public const string ResourceName = "ItemCatalog";

        // --- Public Member Variables ---

        /// <summary>이름표와 프리팹의 짝입니다.</summary>
        [Tooltip("이름표와 프리팹의 짝. 저장·복원되는 물건은 전부 여기 있어야 합니다.")]
        public List<ItemCatalogEntry> entries = new List<ItemCatalogEntry>();

        // --- Public Properties ---

        /// <summary>
        /// 어디서나 쓸 수 있는 표입니다. 없으면 <b>빈 표</b>를 돌려줍니다.
        ///
        /// 없을 때 예외를 던지지 않는 것이 중요합니다. 표를 아직 만들지 않은 상태에서도
        /// 게임은 돌아가야 합니다 — 물건이 저장되지 않을 뿐입니다.
        /// </summary>
        public static ItemCatalog Instance
        {
            get
            {
                if (cached != null) return cached;

                cached = Resources.Load<ItemCatalog>(ResourceName);
                if (cached == null) cached = CreateInstance<ItemCatalog>();

                return cached;
            }
        }

        // --- Private Member Variables ---

        /// <summary>찾아 둔 표입니다.</summary>
        private static ItemCatalog cached;

        /// <summary>이름표로 빠르게 찾기 위한 조회표입니다. 처음 물었을 때 만듭니다.</summary>
        private Dictionary<string, ItemCatalogEntry> lookup;

        // --- Public Methods ---

        /// <summary>
        /// 이름표에 해당하는 항목을 찾습니다.
        /// </summary>
        /// <param name="id">찾을 이름표</param>
        /// <returns>찾은 항목. 없으면 null입니다.</returns>
        public ItemCatalogEntry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            EnsureLookup();

            ItemCatalogEntry entry;
            return lookup.TryGetValue(id, out entry) ? entry : null;
        }

        /// <summary>
        /// 이름표에 해당하는 프리팹을 찾습니다.
        /// </summary>
        /// <param name="id">찾을 이름표</param>
        /// <returns>찾은 프리팹. 없으면 null입니다.</returns>
        public GameObject FindPrefab(string id)
        {
            ItemCatalogEntry entry = Find(id);
            return entry != null ? entry.prefab : null;
        }

        /// <summary>
        /// 이름표에 해당하는 마트 상품 정의를 찾습니다.
        /// </summary>
        /// <param name="id">찾을 이름표</param>
        /// <returns>찾은 정의. 마트에서 팔지 않는 물건이면 null입니다.</returns>
        public ShopItem FindShopItem(string id)
        {
            ItemCatalogEntry entry = Find(id);
            return entry != null ? entry.shopItem : null;
        }

        /// <summary>
        /// 이 상품 정의에 붙은 이름표를 찾습니다. 봉투 속 내용물을 <b>담을 때</b> 씁니다.
        /// </summary>
        /// <param name="item">찾을 상품 정의</param>
        /// <returns>그 상품의 이름표. 표에 없으면 빈 문자열입니다.</returns>
        public string FindIdOf(ShopItem item)
        {
            if (item == null) return "";

            for (int i = 0; i < entries.Count; i++)
            {
                ItemCatalogEntry entry = entries[i];
                if (entry != null && entry.shopItem == item) return entry.id;
            }

            return "";
        }

        // --- Private Methods ---

        /// <summary>조회표가 없으면 만듭니다.</summary>
        private void EnsureLookup()
        {
            if (lookup != null) return;

            lookup = new Dictionary<string, ItemCatalogEntry>();

            for (int i = 0; i < entries.Count; i++)
            {
                ItemCatalogEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;

                // 같은 이름표가 둘이면 뒤엣것은 영영 쓰이지 않습니다. 조용히 넘기지 않습니다.
                if (lookup.ContainsKey(entry.id))
                {
                    GameLog.Warn(GameLog.Channel.Simulation,
                        "ItemCatalog: 이름표 '" + entry.id + "' 가 두 번 있습니다. 뒤엣것은 쓰이지 않습니다.", this);
                    continue;
                }

                lookup.Add(entry.id, entry);
            }
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때 찾아 둔 표를 비웁니다.
        /// 도메인 리로드를 꺼 두면 지난 실행의 값이 그대로 남습니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cached = null;
        }
    }
}
