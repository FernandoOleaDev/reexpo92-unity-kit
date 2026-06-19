// Wireframe de ARISTAS para los tiles de Google, SIN procesar mallas (rápido) y
// sin baricéntricas: detecta los bordes de triángulo por la NORMAL DE CARA que
// sale de las derivadas de pantalla (ddx/ddy de la posición). Esa normal es
// constante dentro de cada triángulo y SALTA en las aristas → ahí se pinta la
// línea. Va por opaqueMaterial (Cesium lo aplica a todos los tiles). URP, Mac.
//
// Color de línea arcoíris por distancia a cámara + degradado. Solo aristas
// (FaceAlpha = 0) o con un velo de cara si se sube FaceAlpha.
Shader "ReExpo92/TechWire"
{
    Properties
    {
        _LineColor ("Color de línea (si no arcoíris; A=opacidad)", Color) = (0.4, 0.9, 1.0, 1.0)
        _LineWidth ("Grosor / sensibilidad", Range(0.2, 8)) = 1.4
        _Rainbow ("Arcoíris (0/1)", Float) = 1
        _FaceAlpha ("Velo de cara (0 = solo aristas)", Range(0,1)) = 0
        _FadeNear ("Distancia cerca (m)", Float) = 40
        _FadeFar ("Distancia lejos (m)", Float) = 700
        _HueRange ("Rango de tono", Range(0,2)) = 0.8
        _HueOffset ("Desfase de tono", Range(0,1)) = 0
        _Sat ("Saturación", Range(0,1)) = 1
        _Val ("Brillo", Range(0,3)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "TechWire"
            Tags { "LightMode" = "UniversalForward" } // imprescindible o URP no dibuja (negro)
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            float4 _LineColor;
            float  _LineWidth, _Rainbow, _FaceAlpha, _FadeNear, _FadeFar, _HueRange, _HueOffset, _Sat, _Val;

            float3 Hue2RGB(float h)
            {
                h = frac(h) * 6.0;
                return saturate(float3(abs(h - 3.0) - 1.0, 2.0 - abs(h - 2.0), 2.0 - abs(h - 4.0)));
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // color por distancia (arcoíris o fijo)
                float dist = distance(IN.positionWS, _WorldSpaceCameraPos);
                float t = saturate((dist - _FadeNear) / max(0.001, _FadeFar - _FadeNear));
                float3 hue   = Hue2RGB(_HueOffset + t * _HueRange);
                float3 sated = lerp(float3(1,1,1), hue, _Sat) * _Val;
                float3 lineRGB = lerp(_LineColor.rgb, sated, saturate(_Rainbow));
                float farFade = 1.0 - smoothstep(0.85, 1.0, t);

                // Normal de cara por derivadas de pantalla (constante por triángulo).
                float3 cn = cross(ddx(IN.positionWS), ddy(IN.positionWS));
                float l = length(cn);
                float3 fn = (l > 1e-8) ? cn / l : float3(0, 0, 1);

                // En las aristas la normal SALTA → fwidth grande. Dentro del
                // triángulo es constante → ~0. Eso nos da la línea, sin baricéntricas.
                float e = length(fwidth(fn));
                float edge = saturate(e * _LineWidth * 8.0);

                half4 col;
                col.rgb = lineRGB;
                col.a   = max(edge, _FaceAlpha) * farFade; // FaceAlpha = 0 → SOLO aristas
                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
