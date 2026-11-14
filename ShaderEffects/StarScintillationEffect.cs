
using System.IO;
using System.Windows;
using System.Windows.Media.Effects;
using Brush = System.Windows.Media.Brush;

namespace RealmStudioX._3D.ShaderEffects;

public sealed class StarScintillationEffect : ShaderEffect
{
    private static readonly PixelShader _pixelShader = new()
    {
        UriSource = new Uri(
            Path.Combine(
                AppContext.BaseDirectory,
                "Shaders",
                "StarScintillationEffect.ps"),
            UriKind.Absolute)
    };

    public StarScintillationEffect()
    {
        PixelShader = _pixelShader;

        UpdateShaderValue(InputProperty);
        UpdateShaderValue(ThresholdProperty);
        UpdateShaderValue(ContrastThresholdProperty);
        UpdateShaderValue(IntensityProperty);
        UpdateShaderValue(SpeedProperty);
        UpdateShaderValue(TimeProperty);
        UpdateShaderValue(TexelWidthProperty);
        UpdateShaderValue(TexelHeightProperty);
        UpdateShaderValue(MaximumStarSizeProperty);
    }

    public static readonly DependencyProperty InputProperty =
        ShaderEffect.RegisterPixelShaderSamplerProperty(
            nameof(Input),
            typeof(StarScintillationEffect),
            0);

    public Brush Input
    {
        get => (Brush)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    public static readonly DependencyProperty ThresholdProperty =
        DependencyProperty.Register(
            nameof(Threshold),
            typeof(float),
            typeof(StarScintillationEffect),
            new UIPropertyMetadata(
                0.50f,
                PixelShaderConstantCallback(0)));

    public float Threshold
    {
        get => (float)GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    public static readonly DependencyProperty ContrastThresholdProperty =
        DependencyProperty.Register(
            nameof(ContrastThreshold),
            typeof(float),
            typeof(StarScintillationEffect),
            new UIPropertyMetadata(
                0.05f,
                PixelShaderConstantCallback(1)));

    public float ContrastThreshold
    {
        get => (float)GetValue(ContrastThresholdProperty);
        set => SetValue(ContrastThresholdProperty, value);
    }

    public static readonly DependencyProperty IntensityProperty =
        DependencyProperty.Register(
            nameof(Intensity),
            typeof(float),
            typeof(StarScintillationEffect),
            new UIPropertyMetadata(
                0.65f,
                PixelShaderConstantCallback(2)));

    public float Intensity
    {
        get => (float)GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    public static readonly DependencyProperty SpeedProperty =
        DependencyProperty.Register(
            nameof(Speed),
            typeof(float),
            typeof(StarScintillationEffect),
            new UIPropertyMetadata(
                1.0f,
                PixelShaderConstantCallback(3)));

    public float Speed
    {
        get => (float)GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public static readonly DependencyProperty TimeProperty =
        DependencyProperty.Register(
            nameof(Time),
            typeof(float),
            typeof(StarScintillationEffect),
            new UIPropertyMetadata(
                0.0f,
                PixelShaderConstantCallback(4)));

    public float Time
    {
        get => (float)GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    public static readonly DependencyProperty TexelWidthProperty =
        DependencyProperty.Register(
            nameof(TexelWidth),
            typeof(float),
            typeof(StarScintillationEffect),
            new UIPropertyMetadata(
                0.001f,
                PixelShaderConstantCallback(5)));

    public float TexelWidth
    {
        get => (float)GetValue(TexelWidthProperty);
        set => SetValue(TexelWidthProperty, value);
    }

    public static readonly DependencyProperty TexelHeightProperty =
        DependencyProperty.Register(
            nameof(TexelHeight),
            typeof(float),
            typeof(StarScintillationEffect),
            new UIPropertyMetadata(
                0.001f,
                PixelShaderConstantCallback(6)));

    public float TexelHeight
    {
        get => (float)GetValue(TexelHeightProperty);
        set => SetValue(TexelHeightProperty, value);
    }

    public static readonly DependencyProperty MaximumStarSizeProperty =
        DependencyProperty.Register(
            nameof(MaximumStarSize),
            typeof(float),
            typeof(StarScintillationEffect),
            new UIPropertyMetadata(
                3.0f,
                PixelShaderConstantCallback(7)));

    public float MaximumStarSize
    {
        get => (float)GetValue(MaximumStarSizeProperty);
        set => SetValue(MaximumStarSizeProperty, value);
    }
}
