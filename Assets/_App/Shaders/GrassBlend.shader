Shader "Custom/GrassBlend"
{
    Properties
    {
        _GrassDark ("Grass Dark", 2D) = "white" {}
        _GrassNormal ("Grass Normal", 2D) = "white" {}
        _GrassLight ("Grass Light", 2D) = "white" {}
        _DirtTex ("Dirt", 2D) = "white" {}
        _Tiling ("Texture Tiling", Float) = 1.0
        _Smoothness ("Smoothness", Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 vertColor   : COLOR;
                float  fogFactor   : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
            };

            TEXTURE2D(_GrassDark);   SAMPLER(sampler_GrassDark);
            TEXTURE2D(_GrassNormal); SAMPLER(sampler_GrassNormal);
            TEXTURE2D(_GrassLight);  SAMPLER(sampler_GrassLight);
            TEXTURE2D(_DirtTex);     SAMPLER(sampler_DirtTex);

            CBUFFER_START(UnityPerMaterial)
                float _Tiling;
                float _Smoothness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS  = vertexInput.positionCS;
                output.positionWS  = vertexInput.positionWS;
                output.normalWS    = normalInput.normalWS;
                output.uv          = input.uv * _Tiling;
                output.vertColor   = input.color;
                output.fogFactor   = ComputeFogFactor(vertexInput.positionCS.z);
                output.shadowCoord = GetShadowCoord(vertexInput);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // テクスチャサンプリング
                half4 darkCol   = SAMPLE_TEXTURE2D(_GrassDark,   sampler_GrassDark,   uv);
                half4 normalCol = SAMPLE_TEXTURE2D(_GrassNormal, sampler_GrassNormal, uv);
                half4 lightCol  = SAMPLE_TEXTURE2D(_GrassLight,  sampler_GrassLight,  uv);
                half4 dirtCol   = SAMPLE_TEXTURE2D(_DirtTex,     sampler_DirtTex,     uv);

                // R値で草の明暗をブレンド
                float grassBlend = input.vertColor.r;
                half4 grassColor;
                if (grassBlend < 0.5)
                {
                    grassColor = lerp(darkCol, normalCol, grassBlend * 2.0);
                }
                else
                {
                    grassColor = lerp(normalCol, lightCol, (grassBlend - 0.5) * 2.0);
                }

                // G値で草と土をブレンド
                float dirtBlend = input.vertColor.g;
                half4 finalColor = lerp(grassColor, dirtCol, dirtBlend);

                // ライティング計算
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalColor.rgb;
                surfaceData.alpha = 1.0;
                surfaceData.smoothness = _Smoothness;
                surfaceData.metallic = 0.0;
                surfaceData.occlusion = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }

        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // Depth pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
