using NUnit.Framework;
using UnityEngine;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 눈가 얼룩의 <b>세 판단</b>을 봅니다 — 화면 밖인가, 어느 쪽인가, 얼마나 진한가.
    ///
    /// 카메라도 귀신도 없이 확인할 수 있어야 하는 규칙이라 밖으로 꺼내 두었습니다.
    /// </summary>
    public class GhostEdgeCueTests
    {
        // --- 화면 밖인가 ---

        [Test]
        public void 화면_안에_있으면_알리지_않는다()
        {
            Assert.IsFalse(GhostEdgeCue.OffScreen(new Vector3(0.5f, 0.5f, 10f)));
            Assert.IsFalse(GhostEdgeCue.OffScreen(new Vector3(0.02f, 0.98f, 3f)));
        }

        [Test]
        public void 화면_밖이면_알린다()
        {
            Assert.IsTrue(GhostEdgeCue.OffScreen(new Vector3(1.2f, 0.5f, 10f)));
            Assert.IsTrue(GhostEdgeCue.OffScreen(new Vector3(0.5f, -0.1f, 10f)));
        }

        [Test]
        public void 뒤에_있는_것도_화면_밖이다()
        {
            // ⚠ 뷰포트 z 가 음수면 카메라 뒤입니다. x·y 가 0~1 안에 들어와도
            // 보이지 않습니다 — 뒤에 붙는 귀신이 정확히 그 경우입니다.
            Assert.IsTrue(GhostEdgeCue.OffScreen(new Vector3(0.5f, 0.5f, -4f)));
        }

        // --- 어느 쪽인가 ---

        [Test]
        public void 오른쪽_밖이면_오른쪽에_앉는다()
        {
            Vector2 side = GhostEdgeCue.Toward(new Vector3(1.4f, 0.5f, 10f));

            Assert.That(side.x, Is.GreaterThan(0.9f));
            Assert.That(Mathf.Abs(side.y), Is.LessThan(0.1f));
        }

        [Test]
        public void 뒤에_있으면_좌우를_뒤집는다()
        {
            // 카메라 뒤의 점은 뷰포트에 앞뒤가 뒤집혀 찍힙니다. 그대로 쓰면
            // 반대쪽 눈가가 어두워집니다.
            Vector2 behind = GhostEdgeCue.Toward(new Vector3(1.4f, 0.5f, -10f));

            Assert.That(behind.x, Is.LessThan(-0.9f));
        }

        [Test]
        public void 정확히_가운데_뒤면_아래에_앉는다()
        {
            Vector2 side = GhostEdgeCue.Toward(new Vector3(0.5f, 0.5f, -10f));

            Assert.That(side, Is.EqualTo(new Vector2(0f, -1f)));
        }

        [Test]
        public void 앉는_쪽은_길이가_1_이다()
        {
            Vector2 side = GhostEdgeCue.Toward(new Vector3(1.9f, 1.4f, 5f));

            Assert.That(side.magnitude, Is.EqualTo(1f).Within(0.001f));
        }

        // --- 얼마나 진한가 ---

        [Test]
        public void 가까우면_가장_진하다()
        {
            Assert.That(GhostEdgeCue.Weight(1f, 3f, 22f), Is.EqualTo(1f));
            Assert.That(GhostEdgeCue.Weight(3f, 3f, 22f), Is.EqualTo(1f));
        }

        [Test]
        public void 멀면_사라진다()
        {
            Assert.That(GhostEdgeCue.Weight(22f, 3f, 22f), Is.EqualTo(0f));
            Assert.That(GhostEdgeCue.Weight(100f, 3f, 22f), Is.EqualTo(0f));
        }

        [Test]
        public void 사이는_이어진다()
        {
            float near = GhostEdgeCue.Weight(6f, 3f, 22f);
            float far = GhostEdgeCue.Weight(18f, 3f, 22f);

            Assert.That(near, Is.GreaterThan(far));
            Assert.That(far, Is.GreaterThan(0f));
        }

        [Test]
        public void 두_거리가_같아도_터지지_않는다()
        {
            Assert.That(GhostEdgeCue.Weight(2f, 5f, 5f), Is.EqualTo(1f));
            Assert.That(GhostEdgeCue.Weight(9f, 5f, 5f), Is.EqualTo(0f));
        }
    }
}
