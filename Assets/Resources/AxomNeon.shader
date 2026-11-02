// Un seul shader pour tout le jeu : coeur lumineux + halo doux, en additif.
// TEXCOORD0 : xy = position relative au centre de la forme (-1..1), z = rayon du coeur (0..1).
Shader "Axom/Neon"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float3 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float3 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float d = length(i.uv.xy);
                float core = i.uv.z;
                float edge = max(fwidth(d), 0.01);
                float body = 1.0 - smoothstep(core - edge, core + edge, d);
                float halo = saturate(1.0 - d);
                halo = halo * halo * 0.55;
                float3 rgb = i.color.rgb * (body + halo) + body * 0.35;
                return fixed4(rgb * i.color.a, 1.0);
            }
            ENDCG
        }
    }
}
