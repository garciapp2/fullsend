// Gradient skybox (top / horizon / bottom) with a soft sun, so each biome controls its sky colour exactly.
// The horizon colour matches the fog, so the distant terrain blends into the sky.
Shader "Sledbonk/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.3, 0.5, 0.95, 1)
        _Horizon ("Horizon", Color) = (0.85, 0.9, 1, 1)
        _Bottom ("Bottom", Color) = (0.6, 0.65, 0.8, 1)
        _HorizonPow ("Horizon Falloff", Range(0.1, 2)) = 0.55
        _SunColor ("Sun", Color) = (1, 0.95, 0.85, 1)
        _SunDir ("Sun Direction", Vector) = (0, 0.5, 1, 0)
        _SunSize ("Sun Size", Range(0.001, 0.1)) = 0.025
        _SunGlow ("Sun Glow", Range(0, 2)) = 0.6
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Top, _Horizon, _Bottom, _SunColor; float4 _SunDir; half _HorizonPow, _SunSize, _SunGlow;
            CBUFFER_END
            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.dir = i.pos.xyz; return o; }
            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                half3 col = d.y >= 0 ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(d.y), _HorizonPow)) : lerp(_Horizon.rgb, _Bottom.rgb, saturate(-d.y * 4.0));
                float s = dot(d, normalize(_SunDir.xyz));
                half disk = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.6, s);
                half glow = pow(saturate(s), 64.0) * _SunGlow + pow(saturate(s), 6.0) * _SunGlow * 0.25;
                col += _SunColor.rgb * (disk * 3.0 + glow);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
