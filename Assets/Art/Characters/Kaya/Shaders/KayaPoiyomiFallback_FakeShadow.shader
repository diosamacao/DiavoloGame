// 承接头发 FakeShadow 丢失的 Poiyomi 锁定 Shader GUID；无描边，走材质已有的乘法混合。
Shader "Kaya/PoiyomiFallback/FakeShadow"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(-0.001, 1)) = 0
        _AlphaMask ("Alpha Mask", 2D) = "white" {}
        _AlphaMaskMode ("Alpha Mask Mode", Float) = 0
        _AlphaMaskScale ("Alpha Mask Scale", Float) = 1
        _AlphaMaskValue ("Alpha Mask Value", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 3
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
        _EmissionMap ("Emission Map", 2D) = "black" {}
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _UseEmission ("Use Emission", Float) = 0
        _OutlineWidth ("Outline Width", Float) = 0
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
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
            #define KAYA_FALLBACK_UNLIT 1
            #include "KayaPoiyomiFallback.cginc"
            ENDCG
        }
    }

    FallBack Off
}
