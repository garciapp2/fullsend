// Full-screen comic pass (URP Full Screen Pass Renderer Feature):
//  - outlines: Roberts Cross on depth and normals, drawn in the biome's ink colour; depth edges are
//    relative to distance so far mountains don't turn into noise
//  - impacts: radial blur and an RGB split, plus a flash, all driven from C# through globals (ComicFX)
Shader "Sledbonk/Comic"
{
    Properties
    {
        _Thickness ("Outline Thickness (px)", Float) = 1.4
        _DepthSens ("Depth Sensitivity", Float) = 0.06
        _NormalSens ("Normal Sensitivity", Float) = 0.8
        _NormalFadeDist ("Normal Edge Fade Distance", Float) = 300
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always
        Pass
        {
            Name "Comic"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _Thickness, _DepthSens, _NormalSens, _NormalFadeDist, _GrainStrength;
            TEXTURE2D(_GrainTex); SAMPLER(sampler_GrainTex);
            // globals from ComicFX
            float _FxRadial, _FxSplit, _FxFlash, _FxGrainSeed, _FxBlur, _FxOff;
            float4 _FxFlashColor, _InkColor;

            half3 Scene(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb; }
            float Depth(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                if (_FxOff > 0.5) return half4(Scene(uv), 1);   // portrait camera: untouched

                // impact: radial blur toward the centre + RGB split
                half3 col;
                if (_FxRadial > 0.001)
                {
                    col = 0; float2 dir = uv - 0.5;
                    [unroll] for (int k = 0; k < 8; k++) col += Scene(uv - dir * _FxRadial * (k / 8.0));
                    col /= 8.0;
                }
                else col = Scene(uv);
                // menu / results pages: blur the frozen game behind the UI
                if (_FxBlur > 0.001)
                {
                    half3 b = 0; float2 px1 = _FxBlur / _ScreenParams.xy * (_ScreenParams.y / 1080.0);
                    [unroll] for (int j = 0; j < 24; j++)
                    {
                        float a = j * 2.39996, r = sqrt((j + 0.5) / 24.0);
                        b += Scene(uv + float2(cos(a), sin(a)) * r * px1);
                    }
                    col = b / 24.0;
                }
                if (_FxSplit > 0.0001)
                {
                    float2 o = (uv - 0.5) * _FxSplit + float2(_FxSplit * 0.5, 0);
                    col.r = Scene(uv + o).r; col.b = Scene(uv - o).b;
                }

                // outlines (Roberts Cross)
                float2 px = _Thickness / _ScreenParams.xy;
                float2 a = uv + float2(-px.x, px.y), b = uv + float2(px.x, px.y), c = uv + float2(-px.x, -px.y), d = uv + float2(px.x, -px.y);
                float d0 = Depth(a), d1 = Depth(b), d2 = Depth(c), d3 = Depth(d);
                float dmin = min(min(d0, d1), min(d2, d3));
                float de = (abs(d0 - d3) + abs(d1 - d2)) / max(dmin, 0.5);
                // ground seen at a grazing angle has big depth steps but no real edge: raise the threshold there
                float rawC = SampleSceneDepth(uv);
                float3 wpos = ComputeWorldSpacePosition(uv, rawC, UNITY_MATRIX_I_VP);
                float3 nC = SampleSceneNormals(uv);
                float ndv = saturate(dot(nC, normalize(_WorldSpaceCameraPos - wpos)));
                float graze = 1.0 - ndv;
                float depthEdge = step(_DepthSens * (1.0 + 14.0 * graze * graze * graze), de);
                float3 n0 = SampleSceneNormals(a), n1 = SampleSceneNormals(b), n2 = SampleSceneNormals(c), n3 = SampleSceneNormals(d);
                float ne = length(n0 - n3) + length(n1 - n2);
                float normalEdge = step(_NormalSens, ne) * saturate(1.0 - dmin / _NormalFadeDist);
                float edge = saturate(depthEdge + normalEdge);
                col = lerp(col, _InkColor.rgb, edge * _InkColor.a * saturate(1.0 - _FxBlur * 0.2));

                col = lerp(col, _FxFlashColor.rgb, _FxFlash);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
