Shader "Instanced/FoamParticleShader"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline"}

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs//just following the example in unity docs for RenderMeshIndirect
            #include "UnityIndirect.cginc"
            #include "UnityCG.cginc"

            struct FoamParticle
            {
                float2 velocity;
                float2 position;
                float life;
            };

            float4 particleColor;
            float3 pivotPosition;
            float particleRadius;

            StructuredBuffer<FoamParticle> particles;
            StructuredBuffer<uint> particleCounter;

            struct appdata
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 clipPos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v, uint svInstanceID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);

                v2f o;
                uint i = GetIndirectInstanceID(svInstanceID);
                o.uv = v.uv;

                float r = i < particleCounter[0] ? particleRadius : 0;
                float3 particlePos = float3(pivotPosition.x + particles[i].position.x, pivotPosition.y + particles[i].position.y, pivotPosition.z);
                float3 vertexWorldPos = particlePos + r * mul(unity_ObjectToWorld, v.position);
                o.clipPos = mul(UNITY_MATRIX_VP, float4(vertexWorldPos, 1));

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float s = i.uv.x - 0.5;
                float t = i.uv.y - 0.5;
                float a = 1 - 4 * (s * s + t * t);
                if (a < 0)
                {
                    return 0;
                }
                return fixed4(particleColor.x, particleColor.y, particleColor.z, a * particleColor.w);
            }
            ENDCG
        }
    }
}
