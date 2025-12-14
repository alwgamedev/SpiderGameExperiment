Shader "Custom/FluidShader"
{
    Properties {
        _AirDensity("AirDensity", float) = 0.05
        _ColorMin("ColorMin", Color) = (0,0,1,0.05)
        _ColorMax("ColorMax", Color) = (0,0,1,1)
    }
    
    SubShader {
        Tags { "RenderType" = "Transparent" }

        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            
            #include "UnityCG.cginc"
            
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            static const int MAX_NUM_VERTICES = 4091;
            
            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                uint id : SV_VertexID;
            };
            
            struct v2f {
                float4 clipPos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float density : TEXCOORD1;
            };
            
            float _AirDensity;
            float4 _ColorMin;
            float4 _ColorMax;
            float4 _Density[MAX_NUM_VERTICES];

            v2f vert (appdata v) {

                v2f o;
                o.uv = v.uv;
                o.clipPos = UnityObjectToClipPos(v.vertex);
                o.density = _Density[v.id].x;
                return o;
            }

            fixed4 frag (v2f o) : SV_Target {
                return o.density < _AirDensity ? (0,0,0,0) : o.density < 1 ? lerp(_ColorMin, _ColorMax, (o.density - _AirDensity) / (1 - _AirDensity)) : _ColorMax;
            }
            
            ENDCG
        }
    }
}
