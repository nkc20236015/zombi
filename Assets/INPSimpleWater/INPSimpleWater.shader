Shader "Custom/INPSimpleWater"
{
    Properties
    {
        [HideInInspector] _MainTex("Albedo (RGB)", 2D) = "white" {}
        _Normal("Normal", 2D) = "bump" {}
        _Color("Color", Color) = (1,1,1,1)
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
        _RefractionAngle("RefractionAngle", Range(-1,1)) = 0.0 //Decides Refracted angle.
        _RefractionWaveStrength("RefractionWaveStrength", Range(0,1)) = 0.0 //Decides strength of refracted image's wave
        _WaterTransparency("WaterTransparency", Range(0,1)) = 0.0
        _FresnelStrength("FresnelStrength", Range(0,1)) = 1.0
        _WaveSpeed1("WaveSpeed1", float) = 0.0
        _WaveSpeed2("WaveSpeed2", float) = 0.0
        _WaveDirection("WaveDirection", Range(0,360)) = 0.0
        _WaveSameDirection("WaveSameDirection(1= true, 0=false)", Float) = 0.0 //If this is 1, two normal maps will have same direction. By using this, also you can make look like only one normal map is used.
    }
        SubShader
        {
            Tags { "RenderType" = "Opaque" "Queue" = "AlphaTest"}
            LOD 200

            GrabPass {} //A command for Using rendered contents.

            CGPROGRAM
            #pragma surface surf Standard alpha:fade

            sampler2D _MainTex;
            sampler2D _Normal;
            sampler2D _GrabTexture;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_Normal;
            float4 screenPos;
            float3 viewDir;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float _RefractionAngle; 
        float _RefractionWaveStrength;
        float _WaterTransparency;
        float _WaveSpeed1;
        float _WaveSpeed2;
        float _FresnelStrength;
        float _WaveDirection;
        float _WaveSameDirection;

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 screenUV = IN.screenPos.rgb / (IN.screenPos.a + _RefractionAngle); //Bring screen image from where placed material. Then make a refraction.
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            float rad = radians(_WaveDirection);
            float2 direction = float2(cos(rad), sin(rad));
            o.Albedo = c.rgb;
            float3 Normal1 = UnpackNormal(tex2D(_Normal, IN.uv_Normal + direction * _Time.y * _WaveSpeed1));//Make a Normal map 1 from normal map and make it moves.

            float3 Normal2 = (0, 0, 0);
            if (_WaveSameDirection == 0)
            {
                Normal2 = UnpackNormal(tex2D(_Normal, IN.uv_Normal - direction * _Time.y * _WaveSpeed2));//Make a Normal map 2 from normal map and make it moves.
            }
            else if (_WaveSameDirection == 1) //This will make same direction normal map twice. By using this, you can make look like only one normal map is working, too.
            {
                Normal2 = UnpackNormal(tex2D(_Normal, IN.uv_Normal + direction * _Time.y * _WaveSpeed2));//Make a Normal map 2 from normal map and make it moves.
            }

            o.Normal = Normal1 + Normal2;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;

            float fresnel = saturate(dot(o.Normal, IN.viewDir));
            float finalFresnel = pow(1 - fresnel+0.1, 3) * _FresnelStrength;

            float4 transparencyFinal = tex2D(_GrabTexture, screenUV.xy+o.Normal.xy * _RefractionWaveStrength) * _WaterTransparency;

            o.Emission = lerp(transparencyFinal, finalFresnel, 0.5);
        }
        ENDCG
        }

}