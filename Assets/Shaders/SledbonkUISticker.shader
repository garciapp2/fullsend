// UI shader that turns a live render (character on a magenta key background) into a sticker:
// keys the background out and draws a cream border plus an ink outline by dilating the silhouette.
Shader "Sledbonk/UISticker"
{
    Properties
    {
        [PerRendererData] _MainTex ("Render", 2D) = "black" {}
        _Key ("Key Colour", Color) = (1, 0, 1, 1)
        _Border ("Border Colour", Color) = (1, 0.96, 0.88, 1)
        _Ink ("Ink Colour", Color) = (0.05, 0.05, 0.12, 1)
        _BorderPx ("Border (texels)", Float) = 10
        _InkPx ("Ink (texels)", Float) = 6
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_TexelSize;
            fixed4 _Key, _Border, _Ink; float _BorderPx, _InkPx;
            struct A { float4 v : POSITION; float2 uv : TEXCOORD0; fixed4 c : COLOR; };
            struct V { float4 p : SV_POSITION; float2 uv : TEXCOORD0; fixed4 c : COLOR; };
            V vert(A i) { V o; o.p = UnityObjectToClipPos(i.v); o.uv = i.uv; o.c = i.c; return o; }
            float Solid(float2 uv)
            {
                float3 c = tex2D(_MainTex, uv).rgb;
                return saturate((distance(c, _Key.rgb) - 0.25) * 6.0);
            }
            fixed4 frag(V i) : SV_Target
            {
                float3 col = tex2D(_MainTex, i.uv).rgb;
                float a = Solid(i.uv);
                float border = 0, ink = 0;
                [unroll] for (int k = 0; k < 20; k++)
                {
                    float ang = k * 0.31416;
                    float2 d = float2(cos(ang), sin(ang)) * _MainTex_TexelSize.xy;
                    border = max(border, Solid(i.uv + d * _BorderPx));
                    ink = max(ink, Solid(i.uv + d * (_BorderPx + _InkPx)));
                }
                // despill the magenta fringe toward the border colour
                col = lerp(_Border.rgb, col, a * a);
                fixed3 outCol = a > 0.5 ? col : (border > 0.5 ? _Border.rgb : _Ink.rgb);
                float outA = max(a, max(border, ink));
                return fixed4(outCol, outA * i.c.a);
            }
            ENDCG
        }
    }
}
