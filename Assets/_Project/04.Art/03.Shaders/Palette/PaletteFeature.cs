// PaletteFeature.cs

using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CarDrive.Rendering
{
    /// <summary>
    /// 화면의 색 수를 줄여 <b>제한된 팔레트로 칠한 그림</b>처럼 보이게 하는 URP 렌더러 기능입니다.
    ///
    /// 실제 색 감축은 <see cref="PalettePass"/> 가 맡고, 이 클래스는 <b>설정을 담아 패스를 만들고
    /// 큐에 넣는 일</b>만 합니다. <see cref="PixelizeFeature"/> 와 짝을 이루도록 만들어졌습니다 —
    /// 해상도는 저쪽이 줄이고 색은 이쪽이 줄입니다.
    ///
    /// <b>씬 뷰에서는 걸지 않습니다.</b> 편집 중에는 원래 색을 봐야 재질을 맞출 수 있기 때문입니다.
    /// </summary>
    public class PaletteFeature : ScriptableRendererFeature
    {
        // --- Public Types ---

        /// <summary>
        /// 인스펙터에 노출되는 색 감축 설정입니다.
        ///
        /// <see cref="PalettePass"/> 생성자에 <b>참조로 넘어가므로</b>, 여기서 값을 바꾸면
        /// 패스를 다시 만들지 않아도 다음 프레임부터 반영됩니다.
        /// </summary>
        [System.Serializable]
        public class PaletteSettings
        {
            /// <summary>
            /// 색을 어떤 방식으로 줄일지 고르는 갈래입니다.
            ///
            /// 셰이더의 <c>_METHOD_*</c> 키워드와 하나씩 짝을 이루며,
            /// 고른 갈래에 해당하는 설정만 실제로 쓰입니다.
            /// </summary>
            public enum Method
            {
                /// <summary>밝기를 <see cref="levels"/> 단계로 잘라 계단처럼 만듭니다.</summary>
                LuminanceQuantize,

                /// <summary>가장 가까운 색을 <see cref="paletteTexture"/> 에서 찾아 바꿔칩니다.</summary>
                PaletteMap,

                /// <summary>단계로 자르되 디더 무늬를 섞어 경계의 띠를 흩뜨립니다.</summary>
                DitheredQuantize
            }

            // --- Public Member Variables : 공통 ---

            /// <summary>
            /// 색 감축을 끼워 넣을 시점입니다.
            ///
            /// 기본값은 후처리 직전입니다. 후처리가 <b>줄어든 색 위에</b> 얹히도록 하려는 것으로,
            /// 더 뒤로 미루면 후처리가 만든 중간색이 그대로 남습니다.
            /// </summary>
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            /// <summary>
            /// 효과의 세기입니다. 0 이면 원본 그대로, 1 이면 감축한 색을 그대로 씁니다.
            ///
            /// 중간값은 원본과 섞으므로, 낮추면 색 계단이 부드러워집니다.
            /// </summary>
            [Range(0.0f, 1.0f)]
            public float strength = 1.0f;

            /// <summary>색을 줄이는 방식입니다. 아래 설정 중 어느 묶음이 쓰일지를 정합니다.</summary>
            [Header("Color Reduction Method")]
            public Method method = Method.LuminanceQuantize;

            // --- Public Member Variables : 밝기 단계 나누기 ---

            /// <summary>
            /// 밝기를 몇 단계로 자를지입니다. 낮출수록 계단이 굵어져 포스터처럼 됩니다.
            ///
            /// <see cref="Method.LuminanceQuantize"/> 와 <see cref="Method.DitheredQuantize"/> 에서 쓰입니다.
            /// </summary>
            [Header("Luminance Quantization")]
            [Range(2, 256)]
            public int levels = 16;

            /// <summary>
            /// 어두운 톤과 밝은 톤을 가르는 기준입니다.
            ///
            /// 이 값을 넘는 밝기는 밝은 쪽으로, 아래는 어두운 쪽으로 묶여 처리됩니다.
            /// 올리면 밝은 면까지 어두운 쪽 취급을 받아 화면이 무거워집니다.
            /// </summary>
            [Range(0.0f, 1.0f)]
            public float toneThreshold = 0.5f;

            // --- Public Member Variables : 팔레트 대응 ---

            /// <summary>
            /// 쓸 색이 가로로 늘어선 1D 팔레트 텍스처입니다.
            ///
            /// <b>비어 있으면 셰이더에 넘기지 않습니다.</b> 없는 텍스처를 샘플하면
            /// 플랫폼마다 다른 색이 나오기 때문입니다.
            /// </summary>
            [Header("Palette Mapping")]
            public Texture2D paletteTexture;

            /// <summary>
            /// 팔레트 텍스처에서 실제로 읽을 색의 개수입니다.
            ///
            /// 텍스처의 가로 픽셀 수와 <b>맞지 않으면</b> 엉뚱한 자리를 샘플해 색이 밀립니다.
            /// </summary>
            [Range(2, 256)]
            public int paletteColorCount = 16;

            // --- Public Member Variables : 디더링 ---

            /// <summary>
            /// 디더 무늬의 세기입니다. 올릴수록 점 무늬가 뚜렷해지는 대신 단계 사이의 띠가 옅어집니다.
            ///
            /// <see cref="Method.DitheredQuantize"/> 에서만 쓰입니다.
            /// </summary>
            [Header("Dithering")]
            [Range(0.0f, 1.0f)]
            public float ditherStrength = 0.5f;
        }

        // --- Public Member Variables ---

        /// <summary>인스펙터에서 정한 설정입니다. 생성자를 통해 패스와 공유합니다.</summary>
        [SerializeField] public PaletteSettings settings;

        // --- Private Member Variables ---

        /// <summary>실제로 색을 줄이는 패스입니다. <see cref="Create"/> 에서 한 번 만듭니다.</summary>
        private PalettePass palettePass;

        // --- Public Methods ---

        /// <summary>
        /// 렌더러 기능이 만들어지거나 인스펙터 값이 바뀔 때 호출되어 패스를 새로 준비합니다.
        /// </summary>
        public override void Create()
        {
            palettePass = new PalettePass(settings);
        }

        /// <summary>
        /// 카메라마다 색 감축 패스를 렌더러 큐에 넣습니다.
        /// </summary>
        /// <param name="renderer">패스를 등록할 대상 렌더러입니다.</param>
        /// <param name="renderingData">지금 그리는 카메라의 정보입니다. 씬 뷰 판별에 씁니다.</param>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
#if UNITY_EDITOR
            if (renderingData.cameraData.isSceneViewCamera) return;
#endif
            renderer.EnqueuePass(palettePass);
        }
    }
}
