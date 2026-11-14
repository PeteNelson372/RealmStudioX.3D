
sampler2D InputSampler : register(s0);

float Threshold : register(c0);
float ContrastThreshold : register(c1);
float Intensity : register(c2);
float Speed : register(c3);
float Time : register(c4);
float TexelWidth : register(c5);
float TexelHeight : register(c6);
float MaximumStarSize : register(c7);

static const float PI = 3.14159265;

// Deterministic pseudo-random value in [0, 1].
float Hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float Luminance(float3 color)
{
    return dot(color, float3(0.299, 0.587, 0.114));
}

// Smoothly interpolated noise. Unlike floor-based cell assignment,
// this does not introduce visible rectangular animation boundaries.
float ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);

    f = f * f * (3.0 - 2.0 * f);

    float a = Hash(i);
    float b = Hash(i + float2(1.0, 0.0));
    float c = Hash(i + float2(0.0, 1.0));
    float d = Hash(i + float2(1.0, 1.0));

    return lerp(
        lerp(a, b, f.x),
        lerp(c, d, f.x),
        f.y);
}

float4 PS_Scintillation(float2 uv : TEXCOORD0) : COLOR0
{
    float4 original = tex2D(InputSampler, uv);

    float centerLum = Luminance(original.rgb);

    float2 texel = float2(TexelWidth, TexelHeight);

    // ---------------------------------------------------------
    // 1. Local brightness and contrast detection
    // ---------------------------------------------------------

    float neighborLum = 0.0;

    neighborLum += Luminance(
        tex2D(InputSampler, uv + float2(texel.x, 0.0)).rgb);

    neighborLum += Luminance(
        tex2D(InputSampler, uv + float2(-texel.x, 0.0)).rgb);

    neighborLum += Luminance(
        tex2D(InputSampler, uv + float2(0.0, texel.y)).rgb);

    neighborLum += Luminance(
        tex2D(InputSampler, uv + float2(0.0, -texel.y)).rgb);

    neighborLum += Luminance(
        tex2D(InputSampler, uv + texel).rgb);

    neighborLum += Luminance(
        tex2D(InputSampler, uv - texel).rgb);

    neighborLum += Luminance(
        tex2D(InputSampler, uv + float2(texel.x, -texel.y)).rgb);

    neighborLum += Luminance(
        tex2D(InputSampler, uv + float2(-texel.x, texel.y)).rgb);

    neighborLum *= 0.125;

    float localContrast = centerLum - neighborLum;

    float brightnessMask = smoothstep(
        Threshold - 0.15,
        Threshold + 0.08,
        centerLum);

    float contrastMask = smoothstep(
        ContrastThreshold,
        ContrastThreshold + 0.08,
        localContrast);

    // ---------------------------------------------------------
    // 2. Reject stars that are too large
    // ---------------------------------------------------------

    float radius = max(MaximumStarSize, 1.0);

    float2 offsetX = float2(texel.x * radius, 0.0);
    float2 offsetY = float2(0.0, texel.y * radius);

    float2 offsetXY = float2(
        texel.x * radius * 0.7071,
        texel.y * radius * 0.7071);

    float ringLum = 0.0;

    ringLum += Luminance(
        tex2D(InputSampler, uv + offsetX).rgb);

    ringLum += Luminance(
        tex2D(InputSampler, uv - offsetX).rgb);

    ringLum += Luminance(
        tex2D(InputSampler, uv + offsetY).rgb);

    ringLum += Luminance(
        tex2D(InputSampler, uv - offsetY).rgb);

    ringLum += Luminance(
        tex2D(InputSampler, uv + offsetXY).rgb);

    ringLum += Luminance(
        tex2D(InputSampler, uv - offsetXY).rgb);

    ringLum += Luminance(
        tex2D(InputSampler, uv + float2(offsetXY.x, -offsetXY.y)).rgb);

    ringLum += Luminance(
        tex2D(InputSampler, uv + float2(-offsetXY.x, offsetXY.y)).rgb);

    ringLum *= 0.125;

    float ringRatio = ringLum / max(centerLum, 0.001);

    // A compact star has a relatively dark surrounding ring.
    // A large star has substantial brightness at this radius.
    float sizeMask = 1.0 - smoothstep(
        0.25,
        0.65,
        ringRatio);

    float starMask =
        brightnessMask *
        contrastMask *
        sizeMask;

    // ---------------------------------------------------------
    // 3. Smooth, spatially coherent animation
    // ---------------------------------------------------------

    // Noise coordinates are scaled to approximately one
    // animation region per several screen pixels.
    float2 noiseCoordinates = uv / max(
        texel * 8.0,
        float2(0.000001, 0.000001));

    float phaseNoise = ValueNoise(noiseCoordinates);
    float frequencyNoise = ValueNoise(
        noiseCoordinates + float2(37.2, 91.7));

    float phase = phaseNoise * (2.0 * PI);

    float frequency = lerp(
        0.65,
        1.50,
        frequencyNoise);

    float t = Time * Speed * frequency + phase;

    // Combine several oscillations to create less predictable
    // brightness changes without obvious repetitive blinking.
    float fluctuation =
    0.50 * sin(t) +
    0.30 * sin(t * 0.63 + phase * 1.7) +
    0.20 * sin(t * 1.73 + phase * 2.3);

    // Normalize the result and apply stronger brightness variation.
    float modulation =
    1.0 + starMask * Intensity * 0.85 * fluctuation;

    modulation = max(modulation, 0.15);

    return float4(original.rgb * modulation, original.a);
}

technique Scintillation
{
    pass P0
    {
        PixelShader = compile ps_2_0 PS_Scintillation();
    }
}
