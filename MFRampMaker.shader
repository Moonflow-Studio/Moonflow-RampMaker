Shader"Hidden/Moonflow/RampMaker"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color("Color", Color) = (1,1,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma shader_feature _LERP_MODE
            #pragma shader_feature _GAMMA_MODE
            #pragma shader_feature _QUAD_MODE
            #pragma shader_feature _LOOP_MODE
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/core.hlsl"

            struct appdata
            {
               float4 vertex : POSITION;
               float2 uv : TEXCOORD0;
            };

            struct v2f
            {
               float4 vertex : SV_POSITION;
               float2 uv : TEXCOORD0;
            };

            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            float _RealNum;
            float4 _ColorArray[80];
            float _PointArray[80];

            float linearstep(float u, float left, float right)
            {
                return (u-left)/(right-left);
            }
            float4 GetGradient(int num, float u)
            {
                int i = 0;
                int left = 0;
                int right = 7;
                UNITY_UNROLL
                for (i = 0; i < 8; i++)
                {
                    if(_PointArray[num * 10 + i] >= u)
                    {
                        right = i;
                        break;
                    }
                }
                left = max(0, right - 1);
                float4 resultColor = lerp(_ColorArray[num * 10 + left], _ColorArray[num * 10 + right], linearstep(u, _PointArray[num * 10 + left], _PointArray[num * 10 + right]));
            #ifdef _GAMMA_MODE
                return pow(resultColor, 2.2);
            #else
                return resultColor;
            #endif
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                int realNum = _RealNum;
            #if defined(_LERP_MODE) && !defined (_LOOP_MODE)
                if(_RealNum>1) realNum = realNum - 1;
            #endif
                // float uvBandwidth = 1 / realNum;
                float y = 1 - i.uv.y;
                float yLevel = y * realNum;
            #ifdef _LERP_MODE
                    int next = ceil(yLevel);
                #ifdef _LOOP_MODE
                    next = (next > _RealNum - 1) ? 0 : next;
                #endif

                    float balance = frac(yLevel);
                #ifndef _QUAD_MODE
                    balance = balance * 2 - 1;
                #endif
                return lerp(GetGradient(floor(yLevel), i.uv.x), GetGradient(next, i.uv.x), saturate(balance));
            #else
                return GetGradient(floor(yLevel), i.uv.x);
            #endif
            }
            ENDHLSL
        }
    }
}
