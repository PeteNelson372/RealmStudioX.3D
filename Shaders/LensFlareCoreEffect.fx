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
    float d2 = dot(p, p);
    float r2 = radius * radius;

    // Bright central disc.
    float core = saturate(1.0 - d2 / (r2 * 0.16));
    core *= core;

    // Broad, soft halo.
    float halo = saturate(1.0 - d2 / (r2 * 12.0));
    halo *= halo;

    // Soft ring, approximated using squared radial distance.
    float ringDelta = abs(d2 / (r2 * 6.76) - 1.0);
    float ring = saturate(1.0 - ringDelta * 3.0);
    ring *= ring;

    float3 flare =
        float3(1.0, 0.88, 0.70) *
        (core * 1.1 + halo * 0.42 + ring * 0.16);

    flare *= Intensity * saturate(SourceVisibility);

    return float4(saturate(scene.rgb + flare), scene.a);
}