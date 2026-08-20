using System.Collections.Generic;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// 날씨를 눈에 보이게 만드는 표현 담당입니다.
    /// WeatherSystem에서 수치를 읽어 비 파티클과 안개에 반영하고,
    /// 플레이어를 따라다니며 항상 주변에 날씨가 있게 합니다.
    ///
    /// 주의: 지금 비 파티클은 차량 프리팹 안에 들어 있어 차를 따라다닙니다.
    /// 도보에서도 비가 오게 하려면 그 오브젝트들을 이 리그 아래로 옮겨야 합니다.
    /// followTarget이 그것을 전제로 만들어져 있습니다.
    /// </summary>
    public class WeatherRig : MonoBehaviour
    {
        // --- Public Member Variables ---

        [Header("연동")]
        [Tooltip("날씨 수치를 읽어올 시스템. 비워두면 씬에서 자동으로 찾습니다.")]
        public WeatherSystem weatherSystem;

        [Header("따라다니기")]
        [Tooltip("이 대상을 따라다닙니다. 비워두면 메인 카메라를 씁니다. " +
                 "카메라는 탑승/하차에 따라 옮겨가므로 어느 상태에서도 주변에 날씨가 유지됩니다.")]
        public Transform followTarget;

        [Tooltip("대상 기준 위치 오프셋. 보통 머리 위에 둡니다.")]
        public Vector3 followOffset = new Vector3(0f, 6f, 0f);

        [Tooltip("체크하면 대상의 Y축 회전도 따라갑니다. 보통은 끕니다.")]
        public bool followYaw = false;

        [Tooltip("따라가는 부드러움. 0이면 즉시 붙습니다.")]
        public float followSmooth = 0f;

        [Header("비")]
        [Tooltip("비 파티클들. 방출량이 RainIntensity에 비례합니다.")]
        public List<ParticleSystem> rainParticles = new List<ParticleSystem>();

        [Tooltip("RainIntensity가 1일 때의 초당 방출량")]
        public float maxRainRate = 600f;

        [Tooltip("이 값 아래면 비를 아예 멈춥니다.")]
        [Range(0f, 0.2f)]
        public float rainStopThreshold = 0.02f;

        [Header("비 - 점진적 변화")]
        [Tooltip("보이는 비 세기가 목표치까지 도달하는 데 걸리는 시간(초). " +
                 "목표 크기에 비례해 속도를 잡기 때문에 이슬비든 폭우든 걸리는 시간이 비슷합니다. " +
                 "0이면 즉시 반영합니다.")]
        public float rainRampSeconds = 3f;

        [Header("비 - 세기에 따라 함께 변하는 것")]
        [Tooltip("가늘게 내릴 때의 낙하 속도 배율")]
        public float speedMultiplierAtMin = 0.55f;

        [Tooltip("굵게 내릴 때의 낙하 속도 배율")]
        public float speedMultiplierAtFull = 1f;

        [Tooltip("가늘게 내릴 때의 빗방울 크기 배율")]
        public float sizeMultiplierAtMin = 0.6f;

        [Tooltip("굵게 내릴 때의 빗방울 크기 배율")]
        public float sizeMultiplierAtFull = 1f;

        [Tooltip("세기가 1을 넘을 때(폭우) 빗방울이 더 굵어지는 정도. " +
                 "0이면 최대에서 멈춥니다. 비(0.7)와 폭우(4.0)를 눈으로 구분하려면 0보다 커야 합니다.")]
        public float sizeGrowthAboveFull = 0.2f;

        [Tooltip("빗방울 크기 배율의 상한. 너무 키우면 빗줄기가 판때기처럼 보입니다.")]
        [Range(1f, 3f)]
        public float maxSizeMultiplier = 1.5f;

        [Header("비 - 상한")]
        [Tooltip("세기가 1을 넘으면 파티클 최대 개수도 함께 올립니다. " +
                 "이걸 끄면 폭우가 파티클 상한에 걸려 아무리 세기를 올려도 더 굵어지지 않습니다.")]
        public bool scaleMaxParticles = true;

        [Tooltip("파티클 최대 개수를 올릴 수 있는 배율 한계. 성능이 걱정되면 낮추세요.")]
        [Range(1f, 10f)]
        public float maxParticleScale = 5f;

        [Header("안개")]
        [Tooltip("체크하면 렌더 설정의 안개를 날씨에 맞춰 조절합니다.")]
        public bool controlRenderFog = false;

        [Tooltip("FogDensity가 1일 때의 안개 밀도")]
        public float maxFogDensity = 0.05f;

        // 안개 색 필드는 없앴습니다. SkyController가 시간대에 맞춰 정합니다.

        [Header("어두워짐")]
        [Tooltip("체크하면 Darkness에 맞춰 환경광을 낮춥니다. 흐린 날이 눈에 보이게 됩니다.")]
        public bool controlAmbient = false;

        [Tooltip("Darkness가 1일 때 남는 환경광 비율")]
        [Range(0f, 1f)]
        public float minAmbientFactor = 0.35f;

        [Header("시야")]
        [Tooltip("체크하면 날씨의 시야 배율에 맞춰 카메라 시야 거리와 헤드라이트 범위를 줄입니다. " +
                 "안개(0.35)나 폭우(0.45)에서 멀리 못 보게 됩니다.")]
        public bool controlVisibility = false;

        [Tooltip("시야 거리를 조절할 카메라. 비워두면 메인 카메라를 씁니다.")]
        public Camera visibilityCamera;

        [Tooltip("시야가 최악일 때 남는 비율의 하한. 너무 낮추면 지형이 눈앞에서 잘려 보입니다.")]
        [Range(0.1f, 1f)]
        public float minVisibilityFactor = 0.35f;

        [Tooltip("범위를 함께 줄일 헤드라이트들. 비어 있으면 조명은 건드리지 않습니다.")]
        public List<Light> headlights = new List<Light>();

        // --- Private Member Variables ---

        // 파티클마다 원래 값을 기억해 두고 거기에 배율을 곱합니다.
        //
        // 크기·속도는 startSizeMultiplier 대신 MinMaxCurve 전체를 복사해 둡니다.
        // Multiplier는 내부적으로 m_Scalar 하나만 가리키는데, 랜덤 범위(TwoConstants) 모드에서
        // m_Scalar는 "배율"이 아니라 그냥 최대값입니다. 그래서 Multiplier만 건드리면
        // 하한이 고정된 채 편차만 벌어지고 정작 크기는 거의 변하지 않습니다.
        // (실제로 CFXR 빗줄기가 이 상태라 이슬비와 폭우의 굵기 차이가 14%뿐이었습니다)
        private readonly List<float> baseRates = new List<float>();
        private readonly List<ParticleSystem.MinMaxCurve> baseStartSpeeds = new List<ParticleSystem.MinMaxCurve>();
        private readonly List<ParticleSystem.MinMaxCurve> baseStartSizes = new List<ParticleSystem.MinMaxCurve>();
        private readonly List<int> baseMaxParticles = new List<int>();

        private float displayedRain;   // 실제로 파티클에 적용 중인 세기

        /// <summary>어두워지기 전의 원래 환경광 세기입니다. 여기에 배율을 곱해 적용합니다.</summary>
        private float baseAmbientIntensity;

        /// <summary>어두워지기 전의 원래 환경광 색상입니다.</summary>
        private Color baseAmbientColor;

        /// <summary>환경광 원본값을 이미 기억해 두었는지 여부입니다. 한 번만 캐시합니다.</summary>
        private bool ambientCached;

        /// <summary>시야를 줄이기 전의 원래 카메라 far clip 거리입니다.</summary>
        private float baseFarClip;

        /// <summary>전조등별 원래 조사 거리입니다. 시야 배율을 곱해 적용합니다.</summary>
        private readonly List<float> baseLightRanges = new List<float>();

        /// <summary>시야 관련 원본값을 이미 기억해 두었는지 여부입니다. 한 번만 캐시합니다.</summary>
        private bool visibilityCached;

        /// <summary>마지막으로 적용한 시야 배율입니다. 값이 그대로면 다시 적용하지 않습니다.</summary>
        private float appliedVisibility = -1f;

        // --- Unity Event Functions ---

        /// <summary>
        /// 자신을 레지스트리에 등록합니다. 다른 컴포넌트가 Start에서 찾아 씁니다.
        /// (등록은 Awake, 조회는 Start — Unity가 모든 Awake를 끝낸 뒤 Start를 부릅니다)
        /// </summary>
        void Awake()
        {
            GameContext.Register(this);
        }

        /// <summary>등록을 해제합니다.</summary>
        void OnDestroy()
        {
            GameContext.Unregister(this);
        }

        /// <summary>
        /// 날씨 시스템과 따라갈 대상을 찾고, 파티클마다 원래 방출량·속도·크기를 기억해 둡니다.
        /// playOnAwake로 이미 비가 내리고 있을 수 있으므로 마지막에 비를 멈춰 정리합니다.
        /// </summary>
        void Start()
        {
            if (weatherSystem == null) weatherSystem = GameContext.Resolve<WeatherSystem>(this);
            if (weatherSystem == null)
            {
                Debug.LogWarning("WeatherRig: WeatherSystem을 찾지 못해 날씨가 반영되지 않습니다.", this);
            }

            if (followTarget == null) followTarget = GameContext.MainCameraTransform;

            // 파티클마다 원래 방출량을 기억해 둡니다. (비율로 조절하기 위해)
            baseRates.Clear();
            baseStartSpeeds.Clear();
            baseStartSizes.Clear();
            baseMaxParticles.Clear();

            for (int i = 0; i < rainParticles.Count; i++)
            {
                if (rainParticles[i] == null)
                {
                    baseRates.Add(0f);
                    baseStartSpeeds.Add(new ParticleSystem.MinMaxCurve(1f));
                    baseStartSizes.Add(new ParticleSystem.MinMaxCurve(1f));
                    baseMaxParticles.Add(1000);
                    continue;
                }

                ParticleSystem.EmissionModule em = rainParticles[i].emission;

                // rateOverTime이 상수가 아니면(곡선·랜덤) constant가 0으로 나올 수 있으므로
                // 최대값을 기준으로 잡습니다.
                float rate = Mathf.Max(em.rateOverTime.constant, em.rateOverTime.constantMax);
                baseRates.Add(rate);

                // MinMaxCurve는 구조체라 대입만으로 안전하게 복사됩니다.
                ParticleSystem.MainModule main = rainParticles[i].main;
                baseStartSpeeds.Add(main.startSpeed);
                baseStartSizes.Add(main.startSize);
                baseMaxParticles.Add(main.maxParticles);
            }

            displayedRain = 0f;

            // 파티클이 playOnAwake로 이미 돌고 있을 수 있으므로 날씨에 맞춰 정리합니다.
            // (맑은 날인데 비가 내리고 있으면 이상하니까)
            StopRain();
        }

        /// <summary>
        /// 리그를 대상 위치로 옮기고, 날씨 수치를 비·안개·환경광·시야에 반영합니다.
        /// 카메라 이동이 끝난 뒤에 따라가야 하므로 LateUpdate에서 처리합니다.
        /// </summary>
        void LateUpdate()
        {
            FollowTarget();

            if (weatherSystem == null) return;

            UpdateRain(weatherSystem.RainIntensity);
            if (controlRenderFog) UpdateFog(weatherSystem.FogDensity);
            if (controlAmbient) UpdateAmbient(weatherSystem.Darkness);
            if (controlVisibility) UpdateVisibility(weatherSystem.VisibilityMultiplier);
        }

        // --- Private Methods ---

        /// <summary>
        /// 대상 위로 리그를 옮깁니다.
        /// 카메라가 탑승/하차로 옮겨 다녀도 항상 플레이어 주변에 날씨가 유지됩니다.
        /// </summary>
        private void FollowTarget()
        {
            // 카메라가 런타임에 교체될 수 있으므로 놓쳤으면 다시 찾습니다.
            if (followTarget == null)
            {
                followTarget = GameContext.MainCameraTransform;
                if (followTarget == null) return;
            }

            Vector3 target = followTarget.position + followOffset;

            transform.position = followSmooth > 0f
                ? Vector3.Lerp(transform.position, target, Time.deltaTime * followSmooth)
                : target;

            if (followYaw)
            {
                transform.rotation = Quaternion.Euler(0f, followTarget.eulerAngles.y, 0f);
            }
        }

        /// <summary>
        /// 비 세기에 맞춰 파티클 방출량을 조절합니다.
        /// </summary>
        private void UpdateRain(float target)
        {
            // 보이는 세기를 목표치로 서서히 따라가게 합니다.
            // 날씨 전환 자체도 완만하지만, 디버거로 즉시 바꿨을 때 빗줄기가 튀어 들어오는 것을 막습니다.
            //
            // 속도를 고정하면 목표가 큰 폭우(비 양이 원본의 4배)는 도달까지 몇 배로 오래 걸립니다.
            // 그래서 목표 크기에 비례한 속도를 써서, 어떤 날씨든 rainRampSeconds 안에 도달하게 합니다.
            if (rainRampSeconds > 0f)
            {
                float reference = Mathf.Max(Mathf.Abs(target), displayedRain, 0.05f);
                float step = reference / rainRampSeconds * Time.deltaTime;
                displayedRain = Mathf.MoveTowards(displayedRain, target, step);
            }
            else
            {
                displayedRain = target;
            }

            if (displayedRain <= rainStopThreshold)
            {
                StopRain();
                return;
            }

            // 세기에 따라 방출량뿐 아니라 낙하 속도와 빗방울 크기도 함께 변합니다.
            // 가늘게 내릴 때는 천천히 작은 방울이, 굵을 때는 빠르고 큰 방울이 떨어집니다.
            //
            // 속도는 Mathf.Lerp가 t를 0~1로 자르므로 세기가 1을 넘어도 최대에서 멈춥니다.
            // 빗방울이 총알처럼 날아가지 않게 하려는 의도된 동작입니다.
            float speedMul = Mathf.Lerp(speedMultiplierAtMin, speedMultiplierAtFull, displayedRain);

            // 크기는 1을 넘는 구간에서도 조금씩 더 굵어집니다.
            // 그렇게 하지 않으면 비(0.7)와 폭우(4.0)가 같은 굵기로 보입니다.
            // 세기 자체가 이미 배율이라 그대로 곱하면 4배가 되므로, 완만한 기울기와 상한을 둡니다.
            float sizeMul = Mathf.Lerp(sizeMultiplierAtMin, sizeMultiplierAtFull, displayedRain);
            if (displayedRain > 1f && sizeGrowthAboveFull > 0f)
            {
                sizeMul = sizeMultiplierAtFull + (displayedRain - 1f) * sizeGrowthAboveFull;
            }
            sizeMul = Mathf.Min(sizeMul, maxSizeMultiplier);

            ApplyToParticles(displayedRain, speedMul, sizeMul);
            IsRaining = true;
        }

        /// <summary>
        /// MinMaxCurve 전체에 배율을 곱한 새 곡선을 만듭니다.
        ///
        /// <c>startSizeMultiplier</c> 같은 Multiplier 프로퍼티는 내부의 m_Scalar 하나만 가리킵니다.
        /// 그 값이 "배율"로 동작하는 것은 곡선 모드일 때뿐이고,
        /// 랜덤 범위(TwoConstants) 모드에서는 그냥 <b>최대값</b>입니다.
        /// 그래서 Multiplier만 건드리면 하한이 고정된 채 편차만 벌어져 크기가 변하지 않습니다.
        /// 여기서는 모드별로 두 끝을 모두 곱해 실제로 크기가 변하게 합니다.
        /// </summary>
        /// <param name="source">원래 곡선</param>
        /// <param name="multiplier">곱할 배율</param>
        /// <returns>배율이 적용된 새 곡선</returns>
        private static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve source, float multiplier)
        {
            switch (source.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return new ParticleSystem.MinMaxCurve(source.constant * multiplier);

                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(source.constantMin * multiplier,
                                                          source.constantMax * multiplier);

                case ParticleSystemCurveMode.Curve:
                    return new ParticleSystem.MinMaxCurve(source.curveMultiplier * multiplier, source.curve);

                case ParticleSystemCurveMode.TwoCurves:
                    return new ParticleSystem.MinMaxCurve(source.curveMultiplier * multiplier,
                                                          source.curveMin, source.curveMax);
            }

            return source;
        }

        /// <summary>
        /// 등록된 파티클에 세기를 반영합니다.
        ///
        /// 방출량이 0인 것은 서브 이미터 같은 보조 효과라 부모가 알아서 돌립니다.
        /// 그런 것은 재생 상태와 방출량을 건드리지 않고 크기·속도만 맞춥니다.
        /// </summary>
        private void ApplyToParticles(float intensity, float speedMul, float sizeMul)
        {
            for (int i = 0; i < rainParticles.Count; i++)
            {
                ParticleSystem ps = rainParticles[i];
                if (ps == null) continue;

                float baseRate = i < baseRates.Count ? baseRates[i] : 0f;

                if (baseRate > 0f)
                {
                    ParticleSystem.EmissionModule em = ps.emission;
                    em.enabled = true;
                    em.rateOverTime = baseRate * intensity;

                    // 재생 상태는 항상 유지합니다. 이유는 StopRain 주석 참고.
                    if (!ps.isPlaying) ps.Play();
                }

                ParticleSystem.MainModule main = ps.main;
                if (i < baseStartSpeeds.Count) main.startSpeed = ScaleCurve(baseStartSpeeds[i], speedMul);
                if (i < baseStartSizes.Count) main.startSize = ScaleCurve(baseStartSizes[i], sizeMul);

                // 세기가 1을 넘으면 살아 있을 수 있는 입자 수도 그만큼 필요합니다.
                // 상한을 그대로 두면 방출량만 올라가고 화면에 보이는 양은 늘지 않습니다.
                if (scaleMaxParticles && i < baseMaxParticles.Count)
                {
                    float scale = Mathf.Clamp(intensity, 1f, maxParticleScale);
                    main.maxParticles = Mathf.CeilToInt(baseMaxParticles[i] * scale);
                }
            }
        }

        /// <summary>
        /// 비를 멈춥니다. 이미 떨어지는 빗방울은 그대로 두어 자연스럽게 잦아듭니다.
        ///
        /// 주의: 여기서 ParticleSystem.Stop()을 부르면 안 됩니다.
        /// 이 비 효과에는 CFXR_Effect가 붙어 있고 clearBehavior가 Destroy로 잡혀 있어서,
        /// 파티클이 모두 죽는 순간 <b>GameObject 자체를 파괴</b>해 버립니다.
        /// 그러면 다시 비를 켤 방법이 없어집니다.
        /// 그래서 방출량만 0으로 두고 시스템은 계속 재생 상태로 유지합니다.
        /// </summary>
        private void StopRain()
        {
            ApplyToParticles(0f, speedMultiplierAtMin, sizeMultiplierAtMin);
            IsRaining = false;
        }

        // --- Public Properties ---

        /// <summary>
        /// 지금 실제로 파티클에 적용 중인 비 세기입니다.
        /// WeatherSystem.RainIntensity가 목표치라면 이 값은 화면에 보이는 값이며,
        /// 둘을 비교하면 동기화가 잘 따라오고 있는지 확인할 수 있습니다.
        /// </summary>
        public float DisplayedRain { get { return displayedRain; } }

        /// <summary>지금 비가 실제로 뿜어져 나오고 있는지 여부입니다.</summary>
        public bool IsRaining { get; private set; }

        /// <summary>
        /// 렌더 설정의 안개를 조절합니다.
        /// URP에서는 Volume으로 하는 편이 낫지만, 준비 단계에서는 내장 안개로 감을 잡습니다.
        /// </summary>
        /// <summary>
        /// 흐릴수록 환경광을 낮춥니다. 기준값은 처음 한 번만 기억해 둡니다.
        /// </summary>
        private void UpdateAmbient(float darkness)
        {
            if (!ambientCached)
            {
                baseAmbientIntensity = RenderSettings.ambientIntensity;
                baseAmbientColor = RenderSettings.ambientLight;
                ambientCached = true;
            }

            float factor = Mathf.Lerp(1f, minAmbientFactor, Mathf.Clamp01(darkness));
            RenderSettings.ambientIntensity = baseAmbientIntensity * factor;
            RenderSettings.ambientLight = baseAmbientColor * factor;
        }

        /// <summary>
        /// 안개 짙기를 렌더 설정에 반영합니다. 거의 0이면 안개를 아예 끕니다.
        /// </summary>
        /// <param name="density">날씨가 정한 안개 짙기(0~1). maxFogDensity에 곱해집니다.</param>
        private void UpdateFog(float density)
        {
            RenderSettings.fog = density > 0.01f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = maxFogDensity * density;

            // 안개 '색'은 SkyController가 정합니다.
            //
            // 여기서 고정색을 쓰면 밤에도 밝은 회색 안개가 떠서, 밤하늘보다 안개가 밝은
            // 이상한 그림이 됩니다. 안개는 먼 곳이 하늘에 녹아드는 현상이므로
            // 지평선 색을 따라가야 합니다.
            //
            // 두 컴포넌트 모두 LateUpdate에서 도는데 실행 순서는 보장되지 않습니다.
            // 그래서 <b>밀도는 날씨가, 색은 하늘이</b> 갖도록 나눴습니다.
        }

        /// <summary>
        /// 날씨의 시야 배율에 맞춰 카메라 시야 거리와 헤드라이트 범위를 줄입니다.
        ///
        /// 여기서는 <b>밤을 뺀 날씨만의</b> 배율(VisibilityMultiplier)을 씁니다.
        /// GetEffectiveVisibility()는 밤이면 값이 절반이 되는데, 그 값을 헤드라이트에 쓰면
        /// 정작 필요한 밤에 불빛이 가장 짧아지는 반대 결과가 나옵니다.
        /// </summary>
        private void UpdateVisibility(float visibility)
        {
            if (!visibilityCached)
            {
                if (visibilityCamera == null) visibilityCamera = GameContext.MainCamera;
                if (visibilityCamera != null) baseFarClip = visibilityCamera.farClipPlane;

                baseLightRanges.Clear();
                for (int i = 0; i < headlights.Count; i++)
                {
                    baseLightRanges.Add(headlights[i] != null ? headlights[i].range : 0f);
                }

                visibilityCached = true;
            }

            float factor = Mathf.Clamp(visibility, minVisibilityFactor, 1f);

            // 시야 거리는 매 프레임 바꿀 필요가 없습니다. (카메라 행렬이 다시 계산됩니다)
            if (Mathf.Abs(factor - appliedVisibility) < 0.005f) return;
            appliedVisibility = factor;

            if (visibilityCamera != null) visibilityCamera.farClipPlane = baseFarClip * factor;

            for (int i = 0; i < headlights.Count; i++)
            {
                if (headlights[i] == null || i >= baseLightRanges.Count) continue;
                headlights[i].range = baseLightRanges[i] * factor;
            }
        }
    }
}
