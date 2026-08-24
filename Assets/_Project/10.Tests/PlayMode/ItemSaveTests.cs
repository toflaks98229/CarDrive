using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CarDrive.Common;
using CarDrive.Gameplay;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 물건의 저장·복원 왕복에 대한 PlayMode 테스트입니다.
    ///
    /// <b>PlayMode 인 이유.</b> 복원은 오브젝트를 치우고 다시 만드는 일이고, 그 경로가
    /// <c>PrefabPool</c> 과 <c>Instantiate</c>/<c>Destroy</c> 를 지납니다. 프레임이 흐르지 않으면
    /// 파괴가 실제로 일어나지 않아 "치웠다"는 것을 확인할 수 없습니다.
    ///
    /// <b>무엇을 지키는가.</b> 이 파일의 존재 이유는 하나입니다 —
    /// <b>불러오기가 재화 복제 경로가 되지 않는 것.</b> 세이브에 물건이 없던 시절에는
    /// 마트에서 물건을 사고 불러오면 지갑만 되돌아가고 산 물건은 씬에 그대로 남았습니다.
    /// 무한히 반복할 수 있었습니다. 아래 두 번째 테스트가 정확히 그 경로를 막습니다.
    /// </summary>
    public class ItemSaveTests
    {
        /// <summary>테스트가 만든 것을 한꺼번에 치우기 위한 표식입니다.</summary>
        private sealed class SpawnedByTest : MonoBehaviour { }

        private const string ItemId = "test_item";

        private GameObject participantGo;
        private ItemSaveParticipant participant;
        private ItemCatalog catalog;
        private GameObject prefab;

        [SetUp]
        public void SetUp()
        {
            GameContext.Clear();
            SaveableItem.Clear();

            // 프리팹 역할을 할 오브젝트입니다. 꺼 둔 채로 만들어야 등록부에 잡히지 않습니다.
            prefab = new GameObject("TestItemPrefab");
            prefab.SetActive(false);
            prefab.AddComponent<SpawnedByTest>();
            SaveableItem template = prefab.AddComponent<SaveableItem>();
            template.itemId = ItemId;

            catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            ItemCatalogEntry entry = new ItemCatalogEntry();
            entry.id = ItemId;
            entry.prefab = prefab;
            catalog.entries = new List<ItemCatalogEntry> { entry };

            participantGo = new GameObject("ItemSaveParticipant");
            participantGo.AddComponent<SpawnedByTest>();
            participant = participantGo.AddComponent<ItemSaveParticipant>();
            participant.catalog = catalog;
        }

        [TearDown]
        public void TearDown()
        {
            PrefabPool.Clear();

            SpawnedByTest[] spawned = Object.FindObjectsByType<SpawnedByTest>(FindObjectsInactive.Include);
            for (int i = 0; i < spawned.Length; i++)
            {
                if (spawned[i] != null) Object.Destroy(spawned[i].gameObject);
            }

            SaveableItem[] leftovers = Object.FindObjectsByType<SaveableItem>(FindObjectsInactive.Include);
            for (int i = 0; i < leftovers.Length; i++)
            {
                if (leftovers[i] != null) Object.Destroy(leftovers[i].gameObject);
            }

            if (catalog != null) Object.Destroy(catalog);

            participantGo = null; participant = null; catalog = null; prefab = null;

            SaveableItem.Clear();
            GameContext.Clear();
        }

        /// <summary>
        /// 저장한 그대로 불러오면 물건의 수가 그대로여야 합니다.
        /// 복원이 "치우고 다시 만들기"가 아니라 그냥 "만들기"였다면 여기서 두 배가 됩니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 불러와도_물건이_두_배가_되지_않는다()
        {
            SpawnItem(new Vector3(1f, 0f, 2f));
            yield return null;

            Assert.AreEqual(1, CountTracked(), "먼저 하나가 있어야 합니다.");

            SaveData data = new SaveData();
            participant.CaptureInto(data);
            Assert.AreEqual(1, data.items.Count, "하나가 담겨야 합니다.");

            participant.RestoreFrom(data);
            yield return null;

            Assert.AreEqual(1, CountTracked(), "불러온 뒤에도 하나여야 합니다.");
        }

        /// <summary>
        /// <b>이것이 재화 복제를 막는 테스트입니다.</b>
        ///
        /// 저장한 뒤에 생긴 물건(= 마트에서 산 것)은 불러오면 사라져야 합니다.
        /// 그러지 않으면 "사고 → 불러오기"를 반복해 물건을 공짜로 늘릴 수 있습니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 저장_뒤에_생긴_물건은_불러오면_사라진다()
        {
            SpawnItem(Vector3.zero);
            yield return null;

            SaveData data = new SaveData();
            participant.CaptureInto(data);

            // 저장 뒤에 하나를 더 삽니다.
            SpawnItem(new Vector3(5f, 0f, 5f));
            yield return null;
            Assert.AreEqual(2, CountTracked(), "산 직후에는 둘이어야 합니다.");

            participant.RestoreFrom(data);
            yield return null;

            Assert.AreEqual(1, CountTracked(), "불러오면 저장 시점의 하나로 돌아가야 합니다.");
        }

        /// <summary>
        /// 놓인 자리와 <b>수명 시계</b>가 그대로 돌아와야 합니다.
        /// 수명이 초기화되면 저장·불러오기를 반복해 물건을 영원히 남길 수 있습니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 자리와_수명이_그대로_돌아온다()
        {
            Vector3 spot = new Vector3(3f, 1f, -4f);
            SaveableItem item = SpawnItem(spot);
            item.SetLooseSince(500f);
            yield return null;

            SaveData data = new SaveData();
            participant.CaptureInto(data);

            participant.RestoreFrom(data);
            yield return null;

            SaveableItem restored = FindTracked();
            Assert.IsNotNull(restored, "되돌아와야 합니다.");
            Assert.AreEqual(spot.x, restored.transform.position.x, 0.001f, "자리가 같아야 합니다.");
            Assert.AreEqual(spot.z, restored.transform.position.z, 0.001f, "자리가 같아야 합니다.");
            Assert.AreEqual(500f, restored.LooseSinceMinute, 0.001f, "수명 시계가 이어져야 합니다.");
        }

        /// <summary>
        /// 표에 없는 이름표는 되돌리지 못하므로 경고를 남기고 넘어가야 합니다.
        /// 조용히 지나가면 물건이 사라진 것을 알아챌 방법이 없습니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 표에_없는_이름표는_경고를_남긴다()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("ItemCatalog 에서 찾지 못해"));

            SaveData data = new SaveData();
            ItemSave orphan = new ItemSave();
            orphan.id = "존재하지_않는_이름표";
            data.items.Add(orphan);

            participant.RestoreFrom(data);
            yield return null;

            Assert.AreEqual(0, CountTracked(), "되돌릴 수 없는 것은 만들어지지 않아야 합니다.");
        }

        // --- Helpers ---

        /// <summary>물건 하나를 월드에 만듭니다.</summary>
        /// <param name="position">놓을 자리</param>
        /// <returns>만들어진 물건</returns>
        private SaveableItem SpawnItem(Vector3 position)
        {
            GameObject go = PrefabPool.Get(prefab, position, Quaternion.identity, null);
            go.AddComponent<SpawnedByTest>();
            return go.GetComponent<SaveableItem>();
        }

        /// <summary>지금 등록부에 살아 있는 물건의 수입니다.</summary>
        /// <returns>살아 있는 물건 수</returns>
        private int CountTracked()
        {
            int count = 0;
            IReadOnlyList<SaveableItem> items = SaveableItem.All;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].gameObject.activeInHierarchy) count++;
            }
            return count;
        }

        /// <summary>등록부에서 살아 있는 물건 하나를 찾습니다.</summary>
        /// <returns>찾은 물건. 없으면 null</returns>
        private SaveableItem FindTracked()
        {
            IReadOnlyList<SaveableItem> items = SaveableItem.All;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].gameObject.activeInHierarchy) return items[i];
            }
            return null;
        }
    }
}
