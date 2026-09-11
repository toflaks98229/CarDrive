using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 로봇에게 <b>무엇을 볼지</b> 알려 줍니다. 포탑과 무장이 여기서 깨어납니다.
    ///
    /// <b>왜 필요한가.</b> <see cref="RobotTurret"/> 은 겨눌 곳을 인스펙터에서 받거나
    /// 코드가 주기를 기다립니다. 그런데 이 저장소에는 <b>그것을 주는 코드가 한 줄도
    /// 없었습니다</b> — 조준도 무장도 다 만들어 놓고 게임에서는 한 번도 돌지 않았습니다.
    ///
    /// <b>두 단으로 나눕니다.</b> 이것이 이 기계의 성격 전부입니다.
    /// <code>
    /// 머리(센서 캡) — 반경 안에 들어오면 <b>늘</b> 쳐다봅니다
    /// 포           — <b>건드렸을 때만</b> 겨누고, 겨눈 뒤에도 잠깐 기다렸다 쏩니다
    /// </code>
    /// `로봇_기획.md` 가 스트라이더를 <b>교통</b>으로, "정해진 길을 정해진 시각에
    /// 걷습니다 · <b>건드리면</b>" 으로 못 박아 두었습니다. 쳐다보기만 하는 기계와
    /// 겨누는 기계는 <b>다른 물건</b>이고, 그 사이를 건너는 것은 플레이어입니다.
    ///
    /// <b>쫓아가지 않습니다.</b> 이 부품은 <see cref="RobotDriver"/> 를 만지지 않습니다.
    /// 반경 밖으로 나가면 잊고 제 자세로 돌아갑니다 — 도망칠 수 있어야 "싸울지 말지를
    /// 플레이어가 고른다" 가 성립합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RobotThreat : MonoBehaviour
    {
        // --- Public Types ---

        /// <summary>지금 어떤 상태인지입니다.</summary>
        public enum Mood
        {
            /// <summary>아무것도 없습니다. 쉬는 자세입니다.</summary>
            Idle = 0,

            /// <summary>보고 있습니다. 머리만 따라갑니다.</summary>
            Watch,

            /// <summary>겨누고 있습니다. 아직 안 쏩니다.</summary>
            Aim,

            /// <summary>쏩니다.</summary>
            Engage,
        }

        // --- Public Member Variables : 배선 ---

        /// <summary>쳐다보는 마디입니다. 보통 센서 캡입니다.</summary>
        [Header("배선")]
        [Tooltip("쳐다보는 마디. 보통 센서 캡")]
        public RobotTurret head;

        /// <summary>겨누는 마디입니다. 건드렸을 때만 씁니다.</summary>
        [Tooltip("겨누는 마디. 건드렸을 때만 씁니다")]
        public RobotTurret gun;

        /// <summary>이 무장들을 켜고 끕니다. 겨누는 동안에는 꺼 둡니다.</summary>
        [Tooltip("켜고 끌 무장들. 겨누는 동안에는 꺼 둡니다")]
        public RobotWeapon[] weapons;

        /// <summary>
        /// 볼 것을 <b>손으로 지정</b>합니다. 비어 있으면 차와 사람을 찾습니다.
        ///
        /// 연출로 세워 둔 장면과 <b>실측</b>이 이것을 씁니다 - 빈 씬에는 차도 사람도
        /// 없어서, 지정할 길이 없으면 이 부품을 재 볼 방법이 없습니다.
        /// </summary>
        [Tooltip("볼 것을 손으로 지정. 비어 있으면 차와 사람을 찾습니다")]
        public Transform authoredTarget;

        // --- Public Member Variables : 규칙 ---

        /// <summary>이 안에 들어와야 쳐다봅니다(m).</summary>
        [Header("규칙")]
        [Tooltip("쳐다보기 시작하는 거리(m)")]
        public float watchRadius = 55f;

        /// <summary>
        /// <b>건드려야만</b> 겨누는지입니다.
        ///
        /// 꺼 두면 반경 안의 것을 보자마자 겨눕니다 — 드레드노트처럼 현장을 지키는
        /// 기계에 씁니다. 스트라이더는 켜 둡니다.
        /// </summary>
        [Tooltip("건드려야만 겨눕니다. 끄면 보자마자 겨눕니다")]
        public bool provokedOnly = true;

        /// <summary>
        /// 겨누고 나서 <b>이만큼 기다렸다</b> 쏩니다(초).
        ///
        /// 이 시간이 <b>경고</b>입니다. 포가 나를 향해 도는 것을 보고 물러날 수 있어야
        /// "싸울지 말지를 플레이어가 고른다" 가 성립합니다. 없애지 마십시오.
        /// </summary>
        [Tooltip("겨누고 나서 쏘기까지(초). 이것이 경고입니다")]
        public float warnSeconds = 2f;

        /// <summary>마지막으로 건드려진 뒤 이만큼 지나면 잊습니다(초).</summary>
        [Tooltip("이만큼 조용하면 잊습니다(초)")]
        public float forgetSeconds = 14f;

        /// <summary>목표를 다시 찾는 간격입니다(초). 매 프레임 찾을 이유가 없습니다.</summary>
        [Tooltip("목표를 다시 찾는 간격(초)")]
        public float scanInterval = 0.4f;

        // --- Public Properties ---

        /// <summary>지금 상태입니다.</summary>
        public Mood State { get; private set; }

        /// <summary>지금 보고 있는 것입니다. 없으면 null 입니다.</summary>
        public Transform Seen { get; private set; }

        // --- Private Member Variables ---

        /// <summary>다음에 목표를 찾을 시각입니다.</summary>
        private float nextScan;

        /// <summary>마지막으로 건드려진 시각입니다. 음수면 아직입니다.</summary>
        private float provokedAt = -1f;

        /// <summary>겨누기 시작한 시각입니다. 음수면 아직입니다.</summary>
        private float aimingSince = -1f;

        /// <summary>부딪힘을 듣는 쪽입니다.</summary>
        private RobotPhysicsMotor motor;

        /// <summary>죽었으면 아무것도 하지 않습니다.</summary>
        private RobotCombatant combatant;

        // --- Public Methods ---

        /// <summary>
        /// <b>건드려졌다</b>고 알립니다. 맞았을 때 · 받혔을 때 불립니다.
        ///
        /// 방향은 받지 않습니다 — 이 기계는 <b>누가</b> 건드렸는지가 아니라
        /// <b>건드려졌다</b>는 것만 알면 됩니다. 겨눌 것은 반경 안에서 다시 찾습니다.
        /// </summary>
        public void Provoke()
        {
            provokedAt = Time.time;
        }

        // --- Unity Event Functions ---

        private void Awake()
        {
            combatant = GetComponentInChildren<RobotCombatant>(true);
            motor = GetComponentInChildren<RobotPhysicsMotor>(true);

            Arm(false);
        }

        private void OnEnable()
        {
            if (motor != null) motor.Struck += OnStruck;
        }

        private void OnDisable()
        {
            if (motor != null) motor.Struck -= OnStruck;
        }

        private void Update()
        {
            if (combatant != null && combatant.IsDead)
            {
                Settle();
                return;
            }

            if (Time.time >= nextScan)
            {
                nextScan = Time.time + Mathf.Max(0.05f, scanInterval);
                Seen = Look();
            }

            if (Seen == null)
            {
                Settle();
                return;
            }

            // 머리는 <b>늘</b> 쳐다봅니다. 건드리지 않아도 이 기계는 나를 봅니다.
            if (head != null) head.AimAt(Seen.position);

            bool angry = !provokedOnly
                         || (provokedAt >= 0f && Time.time - provokedAt <= forgetSeconds);

            if (!angry)
            {
                State = Mood.Watch;
                Rest();
                return;
            }

            if (gun != null) gun.AimAt(Seen.position);

            if (aimingSince < 0f) aimingSince = Time.time;

            bool ready = Time.time - aimingSince >= warnSeconds;
            State = ready ? Mood.Engage : Mood.Aim;
            Arm(ready);
        }

        // --- Private Methods ---

        /// <summary>부딪히면 건드려진 것으로 봅니다.</summary>
        /// <param name="direction">밀린 방향</param>
        /// <param name="velocityChange">그 충격이 만든 속도 변화(m/s)</param>
        private void OnStruck(Vector3 direction, float velocityChange)
        {
            Provoke();
        }

        /// <summary>반경 안에서 볼 것을 고릅니다. 차가 먼저입니다.</summary>
        /// <returns>볼 것. 없으면 null</returns>
        private Transform Look()
        {
            if (authoredTarget != null) return Near(authoredTarget);

            PlayerModeController player = GameContext.Get<PlayerModeController>();

            // 차를 먼저 봅니다 — 이 기계가 사는 곳이 <b>길</b>이기 때문입니다.
            Vehicle prey = Vehicle.GetTargetVehicle(transform.position);
            Transform best = Near(prey != null ? prey.transform : null);

            if (best != null) return best;

            return Near(player != null ? player.PickupAnchor : null);
        }

        /// <summary>반경 안이면 그대로, 아니면 null 입니다.</summary>
        /// <param name="candidate">볼 만한 것</param>
        /// <returns>반경 안이면 <paramref name="candidate"/></returns>
        private Transform Near(Transform candidate)
        {
            if (candidate == null) return null;

            float sqr = (candidate.position - transform.position).sqrMagnitude;
            return sqr <= watchRadius * watchRadius ? candidate : null;
        }

        /// <summary>겨누기를 그만두되 쳐다보기는 이어 갑니다.</summary>
        private void Rest()
        {
            aimingSince = -1f;
            Arm(false);

            if (gun != null) gun.StopAiming();
        }

        /// <summary>아무것도 안 보는 자세로 돌아갑니다.</summary>
        private void Settle()
        {
            State = Mood.Idle;
            Seen = null;
            Rest();

            if (head != null) head.StopAiming();
        }

        /// <summary>무장을 켜거나 끕니다. 꺼져 있으면 <see cref="RobotWeapon.Pose"/> 가 쉬어 갑니다.</summary>
        /// <param name="on">켤지 여부</param>
        private void Arm(bool on)
        {
            if (weapons == null) return;

            foreach (RobotWeapon weapon in weapons)
            {
                if (weapon != null) weapon.enabled = on;
            }
        }

        private void OnValidate()
        {
            watchRadius = Mathf.Max(1f, watchRadius);
            warnSeconds = Mathf.Max(0f, warnSeconds);
            forgetSeconds = Mathf.Max(0.5f, forgetSeconds);
            scanInterval = Mathf.Clamp(scanInterval, 0.05f, 2f);
        }
    }
}
