using System.Collections.Generic;
using UnityEngine;
using VContainer;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 월드에 놓였거나 손에 들렸거나 차에 실린 물건을 담고 되돌립니다.
    ///
    /// <b>왜 필요했는가.</b> 세이브에 물건이 없으면 <b>불러오기가 재화 복제 경로</b>가 됩니다.
    /// 마트에서 물건을 사면 지갑이 줄고 봉투가 씬에 생기는데, 불러오면 지갑만 되돌아가고
    /// 봉투는 그대로 남습니다. 무한히 반복할 수 있었습니다.
    ///
    /// <b>불러오기는 씬을 다시 깔지 않습니다.</b> 그래서 되돌리는 일은 "만들기"가 아니라
    /// <b>맞추기</b>입니다 — 지금 있는 것을 먼저 치우고, 세이브에 적힌 대로 다시 만듭니다.
    /// 그러지 않으면 불러올 때마다 물건이 두 배가 됩니다.
    ///
    /// <b>상자 안의 병은 다루지 않습니다.</b> 상자(<see cref="BeverageBox"/>)는 씬에 놓인
    /// 붙박이고 불러오기가 그것을 되돌리지 않으므로, 안에 든 병까지 다시 만들면 상자에
    /// 병이 겹쳐 쌓입니다. 병이 상자를 벗어나는 순간부터 이 참여자의 몫이 됩니다.
    /// </summary>
    public class ItemSaveParticipant : MonoBehaviour, ISaveable
    {
        // --- Public Member Variables ---

        /// <summary>
        /// 이름표로 프리팹을 되찾을 표입니다. <b>비워 두면 <c>Resources</c> 의 것을 씁니다.</b>
        ///
        /// 칸을 둔 이유는 <see cref="NeedsSystem.profile"/> 과 같습니다 — 정적 접근자만
        /// 있으면 이 클래스를 <b>다른 표로 시험할 수 없습니다.</b> 채워 두면 그것을,
        /// 비워 두면 기본 표를 씁니다.
        /// </summary>
        [Header("물건 목록 (비워두면 Resources 의 것을 씁니다)")]
        [Tooltip("이름표로 프리팹을 되찾을 표. 비워 두면 Resources/ItemCatalog 을 씁니다.")]
        public ItemCatalog catalog;

        // --- Public Properties ---

        /// <summary>실제로 쓰는 표입니다. 칸이 비어 있으면 기본 표입니다.</summary>
        public ItemCatalog Catalog { get { return catalog != null ? catalog : ItemCatalog.Instance; } }

        // --- Private Member Variables ---

        /// <summary>수명 시각을 읽고 쓸 시계입니다.</summary>
        private IGameClock clock = NullGameClock.Instance;

        /// <summary>다시 만든 물건을 손에 쥐어 줄 쪽입니다.</summary>
        private PlayerCarrier carrier;

        /// <summary>되돌린 직후 수명을 한 번 훑을 쪽입니다.</summary>
        private ItemDecay decay;

        /// <summary>치울 대상을 모으는 임시 목록입니다. 매번 새로 만들지 않습니다.</summary>
        private readonly List<SaveableItem> doomed = new List<SaveableItem>();

        /// <summary>봉투 내용물을 옮겨 담는 임시 목록입니다.</summary>
        private readonly List<ShopItem> bagBuffer = new List<ShopItem>();

        // --- Injection ---

        /// <summary>게임 시계를 받습니다.</summary>
        /// <param name="gameClock">게임 시계</param>
        [Inject]
        public void Construct(IGameClock gameClock)
        {
            if (gameClock != null) clock = gameClock;
        }

        // --- Unity Event Functions ---

        /// <summary>세이브 등록부에 자신을 넣습니다.</summary>
        void Awake()
        {
            SaveRegistry.Register(this);
        }

        /// <summary>
        /// 협력자를 찾습니다. <b>Awake가 아니라 Start입니다</b> —
        /// 등록이 각자의 Awake에서 이뤄지는데 그 순서는 정해져 있지 않습니다.
        /// </summary>
        void Start()
        {
            if (carrier == null) carrier = GameContext.Resolve<PlayerCarrier>(this);
            if (decay == null) decay = GameContext.Resolve<ItemDecay>(this);
        }

        /// <summary>세이브 등록부에서 자신을 뺍니다.</summary>
        void OnDestroy()
        {
            SaveRegistry.Unregister(this);
        }

        // --- ISaveable ---

        /// <summary>차량이 제자리에 놓인 뒤, 플레이어보다는 앞입니다.</summary>
        public int SaveOrder { get { return SaveOrders.Items; } }

        /// <summary>
        /// 지금 있는 물건들을 세이브에 적습니다.
        /// </summary>
        /// <param name="data">적어 넣을 세이브 자료</param>
        public void CaptureInto(SaveData data)
        {
            data.items.Clear();

            ItemCatalog catalog = Catalog;

            IReadOnlyList<SaveableItem> items = SaveableItem.All;
            for (int i = 0; i < items.Count; i++)
            {
                SaveableItem item = items[i];
                if (item == null) continue;

                // 되살릴 방법이 없는 것은 적지 않습니다. 적어 봐야 불러올 때 조용히 사라집니다.
                if (!item.HasIdentity) continue;

                // 상자 안의 병은 상자의 몫입니다. (클래스 주석 참고)
                if (item.IsInsideBox) continue;

                data.items.Add(Capture(item, catalog));
            }
        }

        /// <summary>
        /// 세이브에 적힌 대로 물건을 되돌립니다.
        /// </summary>
        /// <param name="data">읽어 올 세이브 자료</param>
        public void RestoreFrom(SaveData data)
        {
            ClearTrackedItems();

            if (data.items == null) return;

            ItemCatalog catalog = Catalog;

            for (int i = 0; i < data.items.Count; i++)
            {
                Spawn(data.items[i], catalog);
            }

            // 불러온 것 중에 이미 수명이 다한 것이 있을 수 있습니다.
            // (오래 전에 저장하고 게임 안에서 며칠이 흐른 뒤 불러오는 경우)
            if (decay != null) decay.Sweep();
        }

        // --- Private Methods : 담기 ---

        /// <summary>
        /// 물건 하나의 상태를 기록으로 만듭니다.
        /// </summary>
        /// <param name="item">기록할 물건</param>
        /// <param name="catalog">봉투 내용물의 이름표를 찾을 표</param>
        /// <returns>세이브에 담길 기록</returns>
        private ItemSave Capture(SaveableItem item, ItemCatalog catalog)
        {
            ItemSave save = new ItemSave();

            save.id = item.itemId;
            save.position = item.transform.position;
            save.eulerAngles = item.transform.eulerAngles;
            save.placement = (int)item.Placement;
            save.looseSinceMinute = item.LooseSinceMinute;

            Vehicle vehicle = item.GetComponentInParent<Vehicle>(true);
            save.vehicleName = vehicle != null ? vehicle.displayName : "";

            // 상자라면 남은 병의 수를 적습니다. 적지 않으면 여섯 병을 마신 상자가
            // 불러오기 한 번에 <b>가득 찬 채로</b> 돌아와, 그것이 곧 복제가 됩니다.
            BeverageBox box = item.GetComponent<BeverageBox>();
            if (box != null) save.containedCount = box.foundBeverages.Count;

            // 봉투라면 안에 든 것까지 적습니다. 봉투만 되돌리고 내용물을 잃으면
            // 산 물건이 사라진 것과 같습니다.
            ShoppingBag bag = item.GetComponent<ShoppingBag>();
            if (bag != null)
            {
                IReadOnlyList<ShopItem> contents = bag.Contents;
                for (int i = 0; i < contents.Count; i++)
                {
                    string id = catalog.FindIdOf(contents[i]);
                    if (string.IsNullOrEmpty(id))
                    {
                        GameLog.Warn(GameLog.Channel.Simulation,
                            "ItemSaveParticipant: 봉투 속 '" +
                            (contents[i] != null ? contents[i].displayName : "?") +
                            "' 이(가) ItemCatalog 에 없어 저장되지 않습니다.", this);
                        continue;
                    }

                    save.bagContents.Add(id);
                }
            }

            return save;
        }

        // --- Private Methods : 되돌리기 ---

        /// <summary>
        /// 지금 추적 중인 물건을 모두 치웁니다. 되돌리기 전에 한 번 부릅니다.
        ///
        /// 손에 든 것이 있으면 먼저 내려놓게 합니다. 들고 있던 대상이 사라지면
        /// <see cref="PlayerCarrier"/> 가 없어진 것을 계속 붙잡고 있게 됩니다.
        /// </summary>
        private void ClearTrackedItems()
        {
            if (carrier != null && carrier.IsCarrying) carrier.Drop(false);

            doomed.Clear();

            IReadOnlyList<SaveableItem> items = SaveableItem.All;
            for (int i = 0; i < items.Count; i++)
            {
                SaveableItem item = items[i];
                if (item == null) continue;
                if (item.IsInsideBox) continue;   // 상자 안은 건드리지 않습니다

                doomed.Add(item);
            }

            // 목록을 따로 모아 두고 치웁니다. 치우는 동안 등록부가 바뀌기 때문입니다.
            for (int i = 0; i < doomed.Count; i++)
            {
                if (doomed[i] != null) ItemDecay.Despawn(doomed[i].gameObject);
            }

            doomed.Clear();
        }

        /// <summary>
        /// 기록 하나를 실제 물건으로 되돌립니다.
        /// </summary>
        /// <param name="save">되돌릴 기록</param>
        /// <param name="catalog">이름표로 프리팹을 찾을 표</param>
        private void Spawn(ItemSave save, ItemCatalog catalog)
        {
            if (save == null || string.IsNullOrEmpty(save.id)) return;

            GameObject prefab = catalog.FindPrefab(save.id);
            if (prefab == null)
            {
                GameLog.Warn(GameLog.Channel.Simulation,
                    "ItemSaveParticipant: 이름표 '" + save.id + "' 를 ItemCatalog 에서 찾지 못해 " +
                    "이 물건을 되돌리지 못했습니다.", this);
                return;
            }

            Quaternion rotation = Quaternion.Euler(save.eulerAngles);

            // 차에 실려 있었다면 그 차의 자식으로 붙입니다. 차가 사라졌으면 바닥에 둡니다.
            Transform parent = null;
            if (!string.IsNullOrEmpty(save.vehicleName))
            {
                Vehicle vehicle = VehicleSaveParticipant.FindVehicle(save.vehicleName);
                if (vehicle != null) parent = vehicle.transform;
            }

            GameObject spawned = PrefabPool.Get(prefab, save.position, rotation, parent);
            if (spawned == null) return;

            RestoreBag(spawned, save, catalog);
            RestoreBox(spawned, save);
            RestoreLifetime(spawned, save);
            RestoreHold(spawned, save);
        }

        /// <summary>
        /// 상자라면 남아 있던 병의 수까지 되돌립니다.
        /// </summary>
        /// <param name="spawned">막 되돌린 물건</param>
        /// <param name="save">그 기록</param>
        private void RestoreBox(GameObject spawned, ItemSave save)
        {
            if (save.containedCount < 0) return;

            BeverageBox box = spawned.GetComponent<BeverageBox>();
            if (box == null) return;

            box.TrimTo(save.containedCount);
        }

        /// <summary>
        /// 봉투라면 담겨 있던 것을 다시 채웁니다.
        /// </summary>
        /// <param name="spawned">막 되돌린 물건</param>
        /// <param name="save">그 기록</param>
        /// <param name="catalog">이름표로 상품 정의를 찾을 표</param>
        private void RestoreBag(GameObject spawned, ItemSave save, ItemCatalog catalog)
        {
            if (save.bagContents == null || save.bagContents.Count == 0) return;

            ShoppingBag bag = spawned.GetComponent<ShoppingBag>();
            if (bag == null) return;

            bagBuffer.Clear();
            for (int i = 0; i < save.bagContents.Count; i++)
            {
                ShopItem item = catalog.FindShopItem(save.bagContents[i]);
                if (item != null) bagBuffer.Add(item);
            }

            bag.Fill(bagBuffer);
            bagBuffer.Clear();
        }

        /// <summary>
        /// 놓여 있던 시각을 그대로 되돌립니다. 수명이 <b>이어서</b> 흐르게 하는 부분입니다.
        /// </summary>
        /// <param name="spawned">막 되돌린 물건</param>
        /// <param name="save">그 기록</param>
        private void RestoreLifetime(GameObject spawned, ItemSave save)
        {
            SaveableItem item = spawned.GetComponent<SaveableItem>();
            if (item == null) return;

            item.SetLooseSince(save.looseSinceMinute);
        }

        /// <summary>
        /// 손에 들려 있던 물건이면 다시 쥐어 줍니다.
        /// </summary>
        /// <param name="spawned">막 되돌린 물건</param>
        /// <param name="save">그 기록</param>
        private void RestoreHold(GameObject spawned, ItemSave save)
        {
            if (save.placement != (int)ItemPlacement.Held) return;
            if (carrier == null) return;

            Carryable carryable = spawned.GetComponent<Carryable>();
            if (carryable == null) return;

            carrier.PickUp(carryable);
        }
    }
}
