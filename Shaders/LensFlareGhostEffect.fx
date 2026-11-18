sampler2D InputSampler : register(s0);

float2 SourcePosition : register(c0);
float AspectRatio : register(c1);
float Intensity : register(c2);
float GlowSize : register(c3);
float StreakLength : register(c4);
float GhostIntensity : register(c5);
float StarburstIntensity : register(c6);
float SourceVisibility : register(c7);

float Disc(float2 p, float radius)
{
    float d2 = dot(p, p);
    float r2 = radius * radius;

    float value = saturate(1.0 - d2 / r2);
    return value * value;
}

float Ring(float2 p, float radius, float thickness)
{
    float d2 = dot(p, p);
    float r2 = radius * radius;

    float delta = abs(d2 / r2 - 1.0);

    return saturate(1.0 - delta / thickness);
}

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float4 scene = tex2D(InputSampler, uv);

    float2 aspectScale = float2(max(AspectRatio, 0.1), 1.0);
    float2 p = (uv - SourcePosition) * aspectScale;

    float radius = max(GlowSize, 0.004);

    float2 axis = (float2(0.5, 0.5) - SourcePosition)
                  * aspectScale;

    float ghost1 = Disc(p - axis * 0.42, radius * 0.55);
    float ghost2 = Ring(p - axis * 0.80, radius * 0.65, 0.12);
    float ghost3 = Disc(p - axis * 1.25, radius * 0.32);
    float ghost4 = Ring(p - axis * 1.65, radius * 0.50, 0.10);

    float3 flare =
        ghost1 * float3(0.45, 0.75, 1.0) +
        ghost2 * float3(0.75, 0.48, 1.0) +
        ghost3 * float3(1.0, 0.65, 0.35) +
        ghost4 * float3(0.45, 0.85, 0.95);

    flare *= GhostIntensity * Intensity * saturate(SourceVisibility);

    return float4(saturate(scene.rgb + flare), scene.a);
}