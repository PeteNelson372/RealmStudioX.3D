
sampler2D InputSampler : register(s0);

float Threshold : register(c0);
float ContrastThreshold : register(c1);
float Intensity : register(c2);
float Speed : register(c3);
float Time : register(c4);
float TexelWidth : register(c5);
float TexelHeight : register(c6);
float MaximumStarSize : register(c7);

float Luminance(float3 color)
{
    return dot(color, float3(0.299, 0.587, 0.114));
}

float4 PS_Scintillation(float2 uv : TEXCOORD0) : COLOR0
{
    float4 original = tex2D(InputSampler, uv);

    float centerLum = Luminance(original.rgb);

    float2 texel = float2(TexelWidth, TexelHeight);

    // Four-neighbor brightness comparison.
    float neighborLum = 0.0;

    neighborLum += Luminance(
        tex2D(InputSampler, uv + float2(texel.x, 0.0)).rgb);

    neighborLum += Luminance(
        tex2D(InputSampler, uv - float2(texel.x, 0.0)).rgb);

    neighborLum += Luminance(
        tex2D(InputSampler, uv + float2(0.0, texel.y)).rgb);

    neighborLum += Luminance(
        tex2D(InputSampler, uv - float2(0.0, texel.y)).rgb);

    neighborLum *= 0.25;

    float localContrast = centerLum - neighborLum;

    // Simplified brightness and contrast masks.
    float brightnessMask = saturate(
        (centerLum - Threshold + 0.15) / 0.23);

    float contrastMask = saturate(
        (localContrast - ContrastThreshold) / 0.08);

    float starMask = brightnessMask * contrastMask;

    // Spatially varied animation.
    float2 noiseCoordinates = uv / max(
        texel * 8.0,
        float2(0.000001, 0.000001));

    float phase = dot(
        noiseCoordinates,
        float2(1.7, 2.3));

    float fluctuation = sin(
        Time * Speed + phase);

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
