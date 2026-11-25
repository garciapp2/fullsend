// The collapse: a huge sphere that paints whatever is inside it, as a decal read from the depth buffer. One pass with
// three zones: thin white cracks over the world, then cracks glowing electric blue that eat the ground cell by cell
// and leave it a flat dark violet. Comic look: hard-edged tones, halftone dots, no gradients.
//   k = distance to _Center / _Radius:   k > _Outer: untouched;  1.._Outer: white cracks growing;
//   _Inner..1: cells turning dark from their cracks inward, with an electric rim;  < _Inner: all dark.
Shader "Sledbonk/Corruption"
{
    Properties
    {
        _Cracks ("Cracks (R: 1 cell centre, 0 crack)", 2D) = "white" {}
        _Voronoi ("Voronoi (warp)", 2D) = "gray" {}
        _Dark ("Dark", Color) = (0.09, 0.0, 0.37, 1)
        _Mid ("Mid", Color) = (0.29, 0.0, 0.56, 1)
        _Glow ("Glow", Color) = (0.25, 0.85, 1.0, 1)
        _White ("White cracks", Color) = (1, 1, 1, 1)
        _Center ("Center", Vector) = (0, 0, 0, 0)
        _Radius ("Radius", Float) = 300
        _Outer ("Outer (cracks reach)", Float) = 1.5
        _Inner ("Inner (all dark)", Float) = 0.84
        _Scale ("Crack scale", Float) = 0.006
        _FineScale ("Fine crack scale", Float) = 0.016
        _Warp ("Warp (m)", Float) = 14
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Front
        Stencil { Ref 1 Comp NotEqual }    // the player (toon materials with _StencilRef 1) stays clean
        Pass
        {
            Name "Corruption"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_Cracks); SAMPLER(sampler_Cracks);
            TEXTURE2D(_Voronoi); SAMPLER(sampler_Voronoi);
            CBUFFER_START(UnityPerMaterial)
                half4 _Dark, _Mid, _Glow, _White;
                float4 _Center;
                float _Radius, _Outer, _Inner, _Scale, _FineScale, _Warp;
            CBUFFER_END
            float _HalftoneScale;

            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float4 scr : TEXCOORD0; };

            V vert(A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.scr = ComputeScreenPos(o.pos);
                return o;
            }

            half Halftone(float2 px, half r)
            {
                float s = max(_HalftoneScale, 2.0);
                float2 q = float2(px.x + px.y, px.x - px.y) * 0.7071 / s;
                float d = length(frac(q) - 0.5) * 1.4142;
                return step(d, r);
            }

            half4 frag(V i) : SV_Target
            {
                float2 uv = i.scr.xy / i.scr.w;
                float raw = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                clip(raw - 1e-6);                       // sky: leave it
            #else
                raw = lerp(UNITY_NEAR_CLIP_VALUE, 1, raw);
                clip(0.999999 - raw);
            #endif
                float3 wp = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float k = distance(wp, _Center.xyz) / max(_Radius, 1.0);
                clip(_Outer - k);

                // project the crack sheet along the surface's main axis (ground: top-down; walls: from the side)
                float3 n = abs(normalize(cross(ddy(wp), ddx(wp))));
                float2 p = n.y > 0.55 ? wp.xz : (n.x > n.z ? wp.zy : wp.xy);
                float2 warp = (SAMPLE_TEXTURE2D(_Voronoi, sampler_Voronoi, p * 0.004).rg - 0.5) * _Warp;
                float c = SAMPLE_TEXTURE2D(_Cracks, sampler_Cracks, (p + warp) * _Scale).r;
                float f = SAMPLE_TEXTURE2D(_Cracks, sampler_Cracks, (p - warp) * _FineScale + 0.37).r;
                float t = _Time.y;
                float wob = sin(t * 2.3 + p.x * 0.05 + p.y * 0.04) * 0.03;

                // how far in: 0 at the outer reach, 1 at the main rim
                float outerT = saturate((_Outer - k) / max(_Outer - 1.0, 0.01));
                // 0 at the main rim, 1 at the inner core
                float innerT = saturate((1.0 - k) / max(1.0 - _Inner, 0.01));

                // the dark eats each cell from its cracks inward: cells go when their value drops under th
                float th = innerT * 1.08 + wob;
                half dark = step(c, th);
                // electric rim on the edge of the dark (only while the cell is still going)
                half rim = (1 - dark) * step(c, th + 0.07) * step(0.001, innerT);
                // white cracks ahead of it: thin lines that thicken toward the rim, the fine sheet joins in later
                half white = step(c, 0.035 + 0.075 * outerT + wob * 0.5) + step(f, 0.05 * saturate(outerT * 1.6 - 0.6));
                white = saturate(white) * (1 - dark) * (1 - rim);

                half4 col = 0;
                if (dark > 0.5)
                {
                    // flat dark violet, purple dots toward the cell centres (comic shading of the void)
                    half dots = Halftone(i.pos.xy, saturate((c - th * 0.55) * 1.6) * 0.8);
                    col = half4(lerp(_Dark.rgb, _Mid.rgb, dots), 1);
                    // faces tipped out of the ground plane (slabs going over, walls) take the lighter violet,
                    // dotting back to dark as they turn further, so the falling world reads as shapes
                    if (n.y < 0.82)
                    {
                        half3 lit = _Mid.rgb * 1.35;
                        col.rgb = lerp(lit, _Dark.rgb, Halftone(i.pos.xy, saturate((0.82 - n.y) * 1.4)));
                    }
                }
                else if (rim > 0.5) col = half4(_Glow.rgb * 1.6, 1);
                else if (white > 0.5) col = half4(_White.rgb * 1.3, 1);
                clip(col.a - 0.5);
                return col;
            }
            ENDHLSL
        }
    }
}
