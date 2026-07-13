Shader "HATAGONG/Phase2/Paint Mask Display"
{
    Properties
    {
        _MainTex ("Paint Mask", 2D) = "black" {}
        _UnpaintedColor ("Unpainted Color", Color) = (0.28, 0.24, 0.20, 1)
        _PaintedColor ("Painted Color", Color) = (0.72, 0.72, 0.68, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "PaintMaskDisplay"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _UnpaintedColor;
            half4 _PaintedColor;

            half4 Frag(v2f_img input) : SV_Target
            {
                half mask = saturate(tex2D(_MainTex, input.uv).r);
                return lerp(_UnpaintedColor, _PaintedColor, mask);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
