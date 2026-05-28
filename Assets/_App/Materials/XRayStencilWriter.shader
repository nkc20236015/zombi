
Shader "Hidden/XRayStencilWriter" {
    SubShader {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass {
            ColorMask 0
            ZWrite Off
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); return o; }
            fixed4 frag(v2f i) : SV_Target { return fixed4(0,0,0,0); }
            ENDCG
        }
    }
}
