Shader "CarDrive/Post/Palette"
{
    // ── 화면 전체의 색 수를 줄이고 그 자리를 디더로 메웁니다 ──
    //
    // <b>왜 화면 전체인가.</b> 재질마다 색을 줄이면 각자 줄어들 뿐, <b>조명·안개·하늘이
    // 만드는 그라데이션</b>은 그대로 남습니다. 화면이 하나로 안 묶입니다. 마지막에 한 번
    // 줄이면 그 셋까지 같은 색 수 안으로 들어옵니다.
    //
    // <b>디더는 이미 이 프로젝트의 것입니다.</b> 나무·바위·건물·풀·게이지·얼룩이 전부
    // <c>CarDriveDither.hlsl</c> 의 4x4 Bayer 를 씁니다. 그 파일 주석대로 "디더 무늬가
    // 결점이 아니라 시대 표현으로 읽히기" 때문입니다. 같은 행렬을 여기서도 씁니다 —
    // 다른 난수를 쓰면 화면의 무늬가 두 종류로 갈립니다.
    //
    // <b>계단을 감마 쪽에서 냅니다.</b> 선형값을 그대로 자르면 단계가 밝은 쪽에 몰려
    // 어두운 부분이 뭉갭니다. 제곱근으로 옮겨 자르고 되돌리면 눈이 보는 대로 고르게 나뉩니다.
    Properties
    {
        _Levels ("색 단계 수 (채널당)", Range(2, 32)) = 6
        _Strength ("세기 (0이면 원래 화면)", Range(0, 1)) = 1
        _DitherStrength ("디더 세기", Range(0, 1)) = 1
        _Desaturate ("채도 빼기", Range(0, 1)) = 0
        _DitherPixel ("디더 한 칸의 화면 화소 수", Range(1, 6)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Palette"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "../Toon/CarDriveDither.hlsl"

            SAMPLER(sampler_BlitTexture);

            half  _Levels;
            half  _Strength;
            half  _DitherStrength;
            half  _Desaturate;
            half  _DitherPixel;

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.texcoord);
                half3 colour = source.rgb;

                // 채도를 빼는 것은 색 수 줄이기와 다른 축입니다. 콘크리트 쪽으로 밀고 싶을 때
                // 쓰라고 따로 두었습니다. 0 이면 아무 일도 하지 않습니다.
                half grey = dot(colour, half3(0.2126h, 0.7152h, 0.0722h));
                colour = lerp(colour, grey.xxx, _Desaturate);

                // 눈에 고르게 나뉘도록 감마 쪽으로 옮겨 자릅니다.
                half3 encoded = sqrt(max(colour, 0.0h));

                half steps = max(_Levels - 1.0h, 1.0h);
                // ⚠ <b>한 칸이 화면 화소 하나면 1080p 에서 안 보입니다.</b> 참조 화면
                // (White Knuckle)의 가로 자기상관 최소가 k=2,3 에 있어 디더 한 칸이
                // 화면 화소 두셋을 덮습니다. 그쪽은 내부 해상도를 낮춰 그리고 확대해서
                // 그렇게 되는데, 이 게임은 주행 게임이라 해상도를 내리면 노면 차선과
                // 먼 지표가 함께 뭉개집니다. 기하는 또렷이 두고 <b>디더 칸만</b> 키워
                // 같은 인상을 냅니다.
                float2 cell = floor(input.positionCS.xy / max(_DitherPixel, 1.0h));
                half threshold = (CarDriveDitherThreshold(cell) - 0.5h) * _DitherStrength;

                half3 quantised = floor(encoded * steps + 0.5h + threshold) / steps;
                quantised = quantised * quantised;

                return half4(lerp(source.rgb, quantised, _Strength), source.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
