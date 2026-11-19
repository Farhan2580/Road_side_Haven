Shader "RoadsideHaven/Ultra Rain"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.75, 0.80, 0.88, 0.55)
        [KeywordEnum(Streak, Splash)] _Shape ("Shape", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            // multi_compile (not shader_feature): materials are created at runtime, so both variants must ship.
            #pragma multi_compile_local _SHAPE_STREAK _SHAPE_SPLASH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            // Scene brightness so drops are not glowing white at night.
            half _UltraDaylight;
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float fog : TEXCOORD1; float near : TEXCOORD2; };
            Varyings vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.color = input.color;
                o.uv = input.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                // Drops right in front of the lens would read as huge white bars; fade them out.
                o.near = saturate((distance(TransformObjectToWorld(input.positionOS.xyz), _WorldSpaceCameraPos) - 1.5) / 4);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                half alpha;
                #if defined(_SHAPE_SPLASH)
                    float r = length(uv - .5) * 2;
                    alpha = smoothstep(.55, .8, r) * (1 - smoothstep(.85, 1, r));
                #else
                    // Thin, soft-edged streak that is brightest at the leading end.
                    float across = 1 - abs(uv.x - .5) * 2;
                    alpha = smoothstep(0, .6, across) * smoothstep(0, .25, uv.y) * smoothstep(1, .55, uv.y);
                #endif
                half3 color = _Tint.rgb * lerp(.18, 1, saturate(_UltraDaylight)) * i.color.rgb;
                color = MixFog(color, i.fog);
                return half4(color, alpha * _Tint.a * i.color.a * i.near);
            }
            ENDHLSL
        }
    }
}
