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
                float colorNoise;
                float sizeNoise;
                float density;
            };
            
            float3 pivotPosition;
            float dt;
            float velocityStretchFactor;
            float velocityStretchMax;
            float sprayThreshold;
            float foamThreshold;
            float bubbleThreshold;
            float densityMax;

            float lifeFadeTime;

            //idea: density will interpolate from one state to the next (e.g. particleColorSpray to particleColorFoam) and noise will interpolate from color0 to color1
            float4 particleColorMin0;
            float4 particleColorMin1;
            float4 particleColorSpray0;
            float4 particleColorSpray1;
            float4 particleColorFoam0;
            float4 particleColorFoam1;
            float4 particleColorBubble0;
            float4 particleColorBubble1;
            float4 particleColorMax0;
            float4 particleColorMax1;

            float2 particleRadiusMin;
            float2 particleRadiusSpray;
            float2 particleRadiusFoam;
            float2 particleRadiusBubble;
            float2 particleRadiusMax;

            StructuredBuffer<FoamParticle> particle;
            StructuredBuffer<uint> particleCounter;

            float NormalizeFloat(float f, float min, float max)
            {
                return f > min ? f < max ? (f - min) / (max - min) : 1 : 0;
            }

            fixed4 ParticleColor(fixed colorNoise, fixed stateProgress, fixed4 colorMin0, fixed4 colorMin1, fixed4 colorMax0, fixed4 colorMax1)
            {
                fixed4 color0 = lerp(colorMin0, colorMax0, stateProgress);
                fixed4 color1 = lerp(colorMin1, colorMax1, stateProgress);
                color1 = lerp(color0, color1, colorNoise);
                return color1;
            }

            struct appdata
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 clipPos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float colorNoise : TEXCOORD1;
                float density : TEXCOORD2;
                float life : TEXCOORD3;
            };

            v2f vert (appdata v, uint svInstanceID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);

                v2f o;
                uint i = GetIndirectInstanceID(svInstanceID);
                o.uv = v.uv;
                o.colorNoise = particle[i].colorNoise;
                float density = particle[i].density;
                o.density = density;
                float life = particle[i].life < lifeFadeTime ? particle[i].life / lifeFadeTime : 1;
                o.life = life;

                float2 vel = particle[i].velocity;
                float spd = sqrt(dot(vel, vel));
                float spdInverse = 1 / spd;
                if (!isnan(spdInverse) && !isinf(spdInverse))
                {
                    float2 velDir = spdInverse * vel;
                    float2 w = float2(-velDir.y, velDir.x);
                    if (v.position.x < 0)
                    {
                        velDir *= clamp(velocityStretchFactor * dt * spd, 1, velocityStretchMax);
                        w *= 0.8;//give it a little bit of a tear drop shape;
                    }
                    v.position = float4(v.position.x * velDir + v.position.y * w, v.position.z, v.position.w);
                }

                float2 radius;
                if (density > bubbleThreshold)
                {
                    float t = NormalizeFloat(density, bubbleThreshold, densityMax);
                    radius = lerp(particleRadiusBubble, particleRadiusMax, t);
                }
                else if (density > foamThreshold)
                {
                    float t = NormalizeFloat(density, foamThreshold, bubbleThreshold);
                    radius = lerp(particleRadiusFoam, particleRadiusBubble, t);
                }
                else if (density > sprayThreshold)
                {
                    float t = NormalizeFloat(density, sprayThreshold, foamThreshold);
                    radius = lerp(particleRadiusSpray, particleRadiusFoam, t);
                }
                else
                {
                    float t = density / sprayThreshold;
                    radius = lerp(particleRadiusMin, particleRadiusSpray, t);
                }

                float r = i < particleCounter[0] ? life * lerp(radius.x, radius.y, particle[i].sizeNoise) : 0;
                float3 particlePos = float3(pivotPosition.x + particle[i].position.x, pivotPosition.y + particle[i].position.y, pivotPosition.z);
                float3 vertexWorldPos = particlePos + r * mul(unity_ObjectToWorld, v.position);
                o.clipPos = mul(UNITY_MATRIX_VP, float4(vertexWorldPos, 1));

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float s = i.uv.x - 0.5;
                float t = i.uv.y - 0.5;
                s = 1 - 4 * (s * s + t * t);
                if (s < 0)
                {
                    return 0;
                }

                fixed4 colorMin0;
                fixed4 colorMin1;
                fixed4 colorMax0;
                fixed4 colorMax1;

                //probably can do this without cases if vertex carries normalizedDensity (0-1) and a *float* state
                //we'll do this in a sec
                //note fragment interpolation is done within each *triangle* from the values at triangle vertices

                if (i.density > bubbleThreshold)
                {
                    colorMin0 = particleColorBubble0;
                    colorMin1 = particleColorBubble1;
                    colorMax0 = particleColorMax0;
                    colorMax1 = particleColorMax1;
                    t = NormalizeFloat(i.density, bubbleThreshold, densityMax);
                }
                else if (i.density > foamThreshold)
                {
                    colorMin0 = particleColorFoam0;
                    colorMin1 = particleColorFoam1;
                    colorMax0 = particleColorBubble0;
                    colorMax1 = particleColorBubble1;
                    t = NormalizeFloat(i.density, foamThreshold, bubbleThreshold);
                }
                else if (i.density > sprayThreshold)
                {
                    colorMin0 = particleColorSpray0;
                    colorMin1 = particleColorSpray1;
                    colorMax0 = particleColorFoam0;
                    colorMax1 = particleColorFoam1;
                    t = NormalizeFloat(i.density, sprayThreshold, foamThreshold);
                }
                else
                {
                    colorMin0 = particleColorMin0;
                    colorMin1 = particleColorMin1;
                    colorMax0 = particleColorSpray0;
                    colorMax1 = particleColorSpray1;
                    t = i.density / sprayThreshold;
                }

                fixed4 color = ParticleColor(i.colorNoise, t, colorMin0, colorMin1, colorMax0, colorMax1);
                color.w *= s * i.life;
                return color;
            }
            ENDCG
        }
    }
}