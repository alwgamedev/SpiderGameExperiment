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
            
            half4 colorMin;
            half4 colorMax;
            half4 foamColor;

            int smoothStepIterations;
            half densityNormalizer;
            half densityThreshold;
            half noiseNormalizer;
            half noiseThreshold;
            half foaminessNormalizer;
            half foaminessThreshold;
            
            sampler2D densityTex;

            half NormalizeFloat(half z, half normalizer, half threshold)
            {
                z *= normalizer;
                return z > threshold ? min(z - threshold, 1) : 0;
            }

            v2f vert (appdata v) 
            {
                v2f o;
                o.uv = v.uv;
                o.clipPos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f o) : SV_Target
            {
                half4 densitySample = tex2D(densityTex, o.uv);

                half density = NormalizeFloat(densitySample.x, densityNormalizer, densityThreshold);
                if (!(density > 0))
                {
                    return 0;
                }

                //helps keep the border crisp when using a larger smoothing radius for texture
                for (int i = 0; i < smoothStepIterations; i++)
                {
                    half density2 = density * density;
                    density *= density2 * (6 * density2 - 15 * density + 10);
                }
                
                half noise = NormalizeFloat(densitySample.y, noiseNormalizer, noiseThreshold);
                half4 color0 = half4(colorMin.x, colorMin.y, colorMin.z, colorMin.w);
                half4 color1 = half4(colorMax.x, colorMax.y, colorMax.z, colorMax.w);
                color1 = lerp(color0, color1, noise);

                half foaminess = NormalizeFloat(densitySample.z, foaminessNormalizer, foaminessThreshold);
                if (foaminess > 0)
                {
                    color1 = lerp(color1, foamColor, foaminess);
                }

                color1.w *= density;
                return color1;
            }
            
            ENDCG
        }
    }
}
