using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Feedbacks;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 플레이어의 앙크 공격을 전담합니다. 입력을 받아 앙크를 꺼내고, 충전하고, 공격 판정을 냅니다.
    ///
    /// 이 컴포넌트는 <see cref="PlayerCameraController"/>와 같은 카메라 GameObject에 붙입니다.
    /// 조준 방향을 카메라 정면에서 읽기 때문입니다.
    ///
    /// 공격은 세 단계로 진행됩니다. 좌클릭을 누르면 앙크를 꺼내고
    /// <see cref="ankhChargeTime"/>만큼 충전한 뒤, 손을 뗄 때까지 매 프레임 구체 캐스트로
    /// <see cref="IDamageable"/>을 찾아 초당 피해를 나눠 넣습니다.
    /// 적의 구체적인 타입은 보지 않으므로 새 적을 추가해도 이 클래스는 고칠 필요가 없습니다.
    /// </summary>
    public class PlayerAttacker : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>앙크가 맞히고 있는 대상에게 초당 넣을 피해량입니다.</summary>
        [Header("앙크 공격 설정")]
        [Tooltip("앙크의 초당 데미지")]
        public float ankhDamagePerSecond = 20f;

        /// <summary>앙크를 꺼낸 뒤 공격 판정이 시작되기까지 걸리는 충전 시간(초)입니다.</summary>
        [Tooltip("앙크 공격이 활성화되기까지의 충전 시간(초)")]
        public float ankhChargeTime = 1.0f;

        /// <summary>앙크 공격이 닿는 최대 사거리(m)입니다.</summary>
        [Tooltip("앙크 공격의 최대 사거리")]
        public float ankhAttackDistance = 10f;

        /// <summary>공격 판정에 쓰는 구체 캐스트의 반경(m)입니다. 클수록 조준이 관대해집니다.</summary>
        [Tooltip("앙크 공격 판정의 반경 (실린더/구체의 굵기)")]
        public float ankhAttackRadius = 0.5f;

        /// <summary>공격 판정이 훑을 레이어입니다. 여기에 없는 레이어는 아예 검사하지 않습니다.</summary>
        [Tooltip("앙크 공격이 감지할 적 레이어")]
        public LayerMask enemyLayer;

        /// <summary>
        /// 조준 광선을 쏠 기준 Transform입니다.
        /// 비워두면 <see cref="PlayerAim.Resolve"/>가 이 오브젝트의 카메라나 메인 카메라를 찾아 줍니다.
        /// </summary>
        [Tooltip("조준 광선을 쏠 기준. 비워두면 이 오브젝트가 카메라인지 확인하고, 아니면 Camera.main을 씁니다.")]
        public Transform aimSource;

        /// <summary>
        /// 한 번의 판정에서 받아 둘 최대 콜라이더 수입니다.
        /// 이 크기만큼 버퍼를 미리 잡아 두므로 공격 중에도 프레임마다 배열을 새로 할당하지 않습니다.
        /// </summary>
        [Tooltip("한 번에 판정할 수 있는 최대 콜라이더 수. 버퍼를 미리 잡아 두므로 " +
                 "공격 중에도 프레임마다 새로 할당하지 않습니다.")]
        public int maxCollidersPerHit = 16;

        /// <summary>
        /// 앙크를 그리는 쪽입니다. 인스펙터에서 <c>AnkhAnimation</c>을 끌어다 놓습니다.
        ///
        /// <b>타입이 왜 MonoBehaviour 인가.</b> 이 필드가 필요한 것은 <see cref="IAnkhView"/>인데,
        /// 유니티는 인터페이스 타입 필드를 인스펙터에 그리지 못합니다. 그렇다고 연출 클래스를
        /// 이름으로 알면 Gameplay 가 UI 를 참조하게 되어 계층 화살표가 거꾸로 납니다.
        ///
        /// 그래서 <b>담는 그릇만 넓히고</b> 실제로 쓰는 것은 아래 <see cref="ankhView"/>입니다.
        /// 잘못된 것을 끼우면 <see cref="Start"/>에서 오류로 알려 줍니다.
        /// </summary>
        [Header("연동 컴포넌트")]
        [Tooltip("앙크의 충전 밝기를 담당하는 컨트롤러입니다. IAnkhView 를 구현해야 합니다. " +
                 "(기본 구현은 AnkhAnimation)")]
        public MonoBehaviour ankhAnimator;

        /// <summary>
        /// 앙크를 꺼내는 연출입니다. 연결하면 <c>AnkhAnimation.ShowAnkh</c> 대신 이쪽이 재생됩니다.
        /// 둘 다 같은 위치를 건드리므로 동시에 쓰면 서로 밀어냅니다.
        /// </summary>
        [Header("연출 대체 (Feel · 선택)")]
        [Tooltip("연결하면 AnkhAnimation.ShowAnkh 대신 이쪽이 재생됩니다. " +
                 "둘 다 같은 위치를 건드리므로 동시에 쓰면 서로 밀어냅니다.")]
        public MMF_Player showFeedback;

        /// <summary>앙크를 내리는 연출입니다. 연결하면 <c>AnkhAnimation.HideAnkh</c> 대신 이쪽이 재생됩니다.</summary>
        [Tooltip("연결하면 AnkhAnimation.HideAnkh 대신 이쪽이 재생됩니다.")]
        public MMF_Player hideFeedback;

        /// <summary>적중 중에 떠는 연출입니다. 연결하면 <c>AnkhAnimation.StartShake</c> 대신 이쪽이 재생됩니다.</summary>
        [Tooltip("연결하면 AnkhAnimation.StartShake 대신 이쪽이 재생됩니다.")]
        public MMF_Player hitFeedback;

        /// <summary>
        /// 물건 들기 컴포넌트입니다. 좌클릭이 들기에 쓰이는 상황이면 앙크를 꺼내지 않습니다.
        /// 비워두면 <see cref="Start"/>에서 같은 오브젝트에서 찾습니다.
        /// </summary>
        [Tooltip("물건 들기 컴포넌트. 좌클릭이 들기에 쓰이는 상황이면 앙크를 꺼내지 않습니다. " +
                 "비워두면 같은 오브젝트에서 찾습니다.")]
        public PlayerCarrier carrier;

        /// <summary>
        /// 앙크 사운드를 재생할 컨트롤러입니다. 비워두면 같은 오브젝트에서 찾으며, 없으면 조용히 넘어갑니다.
        /// </summary>
        [Tooltip("앙크 사운드를 재생할 컨트롤러. 비워두면 같은 오브젝트에서 찾습니다.")]
        public PlayerSoundController soundController;


        // --- Private Member Variables ---

        /// <summary>앙크를 꺼내 들고 있는지 여부입니다.</summary>
        private bool isAnkhHeld = false;

        /// <summary>앙크를 충전한 시간(초)입니다. 충전이 끝나면 발사 루프로 넘어갑니다.</summary>
        private float ankhChargeTimer;

        /// <summary>공격 판정 광선을 쏠 기준 Transform입니다. 보통 메인 카메라입니다.</summary>
        private Transform cameraTransform;

        /// <summary>
        /// 충전이 끝나 발사 루프가 돌고 있는지 여부입니다. 루프 사운드를 한 번만 시작하기 위해 씁니다.
        /// </summary>
        private bool isFiring = false;

        /// <summary>실제로 부리는 앙크 연출입니다. <see cref="ankhAnimator"/>를 계약으로 본 것입니다.</summary>
        private IAnkhView ankhView;

        /// <summary>
        /// 공격 판정용 버퍼입니다. <see cref="maxCollidersPerHit"/> 크기로 미리 잡아 두고 재사용해 GC 압력을 없앱니다.
        /// </summary>
        private RaycastHit[] hitBuffer;

        /// <summary>한 번의 판정에서 이미 때린 대상들입니다. 같은 적을 중복으로 때리지 않게 합니다.</summary>
        private readonly List<IDamageable> hitTargets = new List<IDamageable>();

        // --- Unity Event Functions ---

        /// <summary>
        /// 조준 기준과 판정 버퍼를 마련하고, 비워 둔 참조를 같은 오브젝트에서 찾아 채웁니다.
        /// <see cref="ankhAnimator"/>가 <see cref="IAnkhView"/>가 아니면 여기서 오류로 알려 줍니다.
        /// </summary>
        void Start()
        {
            // "카메라에 붙어 있다"는 가정을 주석이 아니라 코드로 확인합니다.
            cameraTransform = PlayerAim.Resolve(aimSource, this);
            ankhChargeTimer = ankhChargeTime;

            hitBuffer = new RaycastHit[Mathf.Max(1, maxCollidersPerHit)];

            if (carrier == null) carrier = GetComponent<PlayerCarrier>();

            // 사운드는 있으면 쓰고 없으면 조용히 넘어갑니다.
            if (soundController == null) soundController = GetComponent<PlayerSoundController>();

            ankhView = ankhAnimator as IAnkhView;

            if (ankhAnimator == null)
            {
                GameLog.Warn(GameLog.Channel.Player, "PlayerAttacker: AnkhAnimator가 할당되지 않았습니다. 앙크 공격 애니메이션/효과가 작동하지 않습니다.");
            }
            else if (ankhView == null)
            {
                GameLog.Error(GameLog.Channel.Player, "PlayerAttacker: ankhAnimator 에 끼운 " + ankhAnimator.GetType().Name +
                               " 은(는) IAnkhView 를 구현하지 않아 앙크 연출이 동작하지 않습니다.", this);
            }
        }

        /// <summary>
        /// 매 프레임 앙크 입력과 공격 판정을 처리합니다.
        /// 입력이 잠긴 동안에는 들고 있던 앙크를 내리고 아무것도 하지 않습니다.
        /// </summary>
        void Update()
        {
            // 오버레이 버튼을 누르는 클릭이 공격으로 들어가지 않게 합니다.
            //
            // <b>여기는 Suspended를 직접 봅니다.</b> 값이 false가 되는 것만으로는 부족합니다.
            // 앙크를 누르고 있는 도중에 오버레이가 열리면 뗀 순간(AttackReleased)이 영영 오지 않아,
            // 앙크가 켜진 채로 굳고 발사 루프 소리도 계속 납니다. 그래서 직접 내려 줍니다.
            if (GameInput.Suspended)
            {
                if (isAnkhHeld) ReleaseAnkh();
                return;
            }

            HandleAnkhAnimationInput();
            HandleAnkhAttack();
        }

        // --- Private Methods ---

        /// <summary>
        /// 좌클릭 입력을 읽어 앙크를 꺼내거나 내립니다.
        /// 같은 좌클릭을 <see cref="PlayerCarrier"/>가 쓰고 있는 상황이면 앙크를 꺼내지 않습니다.
        /// </summary>
        private void HandleAnkhAnimationInput()
        {
            // 물건을 들고 있는 중이면 앙크를 내립니다. (좌클릭이 내려놓기로 쓰이므로)
            if (isAnkhHeld && carrier != null && carrier.IsCarrying)
            {
                ReleaseAnkh();
                return;
            }

            if (GameInput.AttackPressed)
            {
                // 조준점에 들 수 있는 물건이 있거나 이미 들고 있다면
                // 이번 좌클릭은 PlayerCarrier가 씁니다.
                if (carrier != null && carrier.UsesLeftClick) return;

                isAnkhHeld = true;
                ankhChargeTimer = ankhChargeTime; // 충전 타이머 초기화

                if (showFeedback != null)
                {
                    hideFeedback?.StopFeedbacks();
                    showFeedback.PlayFeedbacks();
                }
                else if (ankhView != null)
                {
                    ankhView.ShowAnkh();
                }

                if (soundController != null) soundController.PlayAnkhCharge();
            }

            if (GameInput.AttackReleased)
            {
                ReleaseAnkh();
            }
        }

        /// <summary>
        /// 앙크를 내리고 충전 상태와 연출, 발사 루프 사운드를 모두 되돌립니다.
        /// 입력이 잠겨 손을 뗀 순간을 놓친 경우에도 여기로 들어와 앙크가 켜진 채 굳는 것을 막습니다.
        /// </summary>
        private void ReleaseAnkh()
        {
            isAnkhHeld = false;

            // 충전 밝기는 언제나 코드가 담당합니다. (연속적인 상태라 피드백에 맞지 않습니다)
            if (ankhView != null) ankhView.SetTargetChargeProgress(0f);

            if (hideFeedback != null)
            {
                showFeedback?.StopFeedbacks();
                hitFeedback?.StopFeedbacks();
                hideFeedback.PlayFeedbacks();
            }
            else if (ankhView != null)
            {
                ankhView.HideAnkh();
                ankhView.StopShake();
            }

            // 발사 중이었다면 루프를 멈춥니다.
            if (isFiring)
            {
                isFiring = false;
                if (soundController != null) soundController.StopAnkhFireLoop();
            }
        }

        /// <summary>
        /// 앙크를 들고 있는 동안의 충전과 공격 판정을 처리합니다.
        ///
        /// 충전이 끝나기 전에는 진행도만 연출에 넘기고 돌아가며, 끝난 뒤에는 매 프레임
        /// 구체 캐스트로 <see cref="IDamageable"/>을 모아 <see cref="ankhDamagePerSecond"/>를
        /// 프레임 시간으로 나눈 만큼 각각에게 넣습니다.
        /// </summary>
        private void HandleAnkhAttack()
        {
            if (!isAnkhHeld || ankhView == null) return;

            // 1. 앙크 충전
            if (ankhChargeTimer > 0)
            {
                ankhChargeTimer -= Time.deltaTime;
                float chargeProgress = Mathf.Clamp01(1.0f - (ankhChargeTimer / ankhChargeTime));
                ankhView.SetTargetChargeProgress(chargeProgress);
                return; // 아직 충전 중
            }

            // 2. 충전 완료 (공격 활성화)
            ankhView.SetTargetChargeProgress(1.0f); // 최대 충전 상태 유지

            // 충전이 막 끝난 순간에만 발사 루프를 시작합니다.
            if (!isFiring)
            {
                isFiring = true;
                if (soundController != null) soundController.StartAnkhFireLoop();
            }

            // 3. 공격 판정
            // NonAlloc은 미리 잡아 둔 버퍼를 채우므로 프레임마다 배열을 새로 만들지 않습니다.
            int hitCount = Physics.SphereCastNonAlloc(
                cameraTransform.position,
                ankhAttackRadius,
                cameraTransform.forward,
                hitBuffer,
                ankhAttackDistance,
                enemyLayer
            );

            // 4. 맞은 대상 모으기
            // 적의 종류를 구분하지 않습니다. IDamageable이면 무엇이든 통합니다.
            // (새 적을 추가할 때 이 메서드를 고칠 필요가 없습니다)
            hitTargets.Clear();
            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = hitBuffer[i].collider;
                if (collider == null) continue;

                // 콜라이더가 자식에 있을 수 있으므로 부모까지 올라가며 찾습니다.
                IDamageable target = collider.GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead) continue;

                // 한 대상이 여러 콜라이더로 잡힐 수 있으므로 중복을 걸러냅니다.
                if (hitTargets.Contains(target)) continue;

                hitTargets.Add(target);
            }

            // 5. 데미지 전달
            if (hitTargets.Count > 0)
            {
                // 적중하는 동안 떨립니다.
                if (hitFeedback != null)
                {
                    if (!hitFeedback.IsPlaying) hitFeedback.PlayFeedbacks();
                }
                else if (ankhView != null)
                {
                    ankhView.StartShake();
                }

                float damageToDeal = ankhDamagePerSecond * Time.deltaTime;
                for (int i = 0; i < hitTargets.Count; i++)
                {
                    hitTargets[i].TakeDamage(damageToDeal);
                }
            }
            else
            {
                if (hitFeedback != null)
                {
                    if (hitFeedback.IsPlaying) hitFeedback.StopFeedbacks();
                }
                else if (ankhView != null)
                {
                    ankhView.StopShake();
                }
            }
        }
    }
}
