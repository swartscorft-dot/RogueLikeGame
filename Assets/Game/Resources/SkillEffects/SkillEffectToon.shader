// Her skills' smoke, fire, dust, embers and the keg's starburst, drawn as the game is drawn (work order 126, 2026-11-25; the
// producer's direction for Kristian's "prosjektiler og røyk/flammer skal heller vise hvordan spellsene utspiller seg", and his
// standard for a look: a finished picture, no "urenheter"). Hand-written for URP 17, unlit, blended by alpha, one pass.
//
// Every shape is read from a texture made in code (SkillEffectArt.cs, half floats), whose channels are FIELDS, not pictures:
//   r: the shape's edge field: 0.5 on its outline, rising inward, falling outward (a distance, scaled);
//   g: its tone field: for smoke and dust how much it faces the room's light (a lump's sphere, lit from the upper left), for
//      fire how hot it is (the core hottest), for the starburst the inner star's own edge field;
//   b: a smooth, low noise: how ragged the edge is where it is eaten;
//   a: how far its front stands out toward the camera (a lump's sphere), a share of its half.
// The fragment cuts each field at named thresholds with an edge exactly one screen pixel wide (fwidth), so every edge is hard
// and clean at any size and with anti-aliasing on or off: no blur, no stair steps, no banding. On top:
//   - an ink line along the (eaten) outline, a named width in pixels at 1080 rows, as wide on the screen at any distance;
//   - eaten away: a ragged circle closing in on a named point of the shape cuts it from its edge in (smoke and fire go by
//     being eaten, never by fading to a ghost; one piece stays one piece, so nothing breaks into specks); only its last
//     remnant thins away, and it is gone at nine tenths, so no dot or single pixel is left behind;
//   - flicker: a tiling noise scrolled upward that licks at the edge and moves the heat's bands (fire);
//   - its depth is a sphere's (the a channel): where it meets the floor, a pillar or a figure the line is a curve, as a ball
//     of smoke would meet them, never a straight cut across it;
//   - smoke cleared round the figures inside it (her, the enemies), only by the puffs in front of each, so they are found.
Shader "Roguelike/Skill Effect Toon"
{
    Properties
    {
        _MainTex("Shapes (r: edge field, g: tone field, b: edge noise, a: front)", 2D) = "black" {}
        _NoiseTex("Flicker noise (tiling)", 2D) = "gray" {}
        _Tone0("Tone 0 (lightest; fire's core)", Color) = (1, 1, 1, 1)
        _Tone1("Tone 1", Color) = (0.7, 0.7, 0.7, 1)
        _Tone2("Tone 2", Color) = (0.5, 0.5, 0.5, 1)
        _Tone3("Tone 3 (darkest; fire's edge)", Color) = (0.3, 0.3, 0.3, 1)
        _Ink("Ink", Color) = (0.1, 0.1, 0.1, 1)
        _Bands("Tone thresholds (x: 0|1, y: 1|2, z: 2|3) and w: tone shift", Vector) = (0.66, 0.4, -1, 0)
        _Shape("x: eaten (0-1), y: cooled (subtracted from the tone field), z: ink px at 1080 rows, w: alpha", Vector) = (0, 0, 2, 1)
        _Flow("x: flicker speed, y: heat wobble, z: noise scale, w: edge licks", Vector) = (0, 0, 1, 0)
        _More("x: flicker time (s), y: seed, z: its front's depth (m), w: clear round figures (0 or 1)", Vector) = (0, 0, 0, 0)
        _Cut("xy: the point it is eaten toward (square's -1 to 1), z: how ragged", Vector) = (0, 0, 0.3, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SkillEffectToon"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Tone0, _Tone1, _Tone2, _Tone3, _Ink;
                float4 _Bands, _Shape, _Flow, _More, _Cut;
            CBUFFER_END

            // The figures the smoke is cleared round (SkillEffects sets them each frame there is smoke): xyz their feet, w the
            // radius cleared (m); how many; how tall a figure is cleared (m).
            float4 _SkillFxFigures[8];
            float _SkillFxFigureCount;
            float _SkillFxFigureHeight;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 centreWS : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = input.uv;
                o.uv1 = input.uv1;
                o.positionWS = ws;
                o.centreWS = TransformObjectToWorld(float3(0, 0, 0));
                return o;
            }

            // How much of a puff is cleared for the figures behind it: a capsule round each figure as the camera sees it (from
            // its feet to its height), for a puff whose middle is nearer the camera than the figure (a whole puff, so no straight
            // line is cut across it where it passes the figure).
            float Cleared(float3 ws, float3 centre)
            {
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 back = UNITY_MATRIX_V[2].xyz; // toward the camera
                float2 tall = float2(dot(float3(0, _SkillFxFigureHeight, 0), right), dot(float3(0, _SkillFxFigureHeight, 0), up));
                float most = 0.0;
                int count = (int)_SkillFxFigureCount;
                [unroll] for (int k = 0; k < 8; k++)
                {
                    if (k < count)
                    {
                        float4 figure = _SkillFxFigures[k];
                        float inFront = saturate((dot(centre - figure.xyz, back) + 0.1) / 0.4);
                        float3 d = ws - figure.xyz;
                        float2 p = float2(dot(d, right), dot(d, up));
                        float u = saturate(dot(p, tall) / max(dot(tall, tall), 1e-4));
                        float dist = length(p - tall * u);
                        float clear = 1.0 - smoothstep(figure.w, figure.w * 1.6, dist);
                        most = max(most, clear * inFront);
                    }
                }
                return most;
            }

            half4 Frag(Varyings input, out float depth : SV_Depth) : SV_Target
            {
                float4 s = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float n = 0.5;
                if (_Flow.y != 0.0 || _Flow.w != 0.0)
                    n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv1 * _Flow.z + float2(_More.y, -_More.x * _Flow.x)).r;

                // Eaten: a ragged circle closing in on _Cut.xy, from past the square's corners (nothing cut) to nothing.
                float eaten = saturate(_Shape.x);
                float2 local = input.uv1 * 2.0 - 1.0;
                float reach = 1.0 + length(_Cut.xy);
                float rad = length(local - _Cut.xy) / reach;
                // Slowly at first (its outer bits), then to nothing; the raggedness goes as it closes, so no last bit is left apart.
                float cut = (1.0 - eaten * eaten) * 1.05 - rad + _Cut.z * (s.b - 0.5) * saturate(eaten * 4.0) * (1.0 - eaten) - 0.04 * eaten;
                float f = min(s.r, 0.5 + 2.5 * cut) - _Flow.w * n;
                if (_More.w > 0.5) f -= 0.75 * Cleared(input.positionWS, input.centreWS);

                float fw = max(fwidth(f), 1e-5);
                float alpha = saturate((f - 0.5) / fw + 0.5);
                clip(alpha - 0.002);

                float t = s.g + _Bands.w - _Shape.y - _Flow.y * (n - 0.5);
                float tw = max(fwidth(t), 1e-5);
                float k0 = saturate((t - _Bands.x) / tw + 0.5);
                float k1 = saturate((t - _Bands.y) / tw + 0.5);
                float k2 = saturate((t - _Bands.z) / tw + 0.5);
                float3 c = lerp(_Tone3.rgb, _Tone2.rgb, k2);
                c = lerp(c, _Tone1.rgb, k1);
                c = lerp(c, _Tone0.rgb, k0);

                float inkPx = _Shape.z * _ScreenParams.y / 1080.0;
                if (inkPx > 0.0)
                {
                    float ink = saturate((0.5 + inkPx * fw - f) / fw + 0.5);
                    c = lerp(c, _Ink.rgb, ink);
                }

                // Its depth: its front, a sphere's, toward the camera.
                float3 toCamera = normalize(GetCameraPositionWS() - input.positionWS);
                float4 front = TransformWorldToHClip(input.positionWS + toCamera * (s.a * _More.z));
                depth = front.z / front.w;

                // The last of it (a small round remnant once nine tenths is eaten) thins away over a frame or two and is gone at
                // 0.9: shrinking to nothing left a field of dots and specks in the third pictures.
                float tail = saturate((0.9 - eaten) / 0.12);
                return half4(c, alpha * saturate(_Shape.w) * tail);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
