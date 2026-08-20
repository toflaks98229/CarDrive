// PalettePass.cs

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace CarDrive.Rendering
{
    /// <summary>
    /// '색상 보정' 효과를 적용하는 ScriptableRenderPass입니다.
    /// </summary>
    public class PalettePass : ScriptableRenderPass
    {
        private const string k_PassName = "Palette Effect";

        private PaletteFeature.PaletteSettings settings;
        private Material paletteMaterial;

        /// <summary>
        /// 생성자
        /// </summary>
        public PalettePass(PaletteFeature.PaletteSettings settings)
        {
            this.settings = settings;
            this.renderPassEvent = settings.renderPassEvent;

            // 셰이더 이름을 변경하지 말라는 요청에 따라 "Hidden/PixelizePalette"를 그대로 사용
            if (paletteMaterial == null)
                paletteMaterial = CoreUtils.CreateEngineMaterial("Hidden/PixelizePalette");

            // 카메라 컬러를 입력 텍스처로 읽으려면 중간 텍스처가 필요하다. (백버퍼는 입력으로 쓸 수 없음)
            requiresIntermediateTexture = true;
        }

        /// <summary>
        /// 렌더링 로직 실행
        /// </summary>
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (paletteMaterial == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

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

            // 원본 -> 새 텍스처로 Blit하며 셰이더 적용.
            // 예전에는 temp 로 갔다가 다시 화면으로 되돌리는 2회 Blit이었지만,
            // RenderGraph에서는 cameraColor 핸들을 교체하면 되므로 1회로 끝난다.
            TextureHandle source = resourceData.activeColorTexture;

            TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = $"CameraColor-{k_PassName}";
            destinationDesc.clearBuffer = false;
            // 풀스크린 Blit 결과이므로 MSAA는 불필요하다. (품질 설정에 MSAA가 켜져 있어도 여기선 해제)
            destinationDesc.msaaSamples = MSAASamples.None;
            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            RenderGraphUtils.BlitMaterialParameters para = new(source, destination, paletteMaterial, 0);
            renderGraph.AddBlitPass(para, passName: k_PassName);

            // 이후 패스들이 이 텍스처를 카메라 컬러로 사용하도록 교체한다.
            resourceData.cameraColor = destination;
        }
    }
}
