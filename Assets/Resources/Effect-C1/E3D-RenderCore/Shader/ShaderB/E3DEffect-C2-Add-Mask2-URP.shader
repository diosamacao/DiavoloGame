// Built-in 兼容替身：双贴图 Additive，按 Alpha 裁切。
Shader "E3DEffect/URP/C2/Add-Alpha"
{
	Properties
	{
		_BaseRGBA("Base-RGBA", 2D) = "white" {}
		_ShapeRGB("Shape-RGB", 2D) = "white" {}
		_Alpha("Alpha", Range(0, 1)) = 1
		[HDR]_BaseColor("BaseColor", Color) = (1,1,1,1)
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

				sampler2D _BaseRGBA;
				sampler2D _ShapeRGB;
				float4 _BaseRGBA_ST;
				fixed4 _BaseColor;
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
					o.color = v.color * _BaseColor;
					o.texcoord = TRANSFORM_TEX(v.texcoord, _BaseRGBA);
					return o;
				}

				fixed4 frag(v2f i) : SV_Target
				{
					fixed4 baseCol = tex2D(_BaseRGBA, i.texcoord);
					fixed4 shape = tex2D(_ShapeRGB, i.texcoord);
					fixed4 col = 2.0f * i.color * baseCol * shape;
					col.a *= _Alpha * shape.a * baseCol.a;
					return col;
				}
				ENDCG
			}
		}
	}
}
