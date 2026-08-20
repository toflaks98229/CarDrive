using System.Collections.Generic;
using UnityEngine;

namespace CarDrive.Systems
{
    /// <summary>
    /// 이 오브젝트가 지나가는 자리의 풀을 눕힙니다.
    ///
    /// 차 바퀴, 플레이어, 유령처럼 <b>땅을 밟고 지나가는 것</b>에 붙입니다.
    /// 붙이기만 하면 됩니다. 스스로 목록에 이름을 올리고, <see cref="GrassPushField"/>가
    /// 매 프레임 그 목록을 모아 풀 셰이더에 넘깁니다.
    ///
    /// 차에도 붙이는 이유가 하나 더 있습니다.
    /// 풀은 지형에 심긴 것이라 차가 그 위에 올라서면 <b>차 바닥을 뚫고 실내로 들어옵니다.</b>
    /// 운전 중에는 카메라가 실내에 있어서 이게 그대로 보입니다.
    /// 차 발밑의 풀을 눕혀 두면 그 문제가 함께 사라집니다.
    /// 그래서 차에는 바퀴뿐 아니라 <b>차체 한가운데에도</b> 넉넉한 반경으로 하나 붙입니다.
    ///
    /// <b>편집 중에도 목록에 오릅니다.</b> (ExecuteAlways)
    /// 그렇지 않으면 플레이를 눌러야만 눌린 모습을 볼 수 있어, 반경을 맞추기가 어렵습니다.
    /// 확인 도구도 편집 모드에서 도는데, 목록이 비어 있으면 아무것도 재지 못합니다.
    /// 매 프레임 도는 코드는 없으므로 편집 중에 붙어 있어도 부담이 없습니다.
    /// </summary>
    [ExecuteAlways]
    public class GrassPusher : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>풀을 눕힐 반경(m)입니다.</summary>
        [Header("누르는 범위")]
        [Tooltip("풀을 눕힐 반경(m). 안쪽 절반은 완전히 눕고 바깥으로 갈수록 서서히 일어섭니다.")]
        public float radius = 1f;

        /// <summary>지나간 자리에 자국을 남길지 여부입니다.</summary>
        [Header("자국")]
        [Tooltip("켜면 지나간 길에 눌린 자국이 남아 천천히 일어섭니다. " +
                 "끄면 겹쳐 있는 동안만 눕고 지나가면 바로 일어섭니다. " +
                 "차는 바퀴만 켜고 차체는 꺼야 합니다. 차체까지 켜면 차 폭만큼 넓은 띠가 남아 " +
                 "바퀴 자국이 아니라 불도저가 지나간 자리처럼 보입니다.")]
        public bool leavesMark = false;

        /// <summary>무게(kg)입니다. 무거울수록 자국이 오래 남습니다.</summary>
        [Tooltip("무게(kg). 무거울수록 자국이 오래 남습니다. " +
                 "0으로 두면 부모의 Rigidbody에서 읽어 옵니다.")]
        public float mass = 0f;

        /// <summary>무게 1kg당 자국이 남는 시간(초)입니다.</summary>
        [Tooltip("무게 1kg당 자국이 남는 시간(초).")]
        public float secondsPerKilogram = 0.05f;

        /// <summary>자국이 남는 시간의 아래/위 한계(초)입니다.</summary>
        [Tooltip("자국이 남는 시간의 아래/위 한계(초).")]
        public Vector2 markSecondsRange = new Vector2(3f, 60f);

        // 위아래로 얼마나 닿는지는 여기가 아니라 풀 머티리얼의 _PushHeightReach 에 있습니다.
        // 누르는 쪽마다 다를 이유가 없고, 한 곳에서 만지는 편이 낫습니다.

        // --- Public Properties ---

        /// <summary>지금 씬에 있는 모든 누르는 것들입니다.</summary>
        public static IReadOnlyList<GrassPusher> All { get { return all; } }

        /// <summary>
        /// 이 자국이 사라지기까지 걸리는 시간(초)입니다.
        ///
        /// 무게에서 계산합니다. 사람은 몇 초면 풀이 다시 서지만, 차바퀴가 지나간 자리는
        /// 한참 남아 있습니다. 그 차이가 <b>무게로 자연히 나오게</b> 했습니다.
        /// </summary>
        public float MarkSeconds
        {
            get
            {
                float kilograms = ResolveMass();
                return Mathf.Clamp(kilograms * secondsPerKilogram,
                                   markSecondsRange.x, markSecondsRange.y);
            }
        }

        // --- Private Member Variables ---

        /// <summary>등록된 목록입니다.</summary>
        private static readonly List<GrassPusher> all = new List<GrassPusher>();

        /// <summary>지난 프레임의 자리입니다. 빠르게 지나가도 자국이 끊기지 않게 이어 붙입니다.</summary>
        private Vector3 previousPosition;

        /// <summary>아직 한 번도 자리를 기록하지 않았는지 여부입니다.</summary>
        private bool hasPrevious;

        // --- Unity Event Functions ---

        /// <summary>목록에 자기를 올립니다.</summary>
        void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);

            hasPrevious = false;
        }

        /// <summary>목록에서 자기를 뺍니다.</summary>
        void OnDisable()
        {
            all.Remove(this);
        }

        // --- Public Methods ---

        /// <summary>
        /// 지난 프레임과 지금 사이의 <b>선분</b>을 돌려줍니다.
        ///
        /// 점 하나만 찍으면 빠르게 달릴 때 자국이 점선으로 끊깁니다.
        /// 시속 60km면 한 프레임에 28cm를 가는데, 바퀴 자국 반경이 그보다 작으면 사이가 빕니다.
        /// 그래서 지나온 자리를 선으로 이어 찍습니다.
        /// </summary>
        /// <param name="from">지난 자리</param>
        /// <param name="to">지금 자리</param>
        public void GetSweep(out Vector3 from, out Vector3 to)
        {
            to = transform.position;
            from = hasPrevious ? previousPosition : to;
        }

        /// <summary>이번 프레임의 자리를 기록해 둡니다. 자국을 찍은 뒤에 부릅니다.</summary>
        public void RememberPosition()
        {
            previousPosition = transform.position;
            hasPrevious = true;
        }

        // --- Private Methods ---

        /// <summary>
        /// 쓸 무게를 정합니다. 직접 적어 두지 않았으면 Rigidbody에서 읽어 옵니다.
        /// </summary>
        /// <returns>무게(kg)</returns>
        private float ResolveMass()
        {
            if (mass > 0.01f) return mass;

            // 바퀴에 붙은 경우 무게는 차 전체에 있습니다. 바퀴 수로 나눠 몫을 봅니다.
            Rigidbody body = GetComponentInParent<Rigidbody>();
            if (body == null) return 70f;

            return body.mass * 0.25f;
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때 목록을 비웁니다.
        /// 에디터에서 도메인 리로드를 꺼 두면 지난 실행의 값이 그대로 남기 때문입니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
        }
    }
}
