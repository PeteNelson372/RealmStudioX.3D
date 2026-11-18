sampler2D InputSampler : register(s0);

float BlurWidth : register(c0);
float TexelSize : register(c1);

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float stepSize = BlurWidth * TexelSize * 4.0;

    float4 color = 0.0;

    color += tex2D(
        InputSampler,
        uv + float2(0.0, -3.0 * stepSize)) * 0.05;

    color += tex2D(
        InputSampler,
        uv + float2(0.0, -2.0 * stepSize)) * 0.10;

    color += tex2D(
        InputSampler,
        uv + float2(0.0, -1.0 * stepSize)) * 0.20;

    color += tex2D(
        InputSampler,
        uv) * 0.30;

    color += tex2D(
        InputSampler,
        uv + float2(0.0, 1.0 * stepSize)) * 0.20;

    color += tex2D(
        InputSampler,
        uv + float2(0.0, 2.0 * stepSize)) * 0.10;

    color += tex2D(
        InputSampler,
        uv + float2(0.0, 3.0 * stepSize)) * 0.05;

    return color;
}