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
            v2f vert(appdata_base v) {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz; return o;
            }
            fixed4 frag(v2f i) : SV_Target {
                float lighting = .35 + .65 * saturate(dot(normalize(i.normal), normalize(float3(.4,.8,-.3))));
                float grid = step(.96, frac(i.world.x)) + step(.96, frac(i.world.z));
                return fixed4(_Color.rgb * lighting * (1 - saturate(grid) * .12), 1);
            }
            ENDCG
        }
    }
}
