Shader "Custom/FluidDensityShader"
{   
    SubShader {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline"}

        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            
            #include "UnityCG.cginc"
            
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            
            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f {
                float4 clipPos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            half4 colorMin;//we could instead of a min color and a max color with a min cutoff (below which color is zero)
            half4 colorMax;
            half normalizer;
            half noiseNormalizer;
            half threshold;
            
            sampler2D densityTex;

            v2f vert (appdata v) {

                v2f o;
                o.uv = v.uv;
                o.clipPos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f o) : SV_Target {
                half2 densitySample = tex2D(densityTex, o.uv);
                densitySample.x *= normalizer;
                if (densitySample.x < threshold)
                {
                    return 0;
                }

                densitySample.x = clamp(densitySample.x - threshold, 0, 1);
                half4 color0 = half4(colorMin.x, colorMin.y, colorMin.z, densitySample.x * colorMin.w);
                half4 color1 = half4(colorMax.x, colorMax.y, colorMax.z, densitySample.x * colorMax.w);
                return lerp(color0, color1, clamp(noiseNormalizer * densitySample.y, 0, 1));
            }
            
            ENDCG
        }
    }
}
