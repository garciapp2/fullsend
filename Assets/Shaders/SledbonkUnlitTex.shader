// Plain texture, no light, no fog: the dotted backdrop behind the fox portrait.
Shader "Sledbonk/UnlitTex"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial) float4 _MainTex_ST; CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = TRANSFORM_TEX(i.uv, _MainTex); return o; }
            half4 frag(V i) : SV_Target { return half4(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb, 1); }
            ENDHLSL
        }
    }
}
