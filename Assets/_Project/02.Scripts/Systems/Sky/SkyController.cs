using UnityEngine;
using VContainer;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// 하늘과 주변광을 시간·날씨에 맞춰 몰아 줍니다.
    ///
    /// <see cref="TimeSystem"/>은 태양을 돌리고 밝기(DaylightFactor)를 계산하지만,
    /// 그 값을 <b>하늘에 반영하는 곳이 없었습니다.</b> 그래서 하늘이 시각과 따로 놀았습니다.
    ///
    /// 이 컴포넌트가 하는 일은 넷입니다.
    ///  1. 밤이 되면 별이 찍힌 하늘로 갈아 끼우고, 노출로 별을 끌어올립니다.
    ///  2. 하늘의 노출과 색을 시각에 맞춥니다. 절차적 하늘이면 낮 정도·해 방향도 넣습니다.
    ///  3. 주변광을 하늘색에서 뽑아 밤에는 어둡고 낮에는 밝게 맞춥니다.
    ///  4. 해의 색과 세기를 시간대에 맞춰 바꿉니다. 밤에는 달빛으로 넘깁니다.
    ///
    /// <b>사진 하늘과 절차적 하늘을 모두 받습니다.</b> 어느 쪽인지는 셰이더 이름이 아니라
    /// 프로퍼티 유무로 가립니다 — <c>_Exposure</c>가 있으면 사진, <c>_DayFactor</c>가 있으면
    /// 절차적입니다. 그래서 하늘을 갈아 끼워도 이 코드는 바뀌지 않습니다.
    ///
    /// 밤에 태양광을 완전히 끄면 지형이 새까매져 아무것도 보이지 않습니다.
    /// 그래서 <b>밤에는 방향을 유지한 채 차갑고 약한 달빛으로 바꿉니다.</b>
    /// </summary>
    [ExecuteAlways]
    public class SkyController : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>낮에 쓸 하늘 머티리얼입니다. 비워두면 RenderSettings의 스카이박스를 씁니다.</summary>
        [Header("하늘")]
        [Tooltip("낮에 쓸 하늘 머티리얼. 비워두면 RenderSettings의 스카이박스를 씁니다.")]
        public Material skyMaterial;

        /// <summary>해 방향을 읽어 올 조명입니다. 비워두면 TimeSystem의 태양광을 씁니다.</summary>
        [Tooltip("해 방향을 읽어 올 조명. 비워두면 TimeSystem의 태양광을 씁니다.")]
        public Light sun;

        /// <summary>구름이 짙을수록 별을 가릴지 여부입니다.</summary>
        [Tooltip("체크하면 구름이 짙을수록 별이 가려집니다.")]
        public bool cloudsHideStars = true;

        /// <summary>사진 기반 하늘의 한낮 노출입니다.</summary>
        [Header("사진 기반 하늘 (Skybox/Cubemap 등)")]
        [Tooltip("한낮의 노출. 사진 하늘을 쓸 때만 의미가 있습니다.")]
        public float dayExposure = 1.1f;

        /// <summary>사진 기반 하늘의 한밤 노출입니다. 0에 가까울수록 캄캄해집니다.</summary>
        [Tooltip("한밤의 노출. 0에 가까울수록 캄캄해집니다.")]
        public float nightExposure = 0.06f;

        /// <summary>한밤에 하늘에 씌울 색입니다. 푸른 기를 남기면 달빛 아래처럼 보입니다.</summary>
        [Tooltip("한밤에 하늘에 씌울 색. 푸른 기를 남기면 달빛 아래처럼 보입니다.")]
        public Color nightSkyTint = new Color(0.30f, 0.38f, 0.62f);

        /// <summary>
        /// 밤에 갈아 끼울 하늘입니다. 비워 두면 낮 하늘을 어둡게만 하고 별은 나오지 않습니다.
        ///
        /// <b>왜 하늘을 통째로 바꾸는가.</b> 낮 사진 한 장을 어둡게 해 봐야 별은 생기지 않습니다.
        /// 사진에 없는 것은 노출을 어떻게 만져도 나오지 않기 때문입니다. 그래서 별이 찍힌
        /// 밤 사진을 따로 두고 갈아 끼웁니다.
        /// </summary>
        [Tooltip("밤에 쓸 하늘 머티리얼. 비워 두면 낮 하늘을 어둡게만 합니다(별 없음).")]
        public Material nightSkyMaterial;

        /// <summary>
        /// 한밤의 밤 하늘 노출입니다. <b>별을 끌어올리는 손잡이가 이것입니다.</b>
        ///
        /// 밤 HDRI 에는 은하수와 별이 밝기 값으로 이미 들어 있습니다. 노출을 올리면
        /// 없던 것을 그리는 게 아니라 있는 것이 드러납니다.
        ///
        /// <b>값이 아주 작습니다.</b> 낮 하늘에 쓰는 1.1 같은 값을 주면 사진 속 달이
        /// 통째로 하얗게 번져 별이 오히려 묻힙니다. 노출을 훑어 재 보니 0.04~0.10 이
        /// 별이 살아 있는 구간이었고, 0.25 부터는 바탕이 떠올라 못 씁니다.
        /// 다른 밤 사진으로 바꾸면 적정값도 달라집니다 —
        /// 그때는 <c>NightSkyExposureSweep.Run</c> 을 돌려 다시 재십시오.
        /// </summary>
        [Tooltip("한밤의 밤 하늘 노출. 0.04~0.10 권장. 크게 주면 달빛에 별이 묻힙니다.")]
        public float nightSkyExposure = 0.04f;

        /// <summary>
        /// 이 밝기 아래에서 밤 하늘로 갈아탑니다.
        ///
        /// <b>왜 팝이 안 보이는가.</b> 낮 곡선과 밤 곡선이 이 지점에서 <b>둘 다 노출 0</b>으로
        /// 만나도록 짜여 있습니다. 갈아 끼우는 순간 양쪽 화면이 똑같이 캄캄하므로
        /// 바뀌는 것이 보이지 않습니다.
        /// </summary>
        [Tooltip("이 밝기 아래에서 밤 하늘로 갈아탑니다. 양쪽 노출이 여기서 0으로 만나 팝이 없습니다.")]
        [Range(0.01f, 0.3f)]
        public float swapDaylight = 0.05f;

        /// <summary>주변광(Ambient)을 시간에 맞춰 조절할지 여부입니다.</summary>
        [Header("주변광")]
        [Tooltip("체크하면 주변광을 시간대에 맞춰 조절합니다. RenderSettings의 Ambient Mode를 Gradient로 바꿉니다.")]
        public bool driveAmbient = true;

        /// <summary>한낮의 하늘 방향 주변광입니다.</summary>
        [Tooltip("한낮의 하늘 쪽 주변광")]
        public Color dayAmbientSky = new Color(0.42f, 0.47f, 0.55f);

        /// <summary>한밤의 하늘 방향 주변광입니다. 너무 어두우면 아무것도 안 보입니다.</summary>
        [Tooltip("한밤의 하늘 쪽 주변광. 너무 어둡게 두면 헤드라이트 밖이 완전히 검어집니다.")]
        public Color nightAmbientSky = new Color(0.055f, 0.065f, 0.095f);

        /// <summary>지면 쪽 주변광입니다. 위아래 대비를 만들어 입체감을 남깁니다.</summary>
        [Tooltip("지면 쪽 주변광. 하늘색보다 어두워야 위아래 대비가 생깁니다.")]
        [Range(0f, 1f)]
        public float groundAmbientScale = 0.35f;

        /// <summary>
        /// 한밤에 <b>높이 그라데이션</b>이 남길 정도입니다.
        ///
        /// <b>무엇을 고치는 값인가.</b> 툰 셰이더의 높이 그라데이션은 <b>조명 뒤에</b>
        /// 얹힙니다 — 포그를 걷어낸 뒤로 거리를 읽히게 하는 것이 이것이라, 빛을 받든
        /// 안 받든 같은 높이면 같은 색이어야 원경이 고르게 눌리기 때문입니다.
        /// 그런데 그 말은 <b>밤에도 그대로 남는다</b>는 뜻입니다.
        ///
        /// 2026-09-11 에 잰 한밤(02시) 화면: 건물 벽이 한낮의 <b>61%</b>, 땅은 8%,
        /// 나무는 10%. 빛을 전부 끄고 주변광을 0 으로 두어도 벽만 그대로 밝았습니다.
        /// 마을 집이 거대구조물과 같은 <c>MegaConcrete</c>(높이 세기 0.5)를 쓰기
        /// 때문입니다. 밤인데 건물만 대낮이면 화면 전체가 대낮으로 보입니다.
        ///
        /// ⚠ <b>0 으로 두지 마십시오.</b> 그러면 밤에 거대구조물이 거리 단서를 잃고
        /// 통째로 새까매집니다. 지붕이 있는 세계라 그 실루엣은 남아야 합니다.
        /// </summary>
        [Tooltip("한밤에 높이 그라데이션이 남길 정도. 0 이면 밤에 원경이 통째로 검어집니다")]
        [Range(0f, 1f)]
        public float nightHeightScale = 0.15f;

        /// <summary>
        /// 밤빛이 <b>온전히 서는</b> 깊이입니다. 해가 지평선 아래로 이만큼 내려가야
        /// 밤빛이 제 세기가 됩니다(도).
        ///
        /// <b>무엇을 막는 값인가.</b> 이 씬의 빛은 방향광 <b>하나뿐</b>이라 낮과 밤이
        /// 그것을 나눠 씁니다. 자리가 <b>한 번에</b> 바뀌면 그림자가 세계를 휩씁니다.
        /// 그래서 지평선 근처에서는 밤빛의 <b>세기도 방향도</b> 함께 물러섭니다.
        ///
        /// 하루가 24시간이므로 1도는 게임 시간 4분입니다. 12도면 해 뜨기 48분
        /// 전부터 밤빛이 물러섭니다.
        /// </summary>
        [Tooltip("밤빛이 제 세기가 되는 깊이(도). 지평선 근처에서는 세기도 방향도 물러섭니다")]
        [Range(0f, 45f)]
        public float nightHandoverDegrees = 12f;

        /// <summary>
        /// 밤빛이 <b>내려오는 각도</b>입니다(도). 90 이면 정수리에서 수직입니다.
        ///
        /// ⚠ <b>이 세계에는 하늘이 없습니다.</b> 머리 위를 덮은 것은 거대구조물이고,
        /// 하늘처럼 보이는 것은 그 천장을 구운 파노라마입니다. 그러니 밤에 남는 빛은
        /// <b>달이 아니라 천장</b>입니다 — 밤 화면에 보이는 그 등불들입니다.
        /// 해의 반대편에서 비추게 두면 <b>있지도 않은 달</b>을 그리게 됩니다.
        ///
        /// 90 도로 세우면 벽이 빛을 하나도 못 받아 통짜 실루엣이 됩니다.
        /// 조금 기울여 두면 벽에도 결이 남습니다.
        /// </summary>
        [Tooltip("밤빛이 내려오는 각도(도). 90 이면 수직. 세울수록 벽이 어두워집니다")]
        [Range(20f, 90f)]
        public float ceilingPitch = 65f;

        /// <summary>
        /// 밤빛이 내려오는 <b>방위</b>입니다(도). 해와 같은 170 이 기본입니다.
        ///
        /// 낮과 밤의 그림자가 아주 다른 쪽으로 지면 같은 자리가 다른 장소처럼 보입니다.
        /// </summary>
        [Tooltip("밤빛의 방위(도). TimeSystem 의 해와 같은 170 이 기본")]
        [Range(0f, 360f)]
        public float ceilingYaw = 170f;

        /// <summary>가장 궂은 날씨에서 주변광이 낮아지는 하한 배율입니다.</summary>
        [Tooltip("가장 궂은 날씨일 때 주변광에 곱할 배율. 0.5면 절반까지 어두워집니다.")]
        [Range(0.1f, 1f)]
        public float weatherDarkFloor = 0.55f;

        /// <summary>구름이 가장 짙을 때 햇빛이 남는 비율입니다.</summary>
        [Tooltip("구름이 가장 짙을 때 햇빛이 남는 비율. 낮을수록 흐린 날이 어두워집니다.")]
        [Range(0f, 1f)]
        public float weatherSunFloor = 0.45f;

        /// <summary>한낮 태양광의 색입니다.</summary>
        [Header("해와 달")]
        [Tooltip("한낮 태양광의 색")]
        public Color dayLightColor = new Color(1f, 0.96f, 0.87f);

        /// <summary>해 뜰 무렵·해 질 무렵의 색입니다.</summary>
        [Tooltip("해 뜰 무렵과 해 질 무렵의 색")]
        public Color duskLightColor = new Color(1f, 0.66f, 0.38f);

        /// <summary>밤에 쓸 달빛 색입니다.</summary>
        [Tooltip("밤에 쓸 달빛 색. 차가운 색이어야 밤처럼 보입니다.")]
        public Color moonLightColor = new Color(0.55f, 0.66f, 0.95f);

        /// <summary>달빛의 세기입니다. 0이면 밤에 조명이 완전히 꺼집니다.</summary>
        [Tooltip("달빛의 세기. 0이면 밤에 조명이 꺼져 헤드라이트 밖이 완전히 검어집니다.")]
        public float moonIntensity = 0.12f;




        // --- Private Member Variables ---

        /// <summary>지금 조작 중인 하늘 머티리얼입니다. 재생 중에는 에셋이 아니라 복제본입니다.</summary>
        private Material activeSky;

        /// <summary><see cref="activeSky"/>를 뜬 원본 에셋입니다. 원본이 바뀔 때만 다시 복제합니다.</summary>
        private Material skySource;

        /// <summary>재생을 시작할 때의 스카이박스입니다. 끝날 때 이것으로 되돌립니다.</summary>
        private Material skyboxBeforePlay;

        /// <summary>
        /// 시각을 묻는 시계입니다. 주입되지 않으면 대낮으로 봅니다.
        ///
        /// <b>이 컴포넌트는 <c>[ExecuteAlways]</c>라 편집 중에도 돕니다.</b> 그때는 컨테이너가
        /// 없으므로 <see cref="NullGameClock"/>이 들어 있고, 인스펙터에서 하늘 값을 만질 때
        /// 한낮 기준으로 보입니다. 예전에 <c>TimeSystem.Instance</c>가 편집 중 null이라
        /// <c>GetDaylight()</c>가 1을 돌려주던 것과 같은 결과입니다.
        /// </summary>
        private IGameClock clock = NullGameClock.Instance;

        /// <summary>해가 누구이고 얼마나 밝을 수 있는지 알려 주는 쪽입니다.</summary>
        private ISunSource sunSource;

        /// <summary>구름과 어둡기를 묻는 쪽입니다. 주입되지 않으면 맑은 하늘로 봅니다.</summary>
        private ISkyConditions skyConditions = NullWeather.Instance;

        // 셰이더 프로퍼티 이름은 문자열로 매번 찾으면 낭비라 한 번만 해석해 둡니다.
        private static readonly int DayFactorId = Shader.PropertyToID("_DayFactor");
        private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
        private static readonly int StarFadeId = Shader.PropertyToID("_StarFade");

        // 사진 기반 하늘(Skybox/Cubemap · Skybox/Panoramic)을 쓸 때 조작할 것들입니다.
        // 그런 하늘은 낮 사진 한 장이라 그냥 두면 <b>한밤중에도 파랗게</b> 빛납니다.
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        /// <summary>높이 그라데이션의 세기입니다. 툰 셰이더가 읽습니다.</summary>
        private static readonly int HeightScaleId = Shader.PropertyToID("_CarDriveHeightScale");

        /// <summary>TimeSystem 이 준 <b>진짜</b> 해의 자리입니다. 달로 접기 전의 것입니다.</summary>
        private Quaternion sunBase = Quaternion.identity;

        /// <summary>우리가 마지막으로 써 넣은 자리입니다. 자기 값을 다시 읽지 않으려고 둡니다.</summary>
        private Quaternion sunWritten = Quaternion.identity;
        private static readonly int TintId = Shader.PropertyToID("_Tint");

        // --- Injection ---

        /// <summary>
        /// 시계·해·하늘 상태를 받습니다.
        ///
        /// <b>셋을 따로 받는 이유가 있습니다.</b> 이 컴포넌트는 "몇 시인가", "어느 라이트가
        /// 해인가", "구름이 얼마나 꼈나" 세 가지를 묻는데, 앞의 둘은 시간 시스템이,
        /// 마지막은 날씨 시스템이 답합니다. 구현이 어디에 있든 이쪽 코드는 바뀌지 않습니다.
        /// </summary>
        /// <param name="gameClock">게임 시계</param>
        /// <param name="sun">해를 알려 주는 쪽</param>
        /// <param name="conditions">구름·어둡기를 알려 주는 쪽</param>
        [Inject]
        public void Construct(IGameClock gameClock, ISunSource sun, ISkyConditions conditions)
        {
            if (gameClock != null) clock = gameClock;
            if (sun != null) sunSource = sun;
            if (conditions != null) skyConditions = conditions;

            // 주입이 OnEnable 뒤에 올 수 있습니다. 그러면 이미 해를 찾아 둔 뒤이므로
            // 여기서 한 번 더 확인해야 시간 시스템이 지정한 해가 반영됩니다.
            ResolveReferences();
        }

        // --- Unity Event Functions ---

        /// <summary>
        /// 하늘 머티리얼과 해를 찾아 둡니다.
        /// </summary>
        void OnEnable()
        {
            ResolveReferences();
        }

        /// <summary>
        /// 복제해 둔 하늘을 버리고 스카이박스를 재생 전 상태로 되돌립니다.
        ///
        /// <b>되돌리지 않으면</b> 재생이 끝난 뒤 <c>RenderSettings.skybox</c> 가 파괴된 복제본을
        /// 가리켜 씬이 더러워지고, 그 상태로 저장하면 씬 파일의 스카이박스 참조가 깨집니다.
        /// </summary>
        void OnDisable()
        {
            if (!Application.isPlaying) return;

            if (skyboxBeforePlay != null) RenderSettings.skybox = skyboxBeforePlay;

            ReleaseClone();

            skySource = null;
            skyboxBeforePlay = null;
        }

        /// <summary>
        /// 매 프레임 하늘·주변광·해를 지금 시각에 맞춥니다.
        /// 편집 중에도 돌게 두어 인스펙터에서 값을 바꾸면 바로 보이게 합니다.
        /// </summary>
        void LateUpdate()
        {
            if (activeSky == null) ResolveReferences();

            float daylight = clock.Daylight;

            ApplySky(daylight);
            ApplySun(daylight);
            ApplyAmbient(daylight);
        }

        // --- Private Methods ---

        /// <summary>
        /// 비어 있는 참조를 채웁니다.
        /// </summary>
        private void ResolveReferences()
        {
            UseSky(skyMaterial != null ? skyMaterial : RenderSettings.skybox);

            if (sun == null && sunSource != null) sun = sunSource.Sun;
            if (sun == null) sun = RenderSettings.sun;
        }

        /// <summary>
        /// 조작할 하늘을 정합니다. 원본이 그대로면 아무 일도 하지 않습니다.
        ///
        /// <b>재생 중에는 에셋이 아니라 복제본에 씁니다.</b> 에셋에 직접 쓰면 재생을 끝내도
        /// 값이 되돌아가지 않아 <c>.mat</c> 이 밤값으로 굳습니다. 실제로 그렇게 굳은 적이
        /// 있습니다 — 절차 하늘 재질에 한밤의 <c>_DayFactor</c> 가 저장돼 git 에 잡혔습니다.
        ///
        /// 편집 중에는 복제하지 않습니다. 그래야 인스펙터에서 값을 만지면 바로 보입니다.
        /// 대신 편집 중에는 시계가 <see cref="NullGameClock"/>(한낮)이라 낮값만 쓰이므로
        /// 밤값이 에셋에 남지 않습니다.
        /// </summary>
        /// <param name="source">쓰려는 하늘 머티리얼 에셋</param>
        private void UseSky(Material source)
        {
            if (source == null)
            {
                activeSky = null;
                return;
            }

            if (!Application.isPlaying)
            {
                skySource = source;
                activeSky = source;
                return;
            }

            if (activeSky != null && skySource == source) return;

            if (skyboxBeforePlay == null) skyboxBeforePlay = RenderSettings.skybox;

            ReleaseClone();

            skySource = source;
            activeSky = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
            RenderSettings.skybox = activeSky;
        }

        /// <summary>복제해 둔 하늘을 버립니다.</summary>
        private void ReleaseClone()
        {
            if (activeSky == null) return;
            if (activeSky.hideFlags != HideFlags.HideAndDontSave) return;

            if (Application.isPlaying) Destroy(activeSky);
            else DestroyImmediate(activeSky);

            activeSky = null;
        }

        /// <summary>
        /// 하늘 머티리얼에 낮 정도와 해 방향, 구름 가림을 넣습니다.
        /// </summary>
        /// <param name="daylight">0이면 한밤, 1이면 한낮</param>
        private void ApplySky(float daylight)
        {
            // 밤이면 별이 찍힌 하늘로, 낮이면 원래 하늘로 갈아 끼웁니다.
            // 갈아 끼우는 것은 원본이 바뀔 때뿐이라 매 프레임 비용은 없습니다.
            UseSky(SelectSkySource(daylight));

            if (activeSky == null) return;

            // 사진 기반 하늘이면 노출과 색으로 밤낮을 만듭니다.
            ApplyPhotoSky(daylight);

            if (!activeSky.HasProperty(DayFactorId)) return;

            activeSky.SetFloat(DayFactorId, daylight);

            // 해가 없으면 위쪽을 향한 것으로 둡니다. (여명과 해가 그려지지 않습니다)
            Vector3 toSun = sun != null ? -sun.transform.forward : Vector3.up;
            activeSky.SetVector(SunDirectionId, toSun);

            if (!activeSky.HasProperty(StarFadeId)) return;

            // 구름이 짙을수록 별을 가립니다. 날씨 시스템이 없으면 0이라 아무 일도 없습니다.
            // 날씨가 없으면 GetCloudCover 가 넘긴 기본값 0 을 그대로 돌려주므로 별이 가려지지 않습니다.
            float clouds = cloudsHideStars ? Mathf.Clamp01(skyConditions.GetCloudCover(0f)) : 0f;
            activeSky.SetFloat(StarFadeId, clouds);
        }

        /// <summary>
        /// 지금 시각에 쓸 하늘 <b>에셋</b>을 고릅니다.
        ///
        /// 밤 하늘이 지정돼 있고 충분히 어두우면 밤 하늘을, 아니면 낮 하늘을 돌려줍니다.
        /// </summary>
        /// <param name="daylight">0이면 한밤, 1이면 한낮</param>
        /// <returns>쓸 하늘 머티리얼 에셋</returns>
        private Material SelectSkySource(float daylight)
        {
            Material day = skyMaterial != null ? skyMaterial : skyboxBeforePlay;
            if (day == null) day = skySource != null ? skySource : RenderSettings.skybox;

            if (nightSkyMaterial != null && daylight < swapDaylight) return nightSkyMaterial;

            return day;
        }

        /// <summary>
        /// 사진 기반 하늘(Skybox/Cubemap · Panoramic)의 노출과 색을 시각에 맞춥니다.
        ///
        /// <b>낮과 밤이 서로 다른 사진입니다.</b> 낮 사진을 어둡게 해 봐야 별은 생기지 않습니다.
        /// 사진에 없는 것은 노출로 꺼낼 수 없기 때문입니다. 그래서 밤에는
        /// <see cref="nightSkyMaterial"/>로 갈아 끼우고, 거기 이미 밝기 값으로 들어 있는
        /// 은하수와 별을 <see cref="nightSkyExposure"/>로 끌어올립니다.
        ///
        /// 두 곡선은 <see cref="swapDaylight"/>에서 <b>둘 다 노출 0</b>으로 만납니다.
        /// 그래서 갈아 끼우는 프레임에 양쪽이 똑같이 캄캄해 팝이 보이지 않습니다.
        /// </summary>
        /// <param name="daylight">0이면 한밤, 1이면 한낮</param>
        private void ApplyPhotoSky(float daylight)
        {
            if (!activeSky.HasProperty(ExposureId)) return;

            bool night = nightSkyMaterial != null && daylight < swapDaylight;

            if (night)
            {
                // 깊어질수록 별이 밝아집니다. 교체 지점에서 0 이라 낮 곡선과 이어집니다.
                float depth = 1f - Mathf.InverseLerp(0f, swapDaylight, daylight);
                float exposure = Mathf.Lerp(0f, nightSkyExposure, depth);

                // 구름이 짙으면 별을 지웁니다. 절차 하늘의 _StarFade 가 하던 일을
                // 사진 하늘에서는 노출을 깎아 잇습니다.
                if (cloudsHideStars)
                {
                    float clouds = Mathf.Clamp01(skyConditions.GetCloudCover(0f));
                    exposure *= Mathf.Lerp(1f, 0.15f, clouds);
                }

                activeSky.SetFloat(ExposureId, exposure);

                // ⚠ 밤 하늘에는 푸른 색을 씌우지 않습니다. 씌우면 별빛까지 물들어 탁해집니다.
                // 밤 분위기는 주변광과 안개색이 이미 만들고 있습니다.
                if (activeSky.HasProperty(TintId)) activeSky.SetColor(TintId, Color.white);

                return;
            }

            // 해가 낮을수록 어두워지되, <b>제곱은 너무 가팔랐습니다.</b>
            // 제곱을 쓰면 해가 아직 떠 있는 노을 무렵에 하늘만 새까맣게 죽어서,
            // 들판은 노을빛으로 물들었는데 그 위가 한밤인 이상한 그림이 나왔습니다.
            // 0.75 제곱은 한낮을 그대로 두면서 저녁 하늘을 살려 둡니다.
            float dayDepth = nightSkyMaterial != null
                ? Mathf.InverseLerp(swapDaylight, 1f, daylight)
                : daylight;
            float curve = Mathf.Pow(dayDepth, 0.75f);

            // 밤 하늘이 있으면 교체 지점에서 0 으로 만나야 팝이 없습니다.
            // 없으면 예전처럼 nightExposure 까지만 어두워집니다.
            float floorExposure = nightSkyMaterial != null ? 0f : nightExposure;

            activeSky.SetFloat(ExposureId, Mathf.Lerp(floorExposure, dayExposure, curve));

            if (!activeSky.HasProperty(TintId)) return;

            activeSky.SetColor(TintId, Color.Lerp(nightSkyTint, Color.white, curve));
        }

        /// <summary>
        /// 해의 색과 세기를 시간대에 맞춥니다. 밤에는 달빛으로 넘깁니다.
        /// </summary>
        /// <param name="daylight">0이면 한밤, 1이면 한낮</param>
        private void ApplySun(float daylight)
        {
            if (sun == null) return;

            // TimeSystem이 이미 세기를 DaylightFactor에 비례해 낮춰 둡니다.
            // 여기서는 그 위에 색과 밤의 하한만 얹습니다.
            float dusk = Mathf.Clamp01(1f - Mathf.Abs(daylight - 0.5f) * 2.2f);

            Color lit = Color.Lerp(moonLightColor, dayLightColor, daylight);
            sun.color = Color.Lerp(lit, duskLightColor, dusk * daylight);

            // 구름이 끼면 <b>햇빛 자체가</b> 가려집니다.
            //
            // 주변광만 낮추면 그늘만 어두워지고 볕은 그대로라, 흐린 날인데도
            // 지면에 쨍한 볕이 남아 이상해 보입니다. 해에도 같은 어둡기를 먹입니다.
            // 날씨가 없으면 Darkness 가 0 이라 weather 는 1 로 남습니다.
            float weather = Mathf.Lerp(1f, weatherSunFloor, Mathf.Clamp01(skyConditions.Darkness));

            // ⚠ <b>우리가 쓴 자리인지 먼저 봅니다.</b> 아래에서 해를 돌려 달로 쓰는데,
            // 그 결과를 다시 읽어 또 돌리면 값이 <b>프레임마다 밀립니다.</b> 시간이
            // 멈춰 TimeSystem 이 자리를 안 써 주는 동안 특히 그렇습니다.
            // 우리가 마지막으로 쓴 것과 같으면, 진짜 해의 자리는 기억해 둔 쪽입니다.
            if (sun.transform.rotation != sunWritten) sunBase = sun.transform.rotation;

            // 밤에 완전히 꺼 버리면 헤드라이트 밖이 아무것도 보이지 않습니다.
            // 약한 달빛만 남깁니다. ⚠ <b>지평선 근처에서는 그 하한도 물러섭니다</b> —
            // 아래에서 방향을 접을 때 세기가 남아 있으면 그림자가 휩쓸립니다.
            float max = sunSource != null ? sunSource.SunMaxIntensity : 1f;
            float up = (sunBase * Vector3.forward).y;
            float sunLevel = max * daylight * weather;
            float night = moonIntensity * NightShare(up, nightHandoverDegrees);

            sun.intensity = Mathf.Max(sunLevel, night);
            sun.enabled = true;

            // ⚠ <b>지평선 아래로 내려간 해를 그 자리에서 비추게 두면 안 됩니다.</b>
            //
            // 이 씬의 빛은 하루를 한 바퀴 도는 방향광 <b>하나뿐</b>입니다(TimeSystem 이
            // <c>Euler(분/하루 × 360 − 90, 170, 0)</c> 로 돌립니다). 밤에는 그 빛이
            // <b>땅 밑에서 위로</b> 올라옵니다. 그러면 바닥은 N·L 이 음수라 통째로
            // 그늘이 되고 <b>벽면만</b> 낮은 해처럼 밝게 섭니다 — 밤인데 새벽 볕이
            // 든 것처럼 보이는 이유가 이것이었습니다.
            //
            // 실측(2026-09-11, 02시): 건물 벽이 한낮의 <b>61%</b> 인데 땅은 8%,
            // 나무는 10%, 하늘은 8% 였습니다. 벽만 낮에 남아 있었습니다.
            //
            // ⚠ <b>해의 반대편으로 돌리지 않습니다.</b> 그것은 달을 그리는 방법인데
            // <b>이 세계에는 하늘이 없습니다</b> — 머리 위는 거대구조물이고 하늘처럼
            // 보이는 것은 그 천장을 구운 파노라마입니다. 밤에 남는 빛은 그 <b>천장의
            // 등불</b>이므로, 위에서 내려오는 <b>고정된 자리</b>가 맞습니다.
            //
            // ⚠ <b>한 번에 옮기지 않습니다.</b> 지평선에서 자리가 튀면 그림자가 세계를
            // 휩씁니다. <see cref="NightFold"/> 가 정하는 만큼 천천히 건너갑니다 —
            // 해가 아직 밝으면 해의 자리, 다 잦아들면 천장의 자리입니다.
            float fold = NightFold(up, nightHandoverDegrees, sunLevel, moonIntensity);

            Quaternion ceiling = Quaternion.Euler(ceilingPitch, ceilingYaw, 0f);

            sun.transform.rotation = fold <= 0f
                ? sunBase
                : Quaternion.Slerp(sunBase, ceiling, fold);

            sunWritten = sun.transform.rotation;
        }

        /// <summary>
        /// 밤빛이 <b>얼마나 섰는가</b>입니다. 0 이면 해가 지평선 위에 있거나 막 그 언저리라
        /// 밤빛이 없는 것과 같고, 1 이면 온전한 밤빛입니다.
        ///
        /// <b>왜 갈라 두는가.</b> 이 값 하나가 밤빛의 <b>세기와 방향을 함께</b> 정합니다.
        /// 둘이 어긋나면 세기가 남은 채 자리만 옮겨 그림자가 휩쓸립니다. 씬도 빛도 없이
        /// 확인할 수 있어야 하는 규칙이라 밖으로 꺼내 둡니다.
        /// </summary>
        /// <param name="forwardUp">빛이 나아가는 방향의 y 성분. 양수면 해가 지평선 아래입니다</param>
        /// <param name="riseDegrees">밤빛이 온전해지는 깊이(도). 0 이하면 지평선에서 바로 섭니다</param>
        /// <returns>0~1</returns>
        public static float NightShare(float forwardUp, float riseDegrees)
        {
            // 해가 지평선 위에 있으면(빛이 내려오면) 밤빛은 없습니다.
            if (forwardUp <= 0f) return 0f;

            float dip = Mathf.Asin(Mathf.Clamp01(forwardUp)) * Mathf.Rad2Deg;

            if (riseDegrees <= 0f) return 1f;

            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dip / riseDegrees));
        }

        /// <summary>
        /// 빛의 자리를 <b>천장 쪽으로 얼마나 옮길지</b>입니다. 0 이면 해가 있는 그대로,
        /// 1 이면 온전히 천장의 자리입니다.
        ///
        /// <b>왜 세기까지 보는가.</b> 이 세계의 밝기 곡선은 19시에도 0.3 인데 해는
        /// 이미 지평선 <b>15도 아래</b>입니다. 기하만 보고 옮기면 노을이 진 쪽이 아니라
        /// <b>천장에서</b> 빛이 들어옵니다. 해가 아직 밤빛보다 세면 해가 주인입니다.
        ///
        /// 둘 사이는 <b>이어집니다.</b> 해가 밤빛 세기까지 잦아드는 동안 자리가 천천히
        /// 건너가고, 그 구간의 세기는 밤빛 하한 이하라 그림자가 휩쓸리지 않습니다.
        /// </summary>
        /// <param name="forwardUp">빛이 나아가는 방향의 y 성분. 양수면 해가 지평선 아래입니다</param>
        /// <param name="riseDegrees">달빛이 온전해지는 깊이(도)</param>
        /// <param name="sunLevel">지금 해가 내는 세기</param>
        /// <param name="nightLevel">밤빛의 하한(<see cref="moonIntensity"/>)</param>
        /// <returns>0~1</returns>
        public static float NightFold(float forwardUp, float riseDegrees,
                                      float sunLevel, float nightLevel)
        {
            float rise = NightShare(forwardUp, riseDegrees);
            if (rise <= 0f || nightLevel <= 0f) return 0f;

            float share = 1f - Mathf.Clamp01(sunLevel / nightLevel);

            return rise * Mathf.SmoothStep(0f, 1f, share);
        }


        /// <summary>
        /// 주변광을 시간대에 맞춥니다.
        /// </summary>
        /// <param name="daylight">0이면 한밤, 1이면 한낮</param>
        private void ApplyAmbient(float daylight)
        {
            if (!driveAmbient) return;

            // 하늘색을 그대로 주변광으로 쓰면(Skybox 모드) 별빛까지 섞여 밤이 이상하게 밝아집니다.
            // 위아래를 직접 정하는 Gradient가 다루기 쉽습니다.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            // 높이 그라데이션도 시간대를 따릅니다. 이것은 <b>주변광이 아니라</b>
            // 조명 뒤에 얹히는 색이라, 여기서 눌러 주지 않으면 밤이 오지 않습니다.
            Shader.SetGlobalFloat(HeightScaleId, Mathf.Lerp(nightHeightScale, 1f, daylight));

            Color sky = Color.Lerp(nightAmbientSky, dayAmbientSky, daylight);

            // 궂은 날씨는 낮에도 하늘을 덮습니다.
            //
            // 이 처리를 여기서 하는 이유가 있습니다. WeatherRig에도 주변광을 낮추는 기능이
            // 있지만(controlAmbient), 둘 다 켜면 매 프레임 서로의 값을 덮어써서 실행 순서에 따라
            // 결과가 달라집니다. 그래서 <b>주변광의 주인은 이 컴포넌트 하나</b>로 정하고
            // WeatherRig의 controlAmbient는 꺼 둡니다.
            // 날씨가 없으면 Darkness 가 0 이라 배율이 1 이 되어 아무 일도 하지 않습니다.
            sky *= Mathf.Lerp(1f, weatherDarkFloor, Mathf.Clamp01(skyConditions.Darkness));

            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = sky * 0.7f;
            RenderSettings.ambientGroundColor = sky * groundAmbientScale;
        }
    }
}
