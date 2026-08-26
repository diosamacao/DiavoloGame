// Kaya 丢失 Poiyomi 锁定 Shader 后的 Built-in 回退实现。
// 只读取原材质已序列化的 _MainTex/_Color/_Cutoff/_AlphaMask/_Outline* /_UseEmission，不改 .mat。
#ifndef KAYA_POIYOMI_FALLBACK_CGINC
#define KAYA_POIYOMI_FALLBACK_CGINC

#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "AutoLight.cginc"

sampler2D _MainTex;
float4 _MainTex_ST;
float4 _Color;
sampler2D _AlphaMask;
float4 _AlphaMask_ST;
float _AlphaMaskMode;
float _AlphaMaskScale;
float _AlphaMaskValue;
float _Cutoff;
sampler2D _EmissionMap;
float4 _EmissionColor;
float _UseEmission;
float _OutlineWidth;
float4 _OutlineColor;

struct KayaAppdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
};

struct KayaV2F
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 worldNormal : TEXCOORD1;
    float3 worldPos : TEXCOORD2;
    UNITY_SHADOW_COORDS(3)
    UNITY_FOG_COORDS(4)
};

// Poiyomi AlphaMaskMode>0 时用遮罩缩放主贴图 alpha，供刘海/睫毛裁切。
float KayaSampleAlpha(float2 mainUV, float mainA)
{
    float alpha = mainA * _Color.a;
    if (_AlphaMaskMode > 0.5)
    {
        float mask = tex2D(_AlphaMask, TRANSFORM_TEX(mainUV, _AlphaMask)).r;
        alpha *= saturate(mask * _AlphaMaskScale + _AlphaMaskValue);
    }
    return alpha;
}

void KayaClipAlpha(float2 mainUV, float mainA)
{
    clip(KayaSampleAlpha(mainUV, mainA) - max(_Cutoff, 0.0));
}

KayaV2F KayaVert(KayaAppdata v)
{
    KayaV2F o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    o.worldNormal = UnityObjectToWorldNormal(v.normal);
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    UNITY_TRANSFER_SHADOW(o, v.uv);
    UNITY_TRANSFER_FOG(o, o.pos);
    return o;
}

fixed4 KayaFrag(KayaV2F i) : SV_Target
{
    float4 tex = tex2D(_MainTex, i.uv);
    float alpha = KayaSampleAlpha(i.uv, tex.a);
    clip(alpha - max(_Cutoff, 0.0));

    float3 albedo = tex.rgb * _Color.rgb;

#ifdef KAYA_FALLBACK_UNLIT
    // FakeShadow 等乘法叠加层：保持原色，避免再打光把脸涂脏。
    float3 color = albedo;
#else
    float3 normal = normalize(i.worldNormal);
    float3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
    UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
    // 半 Lambert + 窄过渡，接近原 Toon 两阶阴影。
    float ndotl = dot(normal, lightDir) * 0.5 + 0.5;
    float shade = smoothstep(0.42, 0.58, ndotl * atten);
    float3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb;
    float3 lightCol = _LightColor0.rgb;
    float3 lighting = lerp(ambient + lightCol * 0.22, ambient + lightCol, shade);
    float3 color = albedo * lighting;
    color += tex2D(_EmissionMap, i.uv).rgb * _EmissionColor.rgb * _UseEmission;
#endif

    fixed4 output = fixed4(color, alpha);
    UNITY_APPLY_FOG(i.fogCoord, output);
    return output;
}

KayaV2F KayaOutlineVert(KayaAppdata v)
{
    KayaV2F o;
    // Poiyomi 的 OutlineWidth 通常再乘 0.01；材质里常见 0.05~0.1。
    float width = max(_OutlineWidth, 0.0) * 0.01;
    float3 objectPos = v.vertex.xyz + v.normal * width;
    o.pos = UnityObjectToClipPos(float4(objectPos, 1.0));
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    o.worldNormal = UnityObjectToWorldNormal(v.normal);
    o.worldPos = mul(unity_ObjectToWorld, float4(objectPos, 1.0)).xyz;
    UNITY_TRANSFER_SHADOW(o, v.uv);
    UNITY_TRANSFER_FOG(o, o.pos);
    return o;
}

fixed4 KayaOutlineFrag(KayaV2F i) : SV_Target
{
    float4 tex = tex2D(_MainTex, i.uv);
    KayaClipAlpha(i.uv, tex.a);
    return float4(_OutlineColor.rgb, 1.0);
}

struct KayaShadowV2F
{
    V2F_SHADOW_CASTER;
    float2 uv : TEXCOORD1;
};

KayaShadowV2F KayaShadowVert(appdata_base v)
{
    KayaShadowV2F o;
    TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
    o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
    return o;
}

float4 KayaShadowFrag(KayaShadowV2F i) : SV_Target
{
    float4 tex = tex2D(_MainTex, i.uv);
    KayaClipAlpha(i.uv, tex.a);
    SHADOW_CASTER_FRAGMENT(i)
}

#endif
