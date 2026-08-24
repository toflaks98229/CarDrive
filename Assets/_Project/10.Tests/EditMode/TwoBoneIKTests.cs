using NUnit.Framework;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Tests
{
    /// <summary>
    /// 두 뼈 IK 가 <b>발을 목표에 정확히 놓고</b>, <b>뼈를 늘이지 않는다</b>는 것을 고정합니다.
    ///
    /// <b>왜 이 테스트인가.</b> 뼈 길이가 어긋나는 것은 화면에서 알아채기 어렵습니다.
    /// 다리 넷이 매 프레임 바뀌는 자세로 서 있으면 1cm 늘어난 정강이는 눈에 띄지 않습니다.
    /// 그런데 그 오차는 <b>발이 땅에서 뜨거나 파고드는 것</b>으로 이어지고,
    /// 그때는 이미 원인이 IK 인지 걸음 계산인지 구분되지 않습니다.
    ///
    /// 무릎이 접히는 방향도 여기서 고정합니다. 부호 하나가 뒤집히면 앞다리와 뒷다리가
    /// <b>같은 쪽으로 접혀</b> 네발짐승이 아니라 탁자가 됩니다.
    /// </summary>
    public class TwoBoneIKTests
    {
        /// <summary>테스트에 쓰는 위 뼈 길이입니다.</summary>
        private const float Upper = 0.42f;

        /// <summary>테스트에 쓰는 아래 뼈 길이입니다.</summary>
        private const float Lower = 0.42f;

        // --- Tests ---

        /// <summary>뻗을 수 있는 목표에는 정확히 닿아야 합니다.</summary>
        /// <param name="x">목표의 X</param>
        /// <param name="y">목표의 Y</param>
        /// <param name="z">목표의 Z</param>
        [TestCase(0f, -0.6f, 0f)]
        [TestCase(0.2f, -0.5f, 0.1f)]
        [TestCase(-0.3f, -0.4f, -0.25f)]
        [TestCase(0f, -0.1f, 0f)]
        public void 닿는_목표에는_정확히_닿는다(float x, float y, float z)
        {
            Vector3 target = new Vector3(x, y, z);

            TwoBoneIK.Solution solution = TwoBoneIK.Solve(Vector3.zero, target, Upper, Lower, Vector3.back);

            Assert.IsTrue(solution.Reached, "닿을 수 있는 목표인데 닿지 못했다고 합니다.");
            Assert.Less(Vector3.Distance(solution.End, target), 1e-4f, "발이 목표에 놓이지 않았습니다.");
        }

        /// <summary>어떤 목표에서도 뼈 길이는 변하지 않아야 합니다.</summary>
        /// <param name="x">목표의 X</param>
        /// <param name="y">목표의 Y</param>
        /// <param name="z">목표의 Z</param>
        [TestCase(0f, -0.6f, 0f)]
        [TestCase(0.2f, -0.5f, 0.1f)]
        [TestCase(0f, -3f, 0f)]
        [TestCase(0f, -0.0001f, 0f)]
        public void 뼈_길이가_유지된다(float x, float y, float z)
        {
            TwoBoneIK.Solution solution = TwoBoneIK.Solve(Vector3.zero, new Vector3(x, y, z), Upper, Lower, Vector3.back);

            Assert.AreEqual(Upper, Vector3.Distance(Vector3.zero, solution.Joint), 1e-4f, "위 뼈가 늘어났습니다.");
            Assert.AreEqual(Lower, Vector3.Distance(solution.Joint, solution.End), 1e-4f, "아래 뼈가 늘어났습니다.");
        }

        /// <summary>
        /// 닿지 않는 목표에서는 <b>목표 쪽으로 최대한 뻗기만</b> 해야 합니다.
        /// 억지로 늘이면 뼈 길이가 변하고, 각도만 0으로 만들면 발이 옆으로 샙니다.
        /// </summary>
        [Test]
        public void 닿지_않는_목표에서는_최대로_뻗는다()
        {
            Vector3 target = new Vector3(0f, -5f, 0f);

            TwoBoneIK.Solution solution = TwoBoneIK.Solve(Vector3.zero, target, Upper, Lower, Vector3.back);

            Assert.IsFalse(solution.Reached, "닿을 수 없는 목표인데 닿았다고 합니다.");
            Assert.AreEqual(Upper + Lower, Vector3.Distance(Vector3.zero, solution.End), 0.001f, "최대로 뻗지 않았습니다.");

            // 방향은 목표 쪽이어야 합니다.
            Assert.Less(Vector3.Angle(solution.End, target), 0.5f, "발이 목표 방향에서 벗어났습니다.");
        }

        /// <summary>
        /// 무릎은 <b>극 방향 쪽으로</b> 접혀야 합니다.
        /// 앞다리와 뒷다리의 차이가 이 인자 하나뿐이므로, 부호가 뒤집히면 걸음이 통째로 어색해집니다.
        /// </summary>
        [Test]
        public void 무릎은_극_방향으로_접힌다()
        {
            Vector3 target = new Vector3(0f, -0.6f, 0f);

            TwoBoneIK.Solution back = TwoBoneIK.Solve(Vector3.zero, target, Upper, Lower, Vector3.back);
            TwoBoneIK.Solution forward = TwoBoneIK.Solve(Vector3.zero, target, Upper, Lower, Vector3.forward);

            Assert.Less(back.Joint.z, -0.1f, "무릎이 뒤로 접히지 않았습니다.");
            Assert.Greater(forward.Joint.z, 0.1f, "무릎이 앞으로 접히지 않았습니다.");
        }

        /// <summary>목표가 밑동과 겹쳐도 계산이 무너지면 안 됩니다. (스폰 첫 프레임에 실제로 생깁니다)</summary>
        [Test]
        public void 목표가_밑동과_겹쳐도_무너지지_않는다()
        {
            TwoBoneIK.Solution solution = TwoBoneIK.Solve(Vector3.zero, Vector3.zero, Upper, Lower, Vector3.back);

            Assert.IsFalse(float.IsNaN(solution.Joint.x + solution.Joint.y + solution.Joint.z), "무릎 위치가 NaN 입니다.");
            Assert.IsFalse(float.IsNaN(solution.End.x + solution.End.y + solution.End.z), "발 위치가 NaN 입니다.");
        }

        /// <summary>
        /// 극 방향이 다리와 나란해도 무너지면 안 됩니다.
        /// 다리를 곧게 편 채로 앞뒤로 흔들 때 실제로 지나가는 자세입니다.
        /// </summary>
        [Test]
        public void 극_방향이_다리와_나란해도_무너지지_않는다()
        {
            TwoBoneIK.Solution solution = TwoBoneIK.Solve(Vector3.zero, new Vector3(0f, -0.6f, 0f), Upper, Lower, Vector3.down);

            Assert.AreEqual(Upper, Vector3.Distance(Vector3.zero, solution.Joint), 1e-4f, "위 뼈가 늘어났습니다.");
            Assert.AreEqual(Lower, Vector3.Distance(solution.Joint, solution.End), 1e-4f, "아래 뼈가 늘어났습니다.");
        }
    }
}
