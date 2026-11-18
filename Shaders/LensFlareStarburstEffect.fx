sampler2D InputSampler : register(s0);

float2 SourcePosition : register(c0);
float AspectRatio : register(c1);
float Intensity : register(c2);
float GlowSize : register(c3);
float StreakLength : register(c4);
float GhostIntensity : register(c5);
float StarburstIntensity : register(c6);
float SourceVisibility : register(c7);

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float4 scene = tex2D(InputSampler, uv);

    float2 p = uv - SourcePosition;
    p.x *= max(AspectRatio, 0.1);

    float radius = max(GlowSize, 0.004);

    float radial = saturate(
        1.0 - (abs(p.x) + abs(p.y)) / (radius * 6.0));

    float horizontal = saturate(
        1.0 - abs(p.y) / (radius * 0.035));

    horizontal *= saturate(
        1.0 - abs(p.x) / (radius * 7.0));

    float vertical = saturate(
        1.0 - abs(p.x) / (radius * 0.035));

    vertical *= saturate(
        1.0 - abs(p.y) / (radius * 7.0));

    float diagonalA = saturate(
        1.0 - abs(p.y - p.x) / (radius * 0.06));

    float diagonalB = saturate(
        1.0 - abs(p.y + p.x) / (radius * 0.06));

    float rays =
        (horizontal + vertical +
         0.45 * (diagonalA + diagonalB)) * radial;

    float3 flare = float3(1.0, 0.91, 0.76) * rays;

    flare *= StarburstIntensity * Intensity
             * saturate(SourceVisibility);

    return float4(saturate(scene.rgb + flare), scene.a);
}