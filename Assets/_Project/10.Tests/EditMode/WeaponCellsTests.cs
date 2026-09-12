using NUnit.Framework;
using UnityEngine;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 미사일 칸이 <b>쏜 만큼 비고 때가 되면 다시 차는지</b> 봅니다.
    ///
    /// 로봇도 무장도 없이 칸만 세워서 잽니다 — 이 부품이 하는 일은 그것뿐입니다.
    /// </summary>
    public class WeaponCellsTests
    {
        private GameObject holder;
        private WeaponCells cells;

        [SetUp]
        public void SetUp()
        {
            holder = new GameObject("Cells");

            Transform[] loaded = new Transform[6];

            for (int i = 0; i < loaded.Length; i++)
            {
                GameObject one = new GameObject("Missile_" + i);
                one.transform.SetParent(holder.transform, false);
                loaded[i] = one.transform;
            }

            cells = holder.AddComponent<WeaponCells>();
            cells.loaded = loaded;
            cells.refillSeconds = 8f;
            cells.Fill();
        }

        [TearDown]
        public void TearDown()
        {
            if (holder != null) Object.DestroyImmediate(holder);
        }

        [Test]
        public void 처음에는_다_차_있다()
        {
            Assert.That(cells.Left, Is.EqualTo(6));
            Assert.That(cells.Spent, Is.EqualTo(0));

            foreach (Transform one in cells.loaded)
            {
                Assert.IsTrue(one.gameObject.activeSelf, one.name + " 이 꺼져 있습니다");
            }
        }

        [Test]
        public void 쏜_만큼_앞에서부터_빈다()
        {
            cells.Spend();
            cells.Spend();

            Assert.That(cells.Spent, Is.EqualTo(2));
            Assert.That(cells.Left, Is.EqualTo(4));

            Assert.IsFalse(cells.loaded[0].gameObject.activeSelf, "첫 칸이 남아 있습니다");
            Assert.IsFalse(cells.loaded[1].gameObject.activeSelf, "둘째 칸이 남아 있습니다");
            Assert.IsTrue(cells.loaded[2].gameObject.activeSelf, "셋째 칸까지 비었습니다");
        }

        [Test]
        public void 칸보다_많이_쏴도_터지지_않는다()
        {
            // ⚠ 발수가 칸보다 많은 무장이 생겨도 여기서 터지면 안 됩니다.
            for (int i = 0; i < 20; i++) cells.Spend();

            Assert.That(cells.Spent, Is.EqualTo(6));
            Assert.That(cells.Left, Is.EqualTo(0));
        }

        [Test]
        public void 예약해도_그_자리에서_차지_않는다()
        {
            cells.Spend();
            cells.Spend();
            cells.Reload();

            // 마지막 발의 섬광이 가시기도 전에 도로 차 있으면 비어 가는 것을
            // 본 적이 없는 것과 같습니다.
            Assert.That(cells.Left, Is.EqualTo(4), "예약하자마자 차 버렸습니다");
        }

        [Test]
        public void 때가_되면_다시_찬다()
        {
            cells.Spend();
            cells.Reload();

            cells.Tick(4f);
            Assert.That(cells.Left, Is.EqualTo(5), "아직 찰 때가 아닙니다");

            cells.Tick(4.1f);
            Assert.That(cells.Left, Is.EqualTo(6), "때가 됐는데 안 찼습니다");

            foreach (Transform one in cells.loaded)
            {
                Assert.IsTrue(one.gameObject.activeSelf, one.name + " 이 안 돌아왔습니다");
            }
        }

        [Test]
        public void 기다릴_시간이_없으면_그_자리에서_찬다()
        {
            cells.refillSeconds = 0f;

            cells.Spend();
            cells.Reload();

            Assert.That(cells.Left, Is.EqualTo(6));
        }

        [Test]
        public void 예약하지_않았으면_시간이_흘러도_안_찬다()
        {
            cells.Spend();

            cells.Tick(100f);

            Assert.That(cells.Left, Is.EqualTo(5), "쏘기만 했는데 저절로 찼습니다");
        }

        [Test]
        public void 칸이_없어도_터지지_않는다()
        {
            cells.loaded = null;

            cells.Fill();
            cells.Spend();
            cells.Tick(1f);

            Assert.That(cells.Count, Is.EqualTo(0));
            Assert.That(cells.Left, Is.EqualTo(0));
        }
    }
}
