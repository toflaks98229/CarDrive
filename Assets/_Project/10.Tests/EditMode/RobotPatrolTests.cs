using NUnit.Framework;
using UnityEngine;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// <see cref="RobotPatrol"/> 의 판단만 봅니다 — 시각표, 닿음, 쉬는 시간, 다음 자리.
    ///
    /// 지형도 시계도 세우지 않습니다. <see cref="RobotPatrol.Tick"/> 이 시각과 자리를
    /// 인자로 받으므로 그대로 몰 수 있습니다.
    /// </summary>
    public class RobotPatrolTests
    {
        private GameObject host;
        private RobotPatrol patrol;
        private GameObject[] stops;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("robot");
            patrol = host.AddComponent<RobotPatrol>();   // RobotDriver 가 함께 붙습니다
            patrol.dwellSeconds = 2f;

            stops = new GameObject[3];
            patrol.route = new Transform[3];

            for (int i = 0; i < 3; i++)
            {
                stops[i] = new GameObject("stop" + i);
                stops[i].transform.position = new Vector3(i * 10f, 0f, 0f);
                patrol.route[i] = stops[i].transform;
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);

            if (stops == null) return;
            foreach (GameObject s in stops) if (s != null) Object.DestroyImmediate(s);
        }

        // --- 시각표 ---

        [Test]
        public void 같은_시각이면_하루_종일_돈다()
        {
            patrol.onDutyHour = 0f;
            patrol.offDutyHour = 0f;

            Assert.That(patrol.IsOnDuty(3f), Is.True);
            Assert.That(patrol.IsOnDuty(14f), Is.True);
            Assert.That(patrol.IsOnDuty(23.9f), Is.True);
        }

        [Test]
        public void 낮_근무는_그_사이에만_돈다()
        {
            patrol.onDutyHour = 8f;
            patrol.offDutyHour = 18f;

            Assert.That(patrol.IsOnDuty(7.9f), Is.False);
            Assert.That(patrol.IsOnDuty(8f), Is.True);
            Assert.That(patrol.IsOnDuty(17.9f), Is.True);
            Assert.That(patrol.IsOnDuty(18f), Is.False);
        }

        [Test]
        public void 자정을_넘긴_근무도_돈다()
        {
            // ⚠ 이 게임은 밤 운전이 본편이라 이 경우가 기본입니다.
            patrol.onDutyHour = 20f;
            patrol.offDutyHour = 6f;

            Assert.That(patrol.IsOnDuty(21f), Is.True);
            Assert.That(patrol.IsOnDuty(2f), Is.True);
            Assert.That(patrol.IsOnDuty(6f), Is.False);
            Assert.That(patrol.IsOnDuty(12f), Is.False);
        }

        // --- 길 ---

        [Test]
        public void 닿으면_쉬었다가_다음_자리로_간다()
        {
            patrol.onDutyHour = 0f;
            patrol.offDutyHour = 0f;

            // 첫 자리에 서 있습니다.
            Vector3 here = stops[0].transform.position;

            Assert.That(patrol.Tick(0f, 12f, here, out Vector3 go), Is.True);
            Assert.That(go, Is.EqualTo(stops[0].transform.position), "아직 첫 자리입니다");

            // 쉬는 시간이 다 차기 전에는 안 넘어갑니다.
            patrol.Tick(1f, 12f, here, out go);
            Assert.That(patrol.StopIndex, Is.EqualTo(0), "덜 쉬었는데 넘어가면 안 됩니다");

            patrol.Tick(1.5f, 12f, here, out go);
            Assert.That(patrol.StopIndex, Is.EqualTo(1), "다 쉬었으면 넘어가야 합니다");
            Assert.That(go, Is.EqualTo(stops[1].transform.position));
        }

        [Test]
        public void 멀리_있으면_쉬는_시간이_안_흐른다()
        {
            patrol.onDutyHour = 0f;
            patrol.offDutyHour = 0f;

            Vector3 far = new Vector3(-50f, 0f, 0f);

            for (int i = 0; i < 20; i++) patrol.Tick(1f, 12f, far, out _);

            Assert.That(patrol.StopIndex, Is.EqualTo(0), "닿지도 않았는데 넘어가면 안 됩니다");
        }

        [Test]
        public void 퇴근하면_격납고로_가고_자리가_처음으로_돌아간다()
        {
            patrol.onDutyHour = 8f;
            patrol.offDutyHour = 18f;

            GameObject home = new GameObject("berth");
            home.transform.position = new Vector3(0f, 0f, 99f);
            patrol.berth = home.transform;

            try
            {
                // 낮에 한 자리 넘어가 둡니다.
                Vector3 here = stops[0].transform.position;
                patrol.Tick(3f, 12f, here, out _);
                Assert.That(patrol.StopIndex, Is.EqualTo(1));

                // 퇴근.
                Assert.That(patrol.Tick(1f, 20f, here, out Vector3 go), Is.True);
                Assert.That(go, Is.EqualTo(home.transform.position), "격납고로 가야 합니다");
                Assert.That(patrol.OnDuty, Is.False);

                // ⚠ 다음 날 어제 쉬던 중간부터 이어지면 안 됩니다.
                Assert.That(patrol.StopIndex, Is.EqualTo(0), "자리가 처음으로 돌아가야 합니다");
            }
            finally
            {
                Object.DestroyImmediate(home);
            }
        }

        [Test]
        public void 격납고가_없으면_그_자리에_선다()
        {
            patrol.onDutyHour = 8f;
            patrol.offDutyHour = 18f;
            patrol.berth = null;

            Assert.That(patrol.Tick(1f, 3f, Vector3.zero, out _), Is.False,
                        "갈 곳이 없다고 말해야 합니다");
        }

        // --- 다음 자리 고르기 ---

        [Test]
        public void 고리로_돌면_끝에서_처음으로()
        {
            int dir = 1;

            Assert.That(RobotPatrol.NextStop(0, 3, true, ref dir), Is.EqualTo(1));
            Assert.That(RobotPatrol.NextStop(2, 3, true, ref dir), Is.EqualTo(0));
        }

        [Test]
        public void 되짚으면_끝에서_방향이_뒤집힌다()
        {
            // ⚠ 한 줄로 난 길에서 고리로 돌면 길 밖으로 질러갑니다.
            int dir = 1;

            Assert.That(RobotPatrol.NextStop(1, 3, false, ref dir), Is.EqualTo(2));
            Assert.That(RobotPatrol.NextStop(2, 3, false, ref dir), Is.EqualTo(1));
            Assert.That(dir, Is.EqualTo(-1), "방향이 뒤집혀야 합니다");
            Assert.That(RobotPatrol.NextStop(1, 3, false, ref dir), Is.EqualTo(0));
            Assert.That(RobotPatrol.NextStop(0, 3, false, ref dir), Is.EqualTo(1));
            Assert.That(dir, Is.EqualTo(1), "다시 뒤집혀야 합니다");
        }

        [Test]
        public void 자리가_하나뿐이면_거기_머문다()
        {
            int dir = 1;

            Assert.That(RobotPatrol.NextStop(0, 1, true, ref dir), Is.EqualTo(0));
            Assert.That(RobotPatrol.NextStop(0, 1, false, ref dir), Is.EqualTo(0));
        }
    }
}
