using UnityEngine;

namespace CarDrive.Systems
{
    /// <summary>
    /// 손그림 빗금(Tonal Art Map)의 텍스처와 수치를 <b>세계 전체에 하나로</b> 넣습니다.
    ///
    /// <b>재질마다 설정하지 않습니다.</b> 빗금은 "이 화면을 누가 그렸는가"에 대한 답이라,
    /// 땅과 풀과 건물과 로봇이 서로 다른 획으로 그어져 있으면 <b>같은 그림으로 읽히지 않습니다.</b>
    /// 그래서 여기서 한 번만 <c>Shader.SetGlobal</c> 로 넣습니다.
    /// (<see cref="CloudShadows"/> 가 구름을, <see cref="Grass.GrassPushField"/> 가 풀 밀림
    /// 좌표를 넣는 것과 같은 방식입니다)
    ///
    /// <b>이 컴포넌트가 없으면 빗금이 그려지지 않습니다.</b> 절차적으로 긋던 때는 수치만
    /// 있으면 됐지만 TAM 은 텍스처가 있어야 하고, 없는 텍스처를 샘플하면 플랫폼마다 다른
    /// 색이 나옵니다. 그래서 셰이더가 아예 건너뛰도록 해 두었습니다.
    ///
    /// 어느 재질에 빗금을 걸지는 재질의 <c>_HATCHING</c> 토글이 정하고,
    /// 이 컴포넌트는 <b>무엇으로 어떻게 그을지</b>만 공급합니다.
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
                 "밝은 쪽과 <b>같은 그림체</b>로 짝을 맞추세요. 섞으면 밝기가 바뀔 때 획이 갈립니다.")]
        public Texture2D tamDark;

        /// <summary>
        /// 획 한 판이 덮는 거리(m)입니다.
        ///
        /// <b>월드 좌표로 긋습니다.</b> 그래서 이 값이 곧 획의 크기입니다 — 작게 잡으면
        /// 촘촘한 펜 그림, 크게 잡으면 성긴 스케치가 됩니다.
        ///
        /// UV 가 아니라 월드인 이유는 이 프로젝트의 사정 때문입니다. 터레인은 100m 타일에
        /// 0~1 UV 라 획이 100m 로 늘어나고, 풀은 잎마다 쓸 만한 UV 가 없습니다.
        /// 월드로 하면 땅·풀·건물이 <b>같은 크기의 획</b>을 받습니다.
        /// </summary>
        [Header("획")]
        [Tooltip("획 한 판이 덮는 거리(m). 월드 좌표로 긋기 때문에 이 값이 곧 획의 크기입니다. " +
                 "작으면 촘촘한 펜, 크면 성긴 스케치.")]
        [Range(0.2f, 12f)]
        public float scale = 1.5f;

        /// <summary>빗금이 얼마나 진하게 얹힐지입니다.</summary>
        [Tooltip("빗금의 세기. 0 이면 없고, 1 이면 TAM 그대로 밑색에 곱합니다. " +
                 "낮추면 밑색이 비쳐 연필에 가까워집니다.")]
        [Range(0f, 1f)]
        public float strength = 1f;

        /// <summary>
        /// 이 밝기부터 빗금이 시작됩니다. 위쪽은 종이 그대로 남습니다.
        ///
        /// 높이면 밝은 면까지 획이 올라와 화면이 빽빽해지고, 낮추면 그늘에만 획이 남아
        /// 담백해집니다. <b>이 값과 세기를 함께 만지세요</b> — 둘 다 올리면 금세 새까매집니다.
        /// </summary>
        [Tooltip("이 밝기부터 빗금이 시작됩니다. 위쪽은 종이 그대로. " +
                 "높이면 밝은 면까지 획이 올라오고, 낮추면 그늘에만 남습니다.")]
        [Range(0.2f, 1f)]
        public float startTone = 0.75f;

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

        /// <summary>인스펙터에서 값을 만지면 재생 중이 아니어도 바로 보이게 합니다.</summary>
        void OnValidate()
        {
            if (isActiveAndEnabled) Apply();
        }

        /// <summary>꺼지면 빗금도 함께 사라져야 합니다. 켜고 끄는 것으로 비교할 수 있게.</summary>
        void OnDisable()
        {
            Shader.SetGlobalVector(ParamsId, new Vector4(scale, strength, startTone, 0f));
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

            Shader.SetGlobalVector(ParamsId, new Vector4(scale, strength, startTone, ready ? 1f : 0f));
        }
    }
}
