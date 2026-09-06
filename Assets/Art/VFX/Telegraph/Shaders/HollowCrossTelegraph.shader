Shader "ACT/VFX/HollowCrossTelegraph"
{
    Properties
    {
        [Header(Color)]
        [HDR] _Color ("Core Color", Color) = (2.2, 1.7, 0.35, 1)
        [HDR] _GlowColor ("Glow Color", Color) = (1.4, 0.75, 0.12, 1)
        _Intensity ("Intensity", Range(0, 8)) = 1.6

        [Header(Cross Shape)]
        _ArmLength ("Arm Length", Range(0.2, 1.2)) = 0.86
        _ArmWidth ("Arm Width", Range(0.01, 0.35)) = 0.065
        _TipScale ("Tip Taper", Range(0.05, 1)) = 0.28
        _Hole ("Center Hole", Range(0.02, 0.6)) = 0.18
        _HoleSoft ("Hole Softness", Range(0.001, 0.2)) = 0.035
        _Soft ("Edge Softness", Range(0.001, 0.12)) = 0.018
        _Rotation ("Rotation Deg", Range(0, 90)) = 0

        [Header(Glow)]
        _GlowWidth ("Glow Width Scale", Range(1, 4)) = 2.2
        _GlowSharp ("Glow Falloff", Range(1, 24)) = 8
        _RingWidth ("Inner Ring Width", Range(0, 0.12)) = 0.028
        _RingIntensity ("Inner Ring Intensity", Range(0, 3)) = 0.85

        [Header(Existing Flare Overlay)]
        [NoScaleOffset] _FlareTex ("Flare Streak (e.g. Flare31)", 2D) = "white" {}
        _FlareAmount ("Flare Mix", Range(0, 2)) = 0.65
        _FlareStretch ("Flare Thickness Stretch", Range(0.2, 8)) = 2.4

        [Header(Motion)]
        _Pulse ("Pulse Amount", Range(0, 1)) = 0.18
        _PulseSpeed ("Pulse Speed", Range(0, 16)) = 7.5

        [Header(Render)]
        [Toggle] _Billboard ("Vertex Billboard", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha One
        ZWrite Off
        ZTest [_ZTest]
        Cull [_Cull]
        Lighting Off
        ColorMask RGB

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature_local _BILLBOARD_ON
            #include "UnityCG.cginc"

            // 绝区零抬手提示：中空十字（程序化 SDF）+ 可选现成光条贴图叠加。
            sampler2D _FlareTex;
            float4 _Color;
            float4 _GlowColor;
            float _Intensity;
            float _ArmLength;
            float _ArmWidth;
            float _TipScale;
            float _Hole;
            float _HoleSoft;
            float _Soft;
            float _Rotation;
            float _GlowWidth;
            float _GlowSharp;
            float _RingWidth;
            float _RingIntensity;
            float _FlareAmount;
            float _FlareStretch;
            float _Pulse;
            float _PulseSpeed;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            float2 Rotate2D(float2 p, float degrees)
            {
                float rad = radians(degrees);
                float s, c;
                sincos(rad, s, c);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }

            // 轴对齐胶囊：从 hole 处伸出到 tip，宽度按距离收尖，四臂拼成中间空的十字。
            float TaperedArm(float along, float across, float hole, float armLen, float halfWidth, float tipScale)
            {
                float span = max(armLen - hole, 1e-4);
                float t = saturate((along - hole) / span);
                float hw = lerp(halfWidth, halfWidth * tipScale, t);
                float body = 1.0 - smoothstep(hw, hw + _Soft, abs(across));
                float startMask = smoothstep(hole - _HoleSoft, hole + _HoleSoft * 0.25, along);
                float endMask = 1.0 - smoothstep(armLen - _Soft, armLen + _Soft * 2.0, along);
                return body * startMask * endMask;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.uv = v.uv;
                o.color = v.color;

                #ifdef _BILLBOARD_ON
                float3 center = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float3 axisX = normalize(UNITY_MATRIX_V[0].xyz);
                float3 axisY = normalize(UNITY_MATRIX_V[1].xyz);
                float scaleX = length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));
                float scaleY = length(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21));
                float3 worldPos = center + axisX * v.vertex.x * scaleX + axisY * v.vertex.y * scaleY;
                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1));
                #else
                o.pos = UnityObjectToClipPos(v.vertex);
                #endif

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                p = Rotate2D(p, _Rotation);

                float ax = abs(p.x);
                float ay = abs(p.y);
                float r = length(p);

                float horiz = TaperedArm(ax, p.y, _Hole, _ArmLength, _ArmWidth, _TipScale);
                float vert = TaperedArm(ay, p.x, _Hole, _ArmLength, _ArmWidth, _TipScale);
                float arms = max(horiz, vert);

                // 更细的芯，做出「亮黄芯 + 橙金边」而不是一块实心板。
                float core = max(
                    TaperedArm(ax, p.y, _Hole + _HoleSoft * 0.35, _ArmLength * 0.96, _ArmWidth * 0.38, _TipScale),
                    TaperedArm(ay, p.x, _Hole + _HoleSoft * 0.35, _ArmLength * 0.96, _ArmWidth * 0.38, _TipScale));

                float glow = max(
                    TaperedArm(ax, p.y, _Hole * 0.72, _ArmLength * 1.04, _ArmWidth * _GlowWidth, saturate(_TipScale + 0.15)),
                    TaperedArm(ay, p.x, _Hole * 0.72, _ArmLength * 1.04, _ArmWidth * _GlowWidth, saturate(_TipScale + 0.15)));
                glow = pow(saturate(glow), _GlowSharp * 0.12);

                float ring = 0.0;
                if (_RingWidth > 1e-4)
                {
                    float inner = _Hole - _RingWidth;
                    ring = smoothstep(inner, _Hole, r) * (1.0 - smoothstep(_Hole, _Hole + _RingWidth, r));
                }

                // 现成光条（Flare31 一类水平光带）转 90° 叠成十字，再乘挖空，避免实心亮核。
                float2 flareUvH = float2(p.x * 0.5 + 0.5, p.y * _FlareStretch * 0.5 + 0.5);
                float2 flareUvV = float2(p.y * 0.5 + 0.5, p.x * _FlareStretch * 0.5 + 0.5);
                float flareH = tex2D(_FlareTex, flareUvH).r;
                float flareV = tex2D(_FlareTex, flareUvV).r;
                float flare = max(flareH, flareV);
                float holeMask = smoothstep(_Hole - _HoleSoft, _Hole + _HoleSoft * 0.15, max(ax, ay) * 0.65 + r * 0.35);
                flare *= holeMask;

                float pulse = 1.0 + _Pulse * sin(_Time.y * _PulseSpeed);
                float shape = (arms * 0.72 + core * 1.35 + ring * _RingIntensity) * pulse;
                shape = lerp(shape, shape * (0.45 + flare * 1.4), saturate(_FlareAmount));

                float3 rgb = _Color.rgb * (core * 1.25 + arms * 0.55 + ring * _RingIntensity);
                rgb += _GlowColor.rgb * glow;
                rgb += _Color.rgb * flare * _FlareAmount * 0.55;
                rgb *= _Intensity * pulse * i.color.rgb;

                float alpha = saturate(shape + glow * 0.55 + flare * _FlareAmount * 0.35) * i.color.a * _Color.a;
                return fixed4(rgb * alpha, alpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
