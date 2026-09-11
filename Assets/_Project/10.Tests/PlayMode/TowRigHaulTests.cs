using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 견인기가 죽은 차를 <b>실제로 걸고 끌어다 놓는지</b> 봅니다.
    ///
    /// <b>걷는 부품 없이 봅니다.</b> <see cref="RobotDriver"/> 를 빼면 기계는 제자리에
    /// 서 있고, 그러면 <b>순서</b>만 남습니다 — 알아채고, 걸고, 값을 받고, 놓습니다.
    /// 다리가 목적지에 닿는지는 걷는 부품의 몫이지 이 부품의 몫이 아닙니다.
    /// 그래서 차와 마을을 갈고리가 닿는 거리에 두고 순서만 잽니다.
    /// </summary>
    public class TowRigHaulTests
    {
        private GameObject rig;
        private GameObject car;
        private GameObject yard;
        private TowRig tow;
        private Vehicle vehicle;

        [SetUp]
        public void SetUp()
        {
            car = new GameObject("DeadCar");
            car.transform.position = new Vector3(3f, 0f, 0f);

            // ⚠ <b>내구도를 먼저 답니다.</b> <c>Vehicle</c> 은 Awake 에서 부품을 찾아
            // 채우므로, 뒤에 달면 <c>vehicle.health</c> 가 빈 채로 남습니다.
            VehicleHealth health = car.AddComponent<VehicleHealth>();
            health.maxHealth = 100f;

            vehicle = car.AddComponent<Vehicle>();

            // ⚠ <b>주행 부품은 재웁니다.</b> <c>Vehicle</c> 이 RequireComponent 로
            // <c>CarController</c> 를 데려오는데, 그것은 <c>CarData</c> 가 없으면
            // 시작하면서 오류를 남깁니다. 플레이 모드 검사는 오류 한 줄에 실패하므로
            // 여기서 재워 둡니다 — 이 검사가 보는 것은 주행이 아니라 견인입니다.
            CarController wheelwork = car.GetComponent<CarController>();
            if (wheelwork != null) wheelwork.enabled = false;

            // ⚠ <b>Vehicle 이 이미 강체를 데려옵니다</b>(RequireComponent). 하나 더 붙이면
            // 유니티가 거절하고 null 을 돌려줍니다 — 처음에 그것으로 넷이 다 터졌습니다.
            Rigidbody body = car.GetComponent<Rigidbody>();
            if (body == null) body = car.AddComponent<Rigidbody>();

            body.isKinematic = false;
            body.useGravity = false;

            yard = new GameObject("Yard");
            yard.transform.position = new Vector3(0f, 0f, 3f);

            rig = new GameObject("TowRig");
            tow = rig.AddComponent<TowRig>();
            tow.yard = yard.transform;
            tow.notice = 50f;
            tow.hookRange = 6f;
            tow.hookSeconds = 0f;
            tow.callOut = 40;
            tow.pricePerHundredMetres = 6;
        }

        [TearDown]
        public void TearDown()
        {
            if (rig != null) Object.Destroy(rig);
            if (car != null) Object.Destroy(car);
            if (yard != null) Object.Destroy(yard);
        }

        /// <summary>차를 죽입니다. 내구도가 0 이면 멈춰 선 차입니다.</summary>
        private void Kill()
        {
            vehicle.health.TakeDamage(vehicle.health.maxHealth * 2f);
        }

        [UnityTest]
        public IEnumerator 멀쩡한_차는_부르지_않는다()
        {
            yield return null;
            yield return null;

            Assert.That(tow.Doing, Is.EqualTo(TowRig.Phase.Waiting),
                        "멀쩡한 차를 보고 움직였습니다");
            Assert.That(tow.Hauled, Is.Null);
        }

        [UnityTest]
        public IEnumerator 죽은_차를_걸고_마을에_내려놓는다()
        {
            yield return null;
            Kill();

            // 알아채고 → 걸어가고 → 걸고 → 끌고 → 놓습니다.
            for (int i = 0; i < 10; i++)
            {
                if (tow.Doing == TowRig.Phase.Leaving || tow.Doing == TowRig.Phase.Waiting
                    && tow.LastBill > 0)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(tow.LastBill, Is.GreaterThan(0),
                        "값을 매기지 않았습니다 — 걸지 못한 것입니다");

            Assert.That(tow.Hauled, Is.Null, "내려놓지 않고 계속 끌고 있습니다");

            Assert.That(tow.Doing, Is.EqualTo(TowRig.Phase.Leaving),
                        "내려놓은 뒤에는 제자리로 돌아가야 합니다");
        }

        [UnityTest]
        public IEnumerator 값은_끌고_갈_거리로_매긴다()
        {
            yield return null;
            Kill();

            for (int i = 0; i < 10 && tow.LastBill == 0; i++) yield return null;

            // 차(3, 0, 0) 에서 마을(0, 0, 3) 까지는 4.24 m 입니다.
            int want = TowRig.Quote(Vector3.Distance(new Vector3(3f, 0f, 0f),
                                                     new Vector3(0f, 0f, 3f)), 6, 40);

            Assert.That(tow.LastBill, Is.EqualTo(want),
                        "값이 거리를 따르지 않습니다");
        }

        [UnityTest]
        public IEnumerator 내려놓은_차는_다시_물리를_받는다()
        {
            yield return null;
            Kill();

            for (int i = 0; i < 10 && tow.Hauled == null && tow.LastBill == 0; i++)
            {
                yield return null;
            }

            for (int i = 0; i < 10 && tow.Doing != TowRig.Phase.Leaving; i++) yield return null;

            Rigidbody body = car.GetComponent<Rigidbody>();

            // ⚠ 재운 채로 놓으면 차가 허공에 못 박힙니다. 밀어도 안 움직이고
            // 떨어지지도 않아, 고쳐 놓고도 못 타는 차가 됩니다.
            Assert.IsFalse(body.isKinematic, "내려놓은 차가 재워진 채입니다");
        }
    }
}
