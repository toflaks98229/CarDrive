Shader "Hidden/PixelizePalette" // 셰이더 이름 변경
{
    Properties
    {
        [Header(Base Settings)]
        _MainTex("Texture", 2D) = "white" {}
        [Space]
        _Strength("Effect Strength", Range(0.0, 1.0)) = 1.0

        [Header(Color Reduction Method)]
        [KeywordEnum(Luminance Quantize, Palette Map, Dithered Quantize)] _METHOD("Method", Float) = 0

        [Header(Luminance Quantization)]
        _Levels("Color Levels", Float) = 16
        // [변경] 어두운 톤과 밝은 톤을 나누는 기준 슬라이더 추가
        _ToneThreshold("Tone Threshold", Range(0.0, 1.0)) = 0.5 

        [Header(Palette Mapping)]
        _PaletteTex("Palette Texture (1D)", 2D) = "white" {}
        _PaletteSize("Palette Color Count", Float) = 16

        [Header(Dithering)]
        _DitherStrength("Dither Strength", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _METHOD_LUMINANCE_QUANTIZE _METHOD_PALETTE_MAP _METHOD_DITHERED_QUANTIZE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_PaletteTex);
            SAMPLER(sampler_PaletteTex);

            float _Strength;
            float _Levels;
            float _PaletteSize;
            float _DitherStrength;
            float _ToneThreshold; // [변경] 셰이더에서 사용할 변수 선언

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionHCS  : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_TARGET
            {
                half4 originalColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 processedColor = originalColor;

                // --- [방법 A] 휘도 양자화 (요청하신 내용으로 수정됨) ---
                #if _METHOD_LUMINANCE_QUANTIZE
                    float originalLuminance = dot(processedColor.rgb, float3(0.299, 0.587, 0.114));
                    
                    // [변경] 휘도 값에 따라 올림(ceil) 또는 반올림(round)을 선택
                    float posterizedLuminance;
                    if (originalLuminance <= _ToneThreshold)
                    {
                        // 기준값 이하(어두운 색)는 올림 처리하여 너무 어두워지는 것을 방지
                        posterizedLuminance = ceil(originalLuminance * _Levels) / _Levels;
                    }
                    else
                    {
                        // 기준값 초과(밝은 색)는 반올림 처리하여 자연스럽게 표현
                        posterizedLuminance = round(originalLuminance * _Levels) / _Levels;
                    }
                    
                    float ratio = (originalLuminance > 0) ? posterizedLuminance / originalLuminance : 0;
                    processedColor.rgb *= ratio;
                #endif

                // --- [방법 B] 팔레트 매핑 (변경 없음) ---
                #if _METHOD_PALETTE_MAP
                    half3 finalPaletteColor = 0;
                    float minDistance = 10000.0;
                    for (int i = 0; i < _PaletteSize; i++)
                    {
                        float2 paletteUV = float2((i + 0.5) / _PaletteSize, 0.5);
                        half3 paletteColor = SAMPLE_TEXTURE2D(_PaletteTex, sampler_PaletteTex, paletteUV).rgb;
                        float dist = distance(processedColor.rgb, paletteColor);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            finalPaletteColor = paletteColor;
                        }
                    }
                    processedColor.rgb = finalPaletteColor;
                #endif

                // --- [방법 C] 디더링을 적용한 휘도 양자화 (변경 없음) ---
                #if _METHOD_DITHERED_QUANTIZE
                    const float4x4 bayerMatrix = float4x4(
                         0,  8,  2, 10,
                        12,  4, 14,  6,
                         3, 11,  1,  9,
                        15,  7, 13,  5
                    );
                    float2 screenCoord = IN.positionHCS.xy;
                    int bayerX = fmod(screenCoord.x, 4);
                    int bayerY = fmod(screenCoord.y, 4);
                    float ditherThreshold = bayerMatrix[bayerY][bayerX];
                    float ditherValue = (ditherThreshold / 16.0 - 0.5) * (1.0 / _Levels) * _DitherStrength;
                    float ditheredLuminance = dot(processedColor.rgb, float3(0.299, 0.587, 0.114)) + ditherValue;
                    float posterizedLuminance = floor(ditheredLuminance * _Levels) / _Levels;
                    float ratio = (ditheredLuminance > 0) ? posterizedLuminance / ditheredLuminance : 0;
                    processedColor.rgb *= ratio;
                #endif

                half4 finalColor = lerp(originalColor, processedColor, _Strength);
                return finalColor;
            }
            ENDHLSL
        }
    }
}