using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CarDrive.Common;
using CarDrive.Gameplay;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 물리(들고 다니기)와 상호작용이 <b>같은 물건 위에서 겹칠 때</b>를 지키는 테스트입니다.
    ///
    /// <b>왜 이것이 예외가 아니라 일상인가.</b> 이 게임에서 들 수 있는 물건은 전부
    /// 상호작용 대상이기도 합니다 — 병·음식·장바구니 모두 좌클릭으로 들고 상호작용 키로 씁니다.
    /// 그래서 "들고 있는 것을 그대로 쓴다"는 상황이 늘 일어납니다.
    ///
    /// 그때 손과 상호작용이 <b>같은 Rigidbody 를 동시에 주인 노릇</b>하면 물리가 무너집니다.
    /// 소비 절차는 물건을 곧바로 꺼 버리는데, 손은 그것을 모른 채 매 물리 프레임마다
    /// 이미 물리 세계에서 빠진 몸에 속도를 밀어 넣습니다. 다 마신 빈 병이 다시 켜질 때는
    /// 손에 붙은 채 되살아나고, 꺼졌다 켜지면서 플레이어와의 충돌 무시까지 풀립니다.
    /// </summary>
    public class CarryInteractionTests
    {
        /// <summary>실제 씬에서 상호작용 레이캐스트가 보는 레이어입니다.</summary>
        private const int InteractableLayer = 6;

        private GameObject player;
        private GameObject footRig;
        private PlayerCarrier carrier;
        private PlayerInteractor interactor;
        private BeverageConsumer consumer;
        private NeedsSystem needs;

        private readonly List<GameObject> spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            GameContext.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (player != null) Object.Destroy(player);
            if (footRig != null) Object.Destroy(footRig);

            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null) Object.Destroy(spawned[i]);
            }
            spawned.Clear();

            player = null; footRig = null; carrier = null;
            interactor = null; consumer = null; needs = null;

            GameContext.Clear();
        }

        /// <summary>들고 있는 병을 마시면 손이 비고 물리 설정이 되돌아와야 합니다.</summary>
        [UnityTest]
        public IEnumerator 들고_있는_병을_마셔도_손과_물리가_정상으로_돌아온다()
        {
            yield return BuildPlayer();

            Beverage bottle = BuildBottle();
            Carryable carryable = bottle.GetComponent<Carryable>();
            Rigidbody body = bottle.GetComponent<Rigidbody>();

            carrier.PickUp(carryable);
            Assert.IsTrue(carrier.IsCarrying, "들고 있어야 합니다.");

            yield return new WaitForFixedUpdate();

            // 들고 있는 채로 상호작용해서 마십니다. 좌클릭과 상호작용 키는 서로 다른 키입니다.
            bottle.Interact();

            yield return new WaitForSeconds(0.6f);

            Assert.IsFalse(carrier.IsCarrying, "마셨으면 손이 비어야 합니다.");
            Assert.IsFalse(carryable.IsHeld, "들린 표식이 남아 있으면 그 물건은 다시 집히지 않습니다.");
            Assert.IsTrue(body.useGravity, "물리 설정이 되돌아와야 합니다.");
        }

        /// <summary>들고 있던 물건이 꺼지면 손에서 놓여야 합니다. (마시기·세이브 복원)</summary>
        [UnityTest]
        public IEnumerator 들고_있던_물건이_꺼지면_손에서_놓인다()
        {
            yield return BuildPlayer();

            Beverage bottle = BuildBottle();
            Carryable carryable = bottle.GetComponent<Carryable>();
            Rigidbody body = bottle.GetComponent<Rigidbody>();

            carrier.PickUp(carryable);
            yield return new WaitForFixedUpdate();

            bottle.gameObject.SetActive(false);
            yield return null;

            Assert.IsFalse(carrier.IsCarrying, "꺼진 물건을 계속 들고 있으면 안 됩니다.");
            Assert.IsFalse(carryable.IsHeld, "들린 표식이 남으면 안 됩니다.");
            Assert.IsTrue(body.useGravity, "물리 설정이 되돌아와야 합니다.");
        }

        /// <summary>들고 있던 물건이 파괴되어도 손이 깨끗하게 비워져야 합니다. (다 꺼낸 봉투)</summary>
        [UnityTest]
        public IEnumerator 들고_있던_물건이_사라져도_손이_깨끗하게_비워진다()
        {
            yield return BuildPlayer();

            Beverage bottle = BuildBottle();
            Carryable carryable = bottle.GetComponent<Carryable>();

            carrier.PickUp(carryable);
            yield return new WaitForFixedUpdate();

            Object.Destroy(carryable.gameObject);
            yield return null;
            yield return null;

            Assert.IsFalse(carrier.IsCarrying, "사라진 물건을 계속 들고 있으면 안 됩니다.");
        }

        /// <summary>탑승해서 도보 리그가 통째로 꺼져도 들고 있던 것이 정상으로 놓여야 합니다.</summary>
        [UnityTest]
        public IEnumerator 들고_탄_뒤에도_물건이_그_자리에_정상으로_놓인다()
        {
            yield return BuildPlayer();

            Beverage bottle = BuildBottle();
            Carryable carryable = bottle.GetComponent<Carryable>();
            Rigidbody body = bottle.GetComponent<Rigidbody>();

            carrier.PickUp(carryable);
            yield return new WaitForFixedUpdate();

            // DrivingState.Enter 가 하는 일입니다. 카메라도 이 아래에 있어 함께 꺼집니다.
            footRig.SetActive(false);
            yield return null;

            Assert.IsFalse(carryable.IsHeld, "리그가 꺼질 때 내려놓아야 합니다.");
            Assert.IsTrue(body.useGravity, "물리 설정이 되돌아와야 합니다.");
        }

        /// <summary>이미 들고 있는데 다른 것을 집으면 앞의 것이 허공에 굳지 않아야 합니다.</summary>
        [UnityTest]
        public IEnumerator 다른_것을_집으면_앞의_것이_허공에_남지_않는다()
        {
            yield return BuildPlayer();

            Beverage first = BuildBottle();
            Beverage second = BuildBottle();

            Carryable firstCarry = first.GetComponent<Carryable>();

            carrier.PickUp(firstCarry);
            yield return new WaitForFixedUpdate();

            carrier.PickUp(second.GetComponent<Carryable>());
            yield return null;

            Assert.IsFalse(firstCarry.IsHeld, "앞의 것이 아직 들려 있습니다.");
            Assert.IsTrue(first.GetComponent<Rigidbody>().useGravity, "앞의 것이 중력 없이 남았습니다.");
        }

        /// <summary>
        /// 다 마신 빈 병을 계속 조준해도 조용해야 합니다.
        ///
        /// 빈 병은 <b>오브젝트와 콜라이더가 그대로 남고 컴포넌트만 사라집니다.</b>
        /// 조준 캐시가 콜라이더로만 판단하면 사라진 컴포넌트를 매 프레임 부르게 되고,
        /// 그 자리에서 MissingReferenceException 이 프레임마다 쏟아집니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 다_마신_병을_계속_조준해도_조용하다()
        {
            yield return BuildPlayer();

            Beverage bottle = BuildBottle();
            bottle.transform.position = player.transform.position + player.transform.forward * 1.5f;

            // 콜라이더의 자리는 물리 프레임에 맞춰집니다. 조준선이 닿으려면 먼저 맞춰야 합니다.
            Physics.SyncTransforms();
            yield return null;
            yield return null;

            Assert.IsNotNull(interactor.CurrentInteractable, "조준에 걸려 있어야 합니다.");

            // 다 마신 병과 같은 상태로 만듭니다. (BeverageConsumer.ThrowEmpty)
            Object.DestroyImmediate(bottle);

            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual("", interactor.GetInteractionPrompt(), "빈 병에는 안내가 없어야 합니다.");
                Assert.IsFalse(interactor.HasTarget, "빈 병은 상호작용 대상이 아닙니다.");
                yield return null;
            }
        }

        /// <summary>
        /// 들고 있는 병을 마신 뒤에도 물리가 폭주하지 않아야 합니다.
        ///
        /// 빈 병은 소비가 끝나면 다시 켜집니다. 그때까지 손이 붙잡고 있으면
        /// 두 주인이 같은 Rigidbody 를 밀고, 플레이어 캡슐과의 충돌 무시도 풀려 있어
        /// 서로를 밀어내며 속도가 발산합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 마신_뒤_빈_병의_속도가_발산하지_않는다()
        {
            yield return BuildPlayer();

            Beverage bottle = BuildBottle();
            Carryable carryable = bottle.GetComponent<Carryable>();
            Rigidbody body = bottle.GetComponent<Rigidbody>();
            GameObject go = bottle.gameObject;

            carrier.PickUp(carryable);
            yield return new WaitForFixedUpdate();

            bottle.Interact();
            yield return new WaitForSeconds(1.0f);

            Assert.IsTrue(go != null, "빈 병은 남아야 합니다.");

            float speed = body.linearVelocity.magnitude;
            Assert.IsFalse(float.IsNaN(speed), "속도가 NaN 이 되면 물리가 무너집니다.");
            Assert.Less(speed, 30f, "빈 병이 손과 몸 사이에서 튕겨 발산했습니다: " + speed);
        }

        // --- Helpers ---

        /// <summary>도보 리그 + 카메라(조준 기준)를 실제 씬과 같은 배치로 세웁니다.</summary>
        /// <returns>Awake·Start 가 돌도록 두 프레임 기다립니다.</returns>
        private IEnumerator BuildPlayer()
        {
            footRig = new GameObject("FootRig", typeof(CharacterController));
            footRig.transform.position = Vector3.zero;

            player = new GameObject("Camera", typeof(Camera));
            player.transform.SetParent(footRig.transform, false);

            needs = player.AddComponent<NeedsSystem>();
            needs.needsEnabled = false;   // 시간에 따라 저절로 차오르면 값 비교가 흔들립니다

            consumer = player.AddComponent<BeverageConsumer>();
            consumer.needsSystem = needs;

            carrier = player.AddComponent<PlayerCarrier>();
            carrier.playerCollider = footRig.GetComponent<CharacterController>();

            interactor = player.AddComponent<PlayerInteractor>();
            interactor.interactionLayer = 1 << InteractableLayer;

            yield return null;
            yield return null;
        }

        /// <summary>실제 병 프리팹과 같은 구성(콜라이더 + Rigidbody + 들 수 있음 + 마실 수 있음)입니다.</summary>
        private Beverage BuildBottle()
        {
            GameObject go = new GameObject("콜라", typeof(BoxCollider), typeof(Rigidbody));
            go.layer = InteractableLayer;
            go.transform.position = new Vector3(0f, 0f, 1.1f);
            go.GetComponent<Rigidbody>().mass = 0.4f;
            spawned.Add(go);

            go.AddComponent<Carryable>();

            Beverage drink = go.AddComponent<Beverage>();
            drink.consumeSeconds = 0.05f;
            return drink;
        }
    }
}
