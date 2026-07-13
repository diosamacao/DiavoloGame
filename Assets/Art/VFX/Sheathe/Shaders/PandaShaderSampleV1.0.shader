// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "VFX/PandaShaderSampleV1.0"
{
	Properties
	{
		[Enum(UnityEngine.Rendering.BlendMode)]_Scr("Scr", Float) = 5
		[Enum(UnityEngine.Rendering.BlendMode)]_Dst("Dst", Float) = 10
		[Enum(UnityEngine.Rendering.CullMode)]_CullMode("CullMode", Float) = 0
		_MainTex("MainTex", 2D) = "white" {}
		[Toggle]_MainTexAR("MainTexAR", Float) = 0
		[HDR]_MainColor("MainColor", Color) = (1,1,1,1)
		_MainTexUSpeed("MainTexUSpeed", Float) = 0
		_MainTexVSpeed("MainTexVSpeed", Float) = 0
		[Toggle]_CustomMainTex("CustomMainTex", Float) = 0
		[Toggle(_FMASKTEX_ON)] _FMaskTex("FMaskTex", Float) = 0
		_MaskTex("MaskTex", 2D) = "white" {}
		[Toggle]_MaskTexAR("MaskTexAR", Float) = 1
		_MaskTexUSpeed("MaskTexUSpeed", Float) = 0
		_MaskTexVSpeed("MaskTexVSpeed", Float) = 0
		[Toggle(_FDISTORTTEX_ON)] _FDistortTex("FDistortTex", Float) = 0
		_DistortTex("DistortTex", 2D) = "white" {}
		[Toggle]_DistortTexAR("DistortTexAR", Float) = 1
		_DistortFactor("DistortFactor", Range( 0 , 1)) = 0
		_DistortTexUSpeed("DistortTexUSpeed", Float) = 0
		_DistortTexVSpeed("DistortTexVSpeed", Float) = 0
		[Toggle]_DistortMainTex("DistortMainTex", Float) = 0
		[Toggle]_DistortMaskTex("DistortMaskTex", Float) = 0
		[Toggle]_DistortDissolveTex("DistortDissolveTex", Float) = 0
		[Toggle(_FDISSOLVETEX_ON)] _FDissolveTex("FDissolveTex", Float) = 0
		_DissolveTex("DissolveTex", 2D) = "white" {}
		[Toggle]_DissolveTexAR("DissolveTexAR", Float) = 1
		[HDR]_DissolveColor("DissolveColor", Color) = (1,1,1,1)
		[Toggle]_CustomDissolve("CustomDissolve", Float) = 0
		_DissolveFactor("DissolveFactor", Range( 0 , 1)) = 0
		_DissolveSoft("DissolveSoft", Range( 0 , 1)) = 0.1
		_DissolveWide("DissolveWide", Range( 0 , 1)) = 0.05
		_DissolveTexUSpeed("DissolveTexUSpeed", Float) = 0
		_DissolveTexVSpeed("DissolveTexVSpeed", Float) = 0
		_MainAlpha("MainAlpha", Range( 0 , 10)) = 1
		[Toggle(_FFNL_ON)] _FFnl("FFnl", Float) = 0
		[Toggle(_FDEPTH_ON)] _FDepth("FDepth", Float) = 0
		[HDR]_FnlColor("FnlColor", Color) = (1,1,1,1)
		_FnlScale("FnlScale", Range( 0 , 2)) = 0
		_FnlPower("FnlPower", Range( 1 , 10)) = 1
		[Toggle]_ReFnl("ReFnl", Float) = 0
		[Enum(Alpha,0,Add,1)]_BlendMode("BlendMode", Float) = 0
		_DepthFade("DepthFade", Range( 0 , 10)) = 1
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IsEmissive" = "true"  }
		Cull [_CullMode]
		ZWrite Off
		ZTest LEqual
		Blend [_Scr] [_Dst]
		
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#include "UnityCG.cginc"
		#pragma target 3.0
		#pragma shader_feature_local _FDISSOLVETEX_ON
		#pragma shader_feature_local _FDISTORTTEX_ON
		#pragma shader_feature_local _FFNL_ON
		#pragma shader_feature_local _FMASKTEX_ON
		#pragma shader_feature_local _FDEPTH_ON
		#pragma surface surf Unlit keepalpha noshadow noambient novertexlights nolightmap  nodynlightmap nodirlightmap nofog nometa noforwardadd 
		#undef TRANSFORM_TEX
		#define TRANSFORM_TEX(tex,name) float4(tex.xy * name##_ST.xy + name##_ST.zw, tex.z, tex.w)
		struct Input
		{
			float4 vertexColor : COLOR;
			float2 uv_texcoord;
			float4 uv2_texcoord2;
			float3 worldPos;
			half3 worldNormal;
			float4 screenPos;
		};

		uniform half _Dst;
		uniform half _CullMode;
		uniform half _BlendMode;
		uniform half _Scr;
		uniform float4 _MainColor;
		uniform sampler2D _MainTex;
		uniform half _MainTexUSpeed;
		uniform half _MainTexVSpeed;
		uniform half _CustomMainTex;
		uniform float4 _MainTex_ST;
		uniform half _DistortMainTex;
		uniform half _DistortTexAR;
		uniform sampler2D _DistortTex;
		uniform half _DistortTexUSpeed;
		uniform half _DistortTexVSpeed;
		uniform float4 _DistortTex_ST;
		uniform half _DistortFactor;
		uniform float4 _DissolveColor;
		uniform half _CustomDissolve;
		uniform half _DissolveFactor;
		uniform half _DissolveWide;
		uniform half _DissolveSoft;
		uniform half _DissolveTexAR;
		uniform sampler2D _DissolveTex;
		uniform half _DissolveTexUSpeed;
		uniform half _DissolveTexVSpeed;
		uniform float4 _DissolveTex_ST;
		uniform half _DistortDissolveTex;
		uniform half _MainAlpha;
		uniform half _ReFnl;
		uniform half4 _FnlColor;
		uniform half _FnlScale;
		uniform half _FnlPower;
		uniform half _MainTexAR;
		uniform half _MaskTexAR;
		uniform sampler2D _MaskTex;
		uniform half _MaskTexUSpeed;
		uniform half _MaskTexVSpeed;
		uniform float4 _MaskTex_ST;
		uniform half _DistortMaskTex;
		UNITY_DECLARE_DEPTH_TEXTURE( _CameraDepthTexture );
		uniform float4 _CameraDepthTexture_TexelSize;
		uniform half _DepthFade;

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float Scr106 = _Scr;
			half2 appendResult4_g42 = (half2(_MainTexUSpeed , _MainTexVSpeed));
			float2 uv_MainTex = i.uv_texcoord * _MainTex_ST.xy + _MainTex_ST.zw;
			half2 temp_output_3_0_g39 = uv_MainTex;
			half2 appendResult4_g33 = (half2(_DistortTexUSpeed , _DistortTexVSpeed));
			float2 uv_DistortTex = i.uv_texcoord * _DistortTex_ST.xy + _DistortTex_ST.zw;
			half2 panner5_g33 = ( 1.0 * _Time.y * appendResult4_g33 + uv_DistortTex);
			half4 tex2DNode7_g33 = tex2D( _DistortTex, panner5_g33 );
			half Distort148 = ( ( _DistortTexAR == 0.0 ? tex2DNode7_g33.a : tex2DNode7_g33.r ) * _DistortFactor );
			#ifdef _FDISTORTTEX_ON
				half2 staticSwitch316 = ( _DistortMainTex == 0.0 ? temp_output_3_0_g39 : ( temp_output_3_0_g39 + Distort148 ) );
			#else
				half2 staticSwitch316 = uv_MainTex;
			#endif
			half2 appendResult330 = (half2(i.uv2_texcoord2.x , i.uv2_texcoord2.y));
			half2 panner5_g42 = ( 1.0 * _Time.y * appendResult4_g42 + (( _CustomMainTex )?( ( staticSwitch316 + appendResult330 ) ):( staticSwitch316 )));
			half4 tex2DNode7_g42 = tex2D( _MainTex, panner5_g42 );
			half4 MainTexColor215 = ( _MainColor * tex2DNode7_g42 );
			half temp_output_275_0 = (-_DissolveWide + ((( _CustomDissolve )?( i.uv2_texcoord2.z ):( _DissolveFactor )) - 0.0) * (1.0 - -_DissolveWide) / (1.0 - 0.0));
			half temp_output_277_0 = ( _DissolveSoft + 0.0001 );
			half temp_output_272_0 = (-temp_output_277_0 + (( temp_output_275_0 + _DissolveWide ) - 0.0) * (1.0 - -temp_output_277_0) / (1.0 - 0.0));
			half2 appendResult4_g41 = (half2(_DissolveTexUSpeed , _DissolveTexVSpeed));
			float2 uv_DissolveTex = i.uv_texcoord * _DissolveTex_ST.xy + _DissolveTex_ST.zw;
			half2 temp_output_3_0_g40 = uv_DissolveTex;
			#ifdef _FDISTORTTEX_ON
				half2 staticSwitch314 = ( _DistortDissolveTex == 0.0 ? temp_output_3_0_g40 : ( temp_output_3_0_g40 + Distort148 ) );
			#else
				half2 staticSwitch314 = uv_DissolveTex;
			#endif
			half2 panner5_g41 = ( 1.0 * _Time.y * appendResult4_g41 + staticSwitch314);
			half4 tex2DNode7_g41 = tex2D( _DissolveTex, panner5_g41 );
			half temp_output_308_20 = ( _DissolveTexAR == 0.0 ? tex2DNode7_g41.a : tex2DNode7_g41.r );
			half smoothstepResult264 = smoothstep( temp_output_272_0 , ( temp_output_272_0 + temp_output_277_0 ) , temp_output_308_20);
			half Alpha337 = _MainAlpha;
			half4 lerpResult223 = lerp( MainTexColor215 , _DissolveColor , ( _DissolveColor.a * ( 1.0 - smoothstepResult264 ) * Alpha337 ));
			#ifdef _FDISSOLVETEX_ON
				half4 staticSwitch298 = lerpResult223;
			#else
				half4 staticSwitch298 = MainTexColor215;
			#endif
			half4 temp_cast_0 = (0.0).xxxx;
			half Refnl339 = _ReFnl;
			float3 ase_worldPos = i.worldPos;
			half3 ase_worldViewDir = normalize( UnityWorldSpaceViewDir( ase_worldPos ) );
			half3 ase_worldNormal = i.worldNormal;
			half fresnelNdotV279 = dot( ase_worldNormal, ase_worldViewDir );
			half fresnelNode279 = ( 0.0 + _FnlScale * pow( 1.0 - fresnelNdotV279, _FnlPower ) );
			half temp_output_283_0 = saturate( fresnelNode279 );
			half4 FnlMainColor286 = ( _FnlColor * temp_output_283_0 * _FnlColor.a );
			half4 temp_cast_1 = (0.0).xxxx;
			#ifdef _FFNL_ON
				half4 staticSwitch300 = ( Refnl339 == 0.0 ? FnlMainColor286 : temp_cast_1 );
			#else
				half4 staticSwitch300 = temp_cast_0;
			#endif
			float4 MainColor98 = ( i.vertexColor * ( staticSwitch298 + staticSwitch300 ) );
			float MainTexAlpha138 = ( _MainColor.a * ( _MainTexAR == 0.0 ? tex2DNode7_g42.a : tex2DNode7_g42.r ) );
			half2 appendResult4_g44 = (half2(_MaskTexUSpeed , _MaskTexVSpeed));
			float2 uv_MaskTex = i.uv_texcoord * _MaskTex_ST.xy + _MaskTex_ST.zw;
			half2 temp_output_3_0_g43 = uv_MaskTex;
			#ifdef _FDISTORTTEX_ON
				half2 staticSwitch312 = ( _DistortMaskTex == 0.0 ? temp_output_3_0_g43 : ( temp_output_3_0_g43 + Distort148 ) );
			#else
				half2 staticSwitch312 = uv_MaskTex;
			#endif
			half2 panner5_g44 = ( 1.0 * _Time.y * appendResult4_g44 + staticSwitch312);
			half4 tex2DNode7_g44 = tex2D( _MaskTex, panner5_g44 );
			#ifdef _FMASKTEX_ON
				half staticSwitch291 = ( _MaskTexAR == 0.0 ? tex2DNode7_g44.a : tex2DNode7_g44.r );
			#else
				half staticSwitch291 = 1.0;
			#endif
			half temp_output_270_0 = (-temp_output_277_0 + (temp_output_275_0 - 0.0) * (1.0 - -temp_output_277_0) / (1.0 - 0.0));
			half smoothstepResult256 = smoothstep( temp_output_270_0 , ( temp_output_270_0 + temp_output_277_0 ) , temp_output_308_20);
			half DissolveAlpha212 = smoothstepResult256;
			#ifdef _FDISSOLVETEX_ON
				half staticSwitch299 = DissolveAlpha212;
			#else
				half staticSwitch299 = 1.0;
			#endif
			half ReFnlAlpha318 = ( 1.0 - temp_output_283_0 );
			#ifdef _FFNL_ON
				half staticSwitch319 = ( Refnl339 == 0.0 ? 1.0 : ReFnlAlpha318 );
			#else
				half staticSwitch319 = 1.0;
			#endif
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			half4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float screenDepth348 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			half distanceDepth348 = abs( ( screenDepth348 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( _DepthFade ) );
			#ifdef _FDEPTH_ON
				half staticSwitch350 = saturate( distanceDepth348 );
			#else
				half staticSwitch350 = 1.0;
			#endif
			float MainAlpha97 = saturate( ( MainTexAlpha138 * staticSwitch291 * i.vertexColor.a * Alpha337 * staticSwitch299 * staticSwitch319 * staticSwitch350 ) );
			o.Emission = ( Scr106 == 5.0 ? MainColor98 : ( MainColor98 * MainAlpha97 ) ).rgb;
			half temp_output_100_0 = MainAlpha97;
			o.Alpha = temp_output_100_0;
		}

		ENDCG
	}
	CustomEditor "SampleGUI"
}
