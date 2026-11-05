// A TRAFFIC LIGHT'S LAMPS (docs/TRAFFIC_CONTROL.md): every lamp of a chunk in one mesh. Each carries when in its junction's cycle it is lit (SignalLampBuffer), and is
// lit here from the one clock every machine shares (_CityXSignalClock, View/Streets/SignalLampDriver.cs), so the lights cost the CPU nothing and are the same on every
// screen. Unlit, and drawn after the cel look's black lines, as the game's other lights are.
Shader "CityX/SignalLamp"
{
    Properties
    {
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Seconds on the shared clock, wrapped at an hour (every cycle divides one).
            float _CityXSignalClock;
            // 1 in the dead of night, when a side street's lights go to flashing.
            float _CityXSignalNight;
            // How strong the glow round a lit lens is: faint by day, plain at night.
            float _CityXSignalGlow;

            // SignalLampBuffer's flags.
            #define FLASHES_IN_WINDOW 1
            #define FLASHES_AT_NIGHT 2
            #define NIGHT_FLASH_JUNCTION 4
            #define SQUARE 8
            #define HIDDEN_WHEN_OFF 16
            #define GLOW 32

            // How bright a lit lens is (over 1, so it blooms where the grade has bloom) and how dark an unlit one.
            #define LIT 2.6
            #define UNLIT 0.07

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 cycle : TEXCOORD0;    // xy: where on the lens; z: the cycle, w: its offset, seconds
                float4 window : TEXCOORD1;   // x: start, y: length, seconds; z: flags
                half4 colour : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 lens : TEXCOORD0;
                half4 colour : TEXCOORD1;     // rgb, and whether lit
                half fog : TEXCOORD2;
                nointerpolation int flags : TEXCOORD3;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.lens = input.cycle.xy;
                int flags = (int)round(input.window.z);

                float cycle = max(input.cycle.z, 1.0);
                float phase = fmod(_CityXSignalClock + input.cycle.w, cycle);
                float into = fmod(phase - input.window.x + cycle, cycle);
                bool lit = into < input.window.y;
                bool flash = frac(_CityXSignalClock) < 0.5;
                if ((flags & FLASHES_IN_WINDOW) != 0)
                {
                    lit = lit && flash;
                }
                if (_CityXSignalNight > 0.5 && (flags & NIGHT_FLASH_JUNCTION) != 0)
                {
                    lit = (flags & FLASHES_AT_NIGHT) != 0 && flash;
                }
                output.colour = half4(input.colour.rgb, lit ? 1.0 : 0.0);
                output.flags = flags;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                bool lit = input.colour.a > 0.5;
                float2 fromMiddle = input.lens * 2.0 - 1.0;
                float r = length(fromMiddle);
                half alpha = 1.0;
                half3 colour = input.colour.rgb;

                if ((input.flags & GLOW) != 0)
                {
                    float soft = saturate(1.0 - r);
                    alpha = lit ? soft * soft * _CityXSignalGlow : 0.0;
                }
                else
                {
                    if ((input.flags & SQUARE) == 0)
                    {
                        // A round lens, a little darker toward its rim.
                        alpha = saturate((1.0 - r) * 24.0);
                        colour *= lerp(1.0, 0.72, saturate(r * r));
                    }
                    if (!lit && (input.flags & HIDDEN_WHEN_OFF) != 0)
                    {
                        alpha = 0.0;
                    }
                    colour *= lit ? LIT : UNLIT;
                }
                clip(alpha - 0.004);
                // A lit lamp shows through the fog further than what it hangs on.
                half fog = lit ? lerp(1.0, input.fog, 0.5) : input.fog;
                return half4(MixFog(colour, fog), alpha);
            }
            ENDHLSL
        }
    }
}
