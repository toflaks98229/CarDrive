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

            // 통은 씬 루트에 따로 서 있어서 리그를 지워도 남습니다.
            GameObject world = GameObject.Find("UrineSplats (World)");
            if (world != null) Object.DestroyImmediate(world);
        }

        /// <summary>한자리에 계속 누면 자국이 하나로 남고 커집니다.</summary>
        [UnityTest]
        public IEnumerator 한자리에_계속_누면_자국_하나가_자란다()
        {
            yield return null;   // Awake 로 판을 만들 틈을 줍니다.

            for (int i = 0; i < 30; i++)
                splatter.Mark(Nozzle, Vector3.down, 3f, 1f, 1f, 0.05f);

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
                splatter.Mark(Nozzle, dir, 3f, 1f, 1f, 0.05f);
            }

            Assert.Greater(VisibleCount(), 1,
                           "조준을 옮겼는데 자국이 하나뿐입니다. 훑은 자리가 안 남습니다.");
        }

        /// <summary>줄기가 멈추면 자국은 마르고 사라집니다.</summary>
        [UnityTest]
        public IEnumerator 시간이_지나면_자국이_사라진다()
        {
            yield return null;

            splatter.Mark(Nozzle, Vector3.down, 3f, 1f, 1f, 0.05f);
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

            splatter.Mark(Nozzle, Vector3.down, 3f, 1f, 0f, 0.05f);

            Assert.AreEqual(0, VisibleCount(), "안 누었는데 자국이 생겼습니다.");
        }

        /// <summary>
        /// 자국을 찍은 뒤 <b>플레이어가 움직이고 돌아도</b> 자국은 제자리에 있어야 합니다.
        ///
        /// <b>이것이 실제로 났던 고장입니다.</b> 자국 판을 이 컴포넌트의 자식으로 만들었더니,
        /// 이 컴포넌트가 붙은 플레이어 몸통이 마우스 좌우에 따라 통째로 돌면서 자국이
        /// 벽이 아니라 <b>카메라를 따라다녔습니다.</b> <c>Transform.position</c> 은 그 순간의
        /// 월드 자세를 로컬로 환산해 저장할 뿐이라, 월드 좌표를 아무리 정확히 넣어도
        /// 부모가 움직이면 끌려갑니다.
        ///
        /// 먼저 있던 테스트들은 리그를 한 번도 움직이지 않아서 이 고장을 <b>전부 통과시켰습니다.</b>
        /// </summary>
        [UnityTest]
        public IEnumerator 플레이어가_움직이고_돌아도_자국은_제자리에_있는다()
        {
            yield return null;

            splatter.Mark(Nozzle, Vector3.down, 3f, 1f, 1f, 0.05f);
            splatter.StopMarking();

            var marks = Visible();
            Assert.AreEqual(1, marks.Count, "자국이 찍히지 않았습니다.");

            Vector3 wasAt = marks[0].position;
            Quaternion wasFacing = marks[0].rotation;

            // 플레이어가 걸어가고, 마우스로 몸을 돌립니다. 실제 게임이 매 프레임 하는 일입니다.
            rig.transform.position = new Vector3(7f, 0f, -4f);
            rig.transform.rotation = Quaternion.Euler(0f, 130f, 0f);
            yield return null;

            var after = Visible();
            Assert.AreEqual(1, after.Count, "움직였더니 자국 수가 달라졌습니다.");

            Assert.Less(Vector3.Distance(after[0].position, wasAt), 0.001f,
                        "플레이어를 따라 자국이 움직였습니다. 판이 플레이어의 자식으로 붙어 있습니다.");
            Assert.Less(Quaternion.Angle(after[0].rotation, wasFacing), 0.1f,
                        "플레이어를 따라 자국이 돌았습니다. 판이 플레이어의 자식으로 붙어 있습니다.");
        }

        /// <summary>
        /// 자국을 <b>키우는 동안</b>에도 플레이어가 움직이면 자국은 제자리여야 합니다.
        ///
        /// 활성 자국은 매 프레임 Place() 로 다시 자리를 잡으므로 위치는 우연히 맞을 수 있습니다.
        /// 하지만 회전까지 다시 잡지 않으면 <b>판만 비스듬히 틀어져</b> 면에서 떨어져 나갑니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 키우는_중에_몸을_돌려도_자국이_면에서_안_떨어진다()
        {
            yield return null;

            splatter.Mark(Nozzle, Vector3.down, 3f, 1f, 1f, 0.05f);
            var marks = Visible();
            Assert.AreEqual(1, marks.Count, "자국이 찍히지 않았습니다.");
            Quaternion wasFacing = marks[0].rotation;

            // 계속 누고 있는 채로 몸을 돌립니다.
            rig.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            splatter.Mark(Nozzle, Vector3.down, 3f, 1f, 1f, 0.05f);
            yield return null;

            var after = Visible();
            Assert.AreEqual(1, after.Count, "같은 자리인데 자국이 늘었습니다.");
            Assert.Less(Quaternion.Angle(after[0].rotation, wasFacing), 0.1f,
                        "몸을 돌렸더니 자국 판이 바닥에서 틀어졌습니다.");
        }

        /// <summary>
        /// 위로 겨눠 멀리 날아가도 닿는 곳에 자국이 남아야 합니다.
        ///
        /// <b>포물선 추적이 0.54초에서 끊겼습니다.</b> 이 씬의 입자는 최대 15m/s 로 2~3초를
        /// 날아가므로, 조금만 위로 겨누면 마지막 레이가 아직 허공에서 끝나 물줄기는 눈에 보이게
        /// 바닥에 부딪히는데 자국은 하나도 안 남았습니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 멀리_날아가도_닿는_곳에_자국이_남는다()
        {
            yield return null;

            // 수평보다 20도 위로, 빠르게. 예전 추적 구간(0.54초)으로는 절대 못 닿습니다.
            Vector3 up20 = Quaternion.Euler(-20f, 0f, 0f) * Vector3.forward;
            splatter.Mark(Nozzle, up20, 15f, 1f, 1f, 0.05f);

            Assert.AreEqual(1, VisibleCount(),
                            "멀리 쏘았더니 자국이 안 남았습니다. 포물선 추적이 짧습니다.");
        }

        // --- 도우미 ---

        /// <summary>
        /// 자국 판들이 담긴 통입니다.
        ///
        /// <b>리그의 자식에서 찾으면 안 됩니다.</b> 자국은 일부러 플레이어 밖, 씬 루트의
        /// 움직이지 않는 통에 담깁니다. 리그 아래를 뒤지는 테스트는 그 규칙이 깨져
        /// 자국이 다시 플레이어에 매달리는 날 <b>조용히 통과</b>합니다.
        /// </summary>
        private Transform SplatRoot()
        {
            GameObject go = GameObject.Find("UrineSplats (World)");
            return go != null ? go.transform : null;
        }

        /// <summary>지금 보이는 자국 판들입니다. 판은 꺼 두는 방식으로 회수됩니다.</summary>
        private System.Collections.Generic.List<Transform> Visible()
        {
            var list = new System.Collections.Generic.List<Transform>();
            Transform root = SplatRoot();
            if (root == null) return list;

            MeshRenderer[] all = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].enabled) list.Add(all[i].transform);
            return list;
        }

        /// <summary>지금 보이는 자국 수입니다.</summary>
        private int VisibleCount()
        {
            return Visible().Count;
        }

        /// <summary>가장 큰 자국의 지름입니다. 판의 가로 크기가 곧 지름입니다.</summary>
        private float WidestDiameter()
        {
            float widest = 0f;
            foreach (Transform t in Visible())
                widest = Mathf.Max(widest, t.localScale.x);
            return widest;
        }
    }
}
