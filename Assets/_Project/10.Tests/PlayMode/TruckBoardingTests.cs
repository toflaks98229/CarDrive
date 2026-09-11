using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 트럭 문으로 타면 <b>트럭에</b> 타는지 봅니다.
    ///
    /// <b>왜 씬을 통째로 여는가.</b> 이 고장은 부품이 아니라 <b>배치</b>에서 납니다 —
    /// 차가 두 대 서 있고, 조준점이 어느 것을 잡느냐가 문제입니다. 프리팹만 띄우면
    /// 옆에 설 다른 차가 없어서 재현되지 않습니다.
    /// </summary>
    public class TruckBoardingTests
    {
        private Vehicle truck;
        private Vehicle sedan;
        private PlayerModeController player;

        /// <summary>
        /// ⚠ <b>열어 둔 씬을 치웁니다.</b> 이 검사가 <c>SampleScene</c> 을 통째로 열어
        /// 두면 <b>다음 검사들이 그 안에서</b> 돕니다. 자기 바닥을 깔고 포물선을 쏘는
        /// 검사(<c>UrineSplatterTests</c>)가 세계의 지형에 먼저 맞아 자국이 하나도
        /// 안 남았고, 열 개가 한꺼번에 빨간불이 됐습니다 — 그 검사들의 잘못이 아닙니다.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene sample = SceneManager.GetSceneByName("SampleScene");
            if (!sample.isLoaded) yield break;

            Scene empty = SceneManager.CreateScene("검사가 쓰고 비워 둔 씬");
            SceneManager.SetActiveScene(empty);

            yield return SceneManager.UnloadSceneAsync(sample);
        }

        private IEnumerator Open()
        {
            SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            for (int i = 0; i < 12; i++) yield return null;

            truck = null;
            sedan = null;

            foreach (Vehicle v in Object.FindObjectsByType<Vehicle>(FindObjectsInactive.Include,
                                                                    FindObjectsSortMode.None))
            {
                if (v.name.Contains("Truck")) truck = v;
                else if (sedan == null) sedan = v;
            }

            player = Object.FindAnyObjectByType<PlayerModeController>(FindObjectsInactive.Include);

            Assert.That(truck, Is.Not.Null, "씬에 트럭이 없습니다");
            Assert.That(sedan, Is.Not.Null, "씬에 세단이 없습니다");
            Assert.That(player, Is.Not.Null, "씬에 PlayerModeController 가 없습니다");
        }

        [UnityTest]
        public IEnumerator 트럭_문을_누르면_트럭에_탄다()
        {
            yield return Open();

            // 걸어 나온 상태로 둡니다. 이 게임은 차에서 시작합니다.
            player.ExitVehicle(true);
            yield return null;

            VehicleDoorInteractable door = truck.GetComponentInChildren<VehicleDoorInteractable>(true);
            Assert.That(door, Is.Not.Null, "트럭에 문이 없습니다");

            // ⚠ <b>문이 자기 차를 알고 있는가.</b> 이것이 어긋나면 조준은 트럭을
            // 잡았는데 탑승은 옆 차로 갑니다.
            Assert.That(door.vehicle, Is.EqualTo(truck),
                        "트럭 문이 가리키는 차가 "
                        + (door.vehicle != null ? door.vehicle.name : "없음") + " 입니다");

            door.Interact();
            yield return null;

            Assert.That(player.CurrentVehicle, Is.EqualTo(truck),
                        "트럭 문으로 탔는데 "
                        + (player.CurrentVehicle != null ? player.CurrentVehicle.name : "아무데도")
                        + " 에 탔습니다");

            Assert.That(Vehicle.Current, Is.EqualTo(truck), "지금 차가 트럭이 아닙니다");
        }

        [UnityTest]
        public IEnumerator 트럭에_타면_카메라도_트럭에_있다()
        {
            yield return Open();

            player.ExitVehicle(true);
            yield return null;

            VehicleDoorInteractable door = truck.GetComponentInChildren<VehicleDoorInteractable>(true);
            door.Interact();

            for (int i = 0; i < 5; i++) yield return null;

            Assert.That(player.carCameraFollow, Is.Not.Null, "추종 카메라가 없습니다");

            Transform target = player.carCameraFollow.target;
            Assert.That(target, Is.Not.Null, "추종 카메라가 볼 자리가 없습니다");

            // 그 자리가 트럭 아래에 있어야 합니다.
            bool underTruck = false;
            for (Transform t = target; t != null; t = t.parent)
            {
                if (t == truck.transform) { underTruck = true; break; }
            }

            Assert.IsTrue(underTruck,
                          "카메라가 보는 자리가 트럭이 아니라 " + Path(target) + " 입니다");

            // 눈이 실제로 트럭 안에 있는가. 세단에 앉아 있으면 5 m 넘게 떨어집니다.
            Camera eye = Camera.main;
            if (eye != null)
            {
                float away = Vector3.Distance(eye.transform.position, truck.DriverAnchor.position);
                Assert.That(away, Is.LessThan(2f),
                            "눈이 트럭 운전석에서 " + away.ToString("F1") + " m 떨어져 있습니다");
            }
        }

        private static string Path(Transform t)
        {
            string s = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }
    }
}
