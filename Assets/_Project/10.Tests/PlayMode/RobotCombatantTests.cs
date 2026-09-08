using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 보행 로봇이 <b>게임의 전투 규약에 실제로 걸리는지</b>를 검증합니다.
    ///
    /// 여기서 확인하는 것은 로봇의 걸음이 아니라 <b>연결</b>입니다 — 앙크가 훑는
    /// <see cref="IDamageable"/> 로 잡히는지, 차와 발이 적으로 보는 <see cref="IHostile"/> 인지,
    /// 그리고 앙크가 매 프레임 때려도 사망 처리가 한 번만 도는지.
    ///
    /// <b>자식 콜라이더에서 찾아지는지가 특히 중요합니다.</b> 실제 로봇은 콜라이더가 몸통과
    /// 다리에 흩어져 있고, <see cref="PlayerAttacker"/> 는 맞은 콜라이더에서 부모로 거슬러
    /// 올라가며 찾습니다. 부품이 루트에 있어야 그 탐색이 닿습니다.
    /// </summary>
    public class RobotCombatantTests
    {
        // --- Private Member Variables ---

        /// <summary>검사에 쓴 오브젝트입니다. 끝나면 지웁니다.</summary>
        private GameObject robot;

        // --- Setup / Teardown ---

        /// <summary>검사가 끝나면 만든 오브젝트를 지웁니다.</summary>
        [TearDown]
        public void TearDown()
        {
            if (robot != null) Object.DestroyImmediate(robot);
        }

        // --- Helpers ---

        /// <summary>
        /// 전투 부품만 갖춘 최소한의 로봇을 만듭니다.
        /// 다리도 물리도 없습니다 — 여기서 보는 것은 <b>규약 연결</b>뿐입니다.
        ///
        /// <b>꺼진 채로 조립한 뒤 켭니다.</b> 켜져 있는 오브젝트에 붙이면 <c>Awake</c> 가
        /// 그 자리에서 돌아, 체력이 <b>기본값으로</b> 채워진 뒤에 최대 체력을 바꾸게 됩니다.
        /// 프리팹은 값이 먼저 서고 그 다음에 깨어나므로, 검사도 그 순서를 따라야 합니다.
        ///
        /// 인스펙터 이벤트도 여기서 만듭니다. 실행 중에 붙인 컴포넌트의 <see cref="UnityEvent"/>
        /// 필드는 비어 있습니다 — 제품 코드가 null 을 견디는 것과 별개로, 검사는 들어야 합니다.
        /// </summary>
        /// <param name="maxHealth">최대 체력</param>
        /// <returns>붙어 있는 전투 부품</returns>
        private RobotCombatant MakeRobot(float maxHealth)
        {
            robot = new GameObject("보행 로봇");
            robot.SetActive(false);

            EnemyHealth health = robot.AddComponent<EnemyHealth>();
            health.maxHealth = maxHealth;

            RobotCombatant combatant = robot.AddComponent<RobotCombatant>();
            combatant.onDamaged = new UnityEvent();
            combatant.onDied = new UnityEvent();

            robot.SetActive(true);

            return combatant;
        }

        // --- Tests ---

        /// <summary>앙크가 훑는 계약으로 잡힙니다. 이것이 없으면 로봇은 때릴 수 없습니다.</summary>
        [UnityTest]
        public IEnumerator 로봇은_때릴_수_있는_것으로_잡힌다()
        {
            MakeRobot(100f);
            yield return null;

            Assert.IsNotNull(robot.GetComponent<IDamageable>(), "IDamageable 로 잡히지 않습니다.");
        }

        /// <summary>차량 충돌과 도보 피격이 보는 적대 표시가 달려 있습니다.</summary>
        [UnityTest]
        public IEnumerator 로봇은_적으로_인식된다()
        {
            MakeRobot(100f);
            yield return null;

            Assert.IsNotNull(robot.GetComponent<IHostile>(), "IHostile 로 잡히지 않습니다.");
        }

        /// <summary>
        /// <b>자식 콜라이더에서 부모로 거슬러 올라가도 찾아집니다.</b>
        /// 실제 로봇은 콜라이더가 다리와 몸통에 흩어져 있으므로 이 경로가 실제 경로입니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 자식에서_부모로_거슬러_올라가도_찾아진다()
        {
            MakeRobot(100f);

            GameObject shin = new GameObject("정강이");
            shin.transform.SetParent(robot.transform);

            yield return null;

            Assert.IsNotNull(shin.GetComponentInParent<IDamageable>(), "앙크가 다리를 맞히면 놓칩니다.");
            Assert.IsNotNull(shin.GetComponentInParent<IHostile>(), "차가 다리를 받으면 적으로 안 봅니다.");
        }

        /// <summary>때리면 체력이 줄고, 다 깎으면 죽은 것으로 바뀝니다.</summary>
        [UnityTest]
        public IEnumerator 때리면_체력이_줄고_다_깎으면_죽는다()
        {
            RobotCombatant combatant = MakeRobot(50f);
            yield return null;

            Assert.IsFalse(combatant.IsDead);

            combatant.TakeDamage(20f);
            Assert.IsFalse(combatant.IsDead, "20 을 맞고 죽었습니다.");

            combatant.TakeDamage(40f);
            Assert.IsTrue(combatant.IsDead, "체력을 다 깎았는데 살아 있습니다.");
        }

        /// <summary>
        /// <b>앙크는 매 프레임 때립니다.</b> 그래서 사망 처리가 한 번만 돌아야 합니다.
        /// 여러 번 돌면 쓰러뜨리는 충격이 프레임마다 들어가 로봇이 누운 채 떱니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 죽은_뒤_계속_때려도_사망_처리는_한_번만_돈다()
        {
            RobotCombatant combatant = MakeRobot(10f);

            int died = 0;
            combatant.onDied.AddListener(() => died++);

            yield return null;

            for (int i = 0; i < 5; i++) combatant.TakeDamage(10f);

            Assert.AreEqual(1, died, "사망 처리가 " + died + "번 돌았습니다.");
        }

        /// <summary>죽은 뒤에는 더 이상 피격 이벤트가 나오지 않습니다.</summary>
        [UnityTest]
        public IEnumerator 죽은_뒤에는_피격_이벤트가_나오지_않는다()
        {
            RobotCombatant combatant = MakeRobot(10f);

            int damaged = 0;
            combatant.onDamaged.AddListener(() => damaged++);

            yield return null;

            combatant.TakeDamage(10f);
            Assert.AreEqual(1, damaged);

            combatant.TakeDamage(10f);
            Assert.AreEqual(1, damaged, "죽은 뒤에도 피격이 처리되었습니다.");
        }
    }
}
