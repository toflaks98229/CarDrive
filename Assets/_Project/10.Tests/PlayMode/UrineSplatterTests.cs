using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 젖은 자국이 <b>고이고, 번지고, 마르는지</b> 확인합니다.
    ///
    /// <b>왜 PlayMode 인가.</b> 자국은 물줄기의 포물선을 레이캐스트로 따라가 면을 찾습니다.
    /// EditMode 에서는 물리 씬이 갱신되지 않아 <c>Physics.Raycast</c> 가 콜라이더를 못 찾고,
    /// 그러면 자국이 하나도 안 생겨 <b>아무것도 확인하지 못한 채 통과</b>합니다.
    ///
    /// <b>무엇을 지키는가.</b> 이 셋은 눈으로 보면 금방 알지만 코드로는 조용히 깨집니다 —
    ///  - 한자리에 계속 누면 <b>자국 하나가 자라야</b> 합니다. 매 프레임 새로 찍으면
    ///    같은 자리에 도장이 스물네 개 겹쳐 풀이 한 번에 바닥납니다.
    ///  - 조준을 옮기면 <b>새 자국이 찍혀야</b> 합니다. 하나만 늘어나면 훑고 지나간 자리가
    ///    안 남아 "이리저리 누면 넓게 퍼진다"가 성립하지 않습니다.
    ///  - 시간이 지나면 <b>없어져야</b> 합니다. 안 없어지면 판이 계속 쌓입니다.
    /// </summary>
    public class UrineSplatterTests
    {
        private GameObject floor;
        private GameObject rig;
        private UrineSplatter splatter;

        /// <summary>노즐 높이입니다. 여기서 아래로 조금 기울여 쏩니다.</summary>
        private static readonly Vector3 Nozzle = new Vector3(0f, 1.2f, 0f);

        [SetUp]
        public void SetUp()
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(4f, 1f, 4f);

            rig = new GameObject("SplatterRig");
            splatter = rig.AddComponent<UrineSplatter>();

            // 자국을 실제로 그리지는 않지만, 재질이 없으면 Mark 가 곧바로 돌아갑니다.
            splatter.splatMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            splatter.maxSplats = 16;
            splatter.dryDuration = 0.4f;
        }

        [TearDown]
        public void TearDown()
        {
            if (rig != null) Object.DestroyImmediate(rig);
            if (floor != null) Object.DestroyImmediate(floor);
        }

        /// <summary>한자리에 계속 누면 자국이 하나로 남고 커집니다.</summary>
        [UnityTest]
        public IEnumerator 한자리에_계속_누면_자국_하나가_자란다()
        {
            yield return null;   // Awake 로 판을 만들 틈을 줍니다.

            for (int i = 0; i < 30; i++)
                splatter.Mark(Nozzle, Vector3.down, 3f, 1f, 0.05f);

            Assert.AreEqual(1, VisibleCount(), "같은 자리를 계속 적셨는데 자국이 여러 장 찍혔습니다.");
            Assert.Greater(WidestDiameter(), splatter.startDiameter + 0.05f,
                           "자국이 자라지 않았습니다. 고이는 것으로 안 읽힙니다.");
        }

        /// <summary>조준을 옮기면 지나간 자리마다 자국이 남습니다.</summary>
        [UnityTest]
        public IEnumerator 조준을_옮기면_지나간_자리마다_자국이_남는다()
        {
            yield return null;

            // 아래를 보며 좌우로 훑습니다. 각도가 벌어질수록 닿는 자리가 멀어집니다.
            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, 0f, i * 7f) * Vector3.down;
                splatter.Mark(Nozzle, dir, 3f, 1f, 0.05f);
            }

            Assert.Greater(VisibleCount(), 1,
                           "조준을 옮겼는데 자국이 하나뿐입니다. 훑은 자리가 안 남습니다.");
        }

        /// <summary>줄기가 멈추면 자국은 마르고 사라집니다.</summary>
        [UnityTest]
        public IEnumerator 시간이_지나면_자국이_사라진다()
        {
            yield return null;

            splatter.Mark(Nozzle, Vector3.down, 3f, 1f, 0.05f);
            Assert.AreEqual(1, VisibleCount(), "자국이 찍히지 않았습니다.");

            // 멈췄다고 알려야 마르기 시작합니다. 알리지 않으면 계속 적셔지는 것으로 봅니다.
            splatter.StopMarking();

            float waited = 0f;
            while (waited < splatter.dryDuration * 2f && VisibleCount() > 0)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(0, VisibleCount(), "다 말랐는데 자국이 남아 있습니다.");
        }

        /// <summary>흐름이 0 이면 아무것도 찍지 않습니다.</summary>
        [UnityTest]
        public IEnumerator 흐름이_없으면_자국도_없다()
        {
            yield return null;

            splatter.Mark(Nozzle, Vector3.down, 3f, 0f, 0.05f);

            Assert.AreEqual(0, VisibleCount(), "안 누었는데 자국이 생겼습니다.");
        }

        // --- 도우미 ---

        /// <summary>지금 보이는 자국 수입니다. 판은 꺼 두는 방식으로 회수됩니다.</summary>
        private int VisibleCount()
        {
            MeshRenderer[] all = rig.GetComponentsInChildren<MeshRenderer>(true);
            int n = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].enabled) n++;
            return n;
        }

        /// <summary>가장 큰 자국의 지름입니다. 판의 가로 크기가 곧 지름입니다.</summary>
        private float WidestDiameter()
        {
            MeshRenderer[] all = rig.GetComponentsInChildren<MeshRenderer>(true);
            float widest = 0f;
            for (int i = 0; i < all.Length; i++)
                if (all[i].enabled)
                    widest = Mathf.Max(widest, all[i].transform.localScale.x);
            return widest;
        }
    }
}
