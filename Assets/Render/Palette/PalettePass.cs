// PalettePass.cs

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// '색상 보정' 효과를 적용하는 ScriptableRenderPass입니다.
/// </summary>
public class PalettePass : ScriptableRenderPass
{
    private PaletteFeature.PaletteSettings settings;
    private RenderTargetIdentifier colorBuffer;
    private RenderTargetHandle tempBuffer;
    private Material paletteMaterial;

    /// <summary>
    /// 생성자
    /// </summary>
    public PalettePass(PaletteFeature.PaletteSettings settings)
    {
        this.settings = settings;
        this.renderPassEvent = settings.renderPassEvent;
        tempBuffer.Init("_TempBuffer");

        // 셰이더 이름을 변경하지 말라는 요청에 따라 "Hidden/PixelizePalette"를 그대로 사용
        if (paletteMaterial == null)
            paletteMaterial = CoreUtils.CreateEngineMaterial("Hidden/PixelizePalette");
    }

    /// <summary>
    /// 카메라 렌더링 시작 전 리소스 설정
    /// </summary>
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        colorBuffer = renderingData.cameraData.renderer.cameraColorTarget;
        RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
        cmd.GetTemporaryRT(tempBuffer.id, descriptor, FilterMode.Bilinear);
    }

    /// <summary>
    /// 렌더링 로직 실행
    /// </summary>
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get("Palette Effect");
        using (new ProfilingScope(cmd, new ProfilingSampler("Palette")))
        {
            // [변경] 셰이더 키워드 설정을 3가지 모드를 모두 지원하도록 switch문으로 변경
            switch (settings.method)
            {
                case PaletteFeature.PaletteSettings.Method.LuminanceQuantize:
                    paletteMaterial.EnableKeyword("_METHOD_LUMINANCE_QUANTIZE");
                    paletteMaterial.DisableKeyword("_METHOD_PALETTE_MAP");
                    paletteMaterial.DisableKeyword("_METHOD_DITHERED_QUANTIZE");
                    break;
                case PaletteFeature.PaletteSettings.Method.PaletteMap:
                    paletteMaterial.DisableKeyword("_METHOD_LUMINANCE_QUANTIZE");
                    paletteMaterial.EnableKeyword("_METHOD_PALETTE_MAP");
                    paletteMaterial.DisableKeyword("_METHOD_DITHERED_QUANTIZE");
                    break;
                case PaletteFeature.PaletteSettings.Method.DitheredQuantize:
                    paletteMaterial.DisableKeyword("_METHOD_LUMINANCE_QUANTIZE");
                    paletteMaterial.DisableKeyword("_METHOD_PALETTE_MAP");
                    paletteMaterial.EnableKeyword("_METHOD_DITHERED_QUANTIZE");
                    break;
            }

            // 셰이더 프로퍼티 설정
            paletteMaterial.SetFloat("_Strength", settings.strength);
            paletteMaterial.SetFloat("_Levels", settings.levels);

            // [변경] 새로 추가된 프로퍼티 값을 셰이더로 전달
            paletteMaterial.SetFloat("_ToneThreshold", settings.toneThreshold);
            paletteMaterial.SetFloat("_DitherStrength", settings.ditherStrength);

            if (settings.paletteTexture != null)
            {
                paletteMaterial.SetTexture("_PaletteTex", settings.paletteTexture);
            }
            paletteMaterial.SetFloat("_PaletteSize", settings.paletteColorCount);

            // 원본(colorBuffer) -> 임시버퍼(tempBuffer)로 Blit하며 셰이더 적용
            Blit(cmd, colorBuffer, tempBuffer.Identifier(), paletteMaterial, 0);
            // 결과(tempBuffer) -> 최종 화면(colorBuffer)으로 다시 Blit
            Blit(cmd, tempBuffer.Identifier(), colorBuffer);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    /// <summary>
    /// 리소스 해제
    /// </summary>
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        if (cmd == null) throw new System.ArgumentNullException("cmd");
        cmd.ReleaseTemporaryRT(tempBuffer.id);
    }
}