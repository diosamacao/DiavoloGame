Shader "ACT/Preview/MalevolentShrine/Ring"
{
    Properties
    {
        _Color ("Color", Color) = (0.18, 0.05, 0.04, 0.55)
        _Inner ("Inner Radius", Range(0, 1)) = 0.86
        _Soft ("Softness", Range(0.001, 0.2)) = 0.04
        _Pulse ("Pulse", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            float _Inner;
            float _Soft;
            float _Pulse;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);
                float outer = 1.0;
                float ring = smoothstep(_Inner - _Soft, _Inner, r) * (1.0 - smoothstep(outer - _Soft, outer, r));
                float pulse = 1.0 + _Pulse * 0.25 * sin(_Time.y * 3.4);
                return _Color * (ring * pulse);
            }
            ENDCG
        }
    }
    FallBack Off
}
