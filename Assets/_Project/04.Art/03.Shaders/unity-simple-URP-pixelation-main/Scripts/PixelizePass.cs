
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class PixelizePass : ScriptableRenderPass
{
    private const string k_PassName = "Pixelize Pass";

    private PixelizeFeature.CustomPassSettings settings;

    private Material material;
    private int pixelScreenHeight, pixelScreenWidth;

    public PixelizePass(PixelizeFeature.CustomPassSettings settings)
    {
        this.settings = settings;
        this.renderPassEvent = settings.renderPassEvent;
        if (material == null) material = CoreUtils.CreateEngineMaterial("Hidden/Pixelize");

        // 카메라 컬러를 입력 텍스처로 읽으려면 중간 텍스처가 필요하다. (백버퍼는 입력으로 쓸 수 없음)
        requiresIntermediateTexture = true;
    }

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
