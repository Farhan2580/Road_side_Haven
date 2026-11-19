Shader "RoadsideHaven/Foliage Lit"
{
    // Bark and leaf-card shader for the procedural trees: alpha-cut leaves, double-sided wrap
    // lighting, sun shining through the canopy, crown-shaped normals, season tint and wind sway.
    // Vertex colour: rgb = tint / ambient occlusion, a = flexibility (0 at the trunk base, 1 at leaf tips).
    Properties
    {
        _BaseMap("Albedo", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha cutoff", Range(0,1)) = .45
        _Translucency("Translucency", Range(0,1)) = .55
        _Leaf("Leaf (1) or bark (0)", Float) = 1
        _Seasonal("Changes with the seasons", Float) = 1
        _WindScale("Wind response", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST; half4 _BaseColor; half _Cutoff,_Translucency,_Leaf,_Seasonal,_WindScale;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        float4 _TreeWind;   // xy = direction, z = strength, w = gustiness
        half4 _TreeSeason;  // canopy tint for the current season
        float3 TreeWind(float3 ws, half flex)
        {
            float3 origin = UNITY_MATRIX_M._m03_m13_m23;
            float phase = dot(origin.xz, float2(.137, .171));
            float t = _Time.y;
            float strength = _TreeWind.z * _WindScale;
            float gust = .65 + .35 * sin(t * .31 + phase * .5) * sin(t * .17 + phase);
            float sway = (sin(t * 1.05 + phase) * .55 + sin(t * 2.15 + phase * 1.7) * .25) * strength * (1 + _TreeWind.w * gust);
            float bend = flex * flex;
            ws.xz += _TreeWind.xy * (sway + strength * .55 * gust) * bend * .32;
            ws.y -= abs(sway) * bend * .05;
            float flutter = sin(t * 8.3 + dot(ws, float3(3.1, 2.3, 2.7))) * .035 * strength * _Leaf * flex;
            ws += float3(flutter, flutter * .6, -flutter);
            return ws;
        }
        half4 Albedo(float2 uv, half4 color)
        {
            half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
            half3 rgb = tex.rgb * _BaseColor.rgb * color.rgb;
            half3 seasonTint = (_TreeSeason.a > 0) ? _TreeSeason.rgb : half3(1,1,1);
            rgb *= lerp(half3(1,1,1), seasonTint, _Leaf * _Seasonal);
            return half4(rgb, tex.a);
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half4 color:COLOR; half fog:TEXCOORD3; };
            Varyings Vert(Attributes input)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(input);
                float3 ws = TreeWind(TransformObjectToWorld(input.positionOS.xyz), input.color.a);
                o.positionWS = ws; o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap); o.color = input.color; o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half3 Shade(half3 albedo, half3 n, half3 v, Light light, half wrap)
            {
                half ndl = saturate((dot(n, light.direction) + wrap) / (1 + wrap));
                half atten = light.shadowAttenuation * light.distanceAttenuation;
                // Sun glowing through the leaves when looking towards it.
                half through = pow(saturate(dot(v, -light.direction)), 5) * _Translucency * _Leaf;
                half3 transmitted = albedo * half3(1.05, 1.15, .7) * through * 1.6;
                half3 h = normalize(light.direction + v);
                half spec = pow(saturate(dot(n, h)), 24) * (_Leaf > .5 ? .08 : .03);
                return light.color * atten * (albedo * ndl + transmitted + spec);
            }
            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 a = Albedo(i.uv, i.color);
                clip(a.a - _Cutoff);
                half3 n = normalize(i.normalWS); n = IS_FRONT_VFACE(face, n, (_Leaf > .5 ? n : -n));
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half wrap = _Leaf > .5 ? .55 : .15;
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half ao = 1;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                ao = aoFactor.indirectAmbientOcclusion; sun.color *= aoFactor.directAmbientOcclusion;
                #endif
                half3 ambient = max(SampleSH(n), half3(.08, .08, .08)) * ao;
                half3 color = a.rgb * ambient + Shade(a.rgb, n, v, sun, wrap);
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0; inputData.positionWS = i.positionWS; inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light lamp = GetAdditionalLight(lightIndex, i.positionWS);
                    color += a.rgb * lamp.color * lamp.distanceAttenuation * saturate((dot(n, lamp.direction) + wrap) / (1 + wrap));
                }
                #endif
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light lamp = GetAdditionalLight(lightIndex, i.positionWS);
                    color += a.rgb * lamp.color * lamp.distanceAttenuation * saturate((dot(n, lamp.direction) + wrap) / (1 + wrap));
                LIGHT_LOOP_END
                #endif
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            V ShadowVert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i);
                float3 p = TreeWind(TransformObjectToWorld(i.positionOS.xyz), i.color.a); float3 n = TransformObjectToWorldNormal(i.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 direction = normalize(_LightPosition - p);
                #else
                float3 direction = _LightDirection;
                #endif
                float4 clip = TransformWorldToHClip(ApplyShadowBias(p, n, direction));
                #if UNITY_REVERSED_Z
                clip.z = min(clip.z, UNITY_NEAR_CLIP_VALUE * clip.w);
                #else
                clip.z = max(clip.z, UNITY_NEAR_CLIP_VALUE * clip.w);
                #endif
                o.positionCS = clip; o.uv = TRANSFORM_TEX(i.uv, _BaseMap); o.color = i.color; return o;
            }
            half4 ShadowFrag(V i) : SV_Target { clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" } ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            V DepthVert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); o.positionCS = TransformWorldToHClip(TreeWind(TransformObjectToWorld(i.positionOS.xyz), i.color.a)); o.uv = TRANSFORM_TEX(i.uv, _BaseMap); return o; }
            half4 DepthFrag(V i) : SV_Target { clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags { "LightMode"="DepthNormalsOnly" } ZWrite On
            HLSLPROGRAM
            #pragma vertex NormalVert
            #pragma fragment NormalFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; half3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; };
            V NormalVert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); o.positionCS = TransformWorldToHClip(TreeWind(TransformObjectToWorld(i.positionOS.xyz), i.color.a)); o.normalWS = TransformObjectToWorldNormal(i.normalOS); o.uv = TRANSFORM_TEX(i.uv, _BaseMap); return o; }
            half4 NormalFrag(V i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                half3 normal = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal) * .5 + .5)), 0);
                #else
                return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
    }
}
