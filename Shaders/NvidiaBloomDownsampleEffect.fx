sampler2D InputSampler : register(s0);

float HighlightThreshold : register(c0);
float TexelWidth : register(c1);
float TexelHeight : register(c2);

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float2 halfTexel = float2(
        TexelWidth * 0.5,
        TexelHeight * 0.5);

    float4 color = tex2D(
        InputSampler,
        uv + float2(-halfTexel.x, -halfTexel.y));

    color += tex2D(
        InputSampler,
        uv + float2(halfTexel.x, -halfTexel.y));

    color += tex2D(
        InputSampler,
        uv + float2(halfTexel.x, halfTexel.y));

    color += tex2D(
        InputSampler,
        uv + float2(-halfTexel.x, halfTexel.y));

    color *= 0.25;

    // NVIDIA's luminance coefficients.
    float luminance = dot(
        color.rgb,
        float3(0.3, 0.59, 0.11));

    // Equivalent to smoothstep(Threshold, 1.0, luminance).
    float range = max(1.0 - HighlightThreshold, 0.0001);

    float t = saturate(
        (luminance - HighlightThreshold) / range);

    float highlight = t * t * (3.0 - 2.0 * t);

    // Preserve RGB and carry the highlight mask in alpha.
    return float4(color.rgb, highlight);
}