sampler2D BlurredSampler : register(s0);
sampler2D SceneSampler : register(s1);

float SceneIntensity : register(c0);
float GlowIntensity : register(c1);
float HighlightIntensity : register(c2);

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float4 scene = tex2D(SceneSampler, uv);
    float4 blur = tex2D(BlurredSampler, uv);

    float3 result =
        scene.rgb * SceneIntensity +
        blur.rgb * GlowIntensity +
        blur.a * HighlightIntensity;

    return float4(result, scene.a);
}