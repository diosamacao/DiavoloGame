Shader "ACTGame/Debug/WorldGrid"
{
    Properties
    {
        [Header(Grid)]
        _GridSize ("Cell Size (World Units)", Float) = 1
        _LineWidth ("Line Width", Range(0.5, 4)) = 1.2
        _MajorInterval ("Major Line Every N Cells", Float) = 5
        _MajorLineWidth ("Major Line Width Multiplier", Range(1, 3)) = 1.8

        [Header(Colors)]
        _CellColor ("Cell Color", Color) = (0.22, 0.24, 0.26, 1)
        _AltCellColor ("Alt Cell Color", Color) = (0.18, 0.20, 0.22, 1)
        _LineColor ("Line Color", Color) = (0.55, 0.58, 0.62, 1)
        _MajorLineColor ("Major Line Color", Color) = (0.85, 0.88, 0.92, 1)
        _OriginColor ("Origin Cross Color", Color) = (0.95, 0.35, 0.25, 1)

        [Header(Space)]
        [Toggle] _UseWorldSpace ("Use World Space", Float) = 1
        [Toggle] _Checkerboard ("Checkerboard Fill", Float) = 1
        [Toggle] _ShowOrigin ("Highlight Origin", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        LOD 200

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            float _GridSize;
            float _LineWidth;
            float _MajorInterval;
            float _MajorLineWidth;

            fixed4 _CellColor;
            fixed4 _AltCellColor;
            fixed4 _LineColor;
            fixed4 _MajorLineColor;
            fixed4 _OriginColor;

            float _UseWorldSpace;
            float _Checkerboard;
            float _ShowOrigin;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 objectPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                SHADOW_COORDS(4)
            };

            float GridLineAA(float coord, float width)
            {
                float grid = abs(frac(coord - 0.5) - 0.5);
                float derivative = fwidth(coord);
                return 1.0 - saturate(grid / max(derivative * width, 1e-5));
            }

            float2 GetGridUV(float3 position, float3 normal)
            {
                float3 blend = abs(normal);
                blend = blend / max(dot(blend, 1.0), 1e-5);

                float2 uvX = position.yz / _GridSize;
                float2 uvY = position.xz / _GridSize;
                float2 uvZ = position.xy / _GridSize;

                return uvX * blend.x + uvY * blend.y + uvZ * blend.z;
            }

            float MajorGridMask(float2 uv, float width)
            {
                float majorX = GridLineAA(uv.x / max(_MajorInterval, 1.0), width * _MajorLineWidth);
                float majorY = GridLineAA(uv.y / max(_MajorInterval, 1.0), width * _MajorLineWidth);
                return saturate(max(majorX, majorY));
            }

            float OriginMask(float3 position)
            {
                float2 origin = position.xz / _GridSize;
                float axisX = GridLineAA(origin.y, _LineWidth * 2.0);
                float axisZ = GridLineAA(origin.x, _LineWidth * 2.0);
                float nearOrigin = step(abs(origin.x), 0.35) * step(abs(origin.y), 0.35);
                return saturate(max(axisX, axisZ)) * nearOrigin;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.objectPos = v.vertex.xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos);
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 samplePos = lerp(i.objectPos, i.worldPos, _UseWorldSpace);
                float3 normal = normalize(i.worldNormal);

                float2 uv = GetGridUV(samplePos, normal);

                float minorX = GridLineAA(uv.x, _LineWidth);
                float minorY = GridLineAA(uv.y, _LineWidth);
                float minorMask = saturate(max(minorX, minorY));

                float majorMask = MajorGridMask(uv, _LineWidth);

                fixed4 baseColor = _CellColor;
                if (_Checkerboard > 0.5)
                {
                    float2 cell = floor(uv);
                    float checker = frac((cell.x + cell.y) * 0.5) * 2.0;
                    baseColor = lerp(_CellColor, _AltCellColor, checker);
                }

                fixed4 color = lerp(baseColor, _LineColor, minorMask);
                color = lerp(color, _MajorLineColor, majorMask);

                if (_ShowOrigin > 0.5 && _UseWorldSpace > 0.5)
                {
                    float originMask = OriginMask(i.worldPos);
                    color = lerp(color, _OriginColor, originMask);
                }

                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float ndotl = saturate(dot(normal, lightDir));
                float3 lighting = UNITY_LIGHTMODEL_AMBIENT.rgb + _LightColor0.rgb * ndotl;
                color.rgb *= lighting;

                UNITY_APPLY_FOG(i.fogCoord, color);
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
                color.rgb *= atten;

                return color;
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ForwardAdd" }

            Blend One One
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd_fullshadows

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            float _GridSize;
            float _LineWidth;
            float _MajorInterval;
            float _MajorLineWidth;

            fixed4 _CellColor;
            fixed4 _AltCellColor;
            fixed4 _LineColor;
            fixed4 _MajorLineColor;
            fixed4 _OriginColor;

            float _UseWorldSpace;
            float _Checkerboard;
            float _ShowOrigin;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 objectPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                SHADOW_COORDS(3)
            };

            float GridLineAA(float coord, float width)
            {
                float grid = abs(frac(coord - 0.5) - 0.5);
                float derivative = fwidth(coord);
                return 1.0 - saturate(grid / max(derivative * width, 1e-5));
            }

            float2 GetGridUV(float3 position, float3 normal)
            {
                float3 blend = abs(normal);
                blend = blend / max(dot(blend, 1.0), 1e-5);

                float2 uvX = position.yz / _GridSize;
                float2 uvY = position.xz / _GridSize;
                float2 uvZ = position.xy / _GridSize;

                return uvX * blend.x + uvY * blend.y + uvZ * blend.z;
            }

            float MajorGridMask(float2 uv, float width)
            {
                float majorX = GridLineAA(uv.x / max(_MajorInterval, 1.0), width * _MajorLineWidth);
                float majorY = GridLineAA(uv.y / max(_MajorInterval, 1.0), width * _MajorLineWidth);
                return saturate(max(majorX, majorY));
            }

            float OriginMask(float3 position)
            {
                float2 origin = position.xz / _GridSize;
                float axisX = GridLineAA(origin.y, _LineWidth * 2.0);
                float axisZ = GridLineAA(origin.x, _LineWidth * 2.0);
                float nearOrigin = step(abs(origin.x), 0.35) * step(abs(origin.y), 0.35);
                return saturate(max(axisX, axisZ)) * nearOrigin;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.objectPos = v.vertex.xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 samplePos = lerp(i.objectPos, i.worldPos, _UseWorldSpace);
                float3 normal = normalize(i.worldNormal);

                float2 uv = GetGridUV(samplePos, normal);

                float minorX = GridLineAA(uv.x, _LineWidth);
                float minorY = GridLineAA(uv.y, _LineWidth);
                float minorMask = saturate(max(minorX, minorY));

                float majorMask = MajorGridMask(uv, _LineWidth);

                fixed4 baseColor = _CellColor;
                if (_Checkerboard > 0.5)
                {
                    float2 cell = floor(uv);
                    float checker = frac((cell.x + cell.y) * 0.5) * 2.0;
                    baseColor = lerp(_CellColor, _AltCellColor, checker);
                }

                fixed4 color = lerp(baseColor, _LineColor, minorMask);
                color = lerp(color, _MajorLineColor, majorMask);

                if (_ShowOrigin > 0.5 && _UseWorldSpace > 0.5)
                {
                    float originMask = OriginMask(i.worldPos);
                    color = lerp(color, _OriginColor, originMask);
                }

                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz - i.worldPos * _WorldSpaceLightPos0.w);
                float ndotl = saturate(dot(normal, lightDir));
                fixed4 addColor = color * _LightColor0 * ndotl;

                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
                addColor.rgb *= atten;

                return addColor;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
