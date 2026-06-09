Shader "CrystalMagic/TransparentSpriteMesh"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _FrameUvMin("Frame UV Min", Vector) = (0, 0, 0, 0)
        _FrameUvSize("Frame UV Size", Vector) = (1, 1, 0, 0)
        _FrameWorldSize("Frame World Size", Vector) = (1, 1, 0, 0)
        _FramePivotOffset("Frame Pivot Offset", Vector) = (0, 0, 0, 0)
        _OverlayColor("Overlay Color", Color) = (1, 1, 1, 1)
        _OverlayStrength("Overlay Strength", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float4 _FrameUvMin;
                float4 _FrameUvSize;
                float4 _FrameWorldSize;
                float4 _FramePivotOffset;
                float4 _OverlayColor;
                float4 _OverlayStrength;
            CBUFFER_END

            UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP(float4, _FrameUvMin)
                UNITY_DOTS_INSTANCED_PROP(float4, _FrameUvSize)
                UNITY_DOTS_INSTANCED_PROP(float4, _FrameWorldSize)
                UNITY_DOTS_INSTANCED_PROP(float4, _FramePivotOffset)
                UNITY_DOTS_INSTANCED_PROP(float4, _OverlayColor)
                UNITY_DOTS_INSTANCED_PROP(float4, _OverlayStrength)
            UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)

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

                float4 frameWorldSize = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _FrameWorldSize);
                float4 framePivotOffset = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _FramePivotOffset);
                float3 positionOS = input.positionOS.xyz;
                positionOS.xy = input.positionOS.xy * frameWorldSize.xy + framePivotOffset.xy;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                output.positionCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 frameUvMin = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _FrameUvMin);
                float4 frameUvSize = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _FrameUvSize);
                float2 atlasUv = input.uv * frameUvSize.xy + frameUvMin.xy;
                float4 overlayColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _OverlayColor);
                float overlayStrength = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _OverlayStrength).x;
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, atlasUv) * _BaseColor;
                color.rgb = lerp(color.rgb, color.rgb * overlayColor.rgb, saturate(overlayStrength));
                return color;
            }
            ENDHLSL
        }
    }
}
