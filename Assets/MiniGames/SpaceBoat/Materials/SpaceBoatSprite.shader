Shader "SpaceBoat/Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _ClipEnabled ("Clip To Field", Float) = 0
        [PerRendererData] _ClipRect ("Field Bounds", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float2 fieldPosition : TEXCOORD1;
            };
            sampler2D _MainTex;
            float _ClipEnabled;
            float4 _ClipRect;
            float4x4 _WorldToField;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.fieldPosition = mul(_WorldToField, mul(unity_ObjectToWorld, v.vertex)).xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_ClipEnabled > 0.5)
                {
                    clip(i.fieldPosition - _ClipRect.xy);
                    clip(_ClipRect.zw - i.fieldPosition);
                }
                return tex2D(_MainTex, i.uv) * i.color;
            }
            ENDCG
        }
    }
}
