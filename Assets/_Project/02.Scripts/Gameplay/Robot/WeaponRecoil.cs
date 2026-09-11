using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 조각 하나를 <b>뒤로 밀었다가 되돌립니다.</b> 반동을 눈에 보이게 하는 부품입니다.
    ///
    /// <b>왜 따로 있는가.</b> 무장마다 미는 조각이 다릅니다 — 중포는 포신 하나,
    /// 연장포는 좌우 포신 둘, 공성포는 포신과 실린더가 <b>다른 거리만큼</b> 밀립니다.
    /// 미는 규칙은 하나이고 대상과 값만 다르므로, 대상마다 이것을 하나씩 답니다.
    ///
    /// <b>왜 스프링인가.</b> 곡선을 손으로 그리면 값 하나를 바꿀 때마다 곡선을 다시
    /// 그려야 합니다. 2차 시스템은 <b>얼마나 밀리는가</b>와 <b>어떤 성격인가</b>를
    /// 따로 잡을 수 있고, 포탑이 겨누는 데 쓰는 것과 같은 풀이라 <b>기계 전체의
    /// 무게감이 한 벌</b>로 맞습니다.
    ///
    /// <b>인스펙터에는 힘이 아니라 거리를 적습니다.</b> "얼마로 때릴까" 는 아무도
    /// 감을 못 잡지만 "0.38 m 밀린다" 는 그림이 그려집니다. 힘으로 바꾸는 것은
    /// <see cref="SecondOrderDynamics.ImpulseForPeak"/> 가 합니다.
    ///
    /// <b>스스로 돌지 않습니다.</b> <see cref="RobotWeapon"/> 이 불러 줍니다 —
    /// 무장이 쏘는 순간과 조각이 밀리는 순간은 같은 프레임이어야 합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponRecoil : MonoBehaviour
    {
        // --- Public Member Variables : 배선 ---

        /// <summary>밀 조각입니다. 비어 있으면 자기 트랜스폼입니다.</summary>
        [Header("배선")]
        [Tooltip("밀 조각. 비어 있으면 이 오브젝트 자신")]
        public Transform part;

        // --- Public Member Variables : 움직임 ---

        /// <summary>
        /// 미는 방향입니다. <b>조각의 부모 기준</b>이고 기본은 뒤(-Z)입니다.
        ///
        /// 리그의 총구 방향이 +Z 이므로 반동은 -Z 입니다. 실린더처럼 다른 방향으로
        /// 접히는 것이 생기면 여기를 바꿉니다.
        /// </summary>
        [Header("움직임")]
        [Tooltip("미는 방향 (조각의 부모 기준). 기본은 뒤")]
        public Vector3 axis = Vector3.back;

        /// <summary>가장 많이 밀렸을 때의 거리(m)입니다.</summary>
        [Tooltip("가장 많이 밀렸을 때의 거리(m)")]
        public float travel = 0.38f;

        /// <summary>
        /// 되돌아오는 성격입니다. 진동수가 <b>빠르기</b>, 감쇠비가 <b>몇 번 넘나드는가</b>입니다.
        ///
        /// 감쇠비를 1 가까이 올리면 밀렸다가 <b>곧게</b> 돌아오고, 낮추면 몇 번
        /// 출렁입니다. 큰 포일수록 낮게 잡아야 무겁게 보입니다.
        /// </summary>
        [Tooltip("되돌아오는 성격. 진동수=빠르기, 감쇠비=출렁임")]
        public SecondOrderSettings spring = new SecondOrderSettings(1.4f, 0.55f, 0f);

        // --- Public Properties ---

        /// <summary>지금 밀려 있는 거리(m)입니다. 0 이면 제자리입니다.</summary>
        public float Offset { get { return motion != null ? motion.Value : 0f; } }

        /// <summary>이번에 밀린 것 중 가장 깊었던 거리(m)입니다. 실측이 이것을 봅니다.</summary>
        public float PeakOffset { get; private set; }

        // --- Private Member Variables ---

        /// <summary>밀린 거리를 따라가는 2차 시스템입니다.</summary>
        private SecondOrderDynamics motion;

        /// <summary>밀기 전의 자리입니다. 여기서 <see cref="axis"/> 만큼 물러납니다.</summary>
        private Vector3 home;

        /// <summary>정규화한 방향입니다. 매 프레임 정규화하지 않으려고 미리 잡아 둡니다.</summary>
        private Vector3 direction;

        /// <summary>배선이 온전한지입니다.</summary>
        private bool ready;

        // --- Public Methods ---

        /// <summary>한 발 쏜 만큼 밀어냅니다.</summary>
        /// <param name="scale">이번 발의 배율. 실린더처럼 절반만 움직이는 것에 씁니다</param>
        public void Kick(float scale = 1f)
        {
            if (!ready) return;

            motion.AddVelocity(SecondOrderDynamics.ImpulseForPeak(spring, travel * scale));
        }

        /// <summary>한 프레임분을 진행합니다. <see cref="RobotWeapon"/> 이 불러 줍니다.</summary>
        /// <param name="dt">시간 간격(초)</param>
        public void Tick(float dt)
        {
            if (!ready || dt <= 0f) return;

            // 목표는 늘 0 입니다 — 밀어 넣은 속도만으로 물러났다 돌아옵니다.
            float value = motion.Update(dt, 0f);

            // ⚠ <b>완충기에는 바닥이 있습니다.</b> 이것이 없으면 연발에서 밀림이
            // <b>쌓입니다</b> - 연장포는 0.14 초마다 쏘고 포신의 고유주기가 0.28 초라,
            // 두 번째 발이 정확히 같은 위상에 들어와 0.11 m 가 <b>0.142 m</b> 가
            // 되었습니다. 그러면 인스펙터에 적은 숫자가 거짓말이 됩니다.
            //
            // 실제 포도 끝까지 가면 멈춥니다. 부딪히면 속도도 함께 죽습니다.
            if (value > travel)
            {
                motion.Reset(travel);
                value = travel;
            }

            if (value > PeakOffset) PeakOffset = value;

            part.localPosition = home + direction * value;
        }

        /// <summary>가장 깊었던 거리를 지웁니다. 실측이 한 발씩 재려고 씁니다.</summary>
        public void ClearPeak()
        {
            PeakOffset = 0f;
        }

        // --- Unity Event Functions ---

        private void Awake()
        {
            if (part == null) part = transform;

            direction = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.back;
            home = part.localPosition;
            motion = new SecondOrderDynamics(spring, 0f);
            ready = true;
        }

        private void OnValidate()
        {
            travel = Mathf.Max(0f, travel);

            // ⚠ 감쇠비가 1 이상이면 <b>넘나들지 않아</b> 밀리는 깊이가 적어 둔 값보다
            // 얕아집니다. 그 자체는 고장이 아니지만, 인스펙터의 숫자와 화면이 달라져
            // 실측이 늘 어긋납니다. 되돌아오는 성격을 바꾸고 싶으면 진동수를 만지십시오.
            spring.damping = Mathf.Clamp(spring.damping, 0.05f, 0.99f);
            spring.frequency = Mathf.Max(0.05f, spring.frequency);
        }
    }
}
