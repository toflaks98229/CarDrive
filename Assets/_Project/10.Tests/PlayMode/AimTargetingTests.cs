using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// 조준 대상 판정에 대한 PlayMode 테스트입니다.
    ///
    /// <b>왜 PlayMode 인가.</b> 여기서 확인하려는 것은 레이캐스트 결과와 컴포넌트 생명주기가
    /// 맞물리는 지점입니다. EditMode 에서는 <c>Start</c> 가 돌지 않아 조준 기준 카메라가
    /// 잡히지 않고, 그러면 판정 자체가 성립하지 않습니다.
    ///
    /// <b>무엇을 지키는가.</b> <see cref="PlayerInteractor"/>는 같은 것을 계속 보고 있을 때
    /// 부모 체인 탐색을 건너뛰도록 직전 콜라이더를 기억합니다. 그 캐시가 제때 비워지지 않으면
    /// <b>조준을 옮겨도 옛 대상이 그대로 남습니다</b> — 눈으로는 안내 문구가 바뀌지 않는 것으로만
    /// 보여서, 원인을 찾기가 매우 어려운 종류의 고장입니다. 그래서 여기서 못박습니다.
    /// </summary>
    public class AimTargetingTests
    {
        /// <summary>시험용 상호작용 대상입니다. 무엇을 했는지만 기록합니다.</summary>
        private sealed class FakeInteractable : MonoBehaviour, IInteractable
        {
            public string label = "대상";
            public int interactCount;

            public bool CanInteract() { return true; }
            public string GetInteractionLabel() { return label; }
            public void Interact() { interactCount++; }
        }

        private GameObject cameraObject;
        private GameObject nearObject;
        private GameObject farObject;
        private PlayerInteractor interactor;

        [SetUp]
        public void SetUp()
        {
            GameContext.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in new[] { cameraObject, nearObject, farObject })
            {
                if (go != null) Object.Destroy(go);
            }
            cameraObject = null; nearObject = null; farObject = null; interactor = null;
            GameContext.Clear();
        }

        /// <summary>
        /// 조준을 옮기면 대상도 따라 바뀌어야 합니다.
        ///
        /// 직전 콜라이더 캐시가 무효화되지 않으면 여기서 첫 대상이 그대로 남습니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 조준을_옮기면_대상도_바뀐다()
        {
            yield return BuildRig();

            FakeInteractable near = nearObject.GetComponent<FakeInteractable>();
            FakeInteractable far = farObject.GetComponent<FakeInteractable>();

            // 앞쪽(+Z)을 봅니다.
            LookAlong(Vector3.forward);
            yield return null;
            Assert.AreSame(near, interactor.CurrentInteractable, "앞쪽 대상이 잡혀야 합니다.");

            // 오른쪽(+X)으로 조준을 옮깁니다.
            LookAlong(Vector3.right);
            yield return null;
            Assert.AreSame(far, interactor.CurrentInteractable, "조준을 옮겼는데 대상이 따라오지 않았습니다.");

            // 다시 앞쪽으로 돌아옵니다.
            LookAlong(Vector3.forward);
            yield return null;
            Assert.AreSame(near, interactor.CurrentInteractable, "되돌아왔는데 대상이 복구되지 않았습니다.");
        }

        /// <summary>아무것도 없는 쪽을 보면 대상이 비어야 합니다.</summary>
        [UnityTest]
        public IEnumerator 빈_곳을_보면_대상이_사라진다()
        {
            yield return BuildRig();

            LookAlong(Vector3.forward);
            yield return null;
            Assert.IsNotNull(interactor.CurrentInteractable, "먼저 대상이 잡혀 있어야 합니다.");

            // 뒤쪽(-Z)에는 아무것도 두지 않았습니다.
            LookAlong(Vector3.back);
            yield return null;

            Assert.IsNull(interactor.CurrentInteractable, "빈 곳을 보는데 대상이 남아 있습니다.");
            Assert.IsFalse(interactor.HasTarget);
        }

        /// <summary>같은 것을 계속 봐도 대상이 흔들리지 않아야 합니다. (캐시가 값을 잃지 않는지)</summary>
        [UnityTest]
        public IEnumerator 같은_것을_계속_봐도_대상이_유지된다()
        {
            yield return BuildRig();

            LookAlong(Vector3.forward);
            yield return null;

            FakeInteractable expected = nearObject.GetComponent<FakeInteractable>();

            for (int i = 0; i < 5; i++)
            {
                yield return null;
                Assert.AreSame(expected, interactor.CurrentInteractable, i + "번째 프레임에서 대상을 잃었습니다.");
            }
        }

        // --- Helpers ---

        /// <summary>
        /// 카메라 하나와 상호작용 대상 둘을 세웁니다.
        /// 대상은 원점 기준 앞(+Z)과 오른쪽(+X)에 하나씩 둡니다.
        /// </summary>
        /// <returns>한 프레임 대기. Start 가 돌아 조준 기준이 잡히게 합니다.</returns>
        private IEnumerator BuildRig()
        {
            cameraObject = new GameObject("Aim", typeof(Camera));
            cameraObject.transform.position = Vector3.zero;

            nearObject = BuildTarget("Near", new Vector3(0f, 0f, 2f), "앞");
            farObject = BuildTarget("Far", new Vector3(2f, 0f, 0f), "옆");

            // 방금 옮긴 위치를 물리 쪽에 반영합니다.
            // autoSyncTransforms 가 꺼져 있으면 첫 레이캐스트가 옛 위치를 봅니다.
            Physics.SyncTransforms();

            interactor = cameraObject.AddComponent<PlayerInteractor>();
            interactor.interactionDistance = 5f;
            interactor.interactionLayer = ~0;   // 모든 레이어

            // Start 가 돌아야 조준 기준(카메라)이 잡힙니다.
            yield return null;
        }

        /// <summary>콜라이더와 상호작용 컴포넌트를 가진 대상을 만듭니다.</summary>
        /// <param name="name">오브젝트 이름</param>
        /// <param name="position">놓을 위치</param>
        /// <param name="label">안내 문구</param>
        /// <returns>만들어진 오브젝트</returns>
        private static GameObject BuildTarget(string name, Vector3 position, string label)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.AddComponent<FakeInteractable>().label = label;
            return go;
        }

        /// <summary>카메라를 그 방향으로 돌립니다.</summary>
        /// <param name="direction">바라볼 방향</param>
        private void LookAlong(Vector3 direction)
        {
            cameraObject.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}
