using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 트럭이 <b>몰 수 있는 차인지</b> 봅니다.
    ///
    /// 씬을 통째로 열지 않고 프리팹만 땅 위에 띄웁니다 — 확인해야 하는 것은
    /// "부품이 다 물렸는가" 와 "바퀴로 서는가" 뿐이고, 그것을 보려고 세계를
    /// 스트리밍할 이유가 없습니다.
    /// </summary>
    public class TruckVehicleTests
    {
        private const string PrefabPath = "Assets/_Project/05.Prefabs/Player/PlayerTruck.prefab";

        private GameObject ground;
        private GameObject truck;

        [SetUp]
        public void SetUp()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            ground.transform.position = Vector3.zero;
        }

        [TearDown]
        public void TearDown()
        {
            if (truck != null) Object.Destroy(truck);
            if (ground != null) Object.Destroy(ground);
        }

        /// <summary>프리팹을 띄웁니다. 에디터 밖에서는 건너뜁니다.</summary>
        private bool Spawn()
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (prefab == null)
            {
                Assert.Ignore("트럭 프리팹이 없습니다 — " + PrefabPath);
                return false;
            }

            truck = Object.Instantiate(prefab, new Vector3(0f, 1.5f, 0f), Quaternion.identity);
            return true;
#else
            Assert.Ignore("에디터에서만 도는 검사입니다");
            return false;
#endif
        }

        [UnityTest]
        public IEnumerator 부품이_다_물린다()
        {
            if (!Spawn()) yield break;

            // Awake 가 돌아야 ResolveParts 가 부품을 채웁니다.
            yield return null;

            Vehicle facade = truck.GetComponent<Vehicle>();

            Assert.That(facade, Is.Not.Null, "Vehicle 이 있어야 합니다");
            Assert.That(facade.controller, Is.Not.Null, "CarController 가 물려야 합니다");
            Assert.That(facade.input, Is.Not.Null, "CarInput 이 물려야 합니다");
            Assert.That(facade.health, Is.Not.Null, "VehicleHealth 가 물려야 합니다");

            // ⚠ 이것이 이번에 한 번 빠졌던 자리입니다. VehicleSeat 은 프리팹에 없고
            // 씬 쪽에 있어서, 복제만 하면 운전석이 차의 원점(땅바닥)이 됩니다.
            Assert.That(facade.seat, Is.Not.Null, "VehicleSeat 이 물려야 합니다");
            Assert.That(facade.DriverAnchor, Is.Not.EqualTo(truck.transform),
                        "운전석이 차의 원점이면 땅바닥에 앉습니다");
        }

        [UnityTest]
        public IEnumerator 바퀴_넷이_다_물린다()
        {
            if (!Spawn()) yield break;
            yield return null;

            CarVisuals visuals = truck.GetComponent<CarVisuals>();
            Assert.That(visuals, Is.Not.Null);

            Assert.That(visuals.frontLeftWheelCollider, Is.Not.Null);
            Assert.That(visuals.frontRightWheelCollider, Is.Not.Null);
            Assert.That(visuals.rearLeftWheelCollider, Is.Not.Null);
            Assert.That(visuals.rearRightWheelCollider, Is.Not.Null);

            // 바퀴 반지름이 모델에서 온 값이어야 합니다. 세단의 0.40 이 남아 있으면
            // 트럭 바퀴(0.63)가 땅에 묻힙니다.
            Assert.That(visuals.frontLeftWheelCollider.radius, Is.GreaterThan(0.5f),
                        "트럭 바퀴 반지름이 세단 것으로 남아 있습니다");
        }

        [UnityTest]
        public IEnumerator 바퀴로_서고_안_넘어진다()
        {
            if (!Spawn()) yield break;

            // 물리가 가라앉을 때까지 둡니다.
            for (int i = 0; i < 180; i++) yield return new WaitForFixedUpdate();

            Assert.That(truck.transform.position.y, Is.GreaterThan(-0.5f),
                        "땅을 뚫고 내려가면 안 됩니다");
            Assert.That(truck.transform.position.y, Is.LessThan(3f),
                        "허공에 뜨면 안 됩니다");

            // ⚠ 트럭은 세단보다 높습니다. 무게중심을 안 낮추면 가라앉는 동안에도
            // 기울어집니다.
            float tilt = Vector3.Angle(truck.transform.up, Vector3.up);
            Assert.That(tilt, Is.LessThan(15f), "가만히 두었는데 " + tilt.ToString("F0") + "도 기울었습니다");
        }

        /// <summary>
        /// <b>밖에서 트럭을 보면 트럭 문이 잡히는가.</b>
        ///
        /// ⚠ <b>부품이 다 물려 있어도 못 탈 수 있습니다.</b> 조준점이 읽는 것은
        /// 문에 딸린 상호작용 콜라이더 하나뿐인데, 그것이 세단 자리(중심에서 1.07 m)에
        /// 남아 있으면 폭 3.15 m 짜리 트럭 <b>껍데기 안에 묻힙니다.</b> 그러면
        /// 트럭을 아무리 봐도 "탑승" 이 안 뜨고, 3 m 안에 다른 차가 서 있으면
        /// 조준선이 트럭을 지나쳐 <b>그 차의 문</b>을 잡습니다.
        ///
        /// 그래서 차의 부품이 아니라 <b>밖에서 보이는가</b>를 잽니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 밖에서_트럭을_보면_트럭_문이_잡힌다()
        {
            if (!Spawn()) yield break;
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            BoxCollider hull = truck.GetComponent<BoxCollider>();
            Assert.That(hull, Is.Not.Null, "몸 상자가 없습니다");

            // 서 있는 사람의 눈높이에서, 양옆으로 두 걸음 떨어져 봅니다.
            Vector3 middle = truck.transform.TransformPoint(hull.center);
            float reach = hull.size.x * 0.5f + 2f;

            foreach (float way in new[] { 1f, -1f })
            {
                Vector3 from = middle + truck.transform.right * (way * reach);
                from.y = truck.transform.position.y + 1.6f;

                Vector3 look = (middle - from).normalized;

                bool hit = Physics.Raycast(from, look, out RaycastHit what, 3f,
                                           ~0, QueryTriggerInteraction.Collide);

                Assert.IsTrue(hit, (way > 0f ? "오른쪽" : "왼쪽")
                              + "에서 트럭을 봤는데 아무것도 안 잡힙니다");

                VehicleDoorInteractable door =
                    what.collider.GetComponentInParent<VehicleDoorInteractable>();

                Assert.That(door, Is.Not.Null,
                            (way > 0f ? "오른쪽" : "왼쪽") + "에서 잡힌 것이 문이 아니라 "
                            + what.collider.name + " 입니다 — 문 손잡이가 껍데기 안에 묻혀 있습니다");

                Assert.That(door.GetComponentInParent<Vehicle>(), Is.EqualTo(truck.GetComponent<Vehicle>()),
                            "다른 차의 문이 잡혔습니다");
            }
        }
    }
}
