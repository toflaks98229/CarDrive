// PalettePass.cs

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace CarDrive.Rendering
{
    /// <summary>
    /// 카메라 컬러의 색 수를 줄여 새 텍스처에 그리고, 그것을 카메라 컬러로 갈아 끼우는 패스입니다.
    ///
    /// <b>Blit 한 번으로 끝냅니다.</b> 예전에는 임시 텍스처로 갔다가 화면으로 되돌리는 두 번이
    /// 필요했지만, RenderGraph 에서는 결과 핸들을 <c>cameraColor</c> 에 꽂아 주면
    /// 이후 패스들이 알아서 그것을 읽습니다.
    ///
    /// 어느 방식으로 줄일지는 <see cref="PaletteFeature.PaletteSettings.method"/> 가 정하고,
    /// 이 패스는 그에 맞는 셰이더 키워드 하나만 켜고 나머지를 끕니다.
    /// </summary>
    public class PalettePass : ScriptableRenderPass
    {
        // --- Constants ---

        /// <summary>프로파일러와 RenderGraph 뷰어에 표시할 패스 이름입니다.</summary>
        private const string k_PassName = "Palette Effect";

        // --- Private Member Variables ---

        /// <summary>렌더러 기능에서 넘겨받은 설정입니다. 참조를 공유하므로 값 변경이 바로 보입니다.</summary>
        private PaletteFeature.PaletteSettings settings;

        /// <summary>색 감축을 수행하는 <c>Hidden/PixelizePalette</c> 재질입니다.</summary>
        private Material paletteMaterial;

        // --- Constructors ---

        /// <summary>
        /// 설정을 받아 패스 시점과 전용 재질을 준비합니다.
        /// </summary>
        /// <param name="settings">렌더러 기능이 인스펙터에 노출한 설정입니다.</param>
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

        // --- Public Methods ---

        /// <summary>
        /// 고른 방식에 맞춰 셰이더 키워드와 수치를 넣고, 색을 줄인 결과를 새 텍스처에 그린 뒤
        /// 이후 패스가 볼 카메라 컬러를 그것으로 교체합니다.
        /// </summary>
        /// <param name="renderGraph">이번 프레임의 렌더 그래프입니다.</param>
        /// <param name="frameData">카메라와 렌더 타겟 정보를 담은 프레임 데이터입니다.</param>
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (paletteMaterial == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            // 세 가지 모드 중 하나만 켜고 나머지는 끈다.
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
