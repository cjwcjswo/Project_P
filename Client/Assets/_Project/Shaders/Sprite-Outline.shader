// URP 2D Sprite Outline Shader — 2D SRP Batcher 호환
// 스프라이트 알파 채널 기반 8방향 edge-detection 외곽선.
// 외곽선은 스프라이트 윤곽에 fit하게 외부에만 렌더링 (여백 padding 제외).
// MaterialPropertyBlock으로 _OutlineColor, _OutlineThickness, _OutlineEnabled를 제어.
//
// [외곽선 두께 조절]
//   _OutlineThickness = UV 공간 오프셋 (0.001 = 매우 얇음, 0.004 = 기본, 0.01 = 두꺼움)
//   MaterialPropertyBlock.SetFloat("_OutlineThickness", 값) 으로 런타임 변경 가능
Shader "ProjectP/Sprite-Outline"
{
    Properties
    {
        [PerRendererData] _MainTex  ("Sprite Texture", 2D)    = "white" {}
        _Color                      ("Tint",           Color) = (1,1,1,1)
        _OutlineColor               ("Outline Color",  Color) = (1,1,1,1)
        _OutlineThickness           ("Outline Thickness (UV 0.002~0.008)", Float) = 0.001
        _OutlineEnabled             ("Outline Enabled", Float) = 0.0

        // Sprite 렌더러 내부 프로퍼티
        [HideInInspector] _RendererColor          ("RendererColor",            Color) = (1,1,1,1)
        [HideInInspector] _Flip                   ("Flip",                     Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex               ("External Alpha",           2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha    ("Enable External Alpha",    Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sprite-Outline"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // 2D SRP Batcher 호환: _ST / _TexelSize 접미사 프로퍼티 사용 금지.
            // UV는 스프라이트 메시에서 직접 전달되므로 _MainTex_ST 불필요.
            // 외곽선 두께는 _OutlineThickness (UV 공간) 로만 제어.
            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                half4  _OutlineColor;
                float  _OutlineThickness;
                float  _OutlineEnabled;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = IN.uv;  // 스프라이트 UV는 메시에서 직접 전달 (TRANSFORM_TEX 불필요)
                // URP 공식 패턴: vertex color × material tint × SpriteRenderer.color
                OUT.color      = IN.color * _Color * unity_SpriteColor;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 mainColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                // 외곽선 비활성화 시 메인 색상만 반환
                if (_OutlineEnabled < 0.5)
                    return mainColor;

                // 현재 픽셀이 불투명이면 외곽선 불필요 — 원본 유지
                if (mainColor.a > 0.01)
                    return mainColor;

                // 8방향 인접 픽셀 알파 샘플링
                // _OutlineThickness = UV 오프셋 (줄이면 얇아짐, 늘리면 두꺼워짐)
                float t = _OutlineThickness;
                float maxAlpha = 0.0;
                maxAlpha = max(maxAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( t,  0)).a);
                maxAlpha = max(maxAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-t,  0)).a);
                maxAlpha = max(maxAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0,  t)).a);
                maxAlpha = max(maxAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0, -t)).a);
                maxAlpha = max(maxAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( t,  t)).a);
                maxAlpha = max(maxAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( t, -t)).a);
                maxAlpha = max(maxAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-t,  t)).a);
                maxAlpha = max(maxAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-t, -t)).a);

                // 인접 픽셀에 불투명 영역이 있으면 외곽선 색상 출력
                if (maxAlpha > 0.01)
                    return half4(_OutlineColor.rgb, maxAlpha * _OutlineColor.a);

                return mainColor;
            }
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
