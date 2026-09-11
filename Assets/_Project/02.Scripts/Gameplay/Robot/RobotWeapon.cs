using UnityEngine;
using UnityEngine.Events;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 무장 하나를 <b>쏘게</b> 합니다. 언제 · 몇 발 · 무엇이 움직이는가를 정합니다.
    ///
    /// <b>왜 필요한가.</b> <see cref="RobotTurret"/> 은 겨누기만 합니다. 이 저장소에
    /// 로봇이 무언가를 쏘는 코드는 <b>한 줄도 없었습니다</b> — 무장 여섯을 달아 놓고도
    /// 전부 장식이었습니다.
    ///
    /// <b>무엇을 쏘는가.</b> 지금은 <b>히트스캔</b>입니다. 총구에서 선을 쏴
    /// <see cref="IDamageable"/> 을 찾습니다. 투사체 체계를 먼저 만들면 이 작업이
    /// 거기에 막히는데, <b>무장이 움직이는 것과 무엇이 날아가는 것은 다른 문제</b>이고
    /// 움직이는 쪽이 먼저 눈에 띕니다.
    ///
    /// <b>포탑을 직접 돌리지 않습니다.</b> 반동으로 포드를 들어 올릴 때는
    /// <see cref="RobotTurret.AddRecoil"/> 로 <b>각속도를 넣습니다.</b> 직접 회전을
    /// 쓰면 같은 프레임에 포탑이 덮어써서 어떤 프레임은 보이고 어떤 프레임은 안 보입니다
    /// — <see cref="IWalkerAttachment"/> 는 부품 사이의 순서를 보장하지 않습니다.
    ///
    /// <b>값은 보이는 것으로 적습니다.</b> 밀리는 거리(m), 간격(초), 각(도).
    /// 힘이나 계수로 적으면 인스펙터에서 조율할 수 없습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RobotWeapon : MonoBehaviour, IWalkerAttachment
    {
        // --- Public Types ---

        /// <summary>쏘는 방식입니다. 무장의 성격이 대부분 여기서 갈립니다.</summary>
        public enum Fire
        {
            /// <summary>한 발씩. 사이가 깁니다. 중포 · 공성포.</summary>
            Single = 0,

            /// <summary>짧게 여러 발을 몰아 쏘고 쉽니다. 연장포.</summary>
            Burst,

            /// <summary>조건이 맞는 동안 계속 쏩니다. 회전포.</summary>
            Continuous,

            /// <summary>덮개를 열고 한 벌을 다 내보낸 뒤 닫습니다. 미사일 랙.</summary>
            Salvo,

            /// <summary>쏘지 않습니다. 견인 갈고리처럼 무장이 아닌 것.</summary>
            None,
        }

        // --- Public Member Variables : 배선 ---

        /// <summary>이 무장이 달린 포탑입니다. 비어 있으면 부모에서 찾습니다.</summary>
        [Header("배선")]
        [Tooltip("이 무장이 달린 포탑. 비어 있으면 부모에서 찾습니다")]
        public RobotTurret turret;

        /// <summary>발사 원점입니다. 비어 있으면 포탑의 총구를 씁니다.</summary>
        [Tooltip("발사 원점. 비어 있으면 포탑의 총구")]
        public Transform muzzle;

        /// <summary>
        /// 밀 조각들입니다. <b>여럿이면 번갈아</b> 부릅니다.
        ///
        /// 연장포의 두 포신이 그렇습니다 — 한 발마다 좌우가 번갈아 물러나야
        /// 두 줄인 것이 읽힙니다. 함께 밀면 그냥 굵은 포신 하나입니다.
        /// </summary>
        [Tooltip("밀 조각들. 여럿이면 한 발마다 번갈아 밀립니다")]
        public WeaponRecoil[] recoils;

        /// <summary>
        /// 한 발마다 <b>같이</b> 밀 조각들입니다. 배율은 각자의 값을 씁니다.
        ///
        /// 공성포의 반동 실린더가 여기 들어갑니다 — 포신과 같은 순간에, 절반만
        /// 접혀야 실린더로 보입니다.
        /// </summary>
        [Tooltip("한 발마다 같이 밀 조각들 (공성포의 실린더 같은 것)")]
        public WeaponRecoil[] alsoRecoil;

        /// <summary>
        /// 도는 조각입니다. <b>있으면 다 돌기 전에는 못 쏩니다.</b>
        ///
        /// 회전포의 예고가 여기서 나옵니다 — 겨눈 뒤 회전수가 오를 때까지 걸리는
        /// 시간이 곧 플레이어가 피할 시간입니다.
        /// </summary>
        [Tooltip("도는 조각. 있으면 다 돌기 전에는 못 쏩니다")]
        public WeaponSpinner spinner;

        /// <summary>
        /// 덮개입니다. <b>있으면 다 열리기 전에는 못 쏩니다.</b>
        ///
        /// 미사일 랙은 되밀리지도 돌지도 않으므로 이것이 유일한 예고입니다.
        /// </summary>
        [Tooltip("덮개. 있으면 다 열리기 전에는 못 쏩니다")]
        public WeaponHatch hatch;

        /// <summary>매달린 갈고리입니다. 쏘는 것과 무관하게 <b>늘 움직입니다.</b></summary>
        [Tooltip("매달린 갈고리. 쏘는 것과 무관하게 늘 움직입니다")]
        public WeaponWinch winch;

        /// <summary>
        /// 총구 섬광입니다. <b>이것이 없으면 쏘는 것이 화면에 안 보입니다.</b>
        ///
        /// 포신이 밀리는 것만으로는 발사인지 무엇인지 알 수 없습니다 - 특히 반동이
        /// 없는 미사일 랙과 밀림이 작은 회전포가 그렇습니다.
        /// </summary>
        [Tooltip("총구 섬광. 없으면 쏘는 것이 화면에 안 보입니다")]
        public WeaponFlash flash;

        // --- Public Member Variables : 소리와 자국 ---

        /// <summary>
        /// 발사음입니다. <b>여럿이면 번갈아</b> 씁니다.
        ///
        /// 같은 소리가 되풀이되면 기계 소리가 아니라 <b>재생 목록</b>으로 들립니다.
        /// 연장포처럼 좌우가 번갈아 밀리는 무장은 소리도 번갈아야 두 줄로 들립니다.
        /// </summary>
        [Header("소리와 자국")]
        [Tooltip("발사음. 여럿이면 번갈아 씁니다")]
        public AudioClip[] fireClips;

        /// <summary>발사음의 크기입니다.</summary>
        [Tooltip("발사음의 크기")]
        [Range(0f, 1f)]
        public float fireVolume = 0.8f;

        /// <summary>맞은 자리에서 낼 소리입니다. 여럿이면 아무거나 고릅니다.</summary>
        [Tooltip("맞은 자리에서 낼 소리")]
        public AudioClip[] impactClips;

        /// <summary>맞은 자리에 띄울 파티클입니다. 없어도 됩니다.</summary>
        [Tooltip("맞은 자리에 띄울 파티클")]
        public ParticleSystem impactEffect;

        // --- Public Member Variables : 쏘는 방식 ---

        /// <summary>쏘는 방식입니다.</summary>
        [Header("쏘는 방식")]
        [Tooltip("쏘는 방식")]
        public Fire fire = Fire.Single;

        /// <summary>발과 발 사이입니다(초).</summary>
        [Tooltip("발과 발 사이(초)")]
        public float interval = 3.2f;

        /// <summary>한 묶음에 몇 발인지입니다. <see cref="Fire.Burst"/> · <see cref="Fire.Salvo"/> 에만 쓰입니다.</summary>
        [Tooltip("한 묶음의 발수 (점사·일제사)")]
        public int roundsPerBurst = 5;

        /// <summary>묶음과 묶음 사이입니다(초).</summary>
        [Tooltip("묶음과 묶음 사이(초)")]
        public float burstCooldown = 1.6f;

        // --- Public Member Variables : 쏠 조건 ---

        /// <summary>이 거리 안에 있어야 쏩니다(m).</summary>
        [Header("쏠 조건")]
        [Tooltip("사거리(m)")]
        public float range = 60f;

        /// <summary>겨눈 방향이 목표와 이만큼 안에 들어야 쏩니다(도).</summary>
        [Tooltip("허용 조준 오차(도)")]
        public float aimTolerance = 2.5f;

        /// <summary>한 발의 피해량입니다.</summary>
        [Tooltip("한 발의 피해량")]
        public float damage = 12f;

        /// <summary>선이 맞을 수 있는 레이어입니다.</summary>
        [Tooltip("맞을 수 있는 레이어")]
        public LayerMask hitMask = ~0;

        /// <summary>
        /// 날아가는 탄입니다. 비워 두면 <b>선으로 쏩니다</b>(히트스캔).
        ///
        /// <b>왜 무기마다 고르게 두는가.</b> 기관포의 탄이 날아가는 것이 보이면
        /// 그것대로 좋겠지만, 초당 여러 발을 쏘는 무기에 전부 물체를 띄우면
        /// 값이 붙습니다. 무엇보다 <b>가까운 거리에서는 날아가는 것이 안 보입니다</b> —
        /// 40 m 를 초속 200 m 로 가면 0.2 초입니다.
        ///
        /// 미사일은 다릅니다. 90 m 를 느리게 가므로 <b>날아가는 동안이 곧 게임</b>입니다.
        /// </summary>
        [Tooltip("날아가는 탄. 비워 두면 선으로 쏩니다")]
        public GameObject projectile;

        // --- Public Member Variables : 반동 ---

        /// <summary>
        /// 한 발마다 포드가 들리는 각입니다(도). 포탑의 스프링이 되돌립니다.
        ///
        /// 0 이면 포드가 안 움직입니다 — 미사일 랙처럼 되밀리지 않는 무장에 씁니다.
        /// </summary>
        [Header("반동")]
        [Tooltip("한 발마다 포드가 들리는 각(도). 0 이면 안 움직입니다")]
        public float podKick = 4.5f;

        /// <summary>
        /// 한 발마다 <b>기계 자신</b>이 뒤로 밀리는 속도입니다(m/s).
        ///
        /// ⚠ <see cref="RobotKnockdown"/> 의 넘어짐 문턱을 <b>넘기지 마십시오.</b>
        /// 제 포에 제가 자빠지면 웃깁니다. 공성포만 쓰는 값입니다.
        /// </summary>
        [Tooltip("한 발마다 기계가 뒤로 밀리는 속도(m/s). 넘어짐 문턱보다 작아야 합니다")]
        public float bodyKick;

        // --- Public Member Variables : 이벤트 ---

        /// <summary>한 발 쏠 때마다 불립니다. 총구 섬광과 소리를 여기에 답니다.</summary>
        [Header("이벤트")]
        [Tooltip("한 발 쏠 때마다. 총구 섬광·소리를 답니다")]
        public UnityEvent onFired;

        // --- Public Properties ---

        /// <summary>지금까지 쏜 발수입니다. 실측이 발사 간격을 이것으로 잽니다.</summary>
        public int ShotCount { get; private set; }

        /// <summary>마지막으로 쏜 시각입니다(<see cref="Time.time"/>).</summary>
        public float LastShotTime { get; private set; }

        /// <summary>지금 쏠 수 있는 상태인지입니다.</summary>
        public bool CanFire { get { return Ready(out _); } }

        // --- Private Member Variables ---

        /// <summary>
        /// 한 프레임으로 인정할 <b>가장 긴 시간</b>입니다(초).
        ///
        /// ⚠ 스프링을 큰 <c>dt</c> 로 한 번에 밀면 <b>튀어 나갑니다.</b> 실측 중
        /// 화면을 한 장 찍느라 프레임이 멎었더니 연장포의 포신이 0.11 m 대신
        /// <b>0.142 m</b> 밀렸고, 회전포는 한 걸음에 최고 회전수에 닿아 가속
        /// 0.65 초가 <b>0.01 초</b>로 나왔습니다.
        ///
        /// 게임에서도 같은 일이 납니다 — 지형이 한 덩어리 올라오거나 창을 옮기면
        /// 프레임이 끊기고, 그때마다 포신이 튀어 나갑니다. 20fps 아래는
        /// <b>느리게 움직이는 것</b>이 튀는 것보다 낫습니다.
        /// </summary>
        private const float MaxStep = 0.05f;

        /// <summary>다음 발을 쏠 수 있는 시각입니다.</summary>
        private float nextShot;

        /// <summary>이번 묶음에서 몇 발 나갔는지입니다.</summary>
        private int firedInBurst;

        /// <summary>다음에 밀 조각의 번호입니다. 번갈아 쓰려고 돌립니다.</summary>
        private int nextRecoil;

        /// <summary>몸을 뒤로 밀 때 쓰는 물리 부품입니다. 없으면 안 밉니다.</summary>
        private RobotPhysicsMotor motor;

        /// <summary>이 로봇의 뿌리입니다. 자기 몸을 쏘지 않으려고 씁니다.</summary>
        private Transform root;

        // --- Public Methods ---

        /// <summary>
        /// 한 프레임분을 진행합니다. <see cref="WalkerRobot"/> 이 몸통을 세운 뒤 불러 줍니다.
        /// </summary>
        /// <param name="dt">시간 간격(초)</param>
        public void Pose(float dt)
        {
            if (dt <= 0f) return;

            // ⚠ <b>꺼진 무장은 쏘면 안 됩니다.</b> <see cref="WalkerRobot"/> 은 부품을
            // <c>includeInactive: true</c> 로 모읍니다 - 프리팹에 무장 여섯이 다
            // 들어 있고 다섯이 꺼져 있는데, 이 줄이 없으면 <b>안 보이는 다섯도
            // 같이 쏩니다.</b> 화면에는 아무것도 안 나오고 피해만 들어갑니다.
            if (!isActiveAndEnabled) return;

            dt = Mathf.Min(dt, MaxStep);

            // 밀린 조각은 <b>쏘지 않아도</b> 되돌아와야 합니다. 발사 판정보다 먼저입니다.
            Advance(recoils, dt);
            Advance(alsoRecoil, dt);

            // 갈고리는 <b>쏘는 것과 무관합니다.</b> 걷는 내내 흔들려야 합니다.
            if (winch != null) winch.Tick(dt);

            // 섬광은 <b>쏜 뒤에도</b> 꺼져야 합니다. 발사 판정보다 먼저입니다.
            if (flash != null) flash.Tick(dt);

            // ⚠ <c>out</c> 을 조건식 안에서 선언하면 안 됩니다. 앞 항이 거짓이면
            // <c>Ready</c> 가 안 불려 <c>point</c> 가 <b>미배정</b>으로 남습니다.
            Vector3 point = Vector3.zero;
            bool want = fire != Fire.None && Ready(out point);

            // 묶음을 쏘는 중이면 목표가 잠깐 어긋나도 <b>준비 상태는 유지</b>합니다.
            // 그러지 않으면 점사 도중에 로터가 서고 덮개가 닫힙니다.
            bool hold = want || firedInBurst > 0;

            if (spinner != null)
            {
                spinner.Demand(hold);
                spinner.Tick(dt);
            }

            if (hatch != null)
            {
                hatch.Demand(hold);
                hatch.Tick(dt);
            }

            if (!want) return;
            if (Time.time < nextShot) return;

            // <b>예고가 끝나야 나갑니다.</b> 도는 중 · 여는 중에는 못 쏩니다.
            if (spinner != null && !spinner.AtSpeed) return;
            if (hatch != null && !hatch.IsOpen) return;

            Shoot(point);
        }

        /// <summary>한 발을 강제로 쏩니다. 실측과 연출이 씁니다.</summary>
        public void FireOnce()
        {
            if (!ResolvePoint(out Vector3 point)) point = Origin + Forward * range;
            Shoot(point);
        }

        // --- Private Methods : 발사 ---

        /// <summary>쏠 조건이 다 맞는지 봅니다.</summary>
        /// <param name="point">겨누고 있는 자리</param>
        /// <returns>쏘아도 되면 참</returns>
        private bool Ready(out Vector3 point)
        {
            point = Vector3.zero;

            if (turret == null || muzzle == null) return false;
            if (!ResolvePoint(out point)) return false;

            Vector3 to = point - Origin;
            if (to.sqrMagnitude > range * range) return false;

            return turret.AimError <= aimTolerance;
        }

        /// <summary>포탑이 겨누고 있는 자리를 받아 옵니다.</summary>
        /// <param name="point">겨누는 자리</param>
        /// <returns>겨눌 것이 있으면 참</returns>
        private bool ResolvePoint(out Vector3 point)
        {
            if (turret != null) return turret.TryGetTarget(out point);

            point = Vector3.zero;
            return false;
        }

        /// <summary>실제로 한 발 나갑니다.</summary>
        /// <param name="point">겨누는 자리</param>
        private void Shoot(Vector3 point)
        {
            ShotCount++;
            LastShotTime = Time.time;

            // <b>번쩍이는 것이 먼저입니다.</b> 이 프레임에 같이 나가야 포신이 밀리는
            // 것과 한 사건으로 보입니다.
            if (flash != null) flash.Pop();

            if (fireClips != null && fireClips.Length > 0)
            {
                AudioClip clip = fireClips[(ShotCount - 1) % fireClips.Length];
                OneShotAudioPool.Play(clip, Origin, fireVolume);
            }

            // 탄이 있으면 날려 보내고, 없으면 지금 이 자리에서 선으로 판정합니다.
            if (projectile != null) Launch();
            else Hit();

            Push();

            if (onFired != null) onFired.Invoke();

            Schedule();
        }

        /// <summary>
        /// 탄을 날려 보냅니다.
        ///
        /// <b>맞는 판정을 여기서 하지 않습니다.</b> 그것이 히트스캔과의 차이 전부입니다 —
        /// 도착할 때까지 아무 일도 일어나지 않으므로, 그 사이에 차를 몰고 나가면
        /// 맞지 않습니다.
        /// </summary>
        private void Launch()
        {
            GameObject shot = PrefabPool.Get(projectile, Origin,
                                             Quaternion.LookRotation(Forward), null);
            if (shot == null) return;

            WeaponMissile flying = shot.GetComponent<WeaponMissile>();

            if (flying == null)
            {
                // ⚠ 조용히 넘어가면 <b>쏜 자리에 탄이 쌓입니다.</b>
                Debug.LogWarning("RobotWeapon: 탄 프리팹에 WeaponMissile 이 없습니다 — " + name);
                if (!PrefabPool.Release(shot)) Destroy(shot);
                return;
            }

            flying.Launch(Origin, Forward, damage, range, hitMask, root);
        }

        /// <summary>선을 쏴 맞은 것에 피해를 줍니다.</summary>
        private void Hit()
        {
            if (damage <= 0f) return;

            // ⚠ <c>QueryTriggerInteraction.Ignore</c> 가 필요합니다. 이 게임의 상호작용
            // 판정과 소리 구역이 전부 트리거라, 켜 두면 <b>포탄이 문 앞에서 멈춥니다.</b>
            if (!Physics.Raycast(Origin, Forward, out RaycastHit hit, range, hitMask,
                                 QueryTriggerInteraction.Ignore))
            {
                return;
            }

            // 자기 몸은 건너뜁니다. 부앙을 많이 내리면 선이 제 다리를 지납니다.
            if (root != null && hit.transform.IsChildOf(root)) return;

            // <b>맞은 자리를 먼저 표시합니다.</b> 벽에 맞아도 자국은 나야 합니다 -
            // 피해를 입는 것만 표시하면 빗나간 탄이 <b>아무 데도 안 간 것</b>이 됩니다.
            Mark(hit);

            IDamageable victim = hit.collider.GetComponentInParent<IDamageable>();
            if (victim == null || victim.IsDead) return;

            victim.TakeDamage(damage);
        }

        /// <summary>맞은 자리에 자국과 소리를 냅니다.</summary>
        /// <param name="hit">맞은 자리</param>
        private void Mark(RaycastHit hit)
        {
            if (impactEffect != null)
            {
                PooledParticleEffect.Spawn(impactEffect, hit.point,
                                           Quaternion.LookRotation(hit.normal));
            }

            if (impactClips == null || impactClips.Length == 0) return;

            AudioClip clip = impactClips[Random.Range(0, impactClips.Length)];
            OneShotAudioPool.Play(clip, hit.point, fireVolume);
        }

        /// <summary>포드와 몸을 뒤로 밉니다.</summary>
        private void Push()
        {
            // 밀 조각이 여럿이면 <b>번갈아</b>입니다. 하나면 늘 그것입니다.
            if (recoils != null && recoils.Length > 0)
            {
                WeaponRecoil one = recoils[nextRecoil % recoils.Length];
                nextRecoil++;

                if (one != null) one.Kick();
            }

            if (alsoRecoil != null)
            {
                foreach (WeaponRecoil extra in alsoRecoil)
                {
                    if (extra != null) extra.Kick();
                }
            }

            if (turret != null && !Mathf.Approximately(podKick, 0f)) turret.AddRecoil(podKick);

            if (motor != null && bodyKick > 0f) motor.AddVelocityChange(-Forward * bodyKick);
        }

        /// <summary>다음 발이 언제인지 정합니다. 방식마다 다릅니다.</summary>
        private void Schedule()
        {
            if (fire == Fire.Single || fire == Fire.Continuous)
            {
                nextShot = Time.time + interval;
                return;
            }

            firedInBurst++;

            if (firedInBurst < Mathf.Max(1, roundsPerBurst))
            {
                nextShot = Time.time + interval;
                return;
            }

            firedInBurst = 0;
            nextShot = Time.time + burstCooldown;
        }

        /// <summary>밀린 조각들을 한 프레임분 되돌립니다.</summary>
        /// <param name="list">되돌릴 조각들</param>
        /// <param name="dt">시간 간격(초)</param>
        private static void Advance(WeaponRecoil[] list, float dt)
        {
            if (list == null) return;

            foreach (WeaponRecoil one in list)
            {
                if (one != null) one.Tick(dt);
            }
        }

        // --- Private Properties ---

        /// <summary>발사 원점입니다.</summary>
        private Vector3 Origin { get { return muzzle != null ? muzzle.position : transform.position; } }

        /// <summary>총구가 보는 방향입니다.</summary>
        private Vector3 Forward { get { return muzzle != null ? muzzle.forward : transform.forward; } }

        // --- Unity Event Functions ---

        private void Awake()
        {
            if (turret == null) turret = GetComponentInParent<RobotTurret>();
            if (muzzle == null && turret != null) muzzle = turret.muzzle;

            motor = GetComponentInParent<RobotPhysicsMotor>();
            root = transform.root;

            if (turret == null || muzzle == null)
            {
                GameLog.Error(GameLog.Channel.Enemy,
                    name + ": 포탑과 총구가 있어야 쏩니다. 이 무장은 쉬어 갑니다.", this);
                fire = Fire.None;
            }
        }

        private void OnValidate()
        {
            interval = Mathf.Max(0.02f, interval);
            burstCooldown = Mathf.Max(0f, burstCooldown);
            roundsPerBurst = Mathf.Max(1, roundsPerBurst);
            range = Mathf.Max(1f, range);
            aimTolerance = Mathf.Clamp(aimTolerance, 0.1f, 90f);
            damage = Mathf.Max(0f, damage);
            bodyKick = Mathf.Max(0f, bodyKick);
        }
    }
}
