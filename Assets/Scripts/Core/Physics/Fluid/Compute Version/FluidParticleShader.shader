Shader "Instanced/FluidParticleShader"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline"}

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs//just following the example in unity docs for RenderMeshIndirect
            #include "UnityIndirect.cginc"
            #include "UnityCG.cginc"

            static const uint ARRAY_LENGTH = 1023;
            static const float PI = 3.14169420;
            
            float3 pivotPosition;
            float4 particleColor;
            float particleRadius;
            float restDensity;
            
            //we'll probably incorporate density in some way too
            StructuredBuffer<float> particleDensity;
            StructuredBuffer<float2> particleVelocity;//would be cool if velocity influenced particle size (slower = bigger particle -- and/or more dense = bigger particle)
            StructuredBuffer<float2> particlePosition;

            struct appdata
            {
                float2 uv : TEXCOORD0;
                float4 position : POSITION;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 clipPos : SV_POSITION;
            };

            //I think I'd like:
            // - particle smaller when less dense (yes the particles are really *bigger* when less dense -- but smaller particles will make it *visually* less dense)
            // - color change change with density
            // - and/or color change with velocity
            // - plus velocity should stretch the quad out (blurring/blending the motion of the particles)

            v2f vert (appdata v, uint svInstanceID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);
                v2f o;
                o.uv = v.uv;
                uint i = GetIndirectInstanceID(svInstanceID);
                float3 particlePos = float3(pivotPosition.x + particlePosition[i].x, pivotPosition.y + particlePosition[i].y, pivotPosition.z);
                float3 vertexWorldPos = particlePos + particleRadius * mul(unity_ObjectToWorld, v.position);
                o.clipPos = mul(UNITY_MATRIX_VP, float4(vertexWorldPos, 1));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float s = i.uv.x - 0.5;
                float t = i.uv.y - 0.5;
                return fixed4(particleColor.x, particleColor.y, particleColor.z, max(1 - 4 * (s * s + t * t), 0) * particleColor.w);
            }
            ENDCG
        }
    }
}
