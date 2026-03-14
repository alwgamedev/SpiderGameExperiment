//no longer in use (now using shader graph with custom vertex position)
Shader "Custom/RopeShader"
{
    Properties {
        _HalfWidth("HalfWidth", float) = 0.1
        _EdgeColor("EdgeColor", Color) = (0, 0, 0, 1)
        _MiddleColor("MiddleColor", Color) = (1, 1, 1, 1)
    }
    
    SubShader {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline"}

        Pass {
            // Tags { "LightMode" = "Universal2D" }
            
            HLSLPROGRAM
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            static const uint MAX_NUM_NODES = 256;
            
            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint id : SV_VertexID;
            };
            
            struct v2f {
                float4 clipPos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            uint _NumNodes;
            uint _EndcapTriangles;
            float _HalfWidth;
            float _Orientation;
            float4 _EdgeColor;
            float4 _MiddleColor;
            float4 _NodePositions[MAX_NUM_NODES];

            v2f vert (appdata v) {

                v2f o;
                o.uv = v.uv;

                if (v.id < 2 * _NumNodes)
                {
                    int nodeIndex = v.id / 2;
                    int displacement = v.id % 2 == 0 ? -_Orientation : _Orientation;
                    float4 nodeData = _NodePositions[nodeIndex];
                    float2 segmentDirection = nodeIndex < (uint)(_NumNodes - 1) ?
                        _NodePositions[nodeIndex + 1].xy - nodeData.xy : nodeData.xy - _NodePositions[nodeIndex - 1].xy;
                    if (segmentDirection.x == 0 && segmentDirection.y == 0)
                    {
                        int k = nodeIndex + 1;
                        while (++k < _NumNodes && segmentDirection.x == 0 && segmentDirection.y == 0)
                        {
                            segmentDirection = normalize(_NodePositions[k].xy - nodeData.xy);
                        }
                    }
                    segmentDirection = normalize(segmentDirection);
                    float a = displacement * _HalfWidth * nodeData.z;
                    o.clipPos = TransformWorldToHClip(float3(nodeData.x - a * segmentDirection.y, nodeData.y + a * segmentDirection.x, 0));
                }
                else//we're on an endcap vertex
                {
                    half2 center = _NodePositions[_NumNodes - 1].xy;
                    half2 right = normalize(center - _NodePositions[_NumNodes - 2].xy);
                    half2 down = _Orientation > 0 ? half2(right.y, -right.x) : half2(-right.y, right.x);
                    if (right.x == 0 && right.y == 0)
                    {
                        int k = _NumNodes - 2;
                        while (!(--k < 0) && right.x == 0 && right.y == 0)
                        {
                            right = normalize(center - _NodePositions[k]);
                        }
                    }
                    int i = v.id - (2 * _NumNodes) + 1;//which endcap triangle are we on? (not zero indexed)
                    half t =  (3.14 * i) / (_EndcapTriangles + 1);
                    half2 p = center + _HalfWidth * (cos(t) * down + sin(t) * right);
                    o.clipPos = TransformWorldToHClip(half3(p, 0));
                }

                return o;
            }

            half4 frag (v2f o) : SV_Target {

                return lerp(_MiddleColor, _EdgeColor, abs(2 * o.uv.y - 1));
            }
            
            ENDHLSL
        }
    }
}
