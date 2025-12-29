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
            
            float4 color;//we could instead of a min color and a max color with a min cutoff (below which color is zero)
            float densityNormalizer;
            
            sampler2D densityTex;

            v2f vert (appdata v) {

                v2f o;
                o.uv = v.uv;
                o.clipPos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f o) : SV_Target {
                float4 c = color;
                c.w *= densityNormalizer * tex2D(densityTex, o.uv);
                return c;
            }
            
            ENDCG
        }
    }
}
