Shader "BruteForce/URP/SnowIceURP" {
	Properties {
		[Header(IIIIIIII          Snow Textures          IIIIIIII)] [Space] _MainTex ("Snow Albedo", 2D) = "white" {}
		_MainTexMult ("Snow Albedo Multiplier", Range(0, 2)) = 0.11
		[HDR] _Color ("Snow Tint", Vector) = (0.77,0.86,0.91,1)
		_OverallScale ("Overall Scale", Float) = 1
		[Space] [NoScaleOffset] _SnowBumpMap ("Snow Bumpmap", 2D) = "white" {}
		_NormalMultiplier ("Snow Bumpmap Multiplier", Range(0, 2)) = 0.4
		_SnowNormalScale ("Snow Bumpmap Scale", Range(0, 2)) = 1
		[Space(20)] [NoScaleOffset] _SpecGlossMapNew ("Snow Specular", 2D) = "black" {}
		[NoScaleOffset] _LittleSpec ("Snow Little Spec", 2D) = "black" {}
		_LittleSpecForce ("Little Spec Multiplier", Float) = 0.5
		[NoScaleOffset] _SnowHeight ("Snow Displacement Texture", 2D) = "white" {}
		_HeightScale ("Displacement Scale", Float) = 0.33
		_DisplacementStrength ("Displacement Strength", Float) = 0.3
		_DisplacementOffset ("Displacement Offset", Float) = 0.1
		_DisplacementColorMult ("Displacement Color Multiplier", Float) = 0.95
		_DisplacementShadowMult ("Displacement Shadow Multiplier", Range(0, 2)) = 0.56
		_UpVector ("Up Vector", Float) = 1
		_NormalVector ("Normal Vector", Float) = 0
		[Space(20)] [NoScaleOffset] _SnowTransition ("Snow Transition", 2D) = "black" {}
		_TransitionScale ("Transition Scale", Float) = 0.73
		_TransitionPower ("Transition Power", Float) = 0.22
		_TransitionColor ("Transition Color (additive only)", Vector) = (1,1,1,1)
		[Space(10)] [Header(IIIIIIII          Snow Values          IIIIIIII)] [Space(10)] _MountColor ("Mount Color", Vector) = (0.12,0.12,0.121,1)
		_BotColor ("Dig Color", Vector) = (0.71,0.87,0.91,0)
		_NormalRTDepth ("Normal Effect Depth", Range(0, 3)) = 0.12
		_NormalRTStrength ("Normal Effect Strength", Range(0, 4)) = 2.2
		_AddSnowStrength ("Mount Snow Strength", Range(0, 3)) = 0.52
		_RemoveSnowStrength ("Dig Snow Strength", Range(0, 3)) = 0.5
		_SnowScale ("Snow Scale", Float) = 1
		[Space(10)] [Header(IIIIIIII          Ice Textures          IIIIIIII)] [Space(10)] _IceTex ("Ice Albedo", 2D) = "white" {}
		[HDR] _IceTint ("Ice Texture Tint", Vector) = (0.14,0.35,0.49,1)
		[Space] [NoScaleOffset] _ParallaxLayers ("Parallax Ice Texture", 2D) = "black" {}
		_ParallaxStrength ("Parallax Fade Strength", Vector) = (-1.04,0.125,-0.13,0.1)
		_OffsetScale ("Parallax Offset Scale", Float) = 0.14
		[NoScaleOffset] _NormalTex ("Ice Normal Texture", 2D) = "black" {}
		_NormalScale ("Ice Normal Scale", Range(0, 1)) = 0.766
		[NoScaleOffset] _Roughness ("Ice Roughness Texture", 2D) = "black" {}
		[NoScaleOffset] _UnderWaterFish ("Under Water Fish", 2D) = "black" {}
		_FishScale ("Fish Scale", Float) = 1
		_FishSpeed ("Fish Speed", Float) = 1
		_FishIntensity ("Fish Intensity", Float) = 1
		_FishAngle ("Fish Angle", Float) = 0
		_FishMaskIntensity ("Fish Mask Intensity", Range(0, 1)) = 0.25
		[Space(10)] [Header(IIIIIIII          Ice Values          IIIIIIII)] [Space(10)] [HDR] _ParalaxColor ("Parallax Color", Vector) = (0,0,0,1)
		_ParalaxColorScale ("Parallax Color Scale", Range(-2, 2)) = -0.88
		_IceTrail ("Ice Trail Color", Vector) = (0.4,0.1,0.01,1)
		_TransparencyValue ("Ice Transparency", Range(0, 1)) = 1
		_IceScale ("Ice Scale", Float) = 1
		[Space(10)] [Header(IIIIIIII          Custom Fog          IIIIIIII)] [Space(10)] [NoScaleOffset] _FogTex ("Fog Texture", 2D) = "black" {}
		[NoScaleOffset] _FlowTex ("Flow Texture", 2D) = "black" {}
		_FlowMultiplier ("Flow Multiplier", Range(0, 1)) = 0.3
		_FogIntensity ("Fog Intensity", Range(0, 1)) = 0.3
		_FogScale ("Fog Scale", Float) = 1
		_FogDirection ("Fog Direction", Vector) = (1,0.3,2,0)
		[Space(10)] [Header(IIIIIIII          Lighting          IIIIIIII)] [Space(10)] _ProjectedShadowColor ("Projected Shadow Color", Vector) = (0.17,0.56,0.1,1)
		_SpecColor ("Specular Color", Vector) = (1,1,1,1)
		_SpecForce ("Specular Force", Float) = 3
		_RoughnessStrength ("Ice Roughness Strength", Float) = 1.75
		_ShininessIce ("Ice Shininess", Float) = 10
		_ShininessSnow ("Snow Shininess", Float) = 25
		[Space] _LightOffset ("Light Offset", Range(0, 1)) = 0.2
		_LightHardness ("Light Hardness", Range(0, 1)) = 0.686
		_ReflectionOpacity ("Reflection Opacity", Float) = 0.25
		_ReflectionStrength ("Rim Ice Strength", Range(0, 2)) = 0.07
		_ReflectionColor ("Rim Ice Color", Vector) = (0,0.1,0.15,0.8)
		_RimColor ("Rim Snow Color", Vector) = (0.03,0.03,0.03,0)
		_LightIntensity ("Additional Lights Intensity", Range(0, 4)) = 1
		[Space(10)] [Header(IIIIIIII          Tessellation          IIIIIIII)] [Space(10)] _TessellationUniform ("Tessellation Ice", Range(1, 16)) = 1
		_TessellationEdgeLength ("Tessellation Snow", Range(0, 100)) = 50
		[Header(Procedural Tiling)] [Space] [Toggle(USE_PR)] _UsePR ("Use Procedural Tiling (Reduce performance)", Float) = 0
		_ProceduralDistance ("Tile start distance", Float) = 5.5
		_ProceduralStrength ("Tile Smoothness", Float) = 1.5
		[Space] [Space(10)] [Header(IIIIIIII          Pragmas          IIIIIIII)] [Space(10)] [Toggle(IS_ICE)] _ISICE ("Is Only Ice", Float) = 0
		[Toggle(IS_SNOW)] _ISSNOW ("Is Only Snow", Float) = 0
		[Toggle(IS_UNLIT)] _ISUNLIT ("Is Unlit", Float) = 0
		[Toggle(IS_ADD)] _ISADD ("Is Additive Snow", Float) = 0
		[Toggle(USE_INTER)] [HideInInspector] _USEINTER ("Use Intersection", Float) = 0
		[Toggle(USE_AL)] _UseAmbientLight ("Use Ambient Light", Float) = 1
		[Toggle(USE_DECAL)] _USEDECAL ("Use Decal", Float) = 0
		[Toggle(USE_LS)] _UseSunReflection ("Use Sun Reflection", Float) = 1
		[Toggle(USE_RT)] _USERT ("Use RT", Float) = 1
		[Toggle(IS_T)] _IST ("Is Terrain", Float) = 0
		[Toggle(USE_VR)] _UseVR ("Use For VR", Float) = 0
		[Toggle(USE_WC)] _USEWC ("Use World Displacement", Float) = 1
		[Toggle(USE_WT)] _USEWT ("Use World Coordinates", Float) = 0
		[Toggle(USE_FOG)] _USEFOG ("Use Custom Fog", Float) = 1
		[Toggle(USE_LOW)] _USELOW ("Use Low End", Float) = 0
		[HideInInspector] _CustomTerrainHolesTexture ("Holes Map (RGB)", 2D) = "white" {}
		[HideInInspector] _Control0 ("Control0 (RGBA)", 2D) = "white" {}
		[HideInInspector] _Control1 ("Control1 (RGBA)", 2D) = "white" {}
		[HideInInspector] _Splat0 ("Layer 0 (R)", 2D) = "white" {}
		[HideInInspector] _Splat1 ("Layer 1 (G)", 2D) = "white" {}
		[HideInInspector] _Splat2 ("Layer 2 (B)", 2D) = "white" {}
		[HideInInspector] _Splat3 ("Layer 3 (A)", 2D) = "white" {}
		[HideInInspector] _Splat4 ("Layer 4 (R)", 2D) = "white" {}
		[HideInInspector] _Splat5 ("Layer 5 (G)", 2D) = "white" {}
		[HideInInspector] _Splat6 ("Layer 6 (B)", 2D) = "white" {}
		[HideInInspector] _Splat7 ("Layer 7 (A)", 2D) = "white" {}
		[HideInInspector] _Normal0 ("Normal 0 (R)", 2D) = "bump" {}
		[HideInInspector] _Normal1 ("Normal 1 (G)", 2D) = "bump" {}
		[HideInInspector] _Normal2 ("Normal 2 (B)", 2D) = "bump" {}
		[HideInInspector] _Normal3 ("Normal 3 (A)", 2D) = "bump" {}
		[HideInInspector] _Normal4 ("Normal 4 (R)", 2D) = "bump" {}
		[HideInInspector] _Normal5 ("Normal 5 (G)", 2D) = "bump" {}
		[HideInInspector] _Normal6 ("Normal 6 (B)", 2D) = "bump" {}
		[HideInInspector] _Normal7 ("Normal 7 (A)", 2D) = "bump" {}
		[HideInInspector] _NormalScale0 ("Normal Scale 0 ", Float) = 1
		[HideInInspector] _NormalScale1 ("Normal Scale 1 ", Float) = 1
		[HideInInspector] _NormalScale2 ("Normal Scale 2 ", Float) = 1
		[HideInInspector] _NormalScale3 ("Normal Scale 3 ", Float) = 1
		[HideInInspector] _NormalScale4 ("Normal Scale 4 ", Float) = 1
		[HideInInspector] _NormalScale5 ("Normal Scale 5 ", Float) = 1
		[HideInInspector] _NormalScale6 ("Normal Scale 6 ", Float) = 1
		[HideInInspector] _NormalScale7 ("Normal Scale 7 ", Float) = 1
		[HideInInspector] _Mask0 ("Mask 0 (R)", 2D) = "bump" {}
		[HideInInspector] _Mask1 ("Mask 1 (G)", 2D) = "bump" {}
		[HideInInspector] _Mask2 ("Mask 2 (B)", 2D) = "bump" {}
		[HideInInspector] _Mask3 ("Mask 3 (A)", 2D) = "bump" {}
		[HideInInspector] _Mask4 ("Mask 4 (R)", 2D) = "bump" {}
		[HideInInspector] _Mask5 ("Mask 5 (G)", 2D) = "bump" {}
		[HideInInspector] _Mask6 ("Mask 6 (B)", 2D) = "bump" {}
		[HideInInspector] _Mask7 ("Mask 7 (A)", 2D) = "bump" {}
		[HideInInspector] _Specular0 ("Specular 0 (R)", Vector) = (1,1,1,1)
		[HideInInspector] _Specular1 ("Specular 1 (G)", Vector) = (1,1,1,1)
		[HideInInspector] _Specular2 ("Specular 2 (B)", Vector) = (1,1,1,1)
		[HideInInspector] _Specular3 ("Specular 3 (A)", Vector) = (1,1,1,1)
		[HideInInspector] _Specular4 ("Specular 4 (R)", Vector) = (1,1,1,1)
		[HideInInspector] _Specular5 ("Specular 5 (G)", Vector) = (1,1,1,1)
		[HideInInspector] _Specular6 ("Specular 6 (B)", Vector) = (1,1,1,1)
		[HideInInspector] _Specular7 ("Specular 7 (A)", Vector) = (1,1,1,1)
		[HideInInspector] _Metallic0 ("Metallic0", Float) = 0
		[HideInInspector] _Metallic1 ("Metallic1", Float) = 0
		[HideInInspector] _Metallic2 ("Metallic2", Float) = 0
		[HideInInspector] _Metallic3 ("Metallic3", Float) = 0
		[HideInInspector] _Metallic4 ("Metallic4", Float) = 0
		[HideInInspector] _Metallic5 ("Metallic5", Float) = 0
		[HideInInspector] _Metallic6 ("Metallic6", Float) = 0
		[HideInInspector] _Metallic7 ("Metallic7", Float) = 0
		[HideInInspector] _Splat0_ST ("Size0", Vector) = (1,1,0,1)
		[HideInInspector] _Splat1_ST ("Size1", Vector) = (1,1,0,1)
		[HideInInspector] _Splat2_ST ("Size2", Vector) = (1,1,0,1)
		[HideInInspector] _Splat3_ST ("Size3", Vector) = (1,1,0,1)
		[HideInInspector] _Splat4_STn ("Size4", Vector) = (1,1,0,1)
		[HideInInspector] _Splat5_STn ("Size5", Vector) = (1,1,0,1)
		[HideInInspector] _Splat6_STn ("Size6", Vector) = (1,1,0,1)
		[HideInInspector] _Splat7_STn ("Size7", Vector) = (1,1,0,1)
		[HideInInspector] _TerrainScale ("Terrain Scale", Vector) = (1,1,0,1)
		[HideInInspector] _BaseMap ("Base Texture", 2D) = "white" {}
		[HideInInspector] _BaseColor ("Example Colour", Vector) = (0,0.66,0.73,1)
		[HideInInspector] _Smoothness ("Smoothness", Float) = 0.5
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _MainTex_ST;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;
			float4 _Color;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy) * _Color;
			}

			ENDHLSL
		}
	}
}