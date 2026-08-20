Shader "ACT/Preview/MalevolentShrine/Slash"
{
    Properties
    {
        _FillColor ("Fill", Color) = (0.02, 0.02, 0.025, 1)
        _OutlineColor ("Outline", Color) = (0.93, 0.93, 0.91, 1)
        _Outline ("Outline Width", Range(0.02, 0.28)) = 0.09
        _TipPower ("Tip Power", Range(0.35, 1.8)) = 0.72
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
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _FillColor;
            fixed4 _OutlineColor;
            float _Outline;
            float _TipPower;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float x = i.uv.x;
                float y = i.uv.y * 2.0 - 1.0;
                float envelope = pow(saturate(sin(UNITY_PI * x)), _TipPower);
                envelope = saturate(envelope * 1.08);
                float inward = envelope - abs(y);
                float aa = max(fwidth(inward), 0.002);
                float alpha = smoothstep(-aa, aa, inward);
                if (alpha <= 0.001)
                    return 0;

                float outline = 1.0 - smoothstep(_Outline * 0.35, _Outline, max(inward, 0.0));
                float tip = 1.0 - smoothstep(0.0, 0.055, min(x, 1.0 - x));
                outline = saturate(outline + tip * 0.25);
                fixed4 col = lerp(_FillColor, _OutlineColor, outline);
                col.a *= alpha;
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
