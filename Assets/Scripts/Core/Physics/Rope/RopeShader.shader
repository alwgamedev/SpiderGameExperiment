Shader "Custom/RopeShader"
{
    Properties {
        _HalfWidth("HalfWidth", float) = 0.1
        _EdgeColor("EdgeColor", Color) = (0, 0, 0, 1)
        _MiddleColor("MiddleColor", Color) = (1, 1, 1, 1)
    }
    
    SubShader {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass {
            ZTest Off
            Cull Off//o/w rope goes invisible when you turn left
            Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            
            #include "UnityCG.cginc"
            
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            static const uint MAX_NUM_NODES = 256;
            static const float PI = 3.14169420;
            
            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                uint id : SV_VertexID;
            };
            
            struct v2f {
                float4 clipPos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            int _NumNodes;
            int _EndcapTriangles;
            float _HalfWidth;
            float4 _EdgeColor;
            float4 _MiddleColor;
            float4 _NodePositions[MAX_NUM_NODES];

            v2f vert (appdata v) {

                v2f o;
                o.uv = v.uv;

                if (v.id < _NumNodes << 1)
                {
                    int nodeIndex = v.id / 2;
                    int displacement = v.id % 2 == 0 ? -1 : 1;
                    float4 nodeData = _NodePositions[nodeIndex];
                    half2 segmentDirection = nodeIndex < _NumNodes - 1 ?
                        _NodePositions[nodeIndex + 1].xy - nodeData.xy : nodeData.xy - _NodePositions[nodeIndex - 1].xy;
                    if (segmentDirection.x == 0 && segmentDirection.y == 0)
                    {
                        int k = nodeIndex + 1;
                        while (++k < _NumNodes && segmentDirection.x == 0 && segmentDirection.y == 0)
                        {
                            segmentDirection = normalize(_NodePositions[k].xy - nodeData.xy);
                        }
                    }
                    segmentDirection = normalize(segmentDirection);//do we have to do anything about NaN or we cool?
                    float a = displacement * _HalfWidth * nodeData.z;
                    o.clipPos = mul(UNITY_MATRIX_VP, 
                        float4(nodeData.x - a * segmentDirection.y, nodeData.y + a * segmentDirection.x, 0, 1));
                }
                else
                {
                    float2 center = _NodePositions[_NumNodes - 1].xy;
                    float2 right = normalize(center - _NodePositions[_NumNodes - 2].xy);
                    float2 down = half2(right.y, -right.x);
                    if (right.x == 0 && right.y == 0)
                    {
                        int k = _NumNodes - 2;
                        while (!(--k < 0) && right.x == 0 && right.y == 0)
                        {
                            right = normalize(center - _NodePositions[k]);
                        }
                    }
                    int i = v.id - (_NumNodes << 1) + 1;//if there are, say, 5 endcap triangles, which one are we on? (1st, 2nd, ... not zero indexed)
                    float t =  (PI * i) / (_EndcapTriangles + 1);
                    float2 p = center + _HalfWidth * (cos(t) * down + sin(t) * right);
                    o.clipPos = mul(UNITY_MATRIX_VP, float4(p, 0, 1));
                }

                return o;
            }

            fixed4 frag (v2f o) : SV_Target {
                return lerp(_MiddleColor, _EdgeColor, abs(2 * o.uv.y - 1));
            }
            
            ENDCG
        }
    }
}
