using NUnit.Framework;
using UnityEngine;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 에디터에서 <b>관절 하나를 끌어 옮기면 나머지가 따라온다</b>는 것을 고정합니다.
    ///
    /// <b>왜 이 테스트인가.</b> 리그를 손으로 다듬는 일은 재생하지 않고 합니다. 그런데 다리의
    /// 아래 마디들은 IK 가 놓는 것이라, 계산이 쓰는 <b>길이</b>와 계층 구조가 만드는 <b>실제 위치</b>가
    /// 어긋나면 화면에서는 멀쩡해 보이는데 재생하는 순간 다리가 다른 곳으로 갑니다.
    /// 그 어긋남은 눈으로 잡을 수 없습니다 — 두 값이 서로 다른 곳에 적혀 있기 때문입니다.
    ///
    /// 그래서 <b>계층 구조를 유일한 진실</b>로 삼았습니다. 이 테스트가 고정하는 것은 세 가지입니다.
    ///  1. 관절을 옮긴 거리가 곧 마디 길이가 된다
    ///  2. 축을 벗어나게 끌어도 <b>+Z 규약으로 되돌아온다</b>
    ///  3. 그렇게 바뀐 길이로 IK 가 다시 풀려 <b>발이 목표에 닿는다</b>
    /// </summary>
    public class WalkerLegRigTests
    {
        /// <summary>처음에 세워 두는 넓적마디 길이입니다.</summary>
        private const float Femur = 0.50f;

        /// <summary>처음에 세워 두는 종아리마디 길이입니다.</summary>
        private const float Tibia = 0.40f;

        /// <summary>길이를 비교할 때 허용하는 오차입니다.</summary>
        private const float Tolerance = 1e-4f;

        // --- Private Member Variables ---

        /// <summary>테스트가 만든 로봇 루트입니다. 끝나면 지웁니다.</summary>
        private GameObject root;

        /// <summary>테스트가 다루는 다리입니다.</summary>
        private WalkerLeg leg;

        /// <summary>무릎 관절(종아리마디의 뿌리)입니다. 이것을 끌어 옮깁니다.</summary>
        private Transform knee;

        /// <summary>발목 관절입니다.</summary>
        private Transform ankle;

        // --- Setup ---

        /// <summary>세 마디짜리 다리를 규약대로 세웁니다.</summary>
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Robot");

            Transform hip = new GameObject("Femur").transform;
            hip.SetParent(root.transform, false);
            hip.localPosition = new Vector3(0.3f, 0.5f, 0.2f);

            knee = new GameObject("Tibia").transform;
            knee.SetParent(hip, false);
            knee.localPosition = new Vector3(0f, 0f, Femur);

            ankle = new GameObject("Tarsus").transform;
            ankle.SetParent(knee, false);
            ankle.localPosition = new Vector3(0f, 0f, Tibia);

            leg = root.AddComponent<WalkerLeg>();
            leg.upperBone = hip;
            leg.lowerBone = knee;
            leg.ankleBone = ankle;
            leg.homeOffset = new Vector3(0.3f, 0f, 0.2f);
            leg.kneePole = Vector3.up;
        }

        /// <summary>만든 오브젝트를 치웁니다.</summary>
        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        // --- Tests ---

        /// <summary>세운 그대로 재면 세운 길이가 나와야 합니다.</summary>
        [Test]
        public void MeasureFromRig_ReadsAuthoredLengths()
        {
            leg.MeasureFromRig();

            Assert.AreEqual(Femur, leg.upperLength, Tolerance, "넓적마디 길이");
            Assert.AreEqual(Tibia, leg.lowerLength, Tolerance, "종아리마디 길이");
        }

        /// <summary>무릎을 앞으로 끌면 넓적마디가 그만큼 길어져야 합니다.</summary>
        [Test]
        public void DraggingKneeChangesBoneLength()
        {
            knee.localPosition = new Vector3(0f, 0f, 0.75f);

            leg.MeasureFromRig();

            Assert.AreEqual(0.75f, leg.upperLength, Tolerance, "끌어 옮긴 거리가 길이가 되어야 합니다");
        }

        /// <summary>
        /// 축을 벗어나게 끌어도 <b>거리만 취하고 +Z 축 위로 되돌려</b> 놓아야 합니다.
        /// 규약이 깨진 채로 두면 IK 가 돌리는 방향과 마디가 향하는 방향이 어긋납니다.
        /// </summary>
        [Test]
        public void DraggingOffAxisSnapsBackToForwardAxis()
        {
            Vector3 dragged = new Vector3(0.2f, -0.3f, 0.6f);
            knee.localPosition = dragged;

            leg.MeasureFromRig();

            Assert.AreEqual(dragged.magnitude, leg.upperLength, Tolerance, "거리는 그대로 살아야 합니다");
            Assert.AreEqual(0f, knee.localPosition.x, Tolerance, "옆으로 샌 성분은 버려야 합니다");
            Assert.AreEqual(0f, knee.localPosition.y, Tolerance, "위아래로 샌 성분은 버려야 합니다");
            Assert.AreEqual(dragged.magnitude, knee.localPosition.z, Tolerance, "+Z 축 위로 옮겨야 합니다");
        }

        /// <summary>
        /// <b>넓적마디를 끌어도 고관절을 끈 것과 같아야</b> 합니다.
        ///
        /// 씬 뷰에서는 다리 뿌리와 넓적마디 중 아무거나 잡힙니다. 넓적마디만 옮겨지면
        /// 좌우 대칭이 뿌리 자리를 보므로 짝이 따라오지 않고, 발자리는 루트 기준이라
        /// 다리만 혼자 밀려납니다. 그래서 어긋남을 뿌리로 넘겨야 합니다.
        /// </summary>
        [Test]
        public void DraggingFemurFoldsIntoLegRoot()
        {
            // 먼저 규약대로 정리해 둡니다. 그래야 이 테스트가 재는 것이 "끈 만큼"이 됩니다.
            leg.MeasureFromRig();

            Vector3 rootWas = leg.transform.localPosition;
            Vector3 hipWas = leg.upperBone.position;
            Vector3 drag = new Vector3(0.15f, -0.2f, 0.05f);

            leg.upperBone.localPosition = drag;

            leg.MeasureFromRig();

            Assert.AreEqual(0f, leg.upperBone.localPosition.magnitude, Tolerance,
                "넓적마디는 다리 뿌리로 돌아와야 합니다");
            Assert.AreEqual(0f, Vector3.Distance(leg.transform.localPosition, rootWas + drag), Tolerance,
                "어긋남이 다리 뿌리로 넘어가야 합니다");
            Assert.AreEqual(0f, Vector3.Distance(leg.upperBone.position, hipWas + drag), Tolerance,
                "고관절은 끌어 놓은 자리에 그대로 있어야 합니다");
        }

        /// <summary>발목을 끌면 종아리마디가 길어져야 합니다.</summary>
        [Test]
        public void DraggingAnkleChangesLowerLength()
        {
            ankle.localPosition = new Vector3(0f, 0f, 0.55f);

            leg.MeasureFromRig();

            Assert.AreEqual(0.55f, leg.lowerLength, Tolerance, "종아리마디 길이");
        }

        /// <summary>
        /// <b>이것이 이 테스트의 핵심입니다.</b> 무릎을 옮긴 뒤 다시 풀면, 바뀐 길이로
        /// 발이 목표에 닿아야 합니다. 길이와 계층 구조가 어긋나면 여기서 벌어집니다.
        /// </summary>
        [Test]
        public void AfterDraggingKnee_FootStillReachesTarget()
        {
            knee.localPosition = new Vector3(0f, 0f, 0.70f);

            leg.MeasureFromRig();
            leg.Initialize(root.transform, 0.9f);

            // 뻗을 수 있는 길이 안쪽의 목표입니다. (0.70 + 0.40 = 1.10 보다 짧습니다)
            Vector3 target = leg.HipPosition + new Vector3(0.25f, -0.80f, 0.10f);

            leg.PlaceAt(target, Vector3.up);
            leg.Solve();

            Assert.AreEqual(0f, Vector3.Distance(ankle.position, target), 1e-3f, "발목이 목표에 닿아야 합니다");
        }

        /// <summary>
        /// <b>모양을 베끼면 마디가 실제로 옮겨져야</b> 합니다.
        ///
        /// 숫자만 베끼고 계층 구조를 두면 IK 가 믿는 길이와 화면의 마디가 어긋납니다.
        /// 다리 하나를 고쳐 나머지에 옮기는 기능이 여기에 걸려 있습니다.
        /// </summary>
        [Test]
        public void CopyShapeFrom_MovesBonesNotJustNumbers()
        {
            GameObject otherRoot = new GameObject("Other");

            try
            {
                Transform hip = new GameObject("Femur2").transform;
                hip.SetParent(otherRoot.transform, false);

                Transform knee2 = new GameObject("Tibia2").transform;
                knee2.SetParent(hip, false);
                knee2.localPosition = new Vector3(0f, 0f, 0.2f);

                Transform ankle2 = new GameObject("Tarsus2").transform;
                ankle2.SetParent(knee2, false);
                ankle2.localPosition = new Vector3(0f, 0f, 0.2f);

                WalkerLeg other = otherRoot.AddComponent<WalkerLeg>();
                other.upperBone = hip;
                other.lowerBone = knee2;
                other.ankleBone = ankle2;

                knee.localPosition = new Vector3(0f, 0f, 0.65f);
                leg.MeasureFromRig();

                other.CopyShapeFrom(leg);

                Assert.AreEqual(0.65f, other.upperLength, Tolerance, "길이 값이 옮겨져야 합니다");
                Assert.AreEqual(0.65f, knee2.localPosition.z, Tolerance, "마디도 함께 옮겨져야 합니다");
                Assert.AreEqual(Tibia, ankle2.localPosition.z, Tolerance, "발목도 함께 옮겨져야 합니다");
            }
            finally
            {
                Object.DestroyImmediate(otherRoot);
            }
        }

        /// <summary>마디를 옮겨도 <b>길이는 늘어나지 않아야</b> 합니다. 계층 구조가 그대로 지켜야 합니다.</summary>
        [Test]
        public void AfterSolve_BoneLengthsAreUnchanged()
        {
            knee.localPosition = new Vector3(0f, 0f, 0.70f);

            leg.MeasureFromRig();
            leg.Initialize(root.transform, 0.9f);

            leg.PlaceAt(leg.HipPosition + new Vector3(0.2f, -0.75f, 0f), Vector3.up);
            leg.Solve();

            Assert.AreEqual(0.70f, Vector3.Distance(leg.upperBone.position, knee.position), 1e-3f, "넓적마디가 늘어났습니다");
            Assert.AreEqual(Tibia, Vector3.Distance(knee.position, ankle.position), 1e-3f, "종아리마디가 늘어났습니다");
        }
    }
}
