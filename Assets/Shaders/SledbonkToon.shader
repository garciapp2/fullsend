// Flat comic shader for everything in Sledbonk (URP):
//  - flat colours: each surface has ONE colour and two darker tones (global _ToneMid / _ToneLow tints),
//    no gradients, no ambient wash
//  - the light is cut into those three tones, with screen-space halftone dots in the transitions
//    (dots grow as the surface darkens)
//  - environment colour: with _PaletteSlot >= 0 the colour comes from the biome palette
//    (_Pal0.._Pal4: ground, rock, foliage, wood, accent), so a new biome repaints the whole world;
//    the texture only adds a little light/dark detail (_GlobalTexStrength)
//  - black outlines come from the full-screen pass (Sledbonk/Comic) reading depth + normals
Shader "Sledbonk/Toon"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        [Toggle] _UseVertexColor ("Use Vertex Color", Float) = 0
        _PaletteSlot ("Palette Slot (-1 = own colour)", Float) = -1
        _TexStrength ("Texture Detail (-1 = global)", Float) = -1
        _ShadeColor ("(unused) Shade Tint", Color) = (0.55,0.62,0.85,1)
        _Bands ("(unused)", Range(0.001, 0.5)) = 0.06
        _RimColor ("Rim", Color) = (1,1,1,0)
        _Emission ("Emission", Color) = (0,0,0,0)
        _TopColor ("Top Color", Color) = (1,1,1,1)
        _TopAmount ("Top Blend", Range(0,1)) = 0
        _TopSharp ("Top Sharpness", Range(0.01,1)) = 0.35
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        // 1 on the player's materials: the collapse decal (Sledbonk/Corruption) skips pixels marked 1
        [IntRange] _StencilRef ("Stencil Mark", Range(0, 255)) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST; half4 _BaseColor; half _UseVertexColor; half _PaletteSlot; half _TexStrength; half4 _ShadeColor; half _Bands;
        half4 _RimColor; half4 _Emission; half _Cutoff; half _Cull; half4 _TopColor; half _TopAmount; half _TopSharp;
    CBUFFER_END
    TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
    // globals set per biome by BiomeLibrary
    half4 _Pal0, _Pal1, _Pal2, _Pal3, _Pal4;
    half4 _ToneMid, _ToneLow;
    float _HalftoneScale, _GlobalTexStrength;

    half3 Palette(int s) { return s == 0 ? _Pal0.rgb : s == 1 ? _Pal1.rgb : s == 2 ? _Pal2.rgb : s == 3 ? _Pal3.rgb : _Pal4.rgb; }
    half AlphaOf(float2 uv) { return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a; }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            Stencil { Ref [_StencilRef] Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; half4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; float3 n : TEXCOORD2; half4 col : COLOR; half fog : TEXCOORD3; };

            V vert(A i)
            {
                V o;
                VertexPositionInputs p = GetVertexPositionInputs(i.pos.xyz);
                o.pos = p.positionCS; o.wpos = p.positionWS; o.n = TransformObjectToWorldNormal(i.n);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap); o.col = i.col; o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // classic halftone: a 45-degree grid of dots in screen space; r = dot radius 0..1
            half Halftone(float2 px, half r)
            {
                float s = max(_HalftoneScale, 2.0);
                float2 q = float2(px.x + px.y, px.x - px.y) * 0.7071 / s;
                float d = length(frac(q) - 0.5) * 1.4142;
                return step(d, r);
            }

            half4 frag(V i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(tex.a * _BaseColor.a - _Cutoff);
                half texStr = _TexStrength >= 0 ? _TexStrength : _GlobalTexStrength;
                half3 col;
                if (_PaletteSlot >= 0)
                {
                    half lum = dot(tex.rgb, half3(0.3, 0.59, 0.11));
                    col = Palette((int)(_PaletteSlot + 0.5)) * lerp(1.0, saturate(lum * 1.5 + 0.25), texStr);
                }
                else col = tex.rgb * _BaseColor.rgb;
                if (_UseVertexColor > 0.5) col *= i.col.rgb;
                float3 n = normalize(i.n);
                half top = step(1.0 - _TopSharp * 0.5, n.y) * _TopAmount;   // flat: hard cut instead of a blend
                col = lerp(col, _TopColor.rgb, top);

                Light L = GetMainLight(TransformWorldToShadowCoord(i.wpos));
                // hard shadows (comic): soft edges would turn into scattered dots
                half sh = smoothstep(0.42, 0.58, L.shadowAttenuation);
                half v = saturate(dot(n, L.direction) * 1.2) * lerp(0.3, 1.0, sh);
                half3 hi = col, mid = col * _ToneMid.rgb, lo = col * _ToneLow.rgb;
                half3 c;
                if (v > 0.42) c = lerp(hi, mid, Halftone(i.pos.xy, saturate((0.62 - v) / 0.2) * 0.75));
                else c = lerp(mid, lo, Halftone(i.pos.xy, saturate((0.42 - v) / 0.42) * 0.95));
                c *= L.color * 0.85 + 0.15;
                float3 vd = normalize(GetWorldSpaceViewDir(i.wpos));
                c += step(0.82, 1.0 - saturate(dot(n, vd))) * _RimColor.rgb * _RimColor.a;
                c += _Emission.rgb;
                c = MixFog(c, i.fog);
                return half4(c, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex sv
            #pragma fragment sf
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V sv(A i)
            {
                V o;
                float3 w = TransformObjectToWorld(i.pos.xyz);
                float3 n = TransformObjectToWorldNormal(i.n);
                o.pos = TransformWorldToHClip(ApplyShadowBias(w, n, _LightDirection));
                #if UNITY_REVERSED_Z
                    o.pos.z = min(o.pos.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.pos.z = max(o.pos.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }
            half4 sf(V i) : SV_Target { clip(AlphaOf(i.uv) - _Cutoff); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex dv
            #pragma fragment df
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V dv(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = TRANSFORM_TEX(i.uv, _BaseMap); return o; }
            half df(V i) : SV_Target { clip(AlphaOf(i.uv) - _Cutoff); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex nv
            #pragma fragment nf
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float2 uv : TEXCOORD1; };
            V nv(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.n = TransformObjectToWorldNormal(i.n); o.uv = TRANSFORM_TEX(i.uv, _BaseMap); return o; }
            half4 nf(V i) : SV_Target { clip(AlphaOf(i.uv) - _Cutoff); return half4(normalize(i.n), 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
