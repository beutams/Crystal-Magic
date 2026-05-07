Shader "CrystalMagic/Flipbook4x4Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _GridX("Grid X", Float) = 4
        _GridY("Grid Y", Float) = 4
        _FrameCount("Frame Count", Float) = 16
        _FPS("FPS", Float) = 16
        _Loop("Loop", Float) = 1
        _StartTime("Start Time", Float) = 0
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Cutoff", Range(0, 1)) = 0.1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
        [ToggleUI] _ZWrite("Z Write", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-100"
            "RenderType" = "Transparent"
            "UniversalMaterialType" = "Unlit"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _GridX;
                float _GridY;
                float _FrameCount;
                float _FPS;
                float _Loop;
                float _StartTime;
                half _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float gridX = max(_GridX, 1.0);
                float gridY = max(_GridY, 1.0);
                float frameCount = max(_FrameCount, 1.0);
                float fps = max(_FPS, 0.01);
                float elapsed = max(_Time.y - _StartTime, 0.0);
                float rawFrame = floor(elapsed * fps);
                float frame = _Loop > 0.5
                    ? fmod(rawFrame, frameCount)
                    : min(rawFrame, frameCount - 1.0);

                float col = fmod(frame, gridX);
                float rowTop = floor(frame / gridX);
                float row = (gridY - 1.0) - rowTop;
                float2 tileSize = float2(1.0 / gridX, 1.0 / gridY);
                float2 atlasUv = input.uv * tileSize + float2(col, row) * tileSize;

                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, atlasUv) * _BaseColor;

                #if defined(_ALPHATEST_ON)
                clip(color.a - _Cutoff);
                #endif

                return color;
            }
            ENDHLSL
        }
    }
}
