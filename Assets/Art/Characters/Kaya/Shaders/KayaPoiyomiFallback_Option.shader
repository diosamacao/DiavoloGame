// 承接 Hair_Bob / Body_Option / Body_Emissive 丢失的 Poiyomi 锁定 Shader GUID。
Shader "Kaya/PoiyomiFallback/Option"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(-0.001, 1)) = 0.5
        _AlphaMask ("Alpha Mask", 2D) = "white" {}
        _AlphaMaskMode ("Alpha Mask Mode", Float) = 0
        _AlphaMaskScale ("Alpha Mask Scale", Float) = 1
        _AlphaMaskValue ("Alpha Mask Value", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
        _OutlineWidth ("Outline Width", Float) = 0.08
        _OutlineColor ("Outline Color", Color) = (0.2, 0.15, 0.15, 1)
        _EmissionMap ("Emission Map", 2D) = "black" {}
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _UseEmission ("Use Emission", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull [_Cull]
        ZWrite [_ZWrite]
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex KayaVert
            #pragma fragment KayaFrag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "KayaPoiyomiFallback.cginc"
            ENDCG
        }

        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On
            CGPROGRAM
            #pragma vertex KayaOutlineVert
            #pragma fragment KayaOutlineFrag
            #include "KayaPoiyomiFallback.cginc"
            ENDCG
        }

        Pass
        {
            Name "SHADOWCASTER"
            Tags { "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma vertex KayaShadowVert
            #pragma fragment KayaShadowFrag
            #pragma multi_compile_shadowcaster
            #include "KayaPoiyomiFallback.cginc"
            ENDCG
        }
    }

    FallBack "Diffuse"
}
