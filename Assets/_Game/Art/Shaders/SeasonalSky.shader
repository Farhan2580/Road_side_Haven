Shader "RoadsideHaven/Seasonal Sky"
{
    Properties
    {
        _Daylight ("Daylight", Range(0,1)) = 1
        _Cloudiness ("Cloudiness", Range(0,1)) = 0.1
        _SunDirection ("Sun direction", Vector) = (1,1,0,0)
        _MoonDirection ("Moon direction", Vector) = (-1,-1,0,0)
        _SkyTime ("Cloud motion", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };
            float _Daylight, _Cloudiness, _SkyTime;
            float3 _SunDirection, _MoonDirection;
            float hash(float3 p) { return frac(sin(dot(p, float3(127.1,311.7,74.7))) * 43758.5453); }
            float noise(float3 p)
            {
                float3 i = floor(p), f = frac(p); f = f*f*(3-2*f);
                return lerp(lerp(lerp(hash(i), hash(i+float3(1,0,0)), f.x),
                    lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),
                    lerp(hash(i+float3(0,1,1)),hash(i+float3(1,1,1)),f.x),f.y),f.z);
            }
            v2f vert(appdata v) { v2f o; o.position=UnityObjectToClipPos(v.vertex); o.direction=v.vertex.xyz; return o; }
            half4 frag(v2f i) : SV_Target
            {
                float3 ray = normalize(i.direction);
                float h = saturate(ray.y);
                float3 zenith = lerp(float3(.008,.014,.045), float3(.14,.39,.74), _Daylight);
                float3 horizon = lerp(float3(.035,.045,.095), float3(.72,.83,.88), _Daylight);
                float3 sky = lerp(horizon, zenith, pow(h,.45));
                float sunset = (1-smoothstep(.02,.42,abs(_SunDirection.y))) * smoothstep(-.2,.05,_SunDirection.y);
                sky += float3(1,.22,.045) * sunset * pow(saturate(dot(ray,normalize(_SunDirection))),6) * (1-h);
                float sunDot = dot(ray, normalize(_SunDirection));
                float moonDot = dot(ray, normalize(_MoonDirection));
                float sunDisc = smoothstep(.9991,.9996,sunDot) * step(0,_SunDirection.y);
                float moonDisc = smoothstep(.9986,.9991,moonDot) * step(0,_MoonDirection.y);
                sky += sunDisc * float3(5,3.6,1.7);
                sky += moonDisc * float3(.85,.90,1.05) * lerp(.72,1,noise(ray*600));
                float3 starCell = floor(ray*650);
                float star = step(.9986,hash(starCell)) * pow(saturate(1-length(frac(ray*650)-.5)),4);
                sky += star * (1-_Daylight) * smoothstep(0,.15,h) * 2;
                float cloud = smoothstep(.48,.72,noise(ray*8+float3(_SkyTime,0,_SkyTime*.4))*.7 + noise(ray*19)*.3);
                cloud *= smoothstep(.01,.25,h) * saturate(_Cloudiness+.3);
                sky = lerp(sky,lerp(float3(.065,.075,.11),float3(.78,.80,.82),_Daylight),cloud);
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
}
