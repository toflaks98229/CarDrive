

Shader "Hidden/Pixelize"
{
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off Cull Off

        HLSLINCLUDE
        #pragma vertex Vert
        #pragma fragment frag

        // RenderGraph 의 AddBlitPass 는 소스를 _BlitTexture 에 바인딩하고
        // 풀스크린 삼각형(Vert)을 그린다. Blit.hlsl 이 Attributes/Varyings/Vert 를 제공한다.
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        uniform float2 _BlockCount;
        uniform float2 _BlockSize;
        uniform float2 _HalfBlockSize;

        ENDHLSL

        Pass
        {
            Name "Pixelation"

            HLSLPROGRAM
            half4 frag(Varyings IN) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 blockPos = floor(IN.texcoord * _BlockCount);
                float2 blockCenter = blockPos * _BlockSize + _HalfBlockSize;

                float4 tex = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, blockCenter, _BlitMipLevel);
                //return float4(IN.texcoord,1,1);

                return tex;
            }
            ENDHLSL
        }


    }
}
