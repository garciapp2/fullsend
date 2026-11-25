// Energy beam for the big lasers (premultiplied blend: it adds light AND tints what's behind, so it still reads
// on white snow and bright sky). On a cylinder: bright where the surface faces the camera (the middle of the
// beam), soft at the silhouette, with energy stripes scrolling along its length and a white-hot centre line.
// With _Radial = 1 it draws a round soft glow on quads instead (impact flares, spark particles).
Shader "Sledbonk/Beam"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.08, 0.3, 1)
        _Intensity ("Intensity", Float) = 1.5
        _Hot ("White-hot centre", Float) = 1
        _Length ("Length (m)", Float) = 50
        _Scroll ("Stripe scroll (m/s)", Float) = 60
        _Radial ("Radial glow (quads)", Float) = 0
        _Opacity ("Tint strength", Float) = 0.75
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            Name "Beam"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color; float _Intensity, _Hot, _Length, _Scroll, _Radial, _Opacity;
            CBUFFER_END

            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; half4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; float2 uv : TEXCOORD2; half4 col : COLOR; };

            V vert(A i)
            {
                V o;
                float3 ws = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(ws);
                o.n = TransformObjectToWorldNormal(i.n);
                o.v = GetWorldSpaceViewDir(ws);
                o.uv = i.uv; o.col = i.col;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                if (_Radial > 0.5)
                {
                    float d = saturate(1.0 - length(i.uv - 0.5) * 2.0);
                    float a = d * d;
                    float3 c = _Color.rgb * a * _Intensity + pow(d, 6.0) * _Hot;
                    return half4(c * i.col.rgb * i.col.a, a * _Opacity * i.col.a);
                }
                float facing = abs(dot(normalize(i.n), normalize(i.v)));
                float glow = pow(facing, 1.5);
                float along = i.uv.y * _Length;
                float stripes = 0.62 + 0.38 * sin((along - _Time.y * _Scroll) * 0.35);
                float fine = 0.85 + 0.15 * sin((along + _Time.y * _Scroll * 1.7) * 1.9);
                float hot = pow(facing, 14.0) * _Hot;
                float3 c = _Color.rgb * glow * stripes * fine * _Intensity + hot * float3(1.0, 0.92, 0.96);
                return half4(c, saturate(glow * stripes * _Opacity));
            }
            ENDHLSL
        }
    }
}
