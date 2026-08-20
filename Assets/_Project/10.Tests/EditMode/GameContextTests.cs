using NUnit.Framework;
using UnityEngine;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// <see cref="GameContext"/> 레지스트리에 대한 EditMode 테스트입니다.
    ///
    /// <b>등록을 테스트가 직접 합니다.</b> EditMode 에서는 MonoBehaviour 의 Awake 가
    /// 돌지 않으므로, 컴포넌트를 붙였다고 저절로 등록되지 않습니다.
    /// 어차피 이 클래스가 검사하는 것은 레지스트리이지 남의 생명주기가 아니므로,
    /// 직접 등록하는 편이 검사 대상도 분명해집니다.
    ///
    /// 이 테스트가 존재한다는 것 자체가 3순위 작업의 성과입니다.
    /// 예전에는 협력자를 <c>FindAnyObjectByType</c>으로 찾았기 때문에
    /// <b>씬 없이는 아무것도 조립할 수 없었습니다.</b>
    /// </summary>
    public class GameContextTests
    {
        /// <summary>테스트가 만든 오브젝트입니다. 끝나면 지웁니다.</summary>
        private GameObject a;
        private GameObject b;

        [SetUp]
        public void SetUp()
        {
            GameContext.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (a != null) Object.DestroyImmediate(a);
            if (b != null) Object.DestroyImmediate(b);
            a = null;
            b = null;
            GameContext.Clear();
        }

        /// <summary>등록한 것을 그대로 돌려주어야 합니다.</summary>
        [Test]
        public void 등록한_것을_찾을_수_있다()
        {
            a = new GameObject("A");
            PlayerHealth health = a.AddComponent<PlayerHealth>();
            GameContext.Register(health);

            Assert.AreSame(health, GameContext.Get<PlayerHealth>());
        }

        /// <summary>등록되지 않은 타입은 조용히 null이어야 합니다. (Get은 씬을 뒤지지 않습니다)</summary>
        [Test]
        public void 등록되지_않으면_null_이다()
        {
            Assert.IsNull(GameContext.Get<PlayerHealth>());
        }

        /// <summary>
        /// 같은 타입이 둘이면 나중 것은 거부되어야 합니다.
        /// 거부를 받은 쪽이 스스로를 끄는 것이 이 프로젝트의 규칙입니다.
        /// </summary>
        [Test]
        public void 같은_타입이_둘이면_나중_것을_거부한다()
        {
            a = new GameObject("A");
            PlayerHealth first = a.AddComponent<PlayerHealth>();

            b = new GameObject("B");
            PlayerHealth second = b.AddComponent<PlayerHealth>();

            Assert.IsTrue(GameContext.Register(first), "첫 번째 등록이 거부되었습니다.");
            Assert.IsFalse(GameContext.Register(second), "두 번째 등록이 거부되지 않았습니다.");

            Assert.AreSame(first, GameContext.Get<PlayerHealth>());
            Assert.AreNotSame(second, GameContext.Get<PlayerHealth>());
        }

        /// <summary>같은 인스턴스를 두 번 등록하는 것은 문제가 아닙니다.</summary>
        [Test]
        public void 같은_인스턴스의_재등록은_허용된다()
        {
            a = new GameObject("A");
            PlayerHealth health = a.AddComponent<PlayerHealth>();

            Assert.IsTrue(GameContext.Register(health));
        }

        /// <summary>
        /// 파괴된 것을 계속 붙들고 있으면 안 됩니다.
        /// Unity의 파괴된 오브젝트는 == null 로만 걸러지므로 표에서도 지워야 합니다.
        /// </summary>
        [Test]
        public void 파괴된_것은_돌려주지_않는다()
        {
            a = new GameObject("A");
            GameContext.Register(a.AddComponent<PlayerHealth>());

            Object.DestroyImmediate(a);
            a = null;

            Assert.IsNull(GameContext.Get<PlayerHealth>());
        }

        /// <summary>등록을 해제하면 더 이상 찾히지 않아야 합니다.</summary>
        [Test]
        public void 해제하면_찾히지_않는다()
        {
            a = new GameObject("A");
            PlayerHealth health = a.AddComponent<PlayerHealth>();
            GameContext.Register(health);

            GameContext.Unregister(health);

            Assert.IsNull(GameContext.Get<PlayerHealth>());
        }

        /// <summary>
        /// 남의 것을 대신 해제해서는 안 됩니다.
        /// 도보 리그가 여럿 오갈 때 엉뚱한 해제가 일어나면 찾기 어려운 버그가 됩니다.
        /// </summary>
        [Test]
        public void 등록되지_않은_것으로는_해제되지_않는다()
        {
            a = new GameObject("A");
            PlayerHealth registered = a.AddComponent<PlayerHealth>();
            GameContext.Register(registered);

            b = new GameObject("B");
            PlayerHealth stranger = b.AddComponent<PlayerHealth>();

            GameContext.Unregister(stranger);

            Assert.AreSame(registered, GameContext.Get<PlayerHealth>());
        }
    }
}
