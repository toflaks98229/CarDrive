// PaletteFeature.cs

using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP에 'Palette' (색상 보정) 효과를 렌더러 기능으로 추가하는 클래스입니다.
/// </summary>
public class PaletteFeature : ScriptableRendererFeature
{
    /// <summary>
    /// 이 렌더러 기능에 대한 설정을 담는 내부 클래스입니다.
    /// </summary>
    [System.Serializable]
    public class PaletteSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        [Range(0.0f, 1.0f)]
        public float strength = 1.0f;

        [Header("Color Reduction Method")]
        public Method method = Method.LuminanceQuantize;

        // [변경] Method 열거형에 DitheredQuantize 추가
        public enum Method
        {
            LuminanceQuantize,
            PaletteMap,
            DitheredQuantize
        }

        [Header("Luminance Quantization")]
        [Range(2, 256)]
        public int levels = 16;
        // [변경] Tone Threshold 변수 추가
        [Range(0.0f, 1.0f)]
        public float toneThreshold = 0.5f;

        [Header("Palette Mapping")]
        public Texture2D paletteTexture;
        [Range(2, 256)]
        public int paletteColorCount = 16;

        // [변경] Dithering 헤더 및 변수 추가
        [Header("Dithering")]
        [Range(0.0f, 1.0f)]
        public float ditherStrength = 0.5f;
    }

    [SerializeField] public PaletteSettings settings;
    private PalettePass palettePass;

    /// <summary>
    /// 렌더러 기능이 처음 생성될 때 호출됩니다.
    /// </summary>
    public override void Create()
    {
        palettePass = new PalettePass(settings);
    }

    /// <summary>
    /// 매 프레임 카메라 렌더링 시 호출됩니다.
    /// </summary>
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
#if UNITY_EDITOR
        if (renderingData.cameraData.isSceneViewCamera) return;
#endif
        renderer.EnqueuePass(palettePass);
    }
}