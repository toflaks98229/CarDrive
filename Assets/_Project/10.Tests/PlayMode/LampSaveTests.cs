using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CarDrive.Gameplay;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 등의 죽고 삶이 <b>세이브를 건너서도 남는지</b> 봅니다.
    ///
    /// <b>왜 재는가.</b> 점등기가 밤새 걸어 다니며 되살린 등이 불러오기 한 번에 도로
    /// 죽으면 그 기계가 한 일이 없던 일이 됩니다. 이 검사가 지키는 것은 등이 아니라
    /// <b>플레이어가 지켜 준 밤</b>입니다.
    ///
    /// ⚠ <b>PlayMode 입니다.</b> 등은 <c>OnEnable</c> 로 등록부에 들어가는데, 에디트
    /// 모드에서는 그것이 돌지 않아 세이브가 빈 목록을 담습니다.
    /// </summary>
    public class LampSaveTests
    {
        private GameObject holder;
        private LampSaveParticipant keeper;
        private StreetLamp first;
        private StreetLamp second;

        /// <summary>등 하나를 세웁니다.</summary>
        /// <param name="name">이름. 세이브가 이것으로 짝을 맞춥니다</param>
        /// <param name="broken">죽은 채로 둘 것인가</param>
        private StreetLamp Raise(string name, bool broken)
        {
            GameObject post = new GameObject(name);
            post.transform.SetParent(holder.transform, false);

            StreetLamp lamp = post.AddComponent<StreetLamp>();
            lamp.broken = broken;

            return lamp;
        }

        [SetUp]
        public void SetUp()
        {
            holder = new GameObject("Lamps");

            first = Raise("Lamp_A", false);
            second = Raise("Lamp_B", true);

            keeper = holder.AddComponent<LampSaveParticipant>();
        }

        [TearDown]
        public void TearDown()
        {
            if (holder != null) Object.Destroy(holder);
        }

        [UnityTest]
        public IEnumerator 살린_등은_불러와도_살아_있는다()
        {
            yield return null;

            // 점등기가 한 일입니다.
            second.Relight();

            SaveData data = new SaveData();
            keeper.CaptureInto(data);

            // 다시 죽었다가 불러옵니다.
            second.Break();
            keeper.RestoreFrom(data);

            Assert.IsFalse(second.broken, "살려 둔 등이 불러오기에서 도로 죽었습니다");
        }

        [UnityTest]
        public IEnumerator 죽은_등은_불러와도_죽어_있는다()
        {
            yield return null;

            SaveData data = new SaveData();
            keeper.CaptureInto(data);

            Assert.That(data.lamps.Count, Is.EqualTo(2), "등 둘을 다 담아야 합니다");

            // 누가 고쳐 놓은 뒤 불러옵니다.
            second.Relight();
            keeper.RestoreFrom(data);

            Assert.IsTrue(second.broken, "죽어 있던 등이 불러오기에서 살아났습니다");
            Assert.IsFalse(first.broken, "성한 등까지 죽였습니다");
        }

        [UnityTest]
        public IEnumerator 짝이_없는_등은_그대로_둔다()
        {
            yield return null;

            // ⚠ 배치 시드를 바꾼 뒤의 옛 세이브입니다. 이름이 하나도 안 맞습니다.
            SaveData data = new SaveData();
            data.lamps.Add(new LampSave { name = "Lamp_옛날것", broken = true });

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "LampSaveParticipant"));

            keeper.RestoreFrom(data);

            Assert.IsFalse(first.broken, "짝이 없는데 성한 등을 죽였습니다");
            Assert.IsTrue(second.broken, "짝이 없는데 죽은 등을 살렸습니다");
        }

        [UnityTest]
        public IEnumerator 담을_등이_없어도_터지지_않는다()
        {
            yield return null;

            keeper.RestoreFrom(null);
            keeper.RestoreFrom(new SaveData());

            Assert.Pass();
        }
    }
}
