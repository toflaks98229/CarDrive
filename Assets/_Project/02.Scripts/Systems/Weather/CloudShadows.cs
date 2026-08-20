using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// 하늘의 구름이 땅에 드리우는 그림자를 셰이더에 넘깁니다.
    ///
    /// <b>재질마다 설정하지 않습니다.</b> 구름은 세계 전체에 걸리는 하나의 현상이라
    /// 땅·풀·차가 서로 다른 구름 밑에 있으면 안 됩니다. 그래서 여기서 한 번만
    /// <c>Shader.SetGlobal</c> 로 넣습니다. (<see cref="GrassPushField"/>가 풀 밀림 좌표를
    /// 넣는 것과 같은 방식입니다)
    ///
    /// 값은 <see cref="WeatherSystem"/>에서 가져옵니다.
    ///  - 구름량이 세기를 정합니다. <b>맑은 날에는 구름 그림자가 없습니다.</b>
    ///  - 바람이 흘러가는 속도와 방향을 정합니다. 폭우에는 빠르게 지나갑니다.
    ///
    /// 날씨 시스템이 없어도 동작합니다. 그때는 아래 기본값으로 고정됩니다.
    /// </summary>
    [DefaultExecutionOrder(400)]
    public class CloudShadows : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>구름 무늬 텍스처입니다. 이어 붙는(타일링) 흑백 노이즈여야 합니다.</summary>
        [Header("구름 무늬")]
        [Tooltip("이어 붙는 흑백 노이즈. CarDrive > Look > 구름 그림자 설정 으로 구울 수 있습니다.")]
        public Texture2D cloudMap;

        /// <summary>무늬 한 장이 덮는 크기(미터)입니다. 클수록 구름이 커집니다.</summary>
        [Tooltip("무늬 한 장이 덮는 크기(미터). 클수록 구름 덩어리가 커집니다.")]
        public float tileSize = 220f;

        /// <summary>그늘이 가장 짙을 때의 어두움입니다. 1이면 새까맣게 됩니다.</summary>
        [Header("세기")]
        [Tooltip("그늘이 가장 짙을 때의 어두움. 1이면 새까맣게 되므로 보통 0.5 아래로 둡니다.")]
        [Range(0f, 1f)]
        public float maxStrength = 0.35f;

        /// <summary>구름 경계의 부드러움입니다. 0이면 칼같이 끊깁니다.</summary>
        [Tooltip("구름 경계의 부드러움. 0이면 칼같이 끊깁니다.")]
        [Range(0f, 0.5f)]
        public float softness = 0.12f;

        /// <summary>바람이 0일 때도 흘러가는 기본 속도(m/s)입니다.</summary>
        [Header("흐름")]
        [Tooltip("바람이 0일 때도 흘러가는 기본 속도(m/s). 완전히 멈춰 있으면 그림 같아 보입니다.")]
        public float baseSpeed = 3f;

        /// <summary>바람 세기 1일 때 더해지는 속도(m/s)입니다.</summary>
        [Tooltip("바람 세기 1일 때 더해지는 속도(m/s)")]
        public float windSpeed = 22f;

        /// <summary>구름이 흘러가는 방향입니다. 정규화해서 씁니다.</summary>
        [Tooltip("구름이 흘러가는 방향 (XZ). 정규화해서 씁니다.")]
        public Vector2 direction = new Vector2(1f, 0.35f);

        /// <summary>날씨 시스템 없이 쓸 때의 구름량입니다.</summary>
        [Header("날씨 없이 쓸 때")]
        [Tooltip("날씨 시스템이 없을 때 쓸 구름량 (0~1)")]
        [Range(0f, 1f)]
        public float fallbackCloudCover = 0.5f;

        // --- Private Member Variables ---

        /// <summary>지금까지 흘러온 거리입니다. 매 프레임 누적합니다.</summary>
        private Vector2 scroll;

        // --- Shader Property IDs ---
        // 이름으로 매번 찾으면 문자열 해시가 프레임마다 돕니다. 한 번만 구해 둡니다.

        private static readonly int MapId = Shader.PropertyToID("_CloudShadowMap");
        private static readonly int ParamsId = Shader.PropertyToID("_CloudShadowParams");
        private static readonly int ScrollId = Shader.PropertyToID("_CloudShadowScroll");

        // --- Unity Event Functions ---

        /// <summary>
        /// 자신을 등록합니다. 다른 곳에서 구름을 잠시 끄고 싶을 때 찾을 수 있게 합니다.
        /// </summary>
        void Awake()
        {
            if (!GameContext.Register(this))
            {
                enabled = false;
                return;
            }
        }

        /// <summary>등록을 해제하고 셰이더에서 구름을 끕니다.</summary>
        void OnDestroy()
        {
            GameContext.Unregister(this);
            Disable();
        }

        /// <summary>
        /// 꺼질 때도 구름을 끕니다. 컴포넌트만 끄고 전역값이 남으면
        /// 왜 화면이 어두운지 알 수 없게 됩니다.
        /// </summary>
        void OnDisable()
        {
            Disable();
        }

        /// <summary>
        /// 구름을 흘려보내고 셰이더에 값을 넘깁니다.
        /// </summary>
        void Update()
        {
            if (cloudMap == null)
            {
                Disable();
                return;
            }

            float cover = WeatherSystem.Instance != null
                ? WeatherSystem.Instance.CloudCover
                : fallbackCloudCover;

            float wind = WeatherSystem.Instance != null
                ? WeatherSystem.Instance.WindStrength
                : 0f;

            // 구름이 없으면 그림자도 없습니다. 맑은 날 하늘이 텅 빈 느낌이 여기서 나옵니다.
            float strength = maxStrength * Mathf.Clamp01(cover);

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            scroll += dir * ((baseSpeed + windSpeed * wind) * Time.deltaTime);

            // 무늬가 이어 붙으므로 타일 하나만큼 흐르면 처음과 같습니다.
            // 그대로 두면 값이 계속 커져 멀리서 정밀도가 떨어지므로 되감습니다.
            float tile = Mathf.Max(1f, tileSize);
            scroll.x = Mathf.Repeat(scroll.x, tile);
            scroll.y = Mathf.Repeat(scroll.y, tile);

            Shader.SetGlobalTexture(MapId, cloudMap);
            Shader.SetGlobalVector(ParamsId, new Vector4(tile, strength, softness, 1f));
            Shader.SetGlobalVector(ScrollId, new Vector4(scroll.x, scroll.y, 0f, 0f));
        }

        // --- Private Methods ---

        /// <summary>셰이더에서 구름 그림자를 끕니다.</summary>
        private void Disable()
        {
            Shader.SetGlobalVector(ParamsId, new Vector4(tileSize, 0f, softness, 0f));
        }
    }
}
