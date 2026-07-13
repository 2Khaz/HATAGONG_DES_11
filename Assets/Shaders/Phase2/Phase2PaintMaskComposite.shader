Shader "HATAGONG/Phase2/Paint Mask Composite"
{
    Properties
    {
        _MainTex ("Existing Mask", 2D) = "black" {}
        _Phase2FrameStampTex ("Frame Stamp Mask", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "MonotonicComposite"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _Phase2FrameStampTex;

            half4 Frag(v2f_img input) : SV_Target
            {
                half existingMask = tex2D(_MainTex, input.uv).r;
                half frameStamp = tex2D(_Phase2FrameStampTex, input.uv).r;
                half combined = max(existingMask, frameStamp);
                return half4(combined, combined, combined, combined);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
