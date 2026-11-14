sampler2D InputSampler : register(s0);

float Threshold : register(c0);
float Intensity : register(c1);
float TexelWidth : register(c2);
float TexelHeight : register(c3);
float BlurRadius : register(c4);
float HaloScale : register(c5);

#define SAMPLE_COUNT 192
#define SAMPLES_PER_ITERATION 8
#define TOTAL_SAMPLE_COUNT (SAMPLE_COUNT * SAMPLES_PER_ITERATION)

static const float GOLDEN_ANGLE = 2.39996323;
static const float PI = 3.14159265;

// Deterministic pseudo-random value in [0, 1].
float Hash(float value)
{
    return frac(sin(value * 127.1) * 43758.5453);
}

// Extract highlights with a soft transition.
float3 SampleBright(float2 uv)
{
    float3 color = tex2D(InputSampler, uv).rgb;

    float luminance = dot(
        color,
        float3(0.299, 0.587, 0.114));

    float knee = 0.20;

    float soft = saturate(
        (luminance - Threshold + knee) /
        (2.0 * knee));

    soft *= soft;

    float contribution = max(
        luminance - Threshold,
        soft * knee);

    contribution /= max(luminance, 0.0001);

    return color * contribution;
}

// Gaussian weighting based on radial distance.
float GaussianWeight(float radius, float sigma)
{
    return exp(
        -0.5 * radius * radius /
        (sigma * sigma));
}

// Fibonacci spiral Gaussian blur with controlled jitter.
float3 BlurHighlights(
    float2 uv,
    float2 radius,
    float sigma)
{
    float3 glow = 0.0;
    float totalWeight = 0.0;

    // Jitter is approximately 15% of the average
    // neighboring sample spacing in the unit disk.
    float jitterAmount =
        0.15 * sqrt(PI / TOTAL_SAMPLE_COUNT);

    [loop]
    for (int i = 0; i < SAMPLE_COUNT; ++i)
    {
        [unroll]
        for (int j = 0; j < SAMPLES_PER_ITERATION; ++j)
        {
            float sampleIndex =
                (float) (i * SAMPLES_PER_ITERATION + j);

            float normalizedIndex =
                (sampleIndex + 0.5) / TOTAL_SAMPLE_COUNT;

            // Original Fibonacci sample position.
            float radialDistance = sqrt(normalizedIndex);

            float angle = sampleIndex * GOLDEN_ANGLE;

            float2 direction = float2(
                cos(angle),
                sin(angle));

            float2 sampleOffset =
                direction * radialDistance;

            // Generate fixed, independent jitter for this sample.
            float randomX = Hash(sampleIndex + 1.0);
            float randomY = Hash(sampleIndex + 7919.0);

            float2 jitter = (float2(randomX, randomY) - 0.5)
                * 2.0 * jitterAmount;

            // Perturb the sample position.
            float2 jitteredOffset = sampleOffset + jitter;

            // Keep samples inside the circular sampling area.
            float jitteredDistance = length(jitteredOffset);

            if (jitteredDistance > 1.0)
            {
                jitteredOffset /= jitteredDistance;
            }

            // Preserve the original Gaussian weight.
            float weight = GaussianWeight(
                radialDistance,
                sigma);

            glow += SampleBright(
                uv + jitteredOffset * radius) * weight;

            totalWeight += weight;
        }
    }

    return glow / max(totalWeight, 0.0001);
}

float4 PS_Bloom(float2 uv : TEXCOORD0) : COLOR0
{
    float4 original = tex2D(InputSampler, uv);

    float2 baseRadius =
        float2(TexelWidth, TexelHeight) * BlurRadius;

    // Tight glow around highlights.
    float3 tightGlow = BlurHighlights(
        uv,
        baseRadius,
        0.55);

    // Broad atmospheric halo.
    float2 broadRadius = baseRadius * HaloScale;

    float3 broadGlow = BlurHighlights(
        uv,
        broadRadius,
        0.95);

    // Combine original image and both glow layers.
    float3 result =
        original.rgb +
        tightGlow * Intensity +
        broadGlow * Intensity * 1.5;

    return float4(result, original.a);
}
