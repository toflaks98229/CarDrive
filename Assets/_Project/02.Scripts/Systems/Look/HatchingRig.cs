using UnityEngine;

namespace CarDrive.Systems
{
    /// <summary>
    /// 손그림 빗금(Tonal Art Map) 텍스처를 <b>세계 전체에 하나로</b> 넣습니다.
    ///
    /// <b>⚠ 월드 표면의 음영에는 더 이상 쓰지 않습니다.</b> 예전에는 땅·건물·차의 그늘을
    /// 이 획으로 그렸지만, 화면 후처리(<c>CarDrivePalette</c>)의 획 판이 그 일을 가져갔습니다.
    /// 재질의 <c>_HATCHING</c> 토글과 조명 쪽 호출은 모두 걷어냈습니다.
    ///
    /// 지금 남은 쓰임은 <b>테두리를 갉는 것</b> 둘뿐입니다.
    /// <list type="bullet">
    /// <item>오줌 자국의 너덜너덜한 가장자리(<c>CarDriveSplat</c> · <c>CarDriveSplatMap</c>)</item>
    /// <item>니즈 게이지가 차오른 자리의 경계(<c>CarDriveNeedGauge</c>)</item>
    /// </list>
    /// 둘 다 <b>획의 크기를 스스로 정합니다.</b> 그래서 여기서 공급하는 것은 텍스처 두 장과
    /// "쓸 수 있는가" 하는 깃발뿐입니다.
    ///
    /// <b>이 컴포넌트가 없으면 그 둘은 디더로 물러섭니다.</b> TAM 은 기본값으로 물러설 수
    /// 없어서입니다 — 바인딩되지 않은 텍스처를 샘플하면 플랫폼마다 다른 색이 나오므로,
    /// 셰이더가 깃발을 보고 아예 다른 길로 갑니다.
    ///
    /// <b>TAM 출처.</b> nkihrk 의 HatchingShader (MIT) 입니다.
    /// 라이선스 전문은 <c>Assets/_Project/04.Art/01.Images/Hatching/LICENSE-nkihrk.txt</c>.
    /// </summary>
    [DefaultExecutionOrder(400)]
    [AddComponentMenu("CarDrive/손그림 빗금 (HatchingRig)")]
    public class HatchingRig : MonoBehaviour
    {
        // --- Constants ---

        private static readonly int BrightId = Shader.PropertyToID("_CarDriveTamBright");
        private static readonly int DarkId = Shader.PropertyToID("_CarDriveTamDark");
        private static readonly int ParamsId = Shader.PropertyToID("_CarDriveHatchParams");

        // --- Public Member Variables ---

        /// <summary>밝은 쪽 세 단계입니다. R·G·B 채널에 하나씩 들어 있습니다.</summary>
        [Header("TAM (밝기 단계별 획 그림)")]
        [Tooltip("밝은 쪽 세 단계(0·1·2)가 R·G·B 채널에 들어 있는 텍스처. " +
                 "Hatching 폴더의 TAM_comic_bright 또는 TAM_pencil_bright 를 넣으세요.")]
        public Texture2D tamBright;

        /// <summary>어두운 쪽 세 단계입니다.</summary>
        [Tooltip("어두운 쪽 세 단계(3·4·5)가 R·G·B 채널에 들어 있는 텍스처. " +
                 "밝은 쪽과 같은 그림체로 짝을 맞추세요. 섞으면 밝기가 바뀔 때 획이 갈립니다.")]
        public Texture2D tamDark;

        // --- Unity Event Functions ---

        void OnEnable()
        {
            Apply();
        }

        /// <summary>
        /// 매 프레임 넣습니다.
        ///
        /// 값이 자주 바뀌지는 않지만, 다른 곳에서 같은 전역을 건드렸을 때
        /// <b>다음 프레임에 저절로 되돌아오게</b> 하려는 것입니다. 비용은 무시할 수 있습니다.
        /// </summary>
        void Update()
        {
            Apply();
        }

        /// <summary>인스펙터에서 텍스처를 갈아 끼우면 재생 중이 아니어도 바로 보이게 합니다.</summary>
        void OnValidate()
        {
            if (isActiveAndEnabled) Apply();
        }

        /// <summary>꺼지면 획도 함께 사라져야 합니다. 켜고 끄는 것으로 비교할 수 있게.</summary>
        void OnDisable()
        {
            Shader.SetGlobalVector(ParamsId, Vector4.zero);
        }

        // --- Private Methods ---

        /// <summary>지금 값을 셰이더 전역에 넣습니다.</summary>
        private void Apply()
        {
            // 텍스처가 한 장이라도 비어 있으면 켜지 않습니다.
            // 셰이더가 없는 텍스처를 샘플하면 플랫폼마다 다른 색이 나오기 때문입니다.
            bool ready = tamBright != null && tamDark != null;

            if (ready)
            {
                Shader.SetGlobalTexture(BrightId, tamBright);
                Shader.SetGlobalTexture(DarkId, tamDark);
            }

            // x·y·z 는 월드 음영이 쓰던 자리입니다. 읽는 쪽이 없어져 0 으로 둡니다.
            Shader.SetGlobalVector(ParamsId, new Vector4(0f, 0f, 0f, ready ? 1f : 0f));
        }
    }
}
