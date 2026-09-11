using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 선회 · 부앙 두 마디를 <b>몸통과 따로</b> 겨눕니다. 포탑에도 센서 캡에도 같은 것이 붙습니다.
    ///
    /// <b>두 마디로 나눈 이유.</b> 하나의 <see cref="Quaternion.LookRotation"/> 으로 돌리면
    /// 포탑이 <b>기울어집니다</b> — 실제 포탑은 수직축으로 돌고 그 위에서 위아래로만 젖힙니다.
    /// 축을 나눠 각도를 따로 풀어야 그 구속이 지켜지고, 각 축의 한계도 뜻을 갖습니다.
    ///
    /// <b>각도로 풀고 각도로 제한합니다.</b> 방향 벡터를 부모 공간으로 옮긴 뒤 선회각과 부앙각을
    /// 삼각함수로 뽑습니다. 그래야 "좌우 120°, 위 60° 아래 25°" 같은 값이 그대로 규약이 됩니다.
    ///
    /// <b>스스로 돌지 않습니다.</b> <see cref="IWalkerAttachment"/> 를 통해
    /// <see cref="WalkerRobot"/> 이 몸통을 세운 뒤 불러 줍니다. 자기 콜백에서 돌면 몸통보다
    /// 먼저 도는 프레임이 생겨 총구가 떱니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RobotTurret : MonoBehaviour, IWalkerAttachment
    {
        // --- Public Member Variables : 배선 ---

        /// <summary>수직축으로 도는 마디입니다. 로컬 <see cref="yawAxis"/> 를 중심으로 돕니다.</summary>
        [Header("배선")]
        [Tooltip("선회 마디. 로컬 yawAxis 를 중심으로 돕니다")]
        public Transform yawNode;

        /// <summary>위아래로 젖히는 마디입니다. 선회 마디의 자식이어야 합니다.</summary>
        [Tooltip("부앙 마디. 선회 마디의 자식이어야 합니다")]
        public Transform pitchNode;

        /// <summary>겨눌 곳입니다. 비어 있으면 <see cref="AimAt"/> 로 준 자리를 씁니다.</summary>
        [Tooltip("겨눌 대상. 비어 있으면 코드로 준 자리를 씁니다")]
        public Transform target;

        /// <summary>총구입니다. 조준 방향의 기준점이고, 발사 원점으로도 씁니다.</summary>
        [Tooltip("총구. 조준 기준점이자 발사 원점")]
        public Transform muzzle;

        // --- Public Member Variables : 축과 한계 ---

        /// <summary>선회축입니다. 보통 몸통의 위쪽(로컬 +Y)입니다.</summary>
        [Header("축과 한계")]
        [Tooltip("선회축 (마디의 로컬 좌표)")]
        public Vector3 yawAxis = Vector3.up;

        /// <summary>부앙축입니다. 보통 로컬 +X 입니다.</summary>
        [Tooltip("부앙축 (마디의 로컬 좌표)")]
        public Vector3 pitchAxis = Vector3.right;

        /// <summary>선회 한계입니다. x 가 왼쪽, y 가 오른쪽(도)입니다.</summary>
        [Tooltip("선회 한계 (도). x=왼쪽 한계, y=오른쪽 한계")]
        public Vector2 yawRange = new Vector2(-120f, 120f);

        /// <summary>부앙 한계입니다. x 가 아래, y 가 위(도)입니다.</summary>
        [Tooltip("부앙 한계 (도). x=아래 한계, y=위 한계")]
        public Vector2 pitchRange = new Vector2(-25f, 60f);

        // --- Public Member Variables : 움직임 ---

        /// <summary>
        /// 겨누는 속도입니다. 진동수가 곧 <b>따라붙는 빠르기</b>입니다.
        ///
        /// 감쇠비를 1보다 낮추면 목표를 지나쳤다 돌아옵니다 — 무거운 포탑이 관성으로 넘어가는
        /// 모양입니다. 다만 너무 낮추면 겨눈 채로 계속 떨어 조준이 안 됩니다.
        /// </summary>
        [Header("움직임")]
        [Tooltip("겨누는 속도와 성격")]
        public SecondOrderSettings spring = new SecondOrderSettings(2.2f, 0.85f, 0f);

        /// <summary>겨눌 것이 없을 때 돌아갈 자세입니다. 도 단위로 선회·부앙입니다.</summary>
        [Tooltip("겨눌 것이 없을 때의 자세 (선회, 부앙). 도 단위")]
        public Vector2 restPose = Vector2.zero;

        // --- Private Member Variables ---

        /// <summary>선회각을 따라가는 2차 시스템입니다.</summary>
        private SecondOrderDynamics yawMotion;

        /// <summary>부앙각을 따라가는 2차 시스템입니다.</summary>
        private SecondOrderDynamics pitchMotion;

        /// <summary>코드로 준 목표 자리입니다.</summary>
        private Vector3 aimPoint;

        /// <summary>코드로 준 목표가 있는지입니다.</summary>
        private bool hasAimPoint;

        /// <summary>배선이 온전한지입니다.</summary>
        private bool ready;

        /// <summary>0으로 보는 제곱 길이입니다.</summary>
        private const float Epsilon = 1e-8f;

        // --- Public Properties ---

        /// <summary>지금 겨누고 있는 선회각(도)입니다.</summary>
        public float Yaw { get { return yawMotion != null ? yawMotion.Value : 0f; } }

        /// <summary>지금 겨누고 있는 부앙각(도)입니다.</summary>
        public float Pitch { get { return pitchMotion != null ? pitchMotion.Value : 0f; } }

        /// <summary>총구가 향하는 방향입니다. 총구가 없으면 부앙 마디의 앞쪽입니다.</summary>
        public Vector3 Forward
        {
            get
            {
                if (muzzle != null) return muzzle.forward;
                return pitchNode != null ? pitchNode.forward : transform.forward;
            }
        }

        /// <summary>총구 자리입니다. 발사 원점으로 씁니다.</summary>
        public Vector3 Origin
        {
            get { return muzzle != null ? muzzle.position : (pitchNode != null ? pitchNode.position : transform.position); }
        }

        /// <summary>
        /// 겨눈 방향이 목표와 이룬 각도(도)입니다. <b>쏘아도 되는지</b>를 이것으로 판단합니다.
        /// 겨눌 것이 없으면 180을 돌려줍니다.
        /// </summary>
        public float AimError
        {
            get
            {
                if (!ResolveTarget(out Vector3 point)) return 180f;

                Vector3 to = point - Origin;
                if (to.sqrMagnitude < Epsilon) return 0f;

                return Vector3.Angle(Forward, to);
            }
        }

        // --- Public Methods ---

        /// <summary>
        /// 반동으로 포탑을 <b>들어 올립니다.</b> 스프링이 알아서 되돌립니다.
        ///
        /// <b>왜 각도가 아니라 여기로 들어오는가.</b> 무장이 <see cref="pitchNode"/> 를
        /// 직접 돌리면 같은 프레임에 <see cref="Pose"/> 가 덮어씁니다.
        /// <see cref="IWalkerAttachment"/> 는 부품 사이의 순서를 보장하지 않으므로
        /// 어떤 프레임은 보이고 어떤 프레임은 안 보입니다 — 그것이 가장 잡기 어려운
        /// 종류의 결함입니다. 속도로 넣으면 <b>순서와 무관하게</b> 결과가 같습니다.
        ///
        /// 겨누는 목표는 그대로이므로, 튀어 오른 포신은 <b>스스로 목표로 돌아옵니다.</b>
        /// 되돌아오는 성격은 <see cref="spring"/> 하나가 정합니다 — 조준과 반동이
        /// 같은 스프링을 쓰는 것이 이 기계의 무게감을 한 벌로 만듭니다.
        /// </summary>
        /// <param name="degrees">들어 올릴 각(도). 음수면 내려갑니다</param>
        public void AddRecoil(float degrees)
        {
            if (pitchMotion == null || Mathf.Approximately(degrees, 0f)) return;

            pitchMotion.AddVelocity(SecondOrderDynamics.ImpulseForPeak(spring, degrees));
        }

        /// <summary>
        /// 지금 겨누고 있는 자리를 알려 줍니다. <b>쏘아도 되는지</b>를 무장이 이것으로 봅니다.
        ///
        /// 목표를 무장이 따로 들고 있으면 포탑과 <b>다른 것을 볼 수</b> 있습니다 —
        /// 겨눈 데 없는 곳으로 쏘는 것이 그렇게 생깁니다.
        /// </summary>
        /// <param name="point">겨누는 자리</param>
        /// <returns>겨눌 것이 있으면 참</returns>
        public bool TryGetTarget(out Vector3 point)
        {
            return ResolveTarget(out point);
        }

        /// <summary>겨눌 자리를 코드로 줍니다. <see cref="target"/> 이 비어 있을 때 쓰입니다.</summary>
        /// <param name="worldPoint">겨눌 월드 좌표</param>
        public void AimAt(Vector3 worldPoint)
        {
            aimPoint = worldPoint;
            hasAimPoint = true;
        }

        /// <summary>겨누기를 그만둡니다. 쉬는 자세로 돌아갑니다.</summary>
        public void StopAiming()
        {
            hasAimPoint = false;
        }

        /// <summary>
        /// 한 프레임분을 진행합니다. <see cref="WalkerRobot"/> 이 몸통을 세운 뒤 불러 줍니다.
        /// </summary>
        /// <param name="dt">시간 간격(초)</param>
        public void Pose(float dt)
        {
            if (!ready || dt <= 0f) return;

            float wantYaw = restPose.x;
            float wantPitch = restPose.y;

            if (ResolveTarget(out Vector3 point)) Solve(point, out wantYaw, out wantPitch);

            wantYaw = Mathf.Clamp(wantYaw, yawRange.x, yawRange.y);
            wantPitch = Mathf.Clamp(wantPitch, pitchRange.x, pitchRange.y);

            yawNode.localRotation = Quaternion.AngleAxis(yawMotion.Update(dt, wantYaw), yawAxis.normalized);

            // <b>부앙은 부호를 뒤집어 넣습니다.</b> 앞이 +Z 인 마디를 +X 축 둘레로 양수만큼
            // 돌리면 오른손 법칙에 따라 총구가 <b>내려갑니다.</b> 그런데 인스펙터의 한계는
            // "아래 -25, 위 60" 처럼 사람이 읽는 뜻이라 위가 양수여야 합니다. 둘을 맞추는
            // 자리가 여기입니다 — 데이터의 축을 음수로 적어 두면 리그를 읽는 사람이 헷갈립니다.
            pitchNode.localRotation = Quaternion.AngleAxis(-pitchMotion.Update(dt, wantPitch), pitchAxis.normalized);
        }

        // --- Unity Methods ---

        private void Awake()
        {
            ready = yawNode != null && pitchNode != null;

            if (!ready)
            {
                GameLog.Error(GameLog.Channel.Enemy,
                    name + ": 선회·부앙 마디가 모두 필요합니다. 이 포탑은 쉬어 갑니다.", this);
                return;
            }

            yawMotion = new SecondOrderDynamics(spring, restPose.x);
            pitchMotion = new SecondOrderDynamics(spring, restPose.y);

            yawNode.localRotation = Quaternion.AngleAxis(restPose.x, yawAxis.normalized);
            pitchNode.localRotation = Quaternion.AngleAxis(-restPose.y, pitchAxis.normalized);
        }

        private void OnValidate()
        {
            if (yawRange.x > yawRange.y) yawRange = new Vector2(yawRange.y, yawRange.x);
            if (pitchRange.x > pitchRange.y) pitchRange = new Vector2(pitchRange.y, pitchRange.x);
        }

        // --- Private Methods ---

        /// <summary>겨눌 자리를 정합니다. 인스펙터의 대상이 코드로 준 자리보다 우선합니다.</summary>
        /// <param name="point">겨눌 월드 좌표</param>
        /// <returns>겨눌 것이 있으면 true</returns>
        private bool ResolveTarget(out Vector3 point)
        {
            if (target != null)
            {
                point = target.position;
                return true;
            }

            point = aimPoint;
            return hasAimPoint;
        }

        /// <summary>
        /// 목표를 <b>선회각과 부앙각으로</b> 풉니다.
        ///
        /// 방향을 선회 마디의 <b>부모 공간</b>으로 옮겨서 풉니다. 그 공간에서 선회축은 고정이고
        /// 몸통이 어떻게 기울어 있든 각도의 뜻이 변하지 않습니다 — 몸통이 걸음마다 흔들리는
        /// 이 기계들에서는 그 점이 중요합니다.
        /// </summary>
        /// <param name="point">겨눌 월드 좌표</param>
        /// <param name="yaw">선회각(도)</param>
        /// <param name="pitch">부앙각(도)</param>
        private void Solve(Vector3 point, out float yaw, out float pitch)
        {
            Transform basis = yawNode.parent != null ? yawNode.parent : yawNode;

            // <b>선회는 피벗에서, 부앙은 총구에서 풉니다.</b> 둘을 갈라야 하는 이유가 있습니다.
            //
            // 총구는 피벗보다 몇 미터 앞이라, 피벗을 목표에 맞추면 총구는 빗나갑니다
            // (스트라이더 기준 40 m 에서 1.5°, 약 1 m). 총구에서 재면 그것이 사라집니다.
            //
            // 그런데 <b>선회까지 총구에서 재면 한계 밖에서 발산합니다.</b> 뒤쪽 목표를
            // 못 따라가는 상태에서 총구가 옆으로 나가면, 요구 선회각이 +180°와 -180°를
            // 넘나들며 클램프가 양끝을 오갑니다. 실제로 뒤쪽 목표에서 선회가 120°에
            // 멈추지 않고 0° 근처로 주저앉았습니다.
            //
            // 선회 시차는 총구가 축 둘레를 도느라 거의 없고(실측에서 같은 값이 나왔습니다),
            // 크게 어긋나는 것은 부앙입니다. 그래서 안정한 쪽은 피벗에, 정확이 필요한 쪽만
            // 총구에 맡깁니다.
            Vector3 fromPivot = basis.InverseTransformDirection(point - yawNode.position);
            Vector3 fromMuzzle = basis.InverseTransformDirection(point - Origin);

            if (fromPivot.sqrMagnitude < Epsilon)
            {
                yaw = restPose.x;
                pitch = restPose.y;
                return;
            }

            Vector3 up = yawAxis.normalized;
            Vector3 side = pitchAxis.normalized;
            Vector3 ahead = Vector3.Cross(side, up).normalized;

            // 선회는 축 둘레의 각입니다. 축 방향 성분을 빼고 남은 평면에서 잽니다.
            Vector3 flat = fromPivot - up * Vector3.Dot(fromPivot, up);
            yaw = flat.sqrMagnitude < Epsilon
                ? restPose.x
                : Mathf.Atan2(Vector3.Dot(flat, side), Vector3.Dot(flat, ahead)) * Mathf.Rad2Deg;

            // 부앙은 그 평면에서 얼마나 들렸는지입니다.
            Vector3 lift = fromMuzzle.sqrMagnitude > Epsilon ? fromMuzzle : fromPivot;
            Vector3 liftFlat = lift - up * Vector3.Dot(lift, up);
            pitch = Mathf.Atan2(Vector3.Dot(lift, up), liftFlat.magnitude) * Mathf.Rad2Deg;
        }
    }
}
