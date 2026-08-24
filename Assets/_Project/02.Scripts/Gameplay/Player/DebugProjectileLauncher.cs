using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 조준한 방향으로 <b>리지드바디 공을 쏘는</b> 디버그 발사기입니다.
    ///
    /// <b>왜 필요한가.</b> 보행 로봇의 휘청임과 넘어짐은 <b>충격량</b>으로 판정합니다.
    /// (<see cref="RobotPhysicsMotor"/>) 그 값을 눈으로 확인하려면 원하는 세기로 원하는 자리를
    /// 때려 볼 수 있어야 하는데, 차를 몰고 가서 받는 것으로는 세기를 조절할 수 없습니다.
    /// 질량과 속도를 인스펙터에서 바꿔 가며 쏘면 <b>문턱값을 바로 맞춰 볼 수 있습니다.</b>
    ///
    /// <b>맞은 쪽이 받는 속도 변화</b>는 이 한 줄로 어림잡을 수 있습니다.
    /// <code>Δv ≈ (발사체 질량 × 속도) ÷ (맞은 것의 질량 + 발사체 질량)</code>
    /// 분모에 발사체 질량을 더하는 이유는 <b>공이 벽처럼 멈춰 서지 않기 때문</b>입니다.
    /// 부딪힌 뒤 둘이 운동량을 나눠 가지므로, 맞은 쪽이 전부 받는 것보다 조금 적습니다.
    ///
    /// 그래서 60kg 공을 40m/s 로 쏘면 운동량이 2400 N·s 이고,
    /// 320kg 짜리 4족 로봇은 Δv 6.3m/s (넘어짐 문턱 3.5 를 넘음),
    /// 2톤짜리 스트라이더는 Δv 1.2m/s (휘청이기만) 가 됩니다.
    /// <b>무거운 로봇을 넘어뜨리려면 질량이나 속도를 올려야 합니다.</b>
    /// 쏠 때마다 그 값을 로그로 남기므로 계산할 필요는 없습니다.
    ///
    /// <b>연속 충돌 검사를 켭니다.</b> 40m/s 짜리 공은 60fps 에서 한 프레임에 0.67m 를 갑니다.
    /// 로봇의 몸통 상자는 그보다 얇아서, 기본 검사로는 <b>그냥 통과해 버립니다.</b>
    ///
    /// 정식 기능이 되면 키를 <see cref="GameAction"/> 으로 올리세요. 지금은 디버그 전용 창구를 씁니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DebugProjectileLauncher : MonoBehaviour
    {
        // --- Public Member Variables : 배선 ---

        /// <summary>조준 기준입니다. 비워두면 <see cref="PlayerAim"/> 규칙으로 카메라를 찾습니다.</summary>
        [Header("배선")]
        [Tooltip("조준 기준. 비워두면 카메라를 찾습니다.")]
        public Transform aimSource;

        /// <summary>비워두면 구를 만들어 씁니다. 다른 것을 쏘고 싶으면 리지드바디가 달린 프리팹을 넣으세요.</summary>
        [Tooltip("쏠 프리팹. 비워두면 구를 만들어 씁니다. 리지드바디가 있어야 합니다.")]
        public GameObject projectilePrefab;

        // --- Public Member Variables : 발사 ---

        /// <summary>쏘는 키입니다.</summary>
        [Header("발사")]
        [Tooltip("쏘는 키")]
        public KeyCode fireKey = KeyCode.F;

        /// <summary>누르고 있으면 계속 쏠지 여부입니다.</summary>
        [Tooltip("누르고 있으면 계속 쏩니다")]
        public bool automatic;

        /// <summary>연사 간격입니다.</summary>
        [Tooltip("연사 간격(초)")]
        public float fireInterval = 0.15f;

        /// <summary>발사 속도입니다.</summary>
        [Tooltip("발사 속도(m/s)")]
        public float speed = 40f;

        /// <summary>발사체의 질량입니다. <b>운동량 = 질량 × 속도</b> 가 맞은 쪽의 반응을 정합니다.</summary>
        [Tooltip("발사체의 질량(kg). 질량 × 속도가 맞은 쪽의 반응을 정합니다.")]
        public float mass = 60f;

        /// <summary>발사체의 반지름입니다.</summary>
        [Tooltip("발사체의 반지름(m)")]
        public float radius = 0.25f;

        /// <summary>조준 기준에서 이만큼 앞에 만듭니다. 쏘는 사람과 겹치지 않게 합니다.</summary>
        [Tooltip("조준 기준에서 이만큼(m) 앞에 만듭니다")]
        public float spawnDistance = 1.5f;

        /// <summary>이 시간이 지나면 사라집니다.</summary>
        [Tooltip("이 시간(초)이 지나면 사라집니다")]
        public float lifetime = 8f;

        // --- Public Member Variables : 표시 ---

        /// <summary>발사체의 색입니다.</summary>
        [Header("표시")]
        [Tooltip("발사체의 색")]
        public Color color = new Color(1f, 0.45f, 0.1f);

        /// <summary>쏠 때마다 운동량을 로그로 남길지 여부입니다.</summary>
        [Tooltip("쏠 때마다 운동량과 예상 반응을 로그로 남깁니다")]
        public bool logMomentum = true;

        // --- Private Member Variables ---

        /// <summary>실제로 쓸 조준 기준입니다.</summary>
        private Transform aim;

        /// <summary>다음에 쏠 수 있는 시각입니다.</summary>
        private float nextFireTime;

        /// <summary>만들어 쓰는 구의 재질입니다. 발사체마다 만들지 않고 하나를 나눠 씁니다.</summary>
        private Material sharedMaterial;

        // --- Constants ---

        /// <summary>로그에서 기준으로 삼는 대상 질량입니다. 1톤짜리가 어떻게 반응할지 보여 줍니다.</summary>
        private const float ReferenceTargetMass = 1000f;

        // --- Unity Methods ---

        /// <summary>조준 기준을 정합니다. 등록은 Awake, 조회는 Start 라는 규칙을 따릅니다.</summary>
        private void Start()
        {
            aim = PlayerAim.Resolve(aimSource, this);
        }

        /// <summary>키를 보고 쏩니다.</summary>
        private void Update()
        {
            if (aim == null) return;

            bool pressed = automatic
                ? GameInput.GetDebugKey(fireKey) && Time.time >= nextFireTime
                : GameInput.GetDebugKeyDown(fireKey);

            if (!pressed) return;

            nextFireTime = Time.time + Mathf.Max(fireInterval, 0.02f);

            Fire();
        }

        /// <summary>만들어 둔 재질을 치웁니다.</summary>
        private void OnDestroy()
        {
            if (sharedMaterial != null) Destroy(sharedMaterial);
        }

        // --- Public Methods ---

        /// <summary>
        /// 조준한 방향으로 한 발 쏩니다. 다른 곳(디버그 오버레이 버튼 등)에서 불러도 됩니다.
        /// </summary>
        public void Fire()
        {
            if (aim == null) aim = PlayerAim.Resolve(aimSource, this);
            if (aim == null) return;

            Vector3 direction = aim.forward;
            Vector3 origin = aim.position + direction * spawnDistance;

            GameObject projectile = Spawn(origin, Quaternion.LookRotation(direction));
            if (projectile == null) return;

            Rigidbody body = projectile.GetComponent<Rigidbody>();
            if (body == null)
            {
                GameLog.Warn(GameLog.Channel.Player, name + ": 발사체에 리지드바디가 없습니다. 아무 일도 일어나지 않습니다.", this);
                return;
            }

            body.mass = mass;

            // 빠른 탄은 한 프레임에 대상보다 멀리 가므로 연속 검사가 아니면 통과해 버립니다.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearVelocity = direction * speed;

            Destroy(projectile, lifetime);

            if (!logMomentum) return;

            float momentum = mass * speed;

            GameLog.InfoFormat(GameLog.Channel.Player,
                "디버그 발사: 운동량 {0} N·s → 1톤짜리라면 Δv 약 {1} m/s",
                momentum.ToString("F0"), (momentum / (ReferenceTargetMass + mass)).ToString("F1"), this);
        }

        // --- Private Methods ---

        /// <summary>발사체를 만듭니다. 프리팹이 없으면 구를 만들어 씁니다.</summary>
        /// <param name="position">만들 자리</param>
        /// <param name="rotation">만들 자세</param>
        /// <returns>만들어진 오브젝트</returns>
        private GameObject Spawn(Vector3 position, Quaternion rotation)
        {
            if (projectilePrefab != null) return Instantiate(projectilePrefab, position, rotation);

            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            ball.name = "DebugProjectile";
            ball.transform.SetPositionAndRotation(position, rotation);
            ball.transform.localScale = Vector3.one * (radius * 2f);

            MeshRenderer renderer = ball.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = EnsureMaterial();

            ball.AddComponent<Rigidbody>();

            return ball;
        }

        /// <summary>발사체가 나눠 쓸 재질을 만듭니다. 한 번만 만듭니다.</summary>
        /// <returns>재질</returns>
        private Material EnsureMaterial()
        {
            if (sharedMaterial != null) return sharedMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            sharedMaterial = new Material(shader);
            sharedMaterial.name = "DebugProjectile (runtime)";

            sharedMaterial.SetColor("_BaseColor", color);
            sharedMaterial.SetColor("_Color", color);
            sharedMaterial.EnableKeyword("_EMISSION");
            sharedMaterial.SetColor("_EmissionColor", color * 2f);

            return sharedMaterial;
        }
    }
}
