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
            
            float4 particleColorMin;
            float4 particleColorMax;
            float3 pivotPosition;
            float particleRadiusMin;
            float particleRadiusMax;
            float densityNormalizer;
            float restDensity;
            
            //we'll probably incorporate density in some way too
            StructuredBuffer<float> particleDensity;
            StructuredBuffer<float2> particleVelocity;
            StructuredBuffer<float2> particlePosition;

            struct appdata
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 clipPos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float density : TEXCOORD1;
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
                uint i = GetIndirectInstanceID(svInstanceID);

                float t = clamp(densityNormalizer * particleDensity[i], 0, 1);
                o.density = t;

                float r = lerp(particleRadiusMin, particleRadiusMax, t);
                float3 particlePos = float3(pivotPosition.x + particlePosition[i].x, pivotPosition.y + particlePosition[i].y, pivotPosition.z);
                float2 velocity = particleVelocity[i];
                if (dot(v.position.xy, velocity) < 0)
                {
                    v.position.x -= 0.005 * t * velocity.x;
                    v.position.y -= 0.005 * t * velocity.y;
                }
                float3 vertexWorldPos = particlePos + r * mul(unity_ObjectToWorld, v.position);
                o.clipPos = mul(UNITY_MATRIX_VP, float4(vertexWorldPos, 1));

                o.uv = v.uv;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float s = i.uv.x - 0.5;
                float t = i.uv.y - 0.5;
                float4 color = lerp(particleColorMin, particleColorMax, i.density);
                // color.w *= 1 - clamp(pow(4 * (s * s + t * t), 16), 0, 1);
                return s * s + t * t < 0.25 ? color : 0;
            }
            ENDCG
        }
    }
}
