Shader "UI/ViridianCircleWipe"
{
    Properties
    {
        _Color    ("Color", Color) = (0,0,0,1)
        _Cutoff   ("Cutoff", Range(0,1)) = 0
        _Softness ("Softness", Range(0,0.2)) = 0.02
        _MainTex  ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float  _Cutoff;
            float  _Softness;

            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float4 screenPos : TEXCOORD1; // for ComputeScreenPos
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos       = UnityObjectToClipPos(v.vertex);
                o.uv        = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Base texture alpha (keeps UI.Image happy)
                fixed4 baseCol = tex2D(_MainTex, i.uv);

                // 0..1 screen uv
                float2 suv = i.screenPos.xy / i.screenPos.w;

                // center about (0.5, 0.5)
                float2 centered = suv - 0.5;

                // aspect-correct X using built-in _ScreenParams (no need to declare it)
                float aspect = _ScreenParams.x / _ScreenParams.y;
                centered.x *= aspect;

                // radial distance
                float d = length(centered);

                // 0 => fully covered, 1 => fully open
                float radius = lerp(0.0, 0.71, saturate(_Cutoff));
                float edge   = smoothstep(radius - _Softness, radius + _Softness, d);

                // edge=0 (transparent) inside hole, edge=1 (opaque) outside
                fixed4 col = _Color;
                col.a *= edge;
                return col * baseCol.a;
            }
            ENDCG
        }
    }
}
