using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace CarDrive.Rendering
{
    /// <summary>
    /// 카메라 컬러를 저해상도 버퍼로 <b>한 번 줄였다가 다시 키워</b> 픽셀 블록을 만드는 패스입니다.
    ///
    /// <b>두 번의 Blit 이 각각 하는 일이 다릅니다.</b> 내려갈 때는 셰이더가 블록 중심 한 점만
    /// 골라 찍어 블록 안의 색을 하나로 정하고, 올라올 때는 <c>ClampNearest</c> 로 늘려
    /// 그 색이 블록 전체를 채우게 합니다. 어느 한쪽이라도 보간이 섞이면 <b>블록 경계가 흐려져</b>
    /// 픽셀 그림으로 읽히지 않습니다.
    ///
    /// 설정은 <see cref="PixelizeFeature"/> 가 넘겨준 객체를 그대로 들고 있으므로,
    /// 인스펙터에서 값을 바꾸면 다음 프레임에 반영됩니다.
    /// </summary>
    public class PixelizePass : ScriptableRenderPass
    {
        // --- Constants ---

        /// <summary>프로파일러와 RenderGraph 뷰어에 표시할 패스 이름입니다.</summary>
        private const string k_PassName = "Pixelize Pass";

        // --- Private Member Variables ---

        /// <summary>렌더러 기능에서 넘겨받은 설정입니다. 참조를 공유하므로 값 변경이 바로 보입니다.</summary>
        private PixelizeFeature.CustomPassSettings settings;

        /// <summary>블록 중심을 포인트 샘플링하는 <c>Hidden/Pixelize</c> 재질입니다.</summary>
        private Material material;

        /// <summary>이번 프레임의 저해상도 버퍼 세로 픽셀 수입니다. 설정값을 그대로 씁니다.</summary>
        private int pixelScreenHeight;

        /// <summary>이번 프레임의 저해상도 버퍼 가로 픽셀 수입니다. 세로에 카메라 종횡비를 곱해 구합니다.</summary>
        private int pixelScreenWidth;

        // --- Constructors ---

        /// <summary>
        /// 설정을 받아 패스 시점과 전용 재질을 준비합니다.
        /// </summary>
        /// <param name="settings">렌더러 기능이 인스펙터에 노출한 설정입니다.</param>
        public PixelizePass(PixelizeFeature.CustomPassSettings settings)
        {
            this.settings = settings;
            this.renderPassEvent = settings.renderPassEvent;
            if (material == null) material = CoreUtils.CreateEngineMaterial("Hidden/Pixelize");

            // 카메라 컬러를 입력 텍스처로 읽으려면 중간 텍스처가 필요하다. (백버퍼는 입력으로 쓸 수 없음)
            requiresIntermediateTexture = true;
        }

        // --- Public Methods ---

        /// <summary>
        /// 저해상도 버퍼로 내려갔다 올라오는 두 Blit 을 RenderGraph 에 기록하고,
        /// 이후 패스가 볼 카메라 컬러를 그 결과로 교체합니다.
        /// </summary>
        /// <param name="renderGraph">이번 프레임의 렌더 그래프입니다.</param>
        /// <param name="frameData">카메라와 렌더 타겟 정보를 담은 프레임 데이터입니다.</param>
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle source = resourceData.activeColorTexture;

            pixelScreenHeight = settings.screenHeight;
            pixelScreenWidth = (int)(pixelScreenHeight * cameraData.camera.aspect + 0.5f);

            material.SetVector("_BlockCount", new Vector2(pixelScreenWidth, pixelScreenHeight));
            material.SetVector("_BlockSize", new Vector2(1.0f / pixelScreenWidth, 1.0f / pixelScreenHeight));
            material.SetVector("_HalfBlockSize", new Vector2(0.5f / pixelScreenWidth, 0.5f / pixelScreenHeight));

            // 저해상도 중간 버퍼. 기존 GetTemporaryRT(descriptor, FilterMode.Point) 를 대체한다.
            TextureDesc pixelDesc = renderGraph.GetTextureDesc(source);
            pixelDesc.name = "_PixelBuffer";
            pixelDesc.sizeMode = TextureSizeMode.Explicit;
            pixelDesc.width = pixelScreenWidth;
            pixelDesc.height = pixelScreenHeight;
            pixelDesc.useDynamicScale = false;
            pixelDesc.msaaSamples = MSAASamples.None;
            pixelDesc.filterMode = FilterMode.Point;
            pixelDesc.clearBuffer = false;
            TextureHandle pixelBuffer = renderGraph.CreateTexture(pixelDesc);

            // 1) 원본 -> 저해상도 버퍼 (셰이더가 블록 중심 하나만 포인트 샘플링)
            RenderGraphUtils.BlitMaterialParameters downscale = new(source, pixelBuffer, material, 0);
            renderGraph.AddBlitPass(downscale, passName: k_PassName);

            // 2) 저해상도 버퍼 -> 새 카메라 컬러. ClampNearest 로 확대해야 픽셀 블록이 뭉개지지 않는다.
            TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = $"CameraColor-{k_PassName}";
            destinationDesc.clearBuffer = false;
            // 풀스크린 Blit 결과이므로 MSAA는 불필요하다. (품질 설정에 MSAA가 켜져 있어도 여기선 해제)
            destinationDesc.msaaSamples = MSAASamples.None;
            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            renderGraph.AddBlitPass(pixelBuffer, destination, Vector2.one, Vector2.zero,
                filterMode: RenderGraphUtils.BlitFilterMode.ClampNearest,
                passName: k_PassName + " (Upscale)");

            // 이후 패스들이 이 텍스처를 카메라 컬러로 사용하도록 교체한다.
            resourceData.cameraColor = destination;
        }
    }
}
