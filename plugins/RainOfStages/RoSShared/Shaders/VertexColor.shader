Shader "RainOfStages/VertexColor"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType"="Transparent" }
        LOD 200

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 5.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 col : COLOR0;
            };
            struct FragInput
            {
                float4 vertex : SV_POSITION;
                float4 col : COLOR0;
            };

            FragInput Vert(appdata _input)
            {
                FragInput _output;
                _output.vertex = UnityObjectToClipPos(_input.vertex);
                _output.col = _input.col;
                return _output;
            }

            float4 Frag(FragInput _input) : SV_Target
            {
                return _input.col;
            }
            ENDHLSL
        }
    }
}