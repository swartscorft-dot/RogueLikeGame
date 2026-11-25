// Her model's toon look, a trial (work order 115; Kristian, 2026-11-24, of the key art drawn of her: "Jeg liker også denne
// tegnestilen veldig godt. Klarer vi å få modellen i spillet til å ha mer av det?"). Hand-written for URP 17 (Forward+), the
// project's first shader of its own. The values come from ToonLookValues (Rules/ToonLook.cs) through ToonLook.cs, which puts
// this on her model's renderers and takes it off again; the defaults here are the same values.
//
// - Two tones: every light (the key light, the room's lamps, her own light) is cut at a hard, named threshold of N·L into a lit
//   tone and a shadow tone; the shadow tone is a dark, slightly cool share of the light, not grey. A cast shadow cuts the same
//   way. The ambient is laid on flat. Her texture is read a little blurred (a mip bias) to calm its small detail; her normal map
//   is not read.
// - The edge light: a thin cyan-teal light along her outer edge, kept to the side away from the key light.
// - Teal lights: texels whose green and blue stand well over their red glow (her pistols' lights), under the bloom.
// - The ink line: a second pass, her mesh drawn inside out (front faces culled) and pushed out along its own normals in clip
//   space by a width named in pixels at 1080 rows, so it is as wide on the screen at every camera distance. It is depth tested
//   as she is: hidden where she is hidden, never drawn through anything.
//
// Work order 119 (Kristian, 2026-11-25: "Samme på fiendene. Outline er for tyk."): the enemies wear it too, by EnemyToonLook,
// which sets it as the shader of each enemy's own URP Simple Lit materials (the same objects: everything the game paints on
// them, the daze, the wind-ups, the elite's tint, the ward's, still lands on what is drawn) and sets Simple Lit back when the look
// goes off. So it reads what the game writes into those materials: their colour and texture, and their glow (_EmissionMap times
// _EmissionColor, black and so nothing on her), and it declares the Simple Lit values the enemies' code sets (_EMISSION,
// _Smoothness, _SpecularHighlights), so that each is kept as it was across the change of shader. Her line 1.25 px, her texture
// read 0.5 mip levels blurrier (2.25 and 1.5 in work order 115).
Shader "Roguelike/Toon Her"
{
    Properties
    {
        [MainTexture] _BaseMap("Base map", 2D) = "white" {}
        [MainColor] _BaseColor("Base colour", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha cutoff (not used)", Range(0, 1)) = 0.5

        _LitThreshold("Lit threshold (N.L)", Range(-1, 1)) = 0.05
        _LitSoftness("Lit edge softness (N.L)", Range(0.001, 0.5)) = 0.03
        _LitLevel("Lit tone", Range(0, 2)) = 0.9
        _ShadowTint("Shadow tone", Color) = (0.26, 0.23, 0.36, 1)
        _ShadowEdge("Cast shadow edge", Range(0.01, 0.99)) = 0.5
        _AmbientFlat("Ambient flattening", Range(0, 1)) = 1
        _AmbientLevel("Ambient level", Range(0, 4)) = 1
        _DetailCalm("Detail calm (mip bias)", Range(0, 4)) = 0.5

        _RimColor("Edge light colour", Color) = (0.30, 0.95, 0.86, 1)
        _RimStrength("Fixed edge light strength (0: off; the producer's addition)", Range(0, 2)) = 0
        _RimStart("Edge light start (1 - N.V)", Range(0, 1)) = 0.75
        _RimSoftness("Edge light softness", Range(0.001, 0.5)) = 0.05
        _RimAway("Edge light away from the key light", Range(0, 1)) = 0.85
        _RimUnder("Edge light gone on faces turned down this far (-N.y)", Range(0.01, 1)) = 0.5
        _LampEdge("Edge light from the room's lights (1 on, 0 off)", Range(0, 1)) = 1
        _LampEdgeStrength("Its strength, a share of the light reaching her", Range(0, 2)) = 0.6
        _LampEdgeStart("Where on her edge it starts (1 - N.V)", Range(0, 1)) = 0.6
        _LampEdgeSoftness("Its softness", Range(0.001, 0.5)) = 0.08

        _TealGlow("Teal lights glow", Range(0, 3)) = 0
        _TealStart("Teal mask start", Range(0, 1)) = 0.10
        _TealSoftness("Teal mask softness", Range(0.01, 1)) = 0.15

        _OutlineColor("Ink colour", Color) = (0.06, 0.04, 0.06, 1)
        _OutlineWidth("Ink width (px at 1080 rows)", Range(0, 8)) = 1.25
        _OutlineDepthPush("Ink pushed back (m)", Range(0, 0.5)) = 0.06
        // Work order 125, the clean line (ToonLookValues.CleanInk; both 0 while it is off, work order 119's line exactly): 1 pushes
        // the hull back along the line of sight (119's push along the camera's axis moved it on the screen toward its middle, so
        // that the line thickened on one side of a figure and was lost on the other); and 1 draws the hull only round a renderer
        // marked for it (a bit of its shader user value, Runtime/ToonInkFigures.cs: her model and each enemy's own meshes, not a
        // loose piece that copies an enemy's materials).
        [HideInInspector] _OutlineAlongRay("Ink pushed back along the line of sight (work order 125)", Float) = 0
        [HideInInspector] _InkNeedsMark("Ink only round a marked renderer (work order 125)", Float) = 0

        // Work order 119: an enemy's own glow, as URP Simple Lit has it (the caster's eyes and palms, the warder's green); black on her.
        [HDR] _EmissionColor("Glow colour (an enemy's; black on her)", Color) = (0, 0, 0, 1)
        [NoScaleOffset] _EmissionMap("Glow map", 2D) = "white" {}
        // Not read here: declared so that an enemy's Simple Lit values are kept as they were while it wears this shader.
        [HideInInspector] _Smoothness("Smoothness (Simple Lit's, not read)", Range(0, 1)) = 0.5
        [HideInInspector] _SpecularHighlights("Specular highlights (Simple Lit's, not read)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True" "Queue" = "Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // One set of material values for every pass (the SRP batcher wants the same layout in each).
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _Surface;
            half _LitThreshold;
            half _LitSoftness;
            half _LitLevel;
            half4 _ShadowTint;
            half _ShadowEdge;
            half _AmbientFlat;
            half _AmbientLevel;
            half _DetailCalm;
            half4 _RimColor;
            half _RimStrength;
            half _RimStart;
            half _RimSoftness;
            half _RimAway;
            half _RimUnder;
            half _LampEdge;
            half _LampEdgeStrength;
            half _LampEdgeStart;
            half _LampEdgeSoftness;
            half _TealGlow;
            half _TealStart;
            half _TealSoftness;
            half4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineDepthPush;
            half4 _EmissionColor; // work order 119
            float _OutlineAlongRay; // work order 125
            float _InkNeedsMark;    // work order 125
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap); // work order 119: read with the base map's sampler
        ENDHLSL

        // ---------------- Her, in two tones ----------------
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment

            // Work order 119: Simple Lit's own keyword for a glow, declared so that an enemy's material keeps it while it wears this
            // shader (the game asks for it: the warder's green glows, the tests of the caster's). The code below does not branch on
            // it: the glow is added always, and is black where there is none, so a build that strips this variant draws the same.
            #pragma shader_feature_local_fragment _EMISSION

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS    : TEXCOORD2;
                half fogFactor    : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ToonVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(p.positionCS.z);
                return output;
            }

            // A light cut into two tones: the lit tone where N·L (and the cast shadow) is past the threshold, the shadow tone under it.
            // And the edge light it gives (work order 115's follow-up: Kristian, 2026-11-24, "det lyset er en refleksjon? Det er ikke
            // \"på\" karakteren"): on her edge as the camera sees it (grazing, 1 - N·V past _LampEdgeStart), on the side that faces
            // this light and where its shadow lets it through, in the light's own colour and by its own strength at her (times N·L),
            // so a lamp far off gives a faint edge and none at all where no light reaches her.
            half3 Band(Light light, half3 normalWS, half grazing, uint meshLayers, inout half3 edgeLight)
            {
            #ifdef _LIGHT_LAYERS
                if (!IsMatchingLightLayer(light.layerMask, meshLayers)) return 0;
            #endif
                half ndl = dot(normalWS, light.direction);
                half lit = smoothstep(_LitThreshold - _LitSoftness, _LitThreshold + _LitSoftness, ndl);
                half cast = smoothstep(_ShadowEdge - _LitSoftness, _ShadowEdge + _LitSoftness, light.shadowAttenuation);
                lit = min(lit, cast);
                half3 reach = light.color * light.distanceAttenuation;
                edgeLight += reach * (grazing * lit * saturate(ndl));
                half3 tone = lerp(_ShadowTint.rgb, _LitLevel.xxx, lit);
                return reach * tone;
            }

            half4 ToonFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Her texture, read a little blurred: the fields stay, the fine noise goes.
                half4 texel = SAMPLE_TEXTURE2D_BIAS(_BaseMap, sampler_BaseMap, input.uv, _DetailCalm);
                half3 albedo = texel.rgb * _BaseColor.rgb;

                half3 normalWS = normalize(input.normalWS);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #endif
                half4 shadowMask = half4(1, 1, 1, 1);
                uint meshLayers = GetMeshRenderingLayer();

                // The ambient, laid on flat (the same on every face), under the screen's ambient occlusion as today.
                half3 ambient = lerp(EvaluateAmbientProbeSRGB(normalWS), EvaluateAmbientProbeSRGB(half3(0, 0, 0)), _AmbientFlat) * _AmbientLevel;
            #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                ambient *= ao.indirectAmbientOcclusion;
            #endif
                half3 light = ambient;
                half grazing = smoothstep(_LampEdgeStart - _LampEdgeSoftness, _LampEdgeStart + _LampEdgeSoftness, 1.0h - saturate(dot(normalWS, viewWS)));
                half3 edgeLight = 0;

                Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);
                light += Band(mainLight, normalWS, grazing, meshLayers, edgeLight);

            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
            #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light dl = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
                    light += Band(dl, normalWS, grazing, meshLayers, edgeLight);
                }
            #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light al = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
                    light += Band(al, normalWS, grazing, meshLayers, edgeLight);
                LIGHT_LOOP_END
            #endif

                half3 colour = albedo * light;

                // The edge light from the room's own lights (see Band): their colours, their strength at her; none in the dark.
                colour += edgeLight * (_LampEdgeStrength * _LampEdge);

                // The fixed edge light (off by default since the follow-up: strength 0): along her outer edge, on the side away
                // from the key light, always teal, wherever she stands. The producer's addition, kept to compare.
                half edge = 1.0h - saturate(dot(normalWS, viewWS));
                half rim = smoothstep(_RimStart - _RimSoftness, _RimStart + _RimSoftness, edge);
                half away = saturate(0.5h - 0.5h * dot(normalWS, mainLight.direction));
                rim *= lerp(1.0h, saturate(away * 2.0h - 0.5h), _RimAway);
                rim *= saturate((normalWS.y + _RimUnder) / _RimUnder); // not on the underside of the hat's brim and the like: a light from behind her does not reach faces turned down
                colour += _RimColor.rgb * (_RimStrength * rim);

                // Teal lights: a texel whose green and blue both stand over its red glows, at most at _TealGlow.
                half over = min(texel.g, texel.b) - texel.r;
                half teal = saturate((over - _TealStart) / _TealSoftness);
                half peak = max(max(texel.r, texel.g), max(texel.b, 0.001h));
                colour += (texel.rgb / peak) * (teal * _TealGlow);

                // Work order 119: an enemy's own glow, as Simple Lit adds it (its map times its colour; black on her).
                colour += SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, input.uv).rgb * _EmissionColor.rgb;

                colour = MixFog(colour, InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor));
                return half4(colour, 1.0h);
            }
            ENDHLSL
        }

        // ---------------- The ink line ----------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            // Work order 125: the bit of a renderer's shader user value that marks it for the line (ToonInkFigures.InkBit).
            #define INK_MARK 1u

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fogFactor    : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // Pushed a little away from the camera: the line of a small piece lying on her (a buckle, a pouch, a button) stays
                // behind the surface it lies on, so only real edges and the silhouette are inked.
                float3 positionVS = TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz));
                // Work order 125: along the camera's axis (119's) the push moves the hull on the screen toward the screen's middle
                // by push / distance of its way from there (at 6.5 m, 3 px at 300 px out: more than the line is wide): the line
                // thickened on one side of a figure and was lost on the other. Along the line of sight it stays where it is.
                if (_OutlineAlongRay > 0.5) positionVS *= 1.0 + _OutlineDepthPush / max(length(positionVS), 1e-3);
                else positionVS.z -= _OutlineDepthPush;
                float4 positionCS = TransformWViewToHClip(positionVS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy;
                // The normal's way on the screen in pixels (clip space x and y are scaled by the screen's width and height).
                float2 screen = _ScreenParams.xy;
                float2 way = normalCS * screen;
                float len = length(way);
                way = len > 1e-6 ? way / len : float2(0, 0);
                float pixels = _OutlineWidth * screen.y / 1080.0;
                // Pixels to clip space: the screen spans 2 in normalised device coordinates, times w for the perspective divide.
                positionCS.xy += way * (pixels * 2.0) / screen * positionCS.w;
                // Work order 125: no line round a renderer that is not marked for it (every vertex on one point: nothing is drawn).
                if (_InkNeedsMark > 0.5 && (unity_RendererUserValue & INK_MARK) == 0u) positionCS = float4(0.0, 0.0, 0.0, 1.0);
                output.positionCS = positionCS;
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 colour = MixFog(_OutlineColor.rgb, input.fogFactor);
                return half4(colour, 1.0h);
            }
            ENDHLSL
        }

        // ---------------- What URP asks of every opaque material ----------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
