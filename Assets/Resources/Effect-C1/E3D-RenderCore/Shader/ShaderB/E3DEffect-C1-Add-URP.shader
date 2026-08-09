// Built-in 兼容替身：Additive 粒子按 Alpha 裁切 Billboard。
Shader "E3DEffect/URP/C1/Add-Alpha"
{
	Properties
	{
		_MainTex("Main Tex", 2D) = "white" {}
		_Alpha("Alpha", Range(0, 1)) = 1
		[HDR]_TintColor("TintColor", Color) = (1,1,1,1)
	}

	Category
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
		Blend SrcAlpha One
		ColorMask RGB
		Cull Off Lighting Off ZWrite Off Fog { Mode Off }

		SubShader
		{
			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_particles
				#include "UnityCG.cginc"

				sampler2D _MainTex;
				float4 _MainTex_ST;
				fixed4 _TintColor;
				fixed _Alpha;

				struct appdata_t
				{
					float4 vertex : POSITION;
					fixed4 color : COLOR;
					float2 texcoord : TEXCOORD0;
				};

				struct v2f
				{
					float4 vertex : SV_POSITION;
					fixed4 color : COLOR;
					float2 texcoord : TEXCOORD0;
				};

				v2f vert(appdata_t v)
				{
					v2f o;
					o.vertex = UnityObjectToClipPos(v.vertex);
					o.color = v.color * _TintColor;
					o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
					return o;
				}

				fixed4 frag(v2f i) : SV_Target
				{
					fixed4 tex = tex2D(_MainTex, i.texcoord);
					fixed4 col = 2.0f * i.color * tex;
					col.a *= _Alpha;
					return col;
				}
				ENDCG
			}
		}
	}
}
