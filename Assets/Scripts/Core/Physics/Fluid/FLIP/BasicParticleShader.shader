Shader "Instanced/BasicParticleShader"
{
    Properties
    {
        _Color("Color", Color) = (0, 0, 1, 1)
    }
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

            float4 _Color;
            float3 _PivotPosition;
            float _ParticleScale;
            float4 _ParticlePositions[ARRAY_LENGTH];


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

            v2f vert (appdata v, uint svInstanceID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);
                v2f o;
                o.uv = v.uv;
                uint instanceID = GetIndirectInstanceID(svInstanceID);
                uint i = svInstanceID >> 1;
                float3 particlePos = (svInstanceID & 1) == 0 ? 
                    float3(_PivotPosition.x + _ParticlePositions[i].x, _PivotPosition.y + _ParticlePositions[i].y, _PivotPosition.z)
                    : float3(_PivotPosition.x + _ParticlePositions[i].z, _PivotPosition.y + _ParticlePositions[i].w, _PivotPosition.z);
                float3 vertexWorldPos = particlePos + _ParticleScale * mul(unity_ObjectToWorld, v.position);
                o.clipPos = mul(UNITY_MATRIX_VP, float4(vertexWorldPos, 1));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float s = i.uv.x - 0.5;
                float t = i.uv.y - 0.5;
                return fixed4(_Color.x, _Color.y, _Color.z, max(1 - 4 * (s * s + t * t), 0) * _Color.w);
            }
            ENDCG
        }
    }
}
