Shader "RoadsideHaven/Ultra Wind Particle"
{
    Properties { _Tint ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
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
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            half _UltraDaylight;
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.color = input.color; o.uv = input.uv; o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                // Leaf-shaped blob: a diamond with soft edges and a centre vein.
                float2 p = abs(i.uv - .5) * 2;
                float shape = 1 - smoothstep(.75, 1, p.x * 1.6 + p.y);
                float vein = 1 - smoothstep(0, .08, p.x) * .25;
                half3 color = i.color.rgb * _Tint.rgb * vein * lerp(.25, 1, saturate(_UltraDaylight));
                color = MixFog(color, i.fog);
                return half4(color, shape * i.color.a * _Tint.a);
            }
            ENDHLSL
        }
    }
}
