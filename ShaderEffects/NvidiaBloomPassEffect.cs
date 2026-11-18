using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace RealmStudioX._3D.ShaderEffects;

public abstract class NvidiaBloomPassEffect : ShaderEffect
{
    protected NvidiaBloomPassEffect(string shaderFile)
    {
        PixelShader = new PixelShader
        {
            UriSource = new Uri(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "Shaders",
                    shaderFile),
                UriKind.Absolute)
        };

        UpdateShaderValue(InputProperty);
    }

    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty(
            nameof(Input),
            typeof(NvidiaBloomPassEffect),
            0);

    public Brush Input
    {
        get => (Brush)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }
}

public sealed class NvidiaBloomDownsampleEffect
    : NvidiaBloomPassEffect
{
    public NvidiaBloomDownsampleEffect()
        : base("NvidiaBloomDownsampleEffect.ps")
    {
        UpdateShaderValue(HighlightThresholdProperty);
        UpdateShaderValue(TexelWidthProperty);
        UpdateShaderValue(TexelHeightProperty);
    }

    public static readonly DependencyProperty HighlightThresholdProperty =
        DependencyProperty.Register(
            nameof(HighlightThreshold),
            typeof(float),
            typeof(NvidiaBloomDownsampleEffect),
            new UIPropertyMetadata(
                0.70f,
                PixelShaderConstantCallback(0)));

    public float HighlightThreshold
    {
        get => (float)GetValue(HighlightThresholdProperty);
        set => SetValue(HighlightThresholdProperty, value);
    }

    public static readonly DependencyProperty TexelWidthProperty =
        DependencyProperty.Register(
            nameof(TexelWidth),
            typeof(float),
            typeof(NvidiaBloomDownsampleEffect),
            new UIPropertyMetadata(
                0.001f,
                PixelShaderConstantCallback(1)));

    public float TexelWidth
    {
        get => (float)GetValue(TexelWidthProperty);
        set => SetValue(TexelWidthProperty, value);
    }

    public static readonly DependencyProperty TexelHeightProperty =
        DependencyProperty.Register(
            nameof(TexelHeight),
            typeof(float),
            typeof(NvidiaBloomDownsampleEffect),
            new UIPropertyMetadata(
                0.001f,
                PixelShaderConstantCallback(2)));

    public float TexelHeight
    {
        get => (float)GetValue(TexelHeightProperty);
        set => SetValue(TexelHeightProperty, value);
    }
}

public sealed class NvidiaBloomHorizontalBlurEffect
    : NvidiaBloomPassEffect
{
    public NvidiaBloomHorizontalBlurEffect()
        : base("NvidiaBloomHorizontalBlurEffect.ps")
    {
        UpdateShaderValue(BlurWidthProperty);
        UpdateShaderValue(TexelSizeProperty);
    }

    public static readonly DependencyProperty BlurWidthProperty =
        DependencyProperty.Register(
            nameof(BlurWidth),
            typeof(float),
            typeof(NvidiaBloomHorizontalBlurEffect),
            new UIPropertyMetadata(
                2.0f,
                PixelShaderConstantCallback(0)));

    public float BlurWidth
    {
        get => (float)GetValue(BlurWidthProperty);
        set => SetValue(BlurWidthProperty, value);
    }

    public static readonly DependencyProperty TexelSizeProperty =
        DependencyProperty.Register(
            nameof(TexelSize),
            typeof(float),
            typeof(NvidiaBloomHorizontalBlurEffect),
            new UIPropertyMetadata(
                0.001f,
                PixelShaderConstantCallback(1)));

    public float TexelSize
    {
        get => (float)GetValue(TexelSizeProperty);
        set => SetValue(TexelSizeProperty, value);
    }
}

public sealed class NvidiaBloomVerticalBlurEffect
    : NvidiaBloomPassEffect
{
    public NvidiaBloomVerticalBlurEffect()
        : base("NvidiaBloomVerticalBlurEffect.ps")
    {
        UpdateShaderValue(BlurWidthProperty);
        UpdateShaderValue(TexelSizeProperty);
    }

    public static readonly DependencyProperty BlurWidthProperty =
        DependencyProperty.Register(
            nameof(BlurWidth),
            typeof(float),
            typeof(NvidiaBloomVerticalBlurEffect),
            new UIPropertyMetadata(
                2.0f,
                PixelShaderConstantCallback(0)));

    public float BlurWidth
    {
        get => (float)GetValue(BlurWidthProperty);
        set => SetValue(BlurWidthProperty, value);
    }

    public static readonly DependencyProperty TexelSizeProperty =
        DependencyProperty.Register(
            nameof(TexelSize),
            typeof(float),
            typeof(NvidiaBloomVerticalBlurEffect),
            new UIPropertyMetadata(
                0.001f,
                PixelShaderConstantCallback(1)));

    public float TexelSize
    {
        get => (float)GetValue(TexelSizeProperty);
        set => SetValue(TexelSizeProperty, value);
    }
}

public sealed class NvidiaBloomCompositeEffect
    : NvidiaBloomPassEffect
{
    public NvidiaBloomCompositeEffect()
        : base("NvidiaBloomCompositeEffect.ps")
    {
        UpdateShaderValue(SceneProperty);
        UpdateShaderValue(SceneIntensityProperty);
        UpdateShaderValue(GlowIntensityProperty);
        UpdateShaderValue(HighlightIntensityProperty);
    }

    // Sampler 1: original scene, independent of the effect chain.
    public static readonly DependencyProperty SceneProperty =
        RegisterPixelShaderSamplerProperty(
            nameof(Scene),
            typeof(NvidiaBloomCompositeEffect),
            1);

    public Brush Scene
    {
        get => (Brush)GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public static readonly DependencyProperty SceneIntensityProperty =
        DependencyProperty.Register(
            nameof(SceneIntensity),
            typeof(float),
            typeof(NvidiaBloomCompositeEffect),
            new UIPropertyMetadata(
                1.0f,
                PixelShaderConstantCallback(0)));

    public float SceneIntensity
    {
        get => (float)GetValue(SceneIntensityProperty);
        set => SetValue(SceneIntensityProperty, value);
    }

    public static readonly DependencyProperty GlowIntensityProperty =
        DependencyProperty.Register(
            nameof(GlowIntensity),
            typeof(float),
            typeof(NvidiaBloomCompositeEffect),
            new UIPropertyMetadata(
                1.0f,
                PixelShaderConstantCallback(1)));

    public float GlowIntensity
    {
        get => (float)GetValue(GlowIntensityProperty);
        set => SetValue(GlowIntensityProperty, value);
    }

    public static readonly DependencyProperty HighlightIntensityProperty =
        DependencyProperty.Register(
            nameof(HighlightIntensity),
            typeof(float),
            typeof(NvidiaBloomCompositeEffect),
            new UIPropertyMetadata(
                0.5f,
                PixelShaderConstantCallback(2)));

    public float HighlightIntensity
    {
        get => (float)GetValue(HighlightIntensityProperty);
        set => SetValue(HighlightIntensityProperty, value);
    }
}