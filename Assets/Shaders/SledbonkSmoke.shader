// Smoke for particles, shaded like the rest of the world: two flat tones (lit, and lit * the biome's _ToneMid)
// with the same screen-space halftone dots the toon shader uses in its transitions, and a clean cartoon-cloud edge.
// The puff texture carries R = lighting (the whole cloud lit from the top-left), A = shape.
// It is drawn on its own layer after the comic outline pass, so no ink line from behind shows through it.
Shader "Sledbonk/Smoke"
{
    Properties
    {
        _MainTex ("Puffs (R lit, A shape)", 2D) = "white" {}
        _Light ("Lit", Color) = (1, 0.98, 0.95, 1)
        _Opacity ("Opacity", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Smoke"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; half4 _Light; half _Opacity;
            CBUFFER_END
            half4 _ToneMid; float _HalftoneScale;

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; float fog : TEXCOORD1; };

            V vert(A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                o.col = i.col;
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }

            // the toon shader's halftone: a 45-degree grid of dots in screen space; r = dot radius 0..1
            half Halftone(float2 px, half r)
            {
                float s = max(_HalftoneScale, 2.0);
                float2 q = float2(px.x + px.y, px.x - px.y) * 0.7071 / s;
                float d = length(frac(q) - 0.5) * 1.4142;
                return step(d, r);
            }

            half4 frag(V i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half3 hi = _Light.rgb, mid = _Light.rgb * _ToneMid.rgb;
                // flat lit tone; dots grow into the shadow tone across a short band, then flat shadow
                half r = saturate((0.58 - t.r) / 0.14);
                half3 c = lerp(hi, mid, saturate(Halftone(i.pos.xy, r * 0.95) + step(t.r, 0.44)));
                c = MixFog(c, i.fog);
                return half4(c, t.a * i.col.a * _Opacity);
            }
            ENDHLSL
        }
    }
}
