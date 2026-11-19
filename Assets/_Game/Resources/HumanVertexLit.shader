Shader "RoadsideHaven/Human Vertex Lit"
{
    Properties { _BaseColor("Tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 color:COLOR; half fog:TEXCOORD2; half3 vertexLight:TEXCOORD3; };
            Varyings Vert(Attributes input)
            {
                Varyings o;VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(input.normalOS);o.color=input.color;o.fog=ComputeFogFactor(p.positionCS.z);o.vertexLight=VertexLighting(p.positionWS,o.normalWS);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 n=normalize(i.normalWS);Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half diffuse=saturate(dot(n,light.direction));half3 ambient=max(SampleSH(n),half3(.1,.1,.1));
                half3 color=i.color.rgb*(ambient+light.color*diffuse*light.shadowAttenuation*light.distanceAttenuation);
                color+=i.color.rgb*i.vertexLight;
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData=(InputData)0;inputData.positionWS=i.positionWS;inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light lamp=GetAdditionalLight(lightIndex,i.positionWS);
                    color+=i.color.rgb*lamp.color*lamp.distanceAttenuation*saturate(dot(n,lamp.direction));
                }
                #endif
                uint pixelLightCount=GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light lamp=GetAdditionalLight(lightIndex,i.positionWS);
                    color+=i.color.rgb*lamp.color*lamp.distanceAttenuation*saturate(dot(n,lamp.direction));
                LIGHT_LOOP_END
                #endif
                half3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);half sheen=pow(saturate(dot(n,normalize(view+light.direction))),28)*.055;
                color+=light.color*sheen*light.shadowAttenuation;return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;float3 _LightPosition;
            struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
            float4 ShadowVert(A i):SV_POSITION
            {
                float3 p=TransformObjectToWorld(i.positionOS.xyz);float3 n=TransformObjectToWorldNormal(i.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 direction=normalize(_LightPosition-p);
                #else
                float3 direction=_LightDirection;
                #endif
                float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,direction));
                #if UNITY_REVERSED_Z
                clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #else
                clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #endif
                return clip;
            }
            half4 ShadowFrag():SV_Target {return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags { "LightMode"="DepthNormalsOnly" } ZWrite On
            HLSLPROGRAM
            #pragma vertex NormalVert
            #pragma fragment NormalFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct N { float4 positionCS:SV_POSITION;half3 normalWS:TEXCOORD0; };
            N NormalVert(float4 position:POSITION,float3 normal:NORMAL) {N o;o.positionCS=TransformObjectToHClip(position.xyz);o.normalWS=TransformObjectToWorldNormal(normal);return o;}
            half4 NormalFrag(N i):SV_Target
            {
                half3 normal=normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal)*.5+.5)),0);
                #else
                return half4(normal,0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" } ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 DepthVert(float4 p:POSITION):SV_POSITION {return TransformObjectToHClip(p.xyz);}
            half4 DepthFrag():SV_Target {return 0;}
            ENDHLSL
        }
    }
}
