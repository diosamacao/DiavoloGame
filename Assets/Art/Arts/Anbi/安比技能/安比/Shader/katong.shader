// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "ASE/katong"
{
	Properties
	{
		_02anbu_ruanying("02anbu_ruanying", Range( 0 , 1)) = 0
		_02anbu_fanwei("02anbu_fanwei", Range( -1 , 1)) = 1
		_01NormalMap("01NormalMap", 2D) = "white" {}
		_04diffuseTex("04diffuseTex", 2D) = "white" {}
		_04diffuseColor("04diffuseColor", Color) = (0,0,0,0)
		_05Rimoffser("05Rimoffser", Float) = 0
		_05RimPower("05RimPower", Float) = 2
		_06specpower("06specpower", Float) = 0
		_05Color0("05Color 0", Color) = (0,0,0,0)
		_06spec_min("06spec_min", Float) = 0
		_06spec_max("06spec_max", Float) = 0
		_06spec_int("06spec_int", Float) = 0
		_TextureSample0("Texture Sample 0", 2D) = "white" {}
		_06spec_lerp("06spec_lerp", Range( 0 , 1)) = 0
		_outlinecolor("outlinecolor", Color) = (0,0,0,0)
		_outlinewidth("outlinewidth", Range( 0 , 0.01)) = 0
		_TextureSample1("Texture Sample 1", 2D) = "white" {}
		[Toggle(_KEYWORD0_ON)] _Keyword0("Keyword 0", Float) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ }
		Cull Front
		CGPROGRAM
		#pragma target 3.0
		#pragma surface outlineSurf Outline nofog  keepalpha noshadow noambient novertexlights nolightmap nodynlightmap nodirlightmap nometa noforwardadd vertex:outlineVertexDataFunc 
		
		void outlineVertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float outlineVar = _outlinewidth;
			v.vertex.xyz += ( v.normal * outlineVar );
		}
		inline half4 LightingOutline( SurfaceOutput s, half3 lightDir, half atten ) { return half4 ( 0,0,0, s.Alpha); }
		void outlineSurf( Input i, inout SurfaceOutput o )
		{
			o.Emission = (_outlinecolor).rgba.rgb;
		}
		ENDCG
		

		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" }
		Cull Back
		CGINCLUDE
		#include "UnityPBSLighting.cginc"
		#include "UnityShaderVariables.cginc"
		#include "UnityCG.cginc"
		#include "Lighting.cginc"
		#pragma target 3.0
		#pragma shader_feature_local _KEYWORD0_ON
		#ifdef UNITY_PASS_SHADOWCASTER
			#undef INTERNAL_DATA
			#undef WorldReflectionVector
			#undef WorldNormalVector
			#define INTERNAL_DATA half3 internalSurfaceTtoW0; half3 internalSurfaceTtoW1; half3 internalSurfaceTtoW2;
			#define WorldReflectionVector(data,normal) reflect (data.worldRefl, half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal)))
			#define WorldNormalVector(data,normal) half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal))
		#endif
		struct Input
		{
			float3 worldNormal;
			INTERNAL_DATA
			float2 uv_texcoord;
			float3 worldPos;
		};

		struct SurfaceOutputCustomLightingCustom
		{
			half3 Albedo;
			half3 Normal;
			half3 Emission;
			half Metallic;
			half Smoothness;
			half Occlusion;
			half Alpha;
			Input SurfInput;
			UnityGIInput GIData;
		};

		uniform sampler2D _01NormalMap;
		uniform float4 _01NormalMap_ST;
		uniform sampler2D _04diffuseTex;
		uniform float4 _04diffuseTex_ST;
		uniform float4 _04diffuseColor;
		uniform float _05Rimoffser;
		uniform float _05RimPower;
		uniform sampler2D _TextureSample1;
		uniform float4 _TextureSample1_ST;
		uniform float4 _05Color0;
		uniform float _06spec_min;
		uniform float _06spec_max;
		uniform float _06specpower;
		uniform sampler2D _TextureSample0;
		uniform float4 _TextureSample0_ST;
		uniform float _06spec_lerp;
		uniform float _06spec_int;
		uniform float _02anbu_fanwei;
		uniform float _02anbu_ruanying;
		uniform float4 _outlinecolor;
		uniform float _outlinewidth;

		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			v.vertex.xyz += 0;
			v.vertex.w = 1;
		}

		inline half4 LightingStandardCustomLighting( inout SurfaceOutputCustomLightingCustom s, half3 viewDir, UnityGI gi )
		{
			UnityGIInput data = s.GIData;
			Input i = s.SurfInput;
			half4 c = 0;
			#ifdef UNITY_PASS_FORWARDBASE
			float ase_lightAtten = data.atten;
			if( _LightColor0.a == 0)
			ase_lightAtten = 0;
			#else
			float3 ase_lightAttenRGB = gi.light.color / ( ( _LightColor0.rgb ) + 0.000001 );
			float ase_lightAtten = max( max( ase_lightAttenRGB.r, ase_lightAttenRGB.g ), ase_lightAttenRGB.b );
			#endif
			#if defined(HANDLE_SHADOWS_BLENDING_IN_GI)
			half bakedAtten = UnitySampleBakedOcclusion(data.lightmapUV.xy, data.worldPos);
			float zDist = dot(_WorldSpaceCameraPos - data.worldPos, UNITY_MATRIX_V[2].xyz);
			float fadeDist = UnityComputeShadowFadeDistance(data.worldPos, zDist);
			ase_lightAtten = UnityMixRealtimeAndBakedShadows(data.atten, bakedAtten, UnityComputeShadowFade(fadeDist));
			#endif
			#if defined(LIGHTMAP_ON) && ( UNITY_VERSION < 560 || ( defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK) && defined(SHADOWS_SCREEN) ) )//aselc
			float4 ase_lightColor = 0;
			#else //aselc
			float4 ase_lightColor = _LightColor0;
			#endif //aselc
			float2 uv_01NormalMap = i.uv_texcoord * _01NormalMap_ST.xy + _01NormalMap_ST.zw;
			float4 NormalMap23 = tex2D( _01NormalMap, uv_01NormalMap );
			float3 indirectNormal100 = WorldNormalVector( i , NormalMap23.rgb );
			Unity_GlossyEnvironmentData g100 = UnityGlossyEnvironmentSetup( 0.5, data.worldViewDir, indirectNormal100, float3(0,0,0));
			float3 indirectSpecular100 = UnityGI_IndirectSpecular( data, 1.0, indirectNormal100, g100 );
			float2 uv_04diffuseTex = i.uv_texcoord * _04diffuseTex_ST.xy + _04diffuseTex_ST.zw;
			float4 Diffuse60 = ( tex2D( _04diffuseTex, uv_04diffuseTex ) * _04diffuseColor );
			float4 Light106 = ( ase_lightColor * float4( ( ase_lightAtten + indirectSpecular100 ) , 0.0 ) * Diffuse60 );
			float3 ase_worldPos = i.worldPos;
			float3 ase_worldViewDir = normalize( UnityWorldSpaceViewDir( ase_worldPos ) );
			float dotResult31 = dot( (WorldNormalVector( i , NormalMap23.rgb )) , ase_worldViewDir );
			float View32 = dotResult31;
			float2 uv_TextureSample1 = i.uv_texcoord * _TextureSample1_ST.xy + _TextureSample1_ST.zw;
			#if defined(LIGHTMAP_ON) && UNITY_VERSION < 560 //aseld
			float3 ase_worldlightDir = 0;
			#else //aseld
			float3 ase_worldlightDir = normalize( UnityWorldSpaceLightDir( ase_worldPos ) );
			#endif //aseld
			float3 normalizeResult73 = normalize( normalize( (WorldNormalVector( i , NormalMap23.rgb )) ) );
			float dotResult77 = dot( ase_worldlightDir , normalizeResult73 );
			float4 temp_cast_4 = (( ase_lightAtten * dotResult77 )).xxxx;
			#ifdef _KEYWORD0_ON
				float4 staticSwitch146 = temp_cast_4;
			#else
				float4 staticSwitch146 = tex2D( _TextureSample1, uv_TextureSample1 );
			#endif
			float4 Fre96 = ( saturate( ( pow( ( 1.0 - ( View32 + _05Rimoffser ) ) , _05RimPower ) * staticSwitch146 ) ) * ase_lightColor * _05Color0 );
			float3 normalizeResult4_g1 = normalize( ( ase_worldViewDir + ase_worldlightDir ) );
			float dotResult117 = dot( normalizeResult4_g1 , (WorldNormalVector( i , NormalMap23.rgb )) );
			float smoothstepResult120 = smoothstep( _06spec_min , _06spec_max , pow( dotResult117 , _06specpower ));
			float2 uv_TextureSample0 = i.uv_texcoord * _TextureSample0_ST.xy + _TextureSample0_ST.zw;
			float4 color134 = IsGammaSpace() ? float4(1,0.9035977,0.0990566,0) : float4(1,0.7945504,0.009877041,0);
			float4 lerpResult133 = lerp( color134 , ase_lightColor , _06spec_lerp);
			float4 spec129 = ( ( saturate( smoothstepResult120 ) * ( tex2D( _TextureSample0, uv_TextureSample0 ) * lerpResult133 ) ) * _06spec_int * ase_lightAtten );
			float3 normalizeResult4 = normalize( normalize( (WorldNormalVector( i , NormalMap23.rgb )) ) );
			float dotResult3 = dot( ase_worldlightDir , normalizeResult4 );
			float4 shadow46 = ( saturate( ( ( dotResult3 + _02anbu_fanwei ) / _02anbu_ruanying ) ) * Diffuse60 );
			c.rgb = ( Light106 + Fre96 + spec129 + shadow46 ).rgb;
			c.a = 1;
			return c;
		}

		inline void LightingStandardCustomLighting_GI( inout SurfaceOutputCustomLightingCustom s, UnityGIInput data, inout UnityGI gi )
		{
			s.GIData = data;
		}

		void surf( Input i , inout SurfaceOutputCustomLightingCustom o )
		{
			o.SurfInput = i;
			o.Normal = float3(0,0,1);
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf StandardCustomLighting keepalpha fullforwardshadows vertex:vertexDataFunc 

		ENDCG
		Pass
		{
			Name "ShadowCaster"
			Tags{ "LightMode" = "ShadowCaster" }
			ZWrite On
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile_shadowcaster
			#pragma multi_compile UNITY_PASS_SHADOWCASTER
			#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
			#include "HLSLSupport.cginc"
			#if ( SHADER_API_D3D11 || SHADER_API_GLCORE || SHADER_API_GLES || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_VULKAN )
				#define CAN_SKIP_VPOS
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			struct v2f
			{
				V2F_SHADOW_CASTER;
				float2 customPack1 : TEXCOORD1;
				float4 tSpace0 : TEXCOORD2;
				float4 tSpace1 : TEXCOORD3;
				float4 tSpace2 : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			v2f vert( appdata_full v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_INITIALIZE_OUTPUT( v2f, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				Input customInputData;
				vertexDataFunc( v, customInputData );
				float3 worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;
				half3 worldNormal = UnityObjectToWorldNormal( v.normal );
				half3 worldTangent = UnityObjectToWorldDir( v.tangent.xyz );
				half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
				half3 worldBinormal = cross( worldNormal, worldTangent ) * tangentSign;
				o.tSpace0 = float4( worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x );
				o.tSpace1 = float4( worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y );
				o.tSpace2 = float4( worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z );
				o.customPack1.xy = customInputData.uv_texcoord;
				o.customPack1.xy = v.texcoord;
				TRANSFER_SHADOW_CASTER_NORMALOFFSET( o )
				return o;
			}
			half4 frag( v2f IN
			#if !defined( CAN_SKIP_VPOS )
			, UNITY_VPOS_TYPE vpos : VPOS
			#endif
			) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				Input surfIN;
				UNITY_INITIALIZE_OUTPUT( Input, surfIN );
				surfIN.uv_texcoord = IN.customPack1.xy;
				float3 worldPos = float3( IN.tSpace0.w, IN.tSpace1.w, IN.tSpace2.w );
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.worldPos = worldPos;
				surfIN.worldNormal = float3( IN.tSpace0.z, IN.tSpace1.z, IN.tSpace2.z );
				surfIN.internalSurfaceTtoW0 = IN.tSpace0.xyz;
				surfIN.internalSurfaceTtoW1 = IN.tSpace1.xyz;
				surfIN.internalSurfaceTtoW2 = IN.tSpace2.xyz;
				SurfaceOutputCustomLightingCustom o;
				UNITY_INITIALIZE_OUTPUT( SurfaceOutputCustomLightingCustom, o )
				surf( surfIN, o );
				#if defined( CAN_SKIP_VPOS )
				float2 vpos = IN.pos;
				#endif
				SHADOW_CASTER_FRAGMENT( IN )
			}
			ENDCG
		}
	}
	Fallback "Diffuse"
	// 已移除 ASEMaterialInspector：项目未安装 Amplify Shader Editor，保留会导致选中材质时刷警告
}
/*ASEBEGIN
Version=18800
2553;12;1920;1007;855.9242;213.5452;1.296722;True;True
Node;AmplifyShaderEditor.CommentaryNode;26;-1224.418,-1011.054;Inherit;False;836.2686;396;Comment;4;24;25;20;19;;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;24;-692.8716,-975.0742;Inherit;False;274;166;注册;1;23;;1,1,1,1;0;0
Node;AmplifyShaderEditor.SamplerNode;20;-992.1494,-925.054;Inherit;True;Property;_01NormalMap;01NormalMap;2;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;34;-1419.747,-523.5722;Inherit;False;1036.25;397;03Rim;6;29;28;31;32;33;30;;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;23;-642.8716,-925.0742;Inherit;False;NormalMap;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;28;-1369.747,-472.1004;Inherit;False;23;NormalMap;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;30;-1108.779,-314.5723;Inherit;False;World;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WorldNormalVector;29;-1128.789,-473.5723;Inherit;False;False;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.CommentaryNode;98;-938.3821,-23.53544;Inherit;False;1921.828;1050.198;05Fre;20;75;76;62;64;73;63;74;70;67;65;77;66;72;71;92;95;94;93;96;145;;1,1,1,1;0;0
Node;AmplifyShaderEditor.DotProductOpNode;31;-833.4963,-454.421;Inherit;True;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;75;-783.7662,809.2511;Inherit;False;23;NormalMap;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;32;-607.4963,-459.421;Inherit;False;View;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;27;-315.2622,-321.2475;Inherit;False;23;NormalMap;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.WorldNormalVector;76;-594.09,806.2697;Inherit;False;True;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.CommentaryNode;17;-141.4352,-566.5425;Inherit;False;1728.222;456.748;Comment;13;46;3;13;2;4;59;10;6;7;5;45;47;88;;1,1,1,1;0;0
Node;AmplifyShaderEditor.GetLocalVarNode;114;-982.4581,1353.921;Inherit;False;23;NormalMap;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.FunctionNode;113;-894.1979,1143.791;Inherit;True;Blinn-Phong Half Vector;-1;;1;91a149ac9d615be429126c95e20753ce;0;0;1;FLOAT3;0
Node;AmplifyShaderEditor.WorldNormalVector;115;-799.5117,1347.294;Inherit;False;False;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;64;-888.3821,119.1467;Inherit;False;Property;_05Rimoffser;05Rimoffser;6;0;Create;True;0;0;0;False;0;False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.WorldNormalVector;2;-130.8995,-331.1411;Inherit;True;True;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.GetLocalVarNode;62;-887.3641,26.46454;Inherit;False;32;View;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.WorldSpaceLightDirHlpNode;74;-532.1413,618.4656;Inherit;True;False;1;0;FLOAT;0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.NormalizeNode;73;-392.7225,801.6621;Inherit;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;70;-417.382,355.1466;Inherit;False;300;275;光衰减;1;69;;1,1,1,1;0;0
Node;AmplifyShaderEditor.DotProductOpNode;77;-105.5268,628.2028;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LightAttenuation;69;-367.3821,405.1466;Inherit;True;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;119;-529.0695,1328.733;Inherit;False;Property;_06specpower;06specpower;8;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;89;1670.894,-596.214;Inherit;False;776;469;05Diffuse;4;61;58;60;57;;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleAddOpNode;63;-702.3821,34.14668;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WorldSpaceLightDirHlpNode;88;-79.10596,-527.2319;Inherit;False;False;1;0;FLOAT;0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.NormalizeNode;4;112.7588,-315.3171;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DotProductOpNode;117;-499.9048,1159.045;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;72;-31.38238,394.1466;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;65;-490.382,36.14668;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LightColorNode;136;-650.037,1989.246;Inherit;False;0;3;COLOR;0;FLOAT3;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;121;-295.747,1303.545;Inherit;False;Property;_06spec_min;06spec_min;10;0;Create;True;0;0;0;False;0;False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;45;324.0201,-255.2523;Inherit;False;Property;_02anbu_fanwei;02anbu_fanwei;1;0;Create;True;0;0;0;False;0;False;1;0.683;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;108;1239.434,2.034683;Inherit;False;1089.203;490;07Light;9;100;102;101;107;105;104;106;99;103;;1,1,1,1;0;0
Node;AmplifyShaderEditor.SamplerNode;145;-198.4846,239.0102;Inherit;True;Property;_TextureSample1;Texture Sample 1;17;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;134;-705.9231,1786.292;Inherit;False;Constant;_Color1;Color 1;13;0;Create;True;0;0;0;False;0;False;1,0.9035977,0.0990566,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;122;-275.8615,1491.794;Inherit;False;Property;_06spec_max;06spec_max;11;0;Create;True;0;0;0;False;0;False;0;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;137;-716.2179,2140.726;Inherit;False;Property;_06spec_lerp;06spec_lerp;14;0;Create;True;0;0;0;False;0;False;0;0.621;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;67;-398.3821,261.1466;Inherit;False;Property;_05RimPower;05RimPower;7;0;Create;True;0;0;0;False;0;False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;58;1748.894,-339.214;Inherit;False;Property;_04diffuseColor;04diffuseColor;5;0;Create;True;0;0;0;False;0;False;0,0,0,0;0.2735849,0.2645514,0.2645514,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DotProductOpNode;3;314.0378,-503.0018;Inherit;True;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;57;1720.894,-546.214;Inherit;True;Property;_04diffuseTex;04diffuseTex;4;0;Create;True;0;0;0;False;0;False;-1;None;9523fd0f3d2a3cf488554e438c5f8f33;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PowerNode;118;-305.0272,1164.347;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;146;156.8155,417.9595;Inherit;False;Property;_Keyword0;Keyword 0;18;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;131;-463.2607,1652.46;Inherit;True;Property;_TextureSample0;Texture Sample 0;13;0;Create;True;0;0;0;False;0;False;-1;None;ba39b2f8de78bef4fac6c7dd4499e20a;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;133;-360.313,1942.184;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.PowerNode;66;-216.3821,41.14668;Inherit;True;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;5;595.7692,-500.6701;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;99;1289.434,243.0019;Inherit;False;23;NormalMap;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.SmoothstepOpNode;120;3.860589,1189.535;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;61;2070.894,-467.214;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;6;601.8103,-239.7581;Inherit;False;Property;_02anbu_ruanying;02anbu_ruanying;0;0;Create;True;0;0;0;False;0;False;0;0.22;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.LightAttenuation;101;1508.819,165.4254;Inherit;False;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;123;250.4404,1190.861;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;132;-102.9438,1721.582;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;71;129.5883,45.41862;Inherit;True;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.IndirectSpecularLight;100;1506.478,274.6057;Inherit;False;Tangent;3;0;FLOAT3;0,0,1;False;1;FLOAT;0.5;False;2;FLOAT;1;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;7;815.5958,-501.1563;Inherit;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;60;2222.894,-482.214;Inherit;False;Diffuse;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;95;346.1797,437.8224;Inherit;False;Property;_05Color0;05Color 0;9;0;Create;True;0;0;0;False;0;False;0,0,0,0;1,1,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SaturateNode;10;1017.133,-501.3385;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;124;436.0378,1182.907;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;92;364.9079,47.03056;Inherit;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;105;1780.637,376.0347;Inherit;False;60;Diffuse;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.LightColorNode;94;362.4106,305.4775;Inherit;False;0;3;COLOR;0;FLOAT3;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleAddOpNode;102;1764.389,197.0816;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LightColorNode;103;1739.637,52.03468;Inherit;False;0;3;COLOR;0;FLOAT3;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;126;418.8036,1310.174;Inherit;False;Property;_06spec_int;06spec_int;12;0;Create;True;0;0;0;False;0;False;0;0.13;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;59;1000.361,-243.8235;Inherit;False;60;Diffuse;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.LightAttenuation;128;433.3864,1482.514;Inherit;False;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;104;1962.637,102.0347;Inherit;False;3;3;0;COLOR;0,0,0,0;False;1;FLOAT3;0,0,0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;13;1187.687,-505.2429;Inherit;True;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;93;614.6155,60.76443;Inherit;False;3;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;125;660.0806,1194.838;Inherit;False;3;3;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;140;2170.227,541.2211;Inherit;False;Property;_outlinecolor;outlinecolor;15;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;106;2104.637,101.0347;Inherit;False;Light;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;129;809.8845,1190.861;Inherit;False;spec;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;96;759.4457,72.0013;Inherit;False;Fre;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;46;1404.689,-505.864;Inherit;False;shadow;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.CommentaryNode;25;-680.7353,-789.3707;Inherit;False;245;161;接收;1;22;;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;142;2420.227,624.2211;Inherit;False;Property;_outlinewidth;outlinewidth;16;0;Create;True;0;0;0;False;0;False;0;0;0;0.01;0;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;141;2383.227,489.221;Inherit;False;True;True;True;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;97;2428.72,-187.5348;Inherit;False;96;Fre;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;138;2481.347,67.35974;Inherit;False;46;shadow;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;91;2420.874,-94.37302;Inherit;False;129;spec;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;81;2437.647,-280.2661;Inherit;False;106;Light;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.CommentaryNode;56;-171.7811,-1080.904;Inherit;False;1327;434;光照ramp图;8;48;54;52;53;50;49;55;51;;1,1,1,1;0;0
Node;AmplifyShaderEditor.FloorOpNode;52;517.2189,-1003.904;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;54;920.2189,-999.9042;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;22;-630.7353,-739.3708;Inherit;False;23;NormalMap;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;111;-1790.759,1326.334;Inherit;False;World;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.OutlineNode;139;2622.227,400.221;Inherit;False;0;True;None;0;0;Front;3;0;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;33;-592.4963,-326.421;Inherit;False;32;View;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;90;2663.755,-210.0167;Inherit;False;4;4;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.WorldSpaceLightDirHlpNode;109;-1825.346,1075.654;Inherit;True;False;1;0;FLOAT;0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;50;312.2189,-1003.904;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;49;86.21887,-1005.904;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;19;-1174.418,-874.5844;Inherit;False;Property;_01Normal_int;01Normal_int;3;0;Create;True;0;0;0;False;0;False;0.6;0.6;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;53;691.2189,-1002.904;Inherit;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.NormalizeNode;112;-1288.709,1090.371;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;47;1423.856,-325.5025;Inherit;False;46;shadow;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;107;2124.637,201.0347;Inherit;False;106;Light;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;110;-1519.652,1088.698;Inherit;True;2;2;0;FLOAT3;1,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;51;162.2189,-764.9042;Inherit;False;Constant;_fenduan;fenduan;5;0;Create;True;0;0;0;False;0;False;5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;130;843.0269,1306.197;Inherit;False;129;spec;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;48;-121.7811,-1030.904;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;55;743.2189,-762.9042;Inherit;False;Constant;_fanwei;fanwei;5;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;144;3016.111,-387.4116;Float;False;True;-1;2;ASEMaterialInspector;0;0;CustomLighting;ASE/katong;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;0;0;False;-1;0;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;23;0;20;0
WireConnection;29;0;28;0
WireConnection;31;0;29;0
WireConnection;31;1;30;0
WireConnection;32;0;31;0
WireConnection;76;0;75;0
WireConnection;115;0;114;0
WireConnection;2;0;27;0
WireConnection;73;0;76;0
WireConnection;77;0;74;0
WireConnection;77;1;73;0
WireConnection;63;0;62;0
WireConnection;63;1;64;0
WireConnection;4;0;2;0
WireConnection;117;0;113;0
WireConnection;117;1;115;0
WireConnection;72;0;69;0
WireConnection;72;1;77;0
WireConnection;65;0;63;0
WireConnection;3;0;88;0
WireConnection;3;1;4;0
WireConnection;118;0;117;0
WireConnection;118;1;119;0
WireConnection;146;1;145;0
WireConnection;146;0;72;0
WireConnection;133;0;134;0
WireConnection;133;1;136;0
WireConnection;133;2;137;0
WireConnection;66;0;65;0
WireConnection;66;1;67;0
WireConnection;5;0;3;0
WireConnection;5;1;45;0
WireConnection;120;0;118;0
WireConnection;120;1;121;0
WireConnection;120;2;122;0
WireConnection;61;0;57;0
WireConnection;61;1;58;0
WireConnection;123;0;120;0
WireConnection;132;0;131;0
WireConnection;132;1;133;0
WireConnection;71;0;66;0
WireConnection;71;1;146;0
WireConnection;100;0;99;0
WireConnection;7;0;5;0
WireConnection;7;1;6;0
WireConnection;60;0;61;0
WireConnection;10;0;7;0
WireConnection;124;0;123;0
WireConnection;124;1;132;0
WireConnection;92;0;71;0
WireConnection;102;0;101;0
WireConnection;102;1;100;0
WireConnection;104;0;103;0
WireConnection;104;1;102;0
WireConnection;104;2;105;0
WireConnection;13;0;10;0
WireConnection;13;1;59;0
WireConnection;93;0;92;0
WireConnection;93;1;94;0
WireConnection;93;2;95;0
WireConnection;125;0;124;0
WireConnection;125;1;126;0
WireConnection;125;2;128;0
WireConnection;106;0;104;0
WireConnection;129;0;125;0
WireConnection;96;0;93;0
WireConnection;46;0;13;0
WireConnection;141;0;140;0
WireConnection;52;0;50;0
WireConnection;54;0;53;0
WireConnection;54;1;55;0
WireConnection;139;0;141;0
WireConnection;139;1;142;0
WireConnection;90;0;81;0
WireConnection;90;1;97;0
WireConnection;90;2;91;0
WireConnection;90;3;138;0
WireConnection;50;0;49;0
WireConnection;50;1;51;0
WireConnection;49;0;48;1
WireConnection;53;0;52;0
WireConnection;53;1;51;0
WireConnection;112;0;110;0
WireConnection;110;0;109;0
WireConnection;110;1;111;0
WireConnection;144;13;90;0
WireConnection;144;11;139;0
ASEEND*/
//CHKSM=25358DC29A2BF171FCCBA5AD9F49E23018868F75