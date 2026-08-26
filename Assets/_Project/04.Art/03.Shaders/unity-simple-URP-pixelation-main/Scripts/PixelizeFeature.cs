using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CarDrive.Rendering
{
    /// <summary>
    /// 화면 전체를 <b>낮은 해상도로 한 번 접었다 펴서</b> 픽셀 그림처럼 만드는 URP 렌더러 기능입니다.
    ///
    /// 카메라를 두 대 두고 저해상도 렌더 텍스처를 거치는 흔한 방법 대신, 렌더러 기능 하나로
    /// 처리합니다. 실제 축소·확대는 <see cref="PixelizePass"/> 가 맡고 이 클래스는
    /// <b>설정을 담아 패스를 만들고 큐에 넣는 일</b>만 합니다.
    ///
    /// <b>씬 뷰에서는 걸지 않습니다.</b> 편집 중에 화면이 뭉개지면 물체를 집기 어렵기 때문입니다.
    ///
    /// <b>출처.</b> itsPeetah 의 unity-simple-URP-pixelation 입니다. 원본은 구형 Blit API 를
    /// 쓰지만 이 프로젝트에서는 RenderGraph 로 옮겨 두었습니다.
    /// 원본 설명은 같은 폴더의 <c>README.md</c> 를 보세요.
    /// </summary>
    public class PixelizeFeature : ScriptableRendererFeature
    {
        // --- Public Types ---

        /// <summary>
        /// 인스펙터에 노출되는 픽셀화 설정입니다.
        ///
        /// <see cref="PixelizePass"/> 생성자에 <b>참조로 넘어가므로</b>, 여기서 값을 바꾸면
        /// 패스를 다시 만들지 않아도 다음 프레임부터 반영됩니다.
        /// </summary>
        [System.Serializable]
        public class CustomPassSettings
        {
            /// <summary>
            /// 픽셀화를 끼워 넣을 시점입니다.
            ///
            /// 기본값은 후처리 직전입니다. 블룸 같은 후처리가 <b>픽셀 블록 위에</b> 얹히도록
            /// 하려는 것으로, 더 뒤로 미루면 후처리 결과까지 다시 뭉개집니다.
            /// </summary>
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            /// <summary>
            /// 목표 세로 해상도(픽셀)입니다. 곧 <b>한 화면에 들어갈 픽셀 블록의 줄 수</b>입니다.
            ///
            /// 가로는 카메라 종횡비로 계산하므로 여기서 정하지 않습니다.
            /// 낮출수록 블록이 커져 거칠어지고, 올릴수록 원본에 가까워집니다.
            /// </summary>
            public int screenHeight = 144;
        }

        // --- Private Member Variables ---

        /// <summary>인스펙터에서 정한 설정입니다. 생성자를 통해 패스와 공유합니다.</summary>
        [SerializeField] private CustomPassSettings settings;

        /// <summary>실제로 축소·확대를 수행하는 패스입니다. <see cref="Create"/> 에서 한 번 만듭니다.</summary>
        private PixelizePass customPass;

        // --- Public Methods ---

        /// <summary>
        /// 렌더러 기능이 만들어지거나 인스펙터 값이 바뀔 때 호출되어 패스를 새로 준비합니다.
        /// </summary>
        public override void Create()
        {
            customPass = new PixelizePass(settings);
        }

        /// <summary>
        /// 카메라마다 픽셀화 패스를 렌더러 큐에 넣습니다.
        /// </summary>
        /// <param name="renderer">패스를 등록할 대상 렌더러입니다.</param>
        /// <param name="renderingData">지금 그리는 카메라의 정보입니다. 씬 뷰 판별에 씁니다.</param>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
#if UNITY_EDITOR
            if (renderingData.cameraData.isSceneViewCamera) return;
#endif
            renderer.EnqueuePass(customPass);
        }
    }
}
