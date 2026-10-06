Shader "NightToyStore/PrototypeSurface"
{
    Properties { _Color ("Color", Color) = (0.5,0.5,0.5,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 world : TEXCOORD1; };
            fixed4 _Color;
            float _EchoMode;
            v2f vert(appdata_base v) {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz; return o;
            }
            fixed4 frag(v2f i) : SV_Target {
                if (_EchoMode > .5) discard;
                float lighting = .35 + .65 * saturate(dot(normalize(i.normal), normalize(float3(.4,.8,-.3))));
                float grid = step(.96, frac(i.world.x)) + step(.96, frac(i.world.z));
                return fixed4(_Color.rgb * lighting * (1 - saturate(grid) * .12), 1);
            }
            ENDCG
        }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; };
            float _EchoMode, _EchoNow;
            int _EchoCount;
            float4 _EchoPulses[24], _EchoSettings[24];
            v2f vert(appdata_base v) {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal); return o;
            }
            fixed4 frag(v2f i) : SV_Target {
                if (_EchoMode < .5) discard;
                float intensity = 0;
                for (int k = 0; k < 24; k++) {
                    if (k >= _EchoCount) break;
                    float distanceToSound = distance(i.world, _EchoPulses[k].xyz);
                    float arrivalAge = _EchoNow - _EchoPulses[k].w - distanceToSound / _EchoSettings[k].x;
                    if (distanceToSound < _EchoSettings[k].y && arrivalAge >= 0 && arrivalAge < _EchoSettings[k].z)
                        intensity = max(intensity, (1 - arrivalAge / _EchoSettings[k].z) * _EchoSettings[k].w);
                }
                if (intensity < .01) discard;
                float3 direction = normalize(_WorldSpaceCameraPos - i.world);
                float rim = pow(1 - abs(dot(normalize(i.normal), direction)), 2);
                float3 grid = abs(frac(i.world * 2) - .5);
                float wire = 1 - step(.025, min(grid.x, min(grid.y, grid.z)));
                return fixed4(float3(.12,.72,1) * intensity * (.12 + rim * .8 + wire * .5), 1);
            }
            ENDCG
        }
    }
}
