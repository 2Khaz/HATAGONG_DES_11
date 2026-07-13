Shader "HATAGONG/Phase2/Paint Mask Brush"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "PaintMaskBrush"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One One
            BlendOp Max
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            float _Phase2RenderTargetYSign;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 brushCoordinate : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 brushCoordinate : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = float4(input.positionOS, 1.0);
                output.positionCS.y *= _Phase2RenderTargetYSign;
                output.brushCoordinate = input.brushCoordinate;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half distanceFromCenter = length(input.brushCoordinate);
                half brush = 1.0h - smoothstep(0.9h, 1.0h, distanceFromCenter);
                return half4(brush, brush, brush, brush);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
