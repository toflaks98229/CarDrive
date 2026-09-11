using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 조각 하나를 <b>돌립니다.</b> 회전포의 포신 뭉치가 이것으로 돕니다.
    ///
    /// <b>가속이 이 무장의 성격입니다.</b> 겨누고 나서 회전수가 오를 때까지
    /// 0.65 초가 걸리고, 그동안은 못 쏩니다. 그 <b>0.65 초가 예고</b>가 되어
    /// 플레이어에게 피할 시간을 줍니다 — 다른 무장에는 없는 것이라
    /// 회전포를 게임 안에서 다르게 만듭니다. <b>줄이지 마십시오.</b>
    ///
    /// <b>관성으로 멈춥니다.</b> 놓자마자 서면 모터가 아니라 스위치로 보입니다.
    ///
    /// ⚠ 조각의 <b>원점이 회전축 위</b>에 있어야 합니다. 어긋나면 포신이 원뿔을
    /// 그립니다. <c>build_strider_guns.py</c> 가 로터를 축 대칭으로 짜므로
    /// 바운딩 박스 중심이 곧 축입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponSpinner : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>돌릴 조각입니다. 비어 있으면 자기 트랜스폼입니다.</summary>
        [Header("배선")]
        [Tooltip("돌릴 조각. 비어 있으면 이 오브젝트 자신")]
        public Transform part;

        /// <summary>회전축입니다. 조각의 로컬 좌표이고, 기본은 총구 방향(+Z)입니다.</summary>
        [Header("움직임")]
        [Tooltip("회전축 (조각의 로컬 좌표). 기본은 총구 방향")]
        public Vector3 axis = Vector3.forward;

        /// <summary>다 돌았을 때의 회전 속도입니다(도/초).</summary>
        [Tooltip("다 돌았을 때의 속도(도/초)")]
        public float speed = 900f;

        /// <summary>정지에서 다 돌기까지 걸리는 시간입니다(초).</summary>
        [Tooltip("정지 → 최고 속도(초)")]
        public float spinUp = 0.65f;

        /// <summary>놓고 나서 멈추기까지 걸리는 시간입니다(초).</summary>
        [Tooltip("놓고 나서 멈추기까지(초)")]
        public float spinDown = 1.4f;

        /// <summary>이 비율을 넘으면 쏠 수 있는 것으로 봅니다.</summary>
        [Tooltip("이 비율을 넘으면 쏠 수 있습니다")]
        [Range(0.5f, 1f)]
        public float readyFraction = 0.95f;

        // --- Public Properties ---

        /// <summary>지금 회전 속도입니다(도/초).</summary>
        public float Speed { get { return current; } }

        /// <summary>쏘아도 될 만큼 돌았는지입니다.</summary>
        public bool AtSpeed { get { return current >= speed * readyFraction; } }

        /// <summary>정지에서 여기까지 오는 데 걸린 시간입니다(초). 실측이 봅니다.</summary>
        public float SpinUpElapsed { get; private set; }

        // --- Private Member Variables ---

        /// <summary>지금 회전 속도입니다(도/초).</summary>
        private float current;

        /// <summary>돌라는 요구가 들어와 있는지입니다.</summary>
        private bool wanted;

        /// <summary>지금까지 돈 각입니다. 누적해야 이어져 보입니다.</summary>
        private float angle;

        /// <summary>정지 상태에서 요구가 들어온 뒤 흐른 시간입니다.</summary>
        private float rising;

        /// <summary>이번 가속이 <b>정지에서</b> 시작했는지입니다.</summary>
        private bool fromRest = true;

        // --- Public Methods ---

        /// <summary>돌지 말지를 알립니다. 매 프레임 불러도 됩니다.</summary>
        /// <param name="spin">돌아야 하면 참</param>
        public void Demand(bool spin)
        {
            if (spin && !wanted)
            {
                rising = 0f;

                // ⚠ 이미 반쯤 돌고 있으면 <b>가속 시간이 짧게 나옵니다.</b> 그것은
                // 고장이 아니라 사실이지만, 그 값을 "가속 0.65 초" 와 견주면 안 됩니다.
                // 실측 중 조준이 잠깐 흔들려 요구가 끊겼다 이어졌을 때 관성으로
                // 돌고 있던 로터가 한 걸음에 회전수에 닿아 <b>0.01 초</b>로 나왔습니다.
                fromRest = current <= speed * 0.02f;
            }

            wanted = spin;
        }

        /// <summary>한 프레임분을 진행합니다. <see cref="RobotWeapon"/> 이 불러 줍니다.</summary>
        /// <param name="dt">시간 간격(초)</param>
        public void Tick(float dt)
        {
            if (part == null || dt <= 0f) return;

            // 가속·감속을 <b>시간</b>으로 적습니다. 각가속도로 적으면 최고 속도를
            // 바꿀 때마다 "몇 초 걸리나" 가 같이 변해 따로 조율할 수 없습니다.
            float rate = wanted
                ? speed / Mathf.Max(spinUp, 0.01f)
                : -speed / Mathf.Max(spinDown, 0.01f);

            float before = current;
            current = Mathf.Clamp(current + rate * dt, 0f, speed);

            if (wanted && before < speed * readyFraction)
            {
                rising += dt;

                if (current >= speed * readyFraction && fromRest && SpinUpElapsed <= 0f)
                {
                    SpinUpElapsed = rising;
                }
            }

            if (current <= 0f) return;

            angle = Mathf.Repeat(angle + current * dt, 360f);
            part.localRotation = Quaternion.AngleAxis(angle, Axis());
        }

        // --- Private Methods ---

        /// <summary>정규화한 회전축입니다.</summary>
        private Vector3 Axis()
        {
            return axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.forward;
        }

        // --- Unity Event Functions ---

        private void Awake()
        {
            if (part == null) part = transform;
        }

        private void OnValidate()
        {
            speed = Mathf.Max(0f, speed);
            spinUp = Mathf.Max(0.01f, spinUp);
            spinDown = Mathf.Max(0.01f, spinDown);
        }
    }
}
