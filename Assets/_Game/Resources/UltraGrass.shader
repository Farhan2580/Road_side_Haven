Shader "RoadsideHaven/Ultra Grass"
{
    Properties
    {
        _BaseColor ("Root colour", Color) = (0.07, 0.20, 0.09, 1)
        _TipColor ("Tip colour", Color) = (0.36, 0.60, 0.20, 1)
        _DryColor ("Dry tip colour", Color) = (0.62, 0.62, 0.30, 1)
        _Translucency ("Back-light translucency", Range(0,1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _TipColor, _DryColor;
            half _Translucency;
        CBUFFER_END
        // x,y wind direction (xz plane), z strength 0..1+, w gust speed.
        float4 _UltraWind;
        // xyz world position, w radius: the player and the active vehicle flatten grass around them.
        float4 _UltraPush0, _UltraPush1;
        // x: distance where grass has fully shrunk away, y: fade band width.
        float4 _UltraGrassFade;
        half4 _UltraSeasonTint;
        half _UltraWetness;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;     // r: per-blade random
            float2 uv : TEXCOORD0;    // y: 0 at root, 1 at tip
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float3 Bend(float3 positionWS, float3 rootWS, float height, float random)
        {
            float3 toCamera = _WorldSpaceCameraPos - rootWS;
            float distance = length(toCamera.xz);
            float fade = saturate((_UltraGrassFade.x - distance) / max(_UltraGrassFade.y, .01));
            // Shrink toward the root instead of popping out at the edge of the grass radius.
            positionWS = rootWS + (positionWS - rootWS) * fade;
            float h2 = height * height;
            float2 dir = normalize(_UltraWind.xy + float2(1e-4, 0));
            float phase = dot(rootWS.xz, dir) * .35 + random * 6.283;
            float gust = sin(_Time.y * _UltraWind.w * .37 + dot(rootWS.xz, dir) * .045) * .5 + .5;
            float sway = (sin(_Time.y * _UltraWind.w + phase) * .55 + .45) * (.25 + gust * .9);
            float flutter = sin(_Time.y * _UltraWind.w * 3.1 + phase * 2.3) * .08;
            float strength = _UltraWind.z * (.35 + random * .4);
            positionWS.xz += (dir * sway * strength + float2(-dir.y, dir.x) * flutter * strength) * h2 * .55 * fade;
            [unroll] for (int i = 0; i < 2; i++)
            {
                float4 pusher = i == 0 ? _UltraPush0 : _UltraPush1;
                float2 away = rootWS.xz - pusher.xz;
                float d = length(away);
                float push = saturate(1 - d / max(pusher.w, .01)) * step(abs(rootWS.y - pusher.y), 2.5);
                positionWS.xz += (away / max(d, .05)) * push * h2 * .7;
                positionWS.y -= push * h2 * .35;
            }
            return positionWS;
        }

        void Displace(Attributes input, out float3 positionWS, out float3 normalWS)
        {
            float3 rootWS = TransformObjectToWorld(float3(0,0,0));
            positionWS = TransformObjectToWorld(input.positionOS.xyz);
            positionWS = Bend(positionWS, rootWS, input.uv.y, input.color.r);
            // Bias normals upward so blades shade like a soft canopy rather than flat cards.
            normalWS = normalize(lerp(TransformObjectToWorldNormal(input.normalOS), float3(0,1,0), .55));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 data : TEXCOORD2;   // x height, y random
                float fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS, normalWS;
                Displace(input, positionWS, normalWS);
                o.positionWS = positionWS;
                o.normalWS = normalWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.data = float2(input.uv.y, input.color.r);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float h = i.data.x, rnd = i.data.y;
                // Large-scale patchiness so a lawn reads as living grass, not a carpet.
                float patch = sin(i.positionWS.x * .11 + sin(i.positionWS.z * .07) * 2.1) * sin(i.positionWS.z * .09 - i.positionWS.x * .03);
                half3 tip = lerp(_TipColor.rgb, _DryColor.rgb, saturate(patch * .55 - .1) * (.4 + rnd * .6));
                half3 albedo = lerp(_BaseColor.rgb, tip, smoothstep(0, 1, h)) * _UltraSeasonTint.rgb * (.88 + rnd * .24);
                albedo *= lerp(1, .78, _UltraWetness);
                float3 n = normalize(front ? i.normalWS : -i.normalWS);
                n = normalize(lerp(n, float3(0,1,0), .35));
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS);
                half ndl = saturate(dot(n, light.direction)) * .75 + .25;
                half3 direct = light.color * ndl * light.shadowAttenuation;
                half3 back = light.color * _Translucency * pow(saturate(dot(viewDir, -light.direction)), 3) * light.shadowAttenuation * h;
                half occlusion = lerp(.62, 1, h);
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                    occlusion *= ao.indirectAmbientOcclusion;
                #endif
                half3 ambient = SampleSH(n) * occlusion;
                half3 spec = light.color * pow(saturate(dot(n, normalize(light.direction + viewDir))), 24) * (.04 + _UltraWetness * .3) * light.shadowAttenuation * h;
                half3 color = albedo * (direct * occlusion + ambient) + albedo * back + spec;
                color = MixFog(color, i.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS, normalWS;
                Displace(input, positionWS, normalWS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS, normalWS;
                Displace(input, positionWS, normalWS);
                o.positionCS = TransformWorldToHClip(positionWS);
                return o;
            }
            half frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS, normalWS;
                Displace(input, positionWS, normalWS);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = normalWS;
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
