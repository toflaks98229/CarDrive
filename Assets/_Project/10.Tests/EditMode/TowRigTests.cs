using NUnit.Framework;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 견인기의 <b>두 판단</b>을 봅니다 — 언제 부르는가, 얼마를 받는가.
    ///
    /// 씬도 차도 지갑도 없이 확인할 수 있어야 하는 규칙이라 밖으로 꺼내 두었습니다.
    /// 기계가 실제로 걸어오는지는 <c>TowRigHaulTests</c> 가 봅니다.
    /// </summary>
    public class TowRigTests
    {
        // --- 언제 멈춰 선 차인가 ---

        [Test]
        public void 내구도가_바닥나면_멈춘_차다()
        {
            Assert.IsTrue(TowRig.Stranded(0f, 30f));
        }

        [Test]
        public void 연료가_바닥나도_멈춘_차다()
        {
            // ⚠ 둘 다 봐야 합니다. 멀쩡한 차가 기름만 떨어져 서는 것이
            // 이 게임에서 더 자주 일어나는 실패입니다.
            Assert.IsTrue(TowRig.Stranded(80f, 0f));
        }

        [Test]
        public void 둘_다_남아_있으면_부르지_않는다()
        {
            Assert.IsFalse(TowRig.Stranded(80f, 30f));
        }

        // --- 얼마를 받는가 ---

        [Test]
        public void 코앞에서_서도_출장비는_받는다()
        {
            Assert.That(TowRig.Quote(0f, 6, 40), Is.EqualTo(40));
        }

        [Test]
        public void 멀리서_설수록_비싸다()
        {
            // 100 m 마다 6. 정액이면 세계 끝에서 서나 마을 앞에서 서나 같은 값이 되어,
            // 멀리 나가는 일에 값이 매겨지지 않습니다.
            Assert.That(TowRig.Quote(100f, 6, 40), Is.EqualTo(46));
            Assert.That(TowRig.Quote(1000f, 6, 40), Is.EqualTo(100));
        }

        [Test]
        public void 끊어진_거리는_올려서_받는다()
        {
            // 250 m 는 2.5 칸이므로 15 를 올려 받습니다. 기계는 반 칸을 봐주지 않습니다.
            Assert.That(TowRig.Quote(250f, 6, 40), Is.EqualTo(55));
        }

        [Test]
        public void 거리가_음수여도_깎이지_않는다()
        {
            Assert.That(TowRig.Quote(-500f, 6, 40), Is.EqualTo(40));
        }
    }
}
