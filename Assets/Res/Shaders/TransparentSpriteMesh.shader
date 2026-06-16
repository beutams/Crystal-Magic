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

#ifdef UNITY_DOTS_INSTANCING_ENABLED
            UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP(float4, _FrameUvMin)
                UNITY_DOTS_INSTANCED_PROP(float4, _FrameUvSize)
                UNITY_DOTS_INSTANCED_PROP(float4, _FrameWorldSize)
                UNITY_DOTS_INSTANCED_PROP(float4, _FramePivotOffset)
                UNITY_DOTS_INSTANCED_PROP(float4, _OverlayColor)
                UNITY_DOTS_INSTANCED_PROP(float4, _OverlayStrength)
            UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)

            static float4 unity_DOTS_Sampled_FrameUvMin;
            static float4 unity_DOTS_Sampled_FrameUvSize;
            static float4 unity_DOTS_Sampled_FrameWorldSize;
            static float4 unity_DOTS_Sampled_FramePivotOffset;
            static float4 unity_DOTS_Sampled_OverlayColor;
            static float4 unity_DOTS_Sampled_OverlayStrength;

            void SetupDOTSTransparentSpriteMeshPropertyCaches()
            {
                unity_DOTS_Sampled_FrameUvMin = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _FrameUvMin);
                unity_DOTS_Sampled_FrameUvSize = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _FrameUvSize);
                unity_DOTS_Sampled_FrameWorldSize = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _FrameWorldSize);
                unity_DOTS_Sampled_FramePivotOffset = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _FramePivotOffset);
                unity_DOTS_Sampled_OverlayColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _OverlayColor);
                unity_DOTS_Sampled_OverlayStrength = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _OverlayStrength);
            }

            #undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
            #define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSTransparentSpriteMeshPropertyCaches()

            #define _FrameUvMin unity_DOTS_Sampled_FrameUvMin
            #define _FrameUvSize unity_DOTS_Sampled_FrameUvSize
            #define _FrameWorldSize unity_DOTS_Sampled_FrameWorldSize
            #define _FramePivotOffset unity_DOTS_Sampled_FramePivotOffset
            #define _OverlayColor unity_DOTS_Sampled_OverlayColor
            #define _OverlayStrength unity_DOTS_Sampled_OverlayStrength
#endif

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

                float3 positionOS = input.positionOS.xyz;
                positionOS.xy = input.positionOS.xy * _FrameWorldSize.xy + _FramePivotOffset.xy;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                output.positionCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 atlasUv = input.uv * _FrameUvSize.xy + _FrameUvMin.xy;
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, atlasUv) * _BaseColor;
                color.rgb = lerp(color.rgb, color.rgb * _OverlayColor.rgb, saturate(_OverlayStrength.x));
                return color;
            }
            ENDHLSL
        }
    }
}
