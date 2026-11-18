using System.IO;
using System.Windows;
using System.Windows.Media.Effects;
using Brush = System.Windows.Media.Brush;
using Point = System.Windows.Point;

namespace RealmStudioX._3D.ShaderEffects;

public abstract class LensFlarePassEffect : ShaderEffect
{
    protected LensFlarePassEffect(string shaderFile)
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
        UpdateShaderValue(SourcePositionProperty);
        UpdateShaderValue(AspectRatioProperty);
        UpdateShaderValue(IntensityProperty);
        UpdateShaderValue(GlowSizeProperty);
        UpdateShaderValue(StreakLengthProperty);
        UpdateShaderValue(GhostIntensityProperty);
        UpdateShaderValue(StarburstIntensityProperty);
        UpdateShaderValue(SourceVisibilityProperty);
    }

    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty(
            nameof(Input),
            typeof(LensFlarePassEffect),
            0);

    public Brush Input
    {
        get => (Brush)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    public static readonly DependencyProperty SourcePositionProperty =
        DependencyProperty.Register(
            nameof(SourcePosition),
            typeof(Point),
            typeof(LensFlarePassEffect),
            new UIPropertyMetadata(
                new Point(0.5, 0.5),
                PixelShaderConstantCallback(0)));

    public Point SourcePosition
    {
        get => (Point)GetValue(SourcePositionProperty);
        set => SetValue(SourcePositionProperty, value);
    }

    public static readonly DependencyProperty AspectRatioProperty =
        DependencyProperty.Register(
            nameof(AspectRatio),
            typeof(float),
            typeof(LensFlarePassEffect),
            new UIPropertyMetadata(
                1.0f,
                PixelShaderConstantCallback(1)));

    public float AspectRatio
    {
        get => (float)GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    public static readonly DependencyProperty IntensityProperty =
        DependencyProperty.Register(
            nameof(Intensity),
            typeof(float),
            typeof(LensFlarePassEffect),
            new UIPropertyMetadata(
                0.8f,
                PixelShaderConstantCallback(2)));

    public float Intensity
    {
        get => (float)GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    public static readonly DependencyProperty GlowSizeProperty =
        DependencyProperty.Register(
            nameof(GlowSize),
            typeof(float),
            typeof(LensFlarePassEffect),
            new UIPropertyMetadata(
                0.035f,
                PixelShaderConstantCallback(3)));

    public float GlowSize
    {
        get => (float)GetValue(GlowSizeProperty);
        set => SetValue(GlowSizeProperty, value);
    }

    public static readonly DependencyProperty StreakLengthProperty =
        DependencyProperty.Register(
            nameof(StreakLength),
            typeof(float),
            typeof(LensFlarePassEffect),
            new UIPropertyMetadata(
                0.65f,
                PixelShaderConstantCallback(4)));

    public float StreakLength
    {
        get => (float)GetValue(StreakLengthProperty);
        set => SetValue(StreakLengthProperty, value);
    }

    public static readonly DependencyProperty GhostIntensityProperty =
        DependencyProperty.Register(
            nameof(GhostIntensity),
            typeof(float),
            typeof(LensFlarePassEffect),
            new UIPropertyMetadata(
                0.35f,
                PixelShaderConstantCallback(5)));

    public float GhostIntensity
    {
        get => (float)GetValue(GhostIntensityProperty);
        set => SetValue(GhostIntensityProperty, value);
    }

    public static readonly DependencyProperty StarburstIntensityProperty =
        DependencyProperty.Register(
            nameof(StarburstIntensity),
            typeof(float),
            typeof(LensFlarePassEffect),
            new UIPropertyMetadata(
                0.2f,
                PixelShaderConstantCallback(6)));

    public float StarburstIntensity
    {
        get => (float)GetValue(StarburstIntensityProperty);
        set => SetValue(StarburstIntensityProperty, value);
    }

    public static readonly DependencyProperty SourceVisibilityProperty =
        DependencyProperty.Register(
            nameof(SourceVisibility),
            typeof(float),
            typeof(LensFlarePassEffect),
            new UIPropertyMetadata(
                0.0f,
                PixelShaderConstantCallback(7)));

    public float SourceVisibility
    {
        get => (float)GetValue(SourceVisibilityProperty);
        set => SetValue(SourceVisibilityProperty, value);
    }
}

public sealed class LensFlareCoreEffect : LensFlarePassEffect
{
    public LensFlareCoreEffect()
        : base("LensFlareCoreEffect.ps")
    {
    }
}

public sealed class LensFlareStreakEffect : LensFlarePassEffect
{
    public LensFlareStreakEffect()
        : base("LensFlareStreakEffect.ps")
    {
    }
}

public sealed class LensFlareGhostEffect : LensFlarePassEffect
{
    public LensFlareGhostEffect()
        : base("LensFlareGhostEffect.ps")
    {
    }
}

public sealed class LensFlareStarburstEffect : LensFlarePassEffect
{
    public LensFlareStarburstEffect()
        : base("LensFlareStarburstEffect.ps")
    {
    }
}