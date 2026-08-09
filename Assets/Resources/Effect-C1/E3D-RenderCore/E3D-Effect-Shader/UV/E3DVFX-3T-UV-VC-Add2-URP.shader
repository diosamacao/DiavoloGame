// Built-in 兼容替身：由 URP/ASE 自动降级生成；Additive 用 SrcAlpha One 避免粒子呈矩形。
Shader "E3DEffect/URP/C3/Add-UV-VC"
{
	Properties
	{
		_MainTex("MainTex", 2D) = "white" {}
		_BaseMap("BaseMap", 2D) = "white" {}
		_Base("Base", 2D) = "white" {}
		_BaseRGBA("BaseRGBA", 2D) = "white" {}
		_Shape("Shape", 2D) = "white" {}
		_ShapeRGB("ShapeRGB", 2D) = "white" {}
		_MaskMap("MaskMap", 2D) = "white" {}
		_DetailMap("DetailMap", 2D) = "white" {}
		_NoiseMap("NoiseMap", 2D) = "white" {}
		_BaseMaskMap("BaseMaskMap", 2D) = "white" {}
		_DissLoveMap("DissLoveMap", 2D) = "white" {}
		_TextureSample0("Texture Sample 0", 2D) = "white" {}
		[HDR]_MainColor("MainColor", Color) = (1,1,1,1)
		[HDR]_TintColor("TintColor", Color) = (1,1,1,1)
		[HDR]_BaseColor("BaseColor", Color) = (1,1,1,1)
		[HDR]_AddColor("AddColor", Color) = (1,1,1,1)
		[HDR]_EdgeColor("EdgeColor", Color) = (1,1,1,1)
		_Alpha("Alpha", Range(0, 10)) = 1
		_BaseAlpha("BaseAlpha", Range(0, 1)) = 1
		_Opacity("Opacity", Range(0, 5)) = 1
		_Diss("Diss", Range(0, 1)) = 1
		_DissInstensity("Diss-Instensity", Range(0.01, 1)) = 0.5
		_Clip("Clip", Float) = 0.5
		_EdgeClip("EdgeClip", Range(0, 10)) = 0
		_Power("Power", Range(0.5, 50)) = 1
		_Glow("Glow", Range(0, 2)) = 1
		_Thickness("Thickness", Range(0, 0.2)) = 0.1
		_UVTiling("UV-Tiling", Range(0, 4)) = 1
		[Toggle]_DissAlpha("Diss-Alpha", Float) = 1
		_UVFlowSpeed("UVFlowSpeed", Vector) = (0,0,0,0)
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
				sampler2D _BaseMap;
				sampler2D _Base;
				sampler2D _BaseRGBA;
				sampler2D _TextureSample0;
				sampler2D _MaskMap;
				sampler2D _Shape;
				sampler2D _ShapeRGB;
				float4 _MainTex_ST;
				fixed4 _MainColor;
				fixed4 _TintColor;
				fixed4 _BaseColor;
				fixed _Alpha;
				fixed _BaseAlpha;
				fixed _Opacity;

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
					o.color = v.color * _MainColor * _TintColor * _BaseColor;
					o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
					return o;
				}

				fixed4 frag(v2f i) : SV_Target
				{
					fixed4 col = tex2D(_MainTex, i.texcoord);
					col *= tex2D(_BaseMap, i.texcoord);
					col *= tex2D(_BaseRGBA, i.texcoord);
					col *= tex2D(_Base, i.texcoord);
					col *= tex2D(_TextureSample0, i.texcoord);
					fixed mask = tex2D(_MaskMap, i.texcoord).r * tex2D(_Shape, i.texcoord).r * tex2D(_ShapeRGB, i.texcoord).r;
					col *= i.color;
					col.a *= max(mask, 0.0001);
					fixed alphaMul = 1;
					if (_Alpha > 0.0001) alphaMul *= _Alpha;
					if (_BaseAlpha > 0.0001) alphaMul *= _BaseAlpha;
					if (_Opacity > 0.0001) alphaMul *= _Opacity;
					col.a *= alphaMul;
					return col;
				}
				ENDCG
			}
		}
	}
}