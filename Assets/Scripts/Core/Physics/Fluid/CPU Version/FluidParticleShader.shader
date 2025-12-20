Shader "Instanced/FluidParticleShader"
{
    Properties
    {
        _Color("Color", Color) = (0, 0, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs//just following the example in unity docs for RenderMeshIndirect
            #include "UnityIndirect.cginc"
            #include "UnityCG.cginc"

            float4 _Color;
            float4 _ParticlePositions[1023];//so we get a max of 2046 particles

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
                float4 particleWorldPosition = (svInstanceID & 1) == 0 ? 
                    float4(_ParticlePositions[i].x, _ParticlePositions[i].y, 0.0f, 0.0f) 
                    : float4(_ParticlePositions[i].z, _ParticlePositions[i].w, 0.0f, 0.0f);
                float4 worldPos = particleWorldPosition + mul(unity_ObjectToWorld, v.position);
                o.clipPos = mul(UNITY_MATRIX_VP, worldPos);
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
