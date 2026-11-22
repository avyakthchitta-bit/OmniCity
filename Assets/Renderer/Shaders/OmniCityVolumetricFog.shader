
Shader "OmniCity/Rendering/Volumetric Fog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.65, 0.72, 0.8, 1)

        _FogDensity ("Atmospheric Density", Range(0, 0.02)) = 0.0015
        _FogStart ("Fog Start Distance", Float) = 10
        _FogEnd ("Fog End Distance", Float) = 150

        _GroundFogDensity ("Ground Fog Density", Range(0, 0.1)) = 0.025
        _GroundFogHeight ("Ground Fog Height", Float) = 0
        _GroundFogFalloff ("Ground Fog Height Falloff", Range(0.01, 1)) = 0.15

        _FogSteps ("Ray March Steps", Range(8, 64)) = 32

        _ScatteringIntensity ("Sun Scattering Intensity", Range(0, 2)) = 0.03
        _ScatteringAnisotropy ("Scattering Anisotropy", Range(-0.9, 0.9)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "VolumetricFog"

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            CBUFFER_START(UnityPerMaterial)

                float4 _FogColor;

                float _FogDensity;
                float _FogStart;
                float _FogEnd;

                float _GroundFogDensity;
                float _GroundFogHeight;
                float _GroundFogFalloff;

                float _FogSteps;

                float _ScatteringIntensity;
                float _ScatteringAnisotropy;

            CBUFFER_END

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS =
                    GetFullScreenTriangleVertexPosition(input.vertexID);

                output.uv =
                    GetFullScreenTriangleTexCoord(input.vertexID);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;

                half4 sceneColor = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_BlitTexture,
                    uv
                );

                float rawDepth = SampleSceneDepth(uv);

                #if UNITY_REVERSED_Z

                    if (rawDepth <= 0.00001)
                        return sceneColor;

                    float depth = rawDepth;

                #else

                    if (rawDepth >= 0.99999)
                        return sceneColor;

                    float depth = lerp(
                        UNITY_NEAR_CLIP_VALUE,
                        1.0,
                        rawDepth
                    );

                #endif

                float3 worldPosition = ComputeWorldSpacePosition(
                    uv,
                    depth,
                    UNITY_MATRIX_I_VP
                );

                float3 cameraPosition = GetCameraPositionWS();

                float3 rayVector = worldPosition - cameraPosition;

                float distanceToScene = length(rayVector);

                if (distanceToScene <= _FogStart)
                    return sceneColor;

                float3 rayDirection =
                    rayVector / max(distanceToScene, 0.0001);

                float marchDistance = min(
                    distanceToScene - _FogStart,
                    max(_FogEnd - _FogStart, 0.001)
                );

                int stepCount = (int)clamp(
                    round(_FogSteps),
                    8.0,
                    64.0
                );

                float stepLength = marchDistance / stepCount;

                float opticalDepth = 0.0;

                float3 scatteredLight = float3(0, 0, 0);

                // Ray march through the atmosphere.
                [loop]
                for (int i = 0; i < 64; i++)
                {
                    if (i >= stepCount)
                        break;

                    float travelDistance =
                        _FogStart + (i + 0.5) * stepLength;

                    float3 samplePosition =
                        cameraPosition +
                        rayDirection * travelDistance;

                    // Atmospheric density.
                    float atmosphericDensity = _FogDensity;

                    // Height-dependent ground fog.
                    float heightAboveGround = max(
                        samplePosition.y - _GroundFogHeight,
                        0.0
                    );

                    float groundDensity =
                        _GroundFogDensity *
                        exp(
                            -heightAboveGround *
                            _GroundFogFalloff
                        );

                    float totalDensity =
                        atmosphericDensity + groundDensity;

                    // Get the main directional light and its shadow.
                    float4 shadowCoord =
                        TransformWorldToShadowCoord(samplePosition);

                    Light mainLight = GetMainLight(shadowCoord);

                    // Henyey-Greenstein scattering phase approximation.
                    float anisotropy = clamp(
                        _ScatteringAnisotropy,
                        -0.9,
                        0.9
                    );

                    float cosTheta = dot(
                        rayDirection,
                        mainLight.direction
                    );

                    float phaseDenominator = max(
                        1.0 +
                        anisotropy * anisotropy -
                        2.0 * anisotropy * cosTheta,
                        0.001
                    );

                    float phase = (
                        1.0 - anisotropy * anisotropy
                    ) / (
                        phaseDenominator *
                        sqrt(phaseDenominator)
                    );

                    // Shadow attenuation suppresses sunlight in shadows.
                    float shadowAttenuation =
                        mainLight.shadowAttenuation;

                    float currentTransmittance =
                        exp(-opticalDepth);

                    // Accumulate sunlight scattered toward the camera.
                    float3 lightContribution =
                        mainLight.color *
                        shadowAttenuation *
                        phase *
                        totalDensity *
                        stepLength *
                        currentTransmittance;

                    scatteredLight += lightContribution;

                    // Accumulate atmospheric extinction.
                    opticalDepth +=
                        totalDensity * stepLength;
                }

                float transmittance = exp(-opticalDepth);

                float fogAmount = saturate(
                    1.0 - transmittance
                );

                // Base atmospheric fog.
                half3 foggedColor = lerp(
                    sceneColor.rgb,
                    _FogColor.rgb,
                    fogAmount
                );

                // Add sunlight scattering.
                half3 finalColor =
                    foggedColor +
                    scatteredLight * _ScatteringIntensity;

                return half4(finalColor, sceneColor.a);
            }

            ENDHLSL
        }
    }
}
