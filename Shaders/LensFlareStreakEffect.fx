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
    float length = max(StreakLength, 0.01);

    float narrow = saturate(
        1.0 - abs(p.y) / (radius * 0.055));

    float broad = saturate(
        1.0 - abs(p.y) / (radius * 0.16));

    float horizontal = saturate(
        1.0 - abs(p.x) / length);

    float broadHorizontal = saturate(
        1.0 - abs(p.x) / (length * 0.72));

    float streak =
        narrow * horizontal * 0.52 +
        broad * broadHorizontal * 0.18;

    float3 flare = float3(1.0, 0.95, 0.84) * streak;

    flare *= Intensity * saturate(SourceVisibility);

    return float4(saturate(scene.rgb + flare), scene.a);
}