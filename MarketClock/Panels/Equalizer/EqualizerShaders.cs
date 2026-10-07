using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media.Effects;

namespace MarketClock
{
    /// <summary>
    /// The equalizer's looks. Each one is a small GPU pixel shader, written in HLSL below and
    /// compiled when first used. Every shader gets the same inputs:
    ///   Spectrum - a one-pixel-high texture, bass on the left, treble on the right;
    ///              red = current level (0..1), green = falling peak marker (0..1)
    ///   Time     - seconds, keeps counting while music plays
    ///   Aspect   - panel width divided by height
    ///   Bass     - loudness of the lowest few bands (0..1)
    /// To add a look, add an entry to <see cref="Styles"/>.
    /// </summary>
    internal static class EqualizerShaders
    {
        public const int BandCount = 24;

        private const string Header = @"
sampler2D Input : register(s0);
sampler2D Spectrum : register(s1);
float Time : register(c0);
float Aspect : register(c1);
float Bass : register(c2);

float4 Spec(float x)
{
    return tex2D(Spectrum, float2(saturate(x), 0.5));
}

// Level of the band that column x falls in (no blending between neighbours).
float4 Band(float x)
{
    return Spec((floor(saturate(x) * 23.999) + 0.5) / 24.0);
}
";

        public static readonly (string Name, string Source)[] Styles =
        {
            // 1. Classic segmented bars with falling peak caps, green to red.
            ("Bars", Header + @"
float4 main(float2 uv : TEXCOORD) : COLOR
{
    float fx = uv.x * 24.0;
    float4 s = Band(uv.x);
    float y = 1.0 - uv.y;

    float inBar = step(abs(frac(fx) - 0.5), 0.38);
    float seg = step(frac(y * 16.0), 0.72);

    float3 col = lerp(float3(0.22, 0.83, 0.33), float3(1.0, 0.84, 0.04), smoothstep(0.45, 0.75, y));
    col = lerp(col, float3(1.0, 0.27, 0.23), smoothstep(0.75, 1.0, y));

    float lit = step(y, s.r) * inBar * seg;
    float cap = inBar * step(abs(y - s.g), 0.015) * step(0.02, s.g);

    float3 c = col * (lit + 0.07 * inBar * seg) + cap * float3(1.0, 1.0, 1.0);
    return float4(saturate(c), 1.0);
}"),

            // 2. A smooth glowing wave, cyan to magenta.
            ("Wave", Header + @"
float4 main(float2 uv : TEXCOORD) : COLOR
{
    float level = Spec(uv.x).r;
    float y = 1.0 - uv.y;
    float d = y - level * 0.9;

    float fill = 1.0 - smoothstep(-0.02, 0.02, d);
    float glow = exp(-abs(d) * 28.0);

    float3 col = lerp(float3(0.1, 0.9, 1.0), float3(1.0, 0.0, 1.0), saturate(uv.x + 0.15 * sin(Time * 0.7)));
    float3 c = col * (fill * (0.25 + 0.5 * y) + glow);
    return float4(saturate(c), 1.0);
}"),

            // 3. Bars mirrored about the centre line.
            ("Mirror", Header + @"
float4 main(float2 uv : TEXCOORD) : COLOR
{
    float fx = uv.x * 24.0;
    float4 s = Band(uv.x);
    float m = abs(uv.y - 0.5) * 2.0;

    float inBar = step(abs(frac(fx) - 0.5), 0.36);
    float lit = step(m, s.r) * inBar;

    float3 col = lerp(float3(0.1, 0.9, 1.0), float3(1.0, 0.0, 1.0), m);
    float line0 = exp(-m * 40.0) * 0.5;

    float3 c = col * lit * (1.0 - m * 0.35) + float3(0.6, 1.0, 1.0) * line0;
    return float4(saturate(c), 1.0);
}"),

            // 4. Rays bursting out of a ring that pulses with the bass.
            ("Radial", Header + @"
float4 main(float2 uv : TEXCOORD) : COLOR
{
    float2 p = (uv - 0.5) * float2(Aspect, 1.0) * 2.0;
    float r = length(p);
    float a = atan2(p.y, p.x) / 6.2831853 + 0.5;
    float t = abs(frac(a + Time * 0.02) * 2.0 - 1.0);

    float level = Band(t).r;
    float ringR = 0.32 + Bass * 0.08;
    float outer = ringR + level * 0.6;

    float ray = step(abs(frac(t * 24.0) - 0.5), 0.36);
    float rays = step(ringR, r) * step(r, outer) * ray;
    float ring = exp(-abs(r - ringR) * 40.0);
    float core = (1.0 - smoothstep(0.0, ringR, r)) * Bass * 0.6;

    float3 col = lerp(float3(0.22, 0.83, 0.33), float3(1.0, 0.0, 1.0), saturate((r - ringR) / 0.6));
    float3 c = col * rays + float3(0.6, 1.0, 0.9) * ring + float3(1.0, 0.0, 1.0) * core;
    return float4(saturate(c), 1.0);
}"),

            // 5. Flowing plasma whose colour and brightness follow the music.
            ("Plasma", Header + @"
float4 main(float2 uv : TEXCOORD) : COLOR
{
    float level = Spec(uv.x).r;
    float2 q = uv * float2(Aspect, 1.0);

    float v = sin(q.x * 6.0 + Time * 1.3)
            + sin(q.y * 9.0 - Time * 1.7)
            + sin((q.x + q.y) * 5.0 + Time * 0.9)
            + sin(length(q - float2(0.5 * Aspect, 0.5)) * 12.0 - Time * 2.0);
    v = v * 0.25 + level * 1.2 + Bass * 0.6;

    float3 col = 0.5 + 0.5 * cos(6.2831853 * (v + float3(0.0, 0.33, 0.67)));

    float y = 1.0 - uv.y;
    float bright = 0.15 + 0.85 * (1.0 - smoothstep(level - 0.15, level + 0.25, y));
    float3 c = col * bright * (0.35 + 0.65 * saturate(Bass * 1.5 + level));
    return float4(saturate(c), 1.0);
}"),
        };
    }

    /// <summary>A WPF effect that runs one of the equalizer shaders over whatever it is applied to.</summary>
    public sealed class SpectrumShaderEffect : ShaderEffect
    {
        public static readonly DependencyProperty InputProperty =
            RegisterPixelShaderSamplerProperty(nameof(Input), typeof(SpectrumShaderEffect), 0);

        public static readonly DependencyProperty SpectrumProperty =
            RegisterPixelShaderSamplerProperty(nameof(Spectrum), typeof(SpectrumShaderEffect), 1);

        public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(
            nameof(Time), typeof(double), typeof(SpectrumShaderEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0)));

        public static readonly DependencyProperty AspectProperty = DependencyProperty.Register(
            nameof(Aspect), typeof(double), typeof(SpectrumShaderEffect),
            new UIPropertyMetadata(1.0, PixelShaderConstantCallback(1)));

        public static readonly DependencyProperty BassProperty = DependencyProperty.Register(
            nameof(Bass), typeof(double), typeof(SpectrumShaderEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(2)));

        public SpectrumShaderEffect(byte[] compiledShader)
        {
            var shader = new PixelShader();
            shader.SetStreamSource(new MemoryStream(compiledShader));
            PixelShader = shader;

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(SpectrumProperty);
            UpdateShaderValue(TimeProperty);
            UpdateShaderValue(AspectProperty);
            UpdateShaderValue(BassProperty);
        }

        public System.Windows.Media.Brush Input
        {
            get => (System.Windows.Media.Brush)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }

        public System.Windows.Media.Brush Spectrum
        {
            get => (System.Windows.Media.Brush)GetValue(SpectrumProperty);
            set => SetValue(SpectrumProperty, value);
        }

        public double Time
        {
            get => (double)GetValue(TimeProperty);
            set => SetValue(TimeProperty, value);
        }

        public double Aspect
        {
            get => (double)GetValue(AspectProperty);
            set => SetValue(AspectProperty, value);
        }

        public double Bass
        {
            get => (double)GetValue(BassProperty);
            set => SetValue(BassProperty, value);
        }
    }

    /// <summary>Compiles HLSL text into a pixel shader using the compiler that ships with Windows.</summary>
    internal static class HlslCompiler
    {
        [ComImport]
        [Guid("8BA5FB08-5195-40E2-AC58-0D989C3A0102")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ID3DBlob
        {
            [PreserveSig]
            IntPtr GetBufferPointer();

            [PreserveSig]
            IntPtr GetBufferSize();
        }

        [DllImport("d3dcompiler_47.dll", PreserveSig = true)]
        private static extern int D3DCompile(
            byte[] source,
            IntPtr sourceSize,
            [MarshalAs(UnmanagedType.LPStr)] string? sourceName,
            IntPtr defines,
            IntPtr include,
            [MarshalAs(UnmanagedType.LPStr)] string entryPoint,
            [MarshalAs(UnmanagedType.LPStr)] string target,
            uint flags1,
            uint flags2,
            out IntPtr code,
            out IntPtr errors);

        /// <summary>Returns the compiled shader, or throws with the compiler's own error text.</summary>
        public static byte[] CompilePixelShader(string hlsl)
        {
            var source = Encoding.ASCII.GetBytes(hlsl);

            // ps_3_0 is the newest shader model WPF effects can run.
            var result = D3DCompile(source, (IntPtr)source.Length, null, IntPtr.Zero, IntPtr.Zero,
                "main", "ps_3_0", 0, 0, out var code, out var errors);

            try
            {
                if (result < 0 || code == IntPtr.Zero)
                {
                    var message = errors == IntPtr.Zero
                        ? $"error 0x{result:X8}"
                        : Encoding.ASCII.GetString(ReadBlob(errors)).TrimEnd('\0', '\r', '\n');
                    throw new InvalidOperationException($"Shader did not compile: {message}");
                }

                return ReadBlob(code);
            }
            finally
            {
                if (code != IntPtr.Zero)
                {
                    Marshal.Release(code);
                }

                if (errors != IntPtr.Zero)
                {
                    Marshal.Release(errors);
                }
            }
        }

        private static byte[] ReadBlob(IntPtr blobPointer)
        {
            var blob = (ID3DBlob)Marshal.GetObjectForIUnknown(blobPointer);
            try
            {
                var bytes = new byte[(int)blob.GetBufferSize()];
                Marshal.Copy(blob.GetBufferPointer(), bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                Marshal.ReleaseComObject(blob);
            }
        }
    }
}
