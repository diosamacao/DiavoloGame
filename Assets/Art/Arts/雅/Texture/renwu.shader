// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Smalleyes/renwu"
{
	Properties
	{
		_Albedo("Albedo", Range( 0 , 1)) = 1
		_Emission("Emission", Range( 0 , 1)) = 0
		_Normal("Normal", Range( 0 , 1)) = 1
		_Unagi_Body_D("Unagi_Body_D", 2D) = "white" {}
		_Unagi_Body_N("Unagi_Body_N", 2D) = "white" {}
		_Unagi_Body_M("Unagi_Body_M", 2D) = "white" {}
		_Metallic("Metallic", Range( 0 , 1)) = 1
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IsEmissive" = "true"  }
		Cull Back
		CGPROGRAM
		#pragma target 3.0
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows 
		struct Input
		{
			float2 uv_texcoord;
		};

		uniform float _Normal;
		uniform sampler2D _Unagi_Body_N;
		uniform float4 _Unagi_Body_N_ST;
		uniform float _Albedo;
		uniform sampler2D _Unagi_Body_D;
		uniform float4 _Unagi_Body_D_ST;
		uniform float _Emission;
		uniform sampler2D _Unagi_Body_M;
		uniform float4 _Unagi_Body_M_ST;
		uniform float _Metallic;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_Unagi_Body_N = i.uv_texcoord * _Unagi_Body_N_ST.xy + _Unagi_Body_N_ST.zw;
			o.Normal = ( _Normal * tex2D( _Unagi_Body_N, uv_Unagi_Body_N ) ).rgb;
			float2 uv_Unagi_Body_D = i.uv_texcoord * _Unagi_Body_D_ST.xy + _Unagi_Body_D_ST.zw;
			float4 tex2DNode2 = tex2D( _Unagi_Body_D, uv_Unagi_Body_D );
			o.Albedo = ( _Albedo * tex2DNode2 ).rgb;
			o.Emission = ( tex2DNode2 * _Emission ).rgb;
			float2 uv_Unagi_Body_M = i.uv_texcoord * _Unagi_Body_M_ST.xy + _Unagi_Body_M_ST.zw;
			o.Metallic = ( tex2D( _Unagi_Body_M, uv_Unagi_Body_M ) * _Metallic ).r;
			o.Smoothness = 0.0;
			o.Occlusion = 0.0;
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18800
2553;6;1920;1013;485;396.5;1;True;True
Node;AmplifyShaderEditor.SamplerNode;2;-354,-281.5;Inherit;True;Property;_Unagi_Body_D;Unagi_Body_D;3;0;Create;True;0;0;0;False;0;False;-1;8d3e16eb7df59ad4dae5c1bfece7fa5f;8d3e16eb7df59ad4dae5c1bfece7fa5f;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;5;-295,39.5;Inherit;True;Property;_Unagi_Body_N;Unagi_Body_N;4;0;Create;True;0;0;0;False;0;False;-1;22f957cf23694d64da4318ac01fffe44;22f957cf23694d64da4318ac01fffe44;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;7;-287,229.5;Inherit;True;Property;_Unagi_Body_M;Unagi_Body_M;5;0;Create;True;0;0;0;False;0;False;-1;95bfa52db78cc4142989668ba8d1fcd2;95bfa52db78cc4142989668ba8d1fcd2;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;9;36,335.5;Inherit;False;Property;_Metallic;Metallic;6;0;Create;True;0;0;0;False;0;False;1;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;4;-258,-64.5;Inherit;False;Property;_Normal;Normal;2;0;Create;True;0;0;0;False;0;False;1;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1;-302,-373.5;Inherit;False;Property;_Albedo;Albedo;0;0;Create;True;0;0;0;False;0;False;1;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;14;-57,-118.5;Inherit;False;Property;_Emission;Emission;1;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;3;-15,-323.5;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;6;106,-56.5;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;8;259,148.5;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;13;302,-148.5;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;15;846,-55.5;Inherit;False;Constant;_Smothness;Smothness;7;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;16;865,76.5;Inherit;False;Constant;_Ao;Ao;7;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;12;539,-107;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;Smalleyes/renwu;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;0;0;False;-1;0;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;3;0;1;0
WireConnection;3;1;2;0
WireConnection;6;0;4;0
WireConnection;6;1;5;0
WireConnection;8;0;7;0
WireConnection;8;1;9;0
WireConnection;13;0;2;0
WireConnection;13;1;14;0
WireConnection;12;0;3;0
WireConnection;12;1;6;0
WireConnection;12;2;13;0
WireConnection;12;3;8;0
WireConnection;12;4;15;0
WireConnection;12;5;16;0
ASEEND*/
//CHKSM=31EB938FF8F577A2FCD51516EF27D783306D53BE