// Godot's ProceduralSkyMaterial, ported.
//
// WHY A SHADER AND NOT UNITY'S OWN PROCEDURAL SKYBOX. Unity's is an
// atmospheric-scattering model driven by a sun position; Godot's is a
// two-colour gradient with an explicit curve, and city.tscn's own values
// (sky_curve = 0.09, ground_curve = 0.02) are tuned against THAT curve.
// Feeding them to a different model would produce a different sky, and the
// sky is most of why the two builds look different at all.
//
// The maths below is Godot's sky_material.cpp shader, line for line: the
// vertical angle, the two gradients either side of the horizon, and a disc
// per directional light with a soft glow around it. Two lights, because the
// sun and the moon each draw their own -- which is what day_night_cycle.gd
// relies on for the moon to be visible at night.
Shader "CityX/ProceduralSky"
{
    Properties
    {
        _SkyTopColour("Sky Top", Color) = (0.22, 0.42, 0.82, 1)
        _SkyHorizonColour("Sky Horizon", Color) = (0.64, 0.76, 0.91, 1)
        _SkyCurve("Sky Curve", Range(0.001, 1)) = 0.09
        _SkyEnergy("Sky Energy", Float) = 1.0

        _GroundBottomColour("Ground Bottom", Color) = (0.16, 0.16, 0.18, 1)
        _GroundHorizonColour("Ground Horizon", Color) = (0.64, 0.76, 0.91, 1)
        _GroundCurve("Ground Curve", Range(0.001, 1)) = 0.02
        _GroundEnergy("Ground Energy", Float) = 0.6

        // Directions point TOWARDS the body, which is -transform.forward for
        // a Unity directional light.
        _SunDirection("Sun Direction", Vector) = (0, 1, 0, 0)
        _SunColour("Sun Colour", Color) = (1, 1, 1, 1)
        _SunEnergy("Sun Energy", Float) = 1.0
        _SunAngularRadius("Sun Angular Radius", Float) = 1.0

        _MoonDirection("Moon Direction", Vector) = (0, -1, 0, 0)
        _MoonColour("Moon Colour", Color) = (0.62, 0.72, 1.0, 1)
        _MoonEnergy("Moon Energy", Float) = 0.0
        _MoonAngularRadius("Moon Angular Radius", Float) = 1.5

        _SunAngleMax("Glow Radius", Float) = 30.0
        _SunCurve("Glow Curve", Range(0.001, 1)) = 0.08

        // The stars: how bright (0 by day; DayNightCycle raises it as night falls and a cloud lid hides it) and how many cells of sky
        // across a radian. With nine buildings in ten dark, the night over this city is a starry one (2026-10-09 lighting pass).
        _StarEnergy("Star Energy", Float) = 0.0
        _StarDensity("Star Density", Float) = 200.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define CITYX_PI 3.14159265358979323846

            float4 _SkyTopColour;
            float4 _SkyHorizonColour;
            float _SkyCurve;
            float _SkyEnergy;

            float4 _GroundBottomColour;
            float4 _GroundHorizonColour;
            float _GroundCurve;
            float _GroundEnergy;

            float4 _SunDirection;
            float4 _SunColour;
            float _SunEnergy;
            float _SunAngularRadius;

            float4 _MoonDirection;
            float4 _MoonColour;
            float _MoonEnergy;
            float _MoonAngularRadius;

            float _SunAngleMax;
            float _SunCurve;

            float _StarEnergy;
            float _StarDensity;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // A skybox mesh's own vertex position IS the view direction.
                output.direction = input.positionOS.xyz;
                return output;
            }

            /// One body's contribution: a solid disc out to its angular radius,
            /// then a glow falling off to nothing at _SunAngleMax. The curve
            /// exponent is 1/_SunCurve -- see SkyCycle.GlowCurve's own doc
            /// comment for why LARGER _SunCurve (a SMALLER exponent) is what
            /// keeps that falloff tight against the disc, not smaller.
            float3 Body(float3 eye, float3 towards, float3 colour, float energy,
                float angularRadius)
            {
                if (energy <= 0.0)
                {
                    return float3(0.0, 0.0, 0.0);
                }

                float angle = degrees(acos(clamp(dot(towards, eye), -1.0, 1.0)));
                if (angle < angularRadius)
                {
                    return colour * energy;
                }
                if (angle < _SunAngleMax)
                {
                    float t = (angle - angularRadius) / max(_SunAngleMax - angularRadius, 1e-4);
                    return colour * energy * (1.0 - pow(t, 1.0 / _SunCurve));
                }
                return float3(0.0, 0.0, 0.0);
            }

            float StarHash(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            /// The stars: the sky cut into cells (a 3D grid the unit sphere passes through), a few cells holding one star each at a place of
            /// its own, of a brightness and tint of its own, twinkling a little. Soft-edged and never under about a pixel and a half, so they
            /// do not shimmer as the camera turns. Faded out towards the horizon, where the haze is.
            float3 Stars(float3 eye)
            {
                if (_StarEnergy <= 0.0 || eye.y <= 0.0)
                {
                    return float3(0.0, 0.0, 0.0);
                }
                float3 p = eye * _StarDensity;
                float3 cell = floor(p);
                float3 total = float3(0.0, 0.0, 0.0);
                // The cell and its neighbours along the axis the sphere crosses most steeply would be exact; the cell itself is enough at
                // this density, the odd star cut at a cell edge is too small to see.
                float exists = StarHash(cell);
                if (exists < 0.025)
                {
                    float3 centre = cell + 0.25 + 0.5 * float3(StarHash(cell + 11.7), StarHash(cell + 23.1), StarHash(cell + 37.9));
                    float d = length(p - centre);
                    float magnitude = pow(StarHash(cell + 51.3), 6.0);       // most stars faint, a few bright
                    float size = lerp(0.16, 0.34, magnitude);
                    float core = saturate(1.0 - d / size);
                    float twinkle = 0.8 + 0.2 * sin(_Time.y * (1.5 + 3.0 * StarHash(cell + 71.9)) + exists * 600.0);
                    float tint = StarHash(cell + 91.3);
                    float3 colour = tint < 0.2 ? float3(0.75, 0.85, 1.0) : (tint > 0.85 ? float3(1.0, 0.86, 0.68) : float3(1.0, 1.0, 1.0));
                    total = colour * core * core * lerp(0.25, 2.6, magnitude) * twinkle;
                }
                return total * _StarEnergy * smoothstep(0.02, 0.35, eye.y);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 eye = normalize(input.direction);

                // Godot works in the angle down from straight up rather than in
                // the y component directly, which is what makes the curve
                // exponent behave the same near the horizon as near the zenith.
                float verticalAngle = acos(clamp(eye.y, -1.0, 1.0));

                float above = 1.0 - verticalAngle / (CITYX_PI * 0.5);
                float skyBlend = saturate(1.0 - pow(max(1.0 - above, 0.0), 1.0 / _SkyCurve));
                float3 sky = lerp(_SkyHorizonColour.rgb, _SkyTopColour.rgb, skyBlend) * _SkyEnergy;

                sky += Body(eye, normalize(_SunDirection.xyz), _SunColour.rgb,
                    _SunEnergy, _SunAngularRadius);
                sky += Body(eye, normalize(_MoonDirection.xyz), _MoonColour.rgb,
                    _MoonEnergy, _MoonAngularRadius);
                sky += Stars(eye);

                float below = (verticalAngle - CITYX_PI * 0.5) / (CITYX_PI * 0.5);
                float groundBlend = saturate(1.0 - pow(max(1.0 - below, 0.0), 1.0 / _GroundCurve));
                float3 ground =
                    lerp(_GroundHorizonColour.rgb, _GroundBottomColour.rgb, groundBlend) *
                    _GroundEnergy;

                return half4(eye.y >= 0.0 ? sky : ground, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
