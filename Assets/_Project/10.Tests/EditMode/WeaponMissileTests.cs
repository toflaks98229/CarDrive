using NUnit.Framework;
using UnityEngine;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 미사일이 <b>도착하는 데 시간이 걸리는지</b> 봅니다.
    ///
    /// 그것이 히트스캔과의 차이 전부입니다. 예전에는 쏘는 순간 이미 맞아 있어서
    /// 멀리 있어도 피할 수 없었습니다.
    /// </summary>
    public class WeaponMissileTests
    {
        /// <summary>맞으면 몇 대 맞았는지만 세는 과녁입니다.</summary>
        private sealed class Target : MonoBehaviour, IDamageable
        {
            public float Taken;
            public bool IsDead { get { return false; } }
            public void TakeDamage(float amount) { Taken += amount; }
        }

        private GameObject shot;
        private GameObject wall;
        private Target target;
        private WeaponMissile missile;

        [SetUp]
        public void SetUp()
        {
            shot = new GameObject("missile");
            missile = shot.AddComponent<WeaponMissile>();
            missile.speed = 50f;

            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 0f, 20f);
            wall.transform.localScale = new Vector3(10f, 10f, 1f);
            target = wall.AddComponent<Target>();

            // ⚠ <b>편집 모드에서는 콜라이더가 저절로 따라오지 않습니다.</b>
            // 트랜스폼을 옮긴 뒤 동기화하지 않으면 레이캐스트가 <b>원점에 있던
            // 옛 자리</b>를 봅니다. 이걸 빼먹어 "탄이 안 맞는다" 는 거짓 실패를
            // 세 개 만들었습니다.
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (shot != null) Object.DestroyImmediate(shot);
            if (wall != null) Object.DestroyImmediate(wall);
        }

        [Test]
        public void 쏜_순간에는_아직_안_맞는다()
        {
            missile.Launch(Vector3.zero, Vector3.forward, 18f, 90f, ~0, null);

            Assert.That(target.Taken, Is.EqualTo(0f), "쏘자마자 맞으면 히트스캔입니다");
        }

        [Test]
        public void 날아간_뒤에_맞는다()
        {
            missile.Launch(Vector3.zero, Vector3.forward, 18f, 90f, ~0, null);

            // 20 m 앞의 벽(두께 절반 0.5) 이므로 19.5 m, 초속 50 이면 0.39 초입니다.
            // 60 fps 로 몰아 봅니다.
            int frames = 0;
            while (missile != null && missile.Advance(1f / 60f) && frames < 600) frames++;

            Assert.That(target.Taken, Is.EqualTo(18f).Within(0.01f), "도착하면 맞아야 합니다");
            Assert.That(frames, Is.GreaterThan(20), "0.39 초면 23 프레임쯤 걸려야 합니다");
            Assert.That(frames, Is.LessThan(30), "그보다 오래 걸리면 속도가 안 맞습니다");
        }

        [Test]
        public void 사거리를_넘으면_사라지고_아무도_안_맞는다()
        {
            // 벽을 사거리 밖으로 밀어 둡니다.
            wall.transform.position = new Vector3(0f, 0f, 60f);
            Physics.SyncTransforms();

            missile.Launch(Vector3.zero, Vector3.forward, 18f, 30f, ~0, null);

            int frames = 0;
            while (missile != null && missile.Advance(1f / 60f) && frames < 600) frames++;

            Assert.That(target.Taken, Is.EqualTo(0f), "사거리 밖은 안 맞아야 합니다");
        }

        [Test]
        public void 쏜_자신은_안_맞는다()
        {
            // ⚠ 탄은 포드 <b>앞에서</b> 태어나므로 첫 프레임에 제 다리를 맞는 일이 있습니다.
            GameObject robot = new GameObject("robot");
            wall.transform.SetParent(robot.transform, true);
            Physics.SyncTransforms();

            try
            {
                missile.Launch(Vector3.zero, Vector3.forward, 18f, 90f, ~0, robot.transform);

                int frames = 0;
                while (missile != null && missile.Advance(1f / 60f) && frames < 600) frames++;

                Assert.That(target.Taken, Is.EqualTo(0f), "쏜 자신은 건너뛰어야 합니다");
            }
            finally
            {
                if (wall != null) wall.transform.SetParent(null, true);
                Object.DestroyImmediate(robot);
            }
        }

        [Test]
        public void 빠른_탄도_얇은_벽을_안_지나친다()
        {
            // ⚠ 위치만 옮기고 그 자리에서 검사하면 한 프레임 안에 있던 벽을 지나칩니다.
            wall.transform.localScale = new Vector3(10f, 10f, 0.05f);
            Physics.SyncTransforms();
            missile.speed = 180f;   // 한 프레임에 3 m — 벽 두께의 60 배

            missile.Launch(Vector3.zero, Vector3.forward, 18f, 90f, ~0, null);

            int frames = 0;
            while (missile != null && missile.Advance(1f / 60f) && frames < 600) frames++;

            Assert.That(target.Taken, Is.EqualTo(18f).Within(0.01f), "얇은 벽도 맞아야 합니다");
        }
    }
}
