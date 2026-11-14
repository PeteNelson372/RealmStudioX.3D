sampler2D InputSampler : register(s0);

float2 SourcePosition : register(c0);
float AspectRatio : register(c1);
float Intensity : register(c2);
float GlowSize : register(c3);
float StreakLength : register(c4);
float GhostIntensity : register(c5);
float StarburstIntensity : register(c6);
float SourceVisibility : register(c7);


// ------------------------------------------------------------
// Soft radial glow
// ------------------------------------------------------------

float SoftDisc(float2 position, float radius)
{
    float r = max(radius, 0.0001);

    return exp(-dot(position, position) / (r * r));
}


// ------------------------------------------------------------
// Soft optical ring
// ------------------------------------------------------------

float SoftRing(
    float2 position,
    float ringRadius,
    float thickness)
{
    float distance = length(position);
    float delta = (distance - ringRadius) / max(thickness, 0.0001);

    return exp(-delta * delta * 2.0);
}


// ------------------------------------------------------------
// Main pixel shader
// ------------------------------------------------------------

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float4 scene = tex2D(InputSampler, uv);

    float visibility = saturate(SourceVisibility);

    if (visibility <= 0.0001)
        return scene;

    float2 aspectScale = float2(max(AspectRatio, 0.1), 1.0);

    // Aspect-corrected distance from the light source.
    float2 p = (uv - SourcePosition) * aspectScale;

    float radius = max(GlowSize, 0.004);

    float distanceSquared = dot(p, p);


    // --------------------------------------------------------
    // Central glow and surrounding halo
    // --------------------------------------------------------

    float core = SoftDisc(p, radius * 0.40);

    float halo = SoftDisc(p, radius * 2.2);

    float haloRing = SoftRing(
        p,
        radius * 2.6,
        radius * 0.22);


    // --------------------------------------------------------
    // Horizontal anamorphic streak
    // --------------------------------------------------------

    float narrowStreak =
        exp(-pow(p.y / (radius * 0.055), 2.0)) *
        exp(-abs(p.x) / max(StreakLength, 0.01));

    float broadStreak =
        exp(-pow(p.y / (radius * 0.16), 2.0)) *
        exp(-abs(p.x) / max(StreakLength * 0.72, 0.01));


    // --------------------------------------------------------
    // Optical ghosts
    //
    // Ghosts are positioned along the line connecting the
    // light source and the center of the screen.
    // --------------------------------------------------------

    float2 centerAxis =
        (float2(0.5, 0.5) - SourcePosition) * aspectScale;

    float ghost1 = SoftDisc(
        p - centerAxis * 0.42,
        radius * 0.55);

    float ghost2 = SoftRing(
        p - centerAxis * 0.80,
        radius * 0.65,
        radius * 0.12);

    float ghost3 = SoftDisc(
        p - centerAxis * 1.25,
        radius * 0.32);

    float ghost4 = SoftRing(
        p - centerAxis * 1.65,
        radius * 0.50,
        radius * 0.10);


    // --------------------------------------------------------
    // Starburst rays
    // --------------------------------------------------------

    float radialFade =
        exp(-length(p) / (radius * 6.0));

    float rayHorizontal =
        exp(-pow(p.y / (radius * 0.035), 2.0)) *
        exp(-abs(p.x) / (radius * 7.0));

    float rayVertical =
        exp(-pow(p.x / (radius * 0.035), 2.0)) *
        exp(-abs(p.y) / (radius * 7.0));

    float rayDiagonalA =
        exp(-pow((p.y - p.x) / (radius * 0.06), 2.0)) *
        radialFade;

    float rayDiagonalB =
        exp(-pow((p.y + p.x) / (radius * 0.06), 2.0)) *
        radialFade;

    float starburst =
        (rayHorizontal + rayVertical +
         0.45 * (rayDiagonalA + rayDiagonalB)) *
        radialFade;


    // --------------------------------------------------------
    // Assemble flare
    // --------------------------------------------------------

    float3 flare = float3(0.0, 0.0, 0.0);

    // Warm central light.
    flare += float3(1.0, 0.88, 0.70) *
             (core * 1.1 + halo * 0.42 + haloRing * 0.16);

    // Anamorphic streak.
    flare += float3(1.0, 0.95, 0.84) *
             (narrowStreak * 0.52 + broadStreak * 0.18);

    // Colored optical reflections.
    flare += GhostIntensity *
             (
                 ghost1 * float3(0.45, 0.75, 1.0) +
                 ghost2 * float3(0.75, 0.48, 1.0) +
                 ghost3 * float3(1.0, 0.65, 0.35) +
                 ghost4 * float3(0.45, 0.85, 0.95)
             );

    // Starburst.
    flare += StarburstIntensity *
             starburst *
             float3(1.0, 0.91, 0.76);

    flare *= Intensity * visibility;

    return float4(
        saturate(scene.rgb + flare),
        scene.a);
}
