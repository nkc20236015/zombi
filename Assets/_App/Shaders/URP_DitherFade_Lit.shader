Shader "Custom/URP_DitherFade_Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (RGB/A)", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1,1,1,1)
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5

        [Header(Dither Fade Settings)]
        _DitherFadeStart("Dither Fade Start Distance", Float) = 8.0
        _DitherFadeEnd("Dither Fade End Distance", Float) = 3.0

        [Header(Surface Options)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // ==================== Forward Lit Pass ====================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _BumpScale;
                half   _Cutoff;
                float  _DitherFadeStart;
                float  _DitherFadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 tangentWS   : TEXCOORD3;
                float  fogFactor   : TEXCOORD4;
                float4 screenPos   : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // 4x4 Bayer dither matrix (normalized 0-1)
            static const float DitherMatrix[16] = {
                 0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                 3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normInputs.normalWS;
                output.tangentWS = float4(normInputs.tangentWS, input.tangentOS.w);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                output.screenPos = ComputeScreenPos(posInputs.positionCS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // ==================== Dither Fade ====================
                float dist = distance(input.positionWS, _WorldSpaceCameraPos.xyz);
                float fadeFactor = saturate((dist - _DitherFadeEnd) / max(_DitherFadeStart - _DitherFadeEnd, 0.001));

                // Screen-space pixel position for dither pattern
                float2 screenPixel = input.screenPos.xy / input.screenPos.w * _ScreenParams.xy;
                int2 ditherCoord = int2(fmod(screenPixel.x, 4), fmod(screenPixel.y, 4));
                float ditherThreshold = DitherMatrix[ditherCoord.y * 4 + ditherCoord.x];

                // If fadeFactor < ditherThreshold, clip (discard) the pixel
                clip(fadeFactor - ditherThreshold);

                // ==================== Standard Lit Calculation ====================
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 color = baseMap * _BaseColor;

                #ifdef _ALPHATEST_ON
                    clip(color.a - _Cutoff);
                #endif

                // Normal mapping
                float3 normalWS = normalize(input.normalWS);
                #ifdef _NORMALMAP
                    float3 bitangentWS = cross(normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                    float3x3 TBN = float3x3(input.tangentWS.xyz, bitangentWS, normalWS);
                    float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                    normalWS = normalize(mul(normalTS, TBN));
                #endif

                // Lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.fogCoord = input.fogFactor;

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = color.rgb;
                surfaceData.alpha = color.a;
                surfaceData.metallic = 0;
                surfaceData.smoothness = 0.1;
                surfaceData.normalTS = float3(0, 0, 1);
                surfaceData.occlusion = 1;

                half4 litColor = UniversalFragmentPBR(inputData, surfaceData);
                litColor.rgb = MixFog(litColor.rgb, input.fogFactor);

                return litColor;
            }
            ENDHLSL
        }

        // ==================== Shadow Caster Pass ====================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _BumpScale;
                half   _Cutoff;
                float  _DitherFadeStart;
                float  _DitherFadeEnd;
            CBUFFER_END

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionWS = positionWS;
                output.screenPos = ComputeScreenPos(output.positionCS);

                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // Dither fade for shadows too
                float dist = distance(input.positionWS, _WorldSpaceCameraPos.xyz);
                float fadeFactor = saturate((dist - _DitherFadeEnd) / max(_DitherFadeStart - _DitherFadeEnd, 0.001));
                float2 screenPixel = input.screenPos.xy / input.screenPos.w * _ScreenParams.xy;
                int2 ditherCoord = int2(fmod(screenPixel.x, 4), fmod(screenPixel.y, 4));
                static const float DitherMatrix[16] = {
                     0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                     3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                float ditherThreshold = DitherMatrix[ditherCoord.y * 4 + ditherCoord.x];
                clip(fadeFactor - ditherThreshold);

                #ifdef _ALPHATEST_ON
                    half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                    clip(tex.a * _BaseColor.a - _Cutoff);
                #endif

                return 0;
            }
            ENDHLSL
        }

        // ==================== Depth Only Pass ====================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _BumpScale;
                half   _Cutoff;
                float  _DitherFadeStart;
                float  _DitherFadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.screenPos = ComputeScreenPos(posInputs.positionCS);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float dist = distance(input.positionWS, _WorldSpaceCameraPos.xyz);
                float fadeFactor = saturate((dist - _DitherFadeEnd) / max(_DitherFadeStart - _DitherFadeEnd, 0.001));
                float2 screenPixel = input.screenPos.xy / input.screenPos.w * _ScreenParams.xy;
                int2 ditherCoord = int2(fmod(screenPixel.x, 4), fmod(screenPixel.y, 4));
                static const float DitherMatrix[16] = {
                     0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                     3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                float ditherThreshold = DitherMatrix[ditherCoord.y * 4 + ditherCoord.x];
                clip(fadeFactor - ditherThreshold);

                #ifdef _ALPHATEST_ON
                    half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                    clip(tex.a * _BaseColor.a - _Cutoff);
                #endif

                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
