using NUnit.Framework;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// <see cref="RepairArm.Quote"/> 만 봅니다 — 이 기계의 유일한 판단입니다.
    ///
    /// 차도 지갑도 세우지 않습니다. 값 매기는 규칙이 맞는지가 전부이고,
    /// 그 규칙이 틀리면 경제가 통째로 틀어집니다.
    /// </summary>
    public class RepairArmTests
    {
        private const int PerPoint = 3;
        private const int PerFuel = 2;
        private const int CallOut = 15;

        private static int Quote(float hurt, float dry)
        {
            return RepairArm.Quote(hurt, dry, PerPoint, PerFuel, CallOut);
        }

        [Test]
        public void 멀쩡하면_한_푼도_안_받는다()
        {
            // ⚠ 출장비만 받으면 멀쩡한 차에 "정비 15" 가 계속 떠서,
            // 정작 고쳐야 할 때 그 표시를 안 믿게 됩니다.
            Assert.That(Quote(0f, 0f), Is.EqualTo(0));
        }

        [Test]
        public void 깎인_만큼_받는다()
        {
            Assert.That(Quote(10f, 0f), Is.EqualTo(CallOut + 10 * PerPoint));
            Assert.That(Quote(40f, 0f), Is.EqualTo(CallOut + 40 * PerPoint));
        }

        [Test]
        public void 많이_망가질수록_비싸다()
        {
            // ⚠ 정액이면 다 부서질 때까지 기다리는 것이 이득이 되어,
            // 조심해서 모는 쪽이 손해를 봅니다.
            Assert.That(Quote(40f, 0f), Is.GreaterThan(Quote(10f, 0f) * 2),
                        "네 배 망가지면 두 배보다 훨씬 비싸야 합니다");
        }

        [Test]
        public void 연료도_같이_받는다()
        {
            Assert.That(Quote(0f, 20f), Is.EqualTo(CallOut + 20 * PerFuel));
            Assert.That(Quote(10f, 20f),
                        Is.EqualTo(CallOut + 10 * PerPoint + 20 * PerFuel));
        }

        [Test]
        public void 연료값이_0_이면_주유를_안_센다()
        {
            Assert.That(RepairArm.Quote(0f, 50f, PerPoint, 0, CallOut), Is.EqualTo(0),
                        "주유를 안 하면 연료만 비어도 받을 것이 없습니다");
        }

        [Test]
        public void 반토막은_올려_받는다()
        {
            // 0.1 만 깎여도 한 점 값을 받습니다. 소수점으로 깎아 주면
            // 긁힌 자국이 영원히 안 고쳐집니다.
            Assert.That(Quote(0.1f, 0f), Is.EqualTo(CallOut + PerPoint));
        }

        [Test]
        public void 음수는_0_으로_본다()
        {
            // 체력이 최대보다 많은 경우(회복 아이템 등)가 생겨도 값이 음수가 되면 안 됩니다.
            Assert.That(Quote(-10f, 0f), Is.EqualTo(0));
            Assert.That(Quote(-10f, 5f), Is.EqualTo(CallOut + 5 * PerFuel));
        }
    }
}
