Shader "Instanced/FluidParticleShader"
{
    Properties
    {
        _Color("Color", Color) = (0, 0, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "UnityCG.cginc"

            float4 _Color;
            StructuredBuffer<float2> _ParticlePositions;

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 position : SV_POSITION;
            };

            v2f vert (appdata_full v, uint id : SV_VertexID)
            {
                v2f o;
                o.position = 0;
                // o.vertex = UnityObjectToClipPos(v.vertex);
                // o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
