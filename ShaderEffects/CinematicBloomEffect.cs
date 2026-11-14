using System.IO;
using System.Windows;
using System.Windows.Media.Effects;
using Brush = System.Windows.Media.Brush;

namespace RealmStudioX._3D.ShaderEffects;

public sealed class CinematicBloomEffect : ShaderEffect
{
    private static readonly PixelShader _pixelShader = new()
    {
        UriSource = new Uri(
            Path.Combine(
                AppContext.BaseDirectory,
                "Shaders",
                "CinematicBloomEffect.ps"),
            UriKind.Absolute)
    };

    public CinematicBloomEffect()
    {
        PixelShader = _pixelShader;

        UpdateShaderValue(InputProperty);
        UpdateShaderValue(ThresholdProperty);
        UpdateShaderValue(IntensityProperty);
        UpdateShaderValue(TexelWidthProperty);
        UpdateShaderValue(TexelHeightProperty);
        UpdateShaderValue(BlurRadiusProperty);
        UpdateShaderValue(HaloScaleProperty);

        Threshold = 0.80f;
        Intensity = 2.0f;
        BlurRadius = 2.0f;
        HaloScale = 3.0f;
    }

    public static readonly DependencyProperty InputProperty =
        ShaderEffect.RegisterPixelShaderSamplerProperty(
            nameof(Input),
            typeof(CinematicBloomEffect),
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
            typeof(CinematicBloomEffect),
            new UIPropertyMetadata(
                0.80f,
                PixelShaderConstantCallback(0)));

    public float Threshold
    {
        get => (float)GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    public static readonly DependencyProperty IntensityProperty =
        DependencyProperty.Register(
            nameof(Intensity),
            typeof(float),
            typeof(CinematicBloomEffect),
            new UIPropertyMetadata(
                2.0f,
                PixelShaderConstantCallback(1)));

    public float Intensity
    {
        get => (float)GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    public static readonly DependencyProperty TexelWidthProperty =
        DependencyProperty.Register(
            nameof(TexelWidth),
            typeof(float),
            typeof(CinematicBloomEffect),
            new UIPropertyMetadata(
                0.001f,
                PixelShaderConstantCallback(2)));

    public float TexelWidth
    {
        get => (float)GetValue(TexelWidthProperty);
        set => SetValue(TexelWidthProperty, value);
    }

    public static readonly DependencyProperty TexelHeightProperty =
        DependencyProperty.Register(
            nameof(TexelHeight),
            typeof(float),
            typeof(CinematicBloomEffect),
            new UIPropertyMetadata(
                0.001f,
                PixelShaderConstantCallback(3)));

    public float TexelHeight
    {
        get => (float)GetValue(TexelHeightProperty);
        set => SetValue(TexelHeightProperty, value);
    }

    public static readonly DependencyProperty BlurRadiusProperty =
        DependencyProperty.Register(
            nameof(BlurRadius),
            typeof(float),
            typeof(CinematicBloomEffect),
            new UIPropertyMetadata(
                2.0f,
                PixelShaderConstantCallback(4)));

    public float BlurRadius
    {
        get => (float)GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }

    public static readonly DependencyProperty HaloScaleProperty =
        DependencyProperty.Register(
            nameof(HaloScale),
            typeof(float),
            typeof(CinematicBloomEffect),
            new UIPropertyMetadata(
                3.0f,
                PixelShaderConstantCallback(5)));

    public float HaloScale
    {
        get => (float)GetValue(HaloScaleProperty);
        set => SetValue(HaloScaleProperty, value);
    }
}
