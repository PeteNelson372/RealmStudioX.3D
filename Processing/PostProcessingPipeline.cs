using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;

namespace RealmStudioX._3D.Processing;

public sealed class PostProcessingPipeline(ContentControl host)
{
    private readonly ContentControl _host = host
            ?? throw new ArgumentNullException(nameof(host));
    private readonly UIElement _sceneContent = host.Content as UIElement
            ?? throw new ArgumentException(
                "The post-processing host must initially contain a UIElement.",
                nameof(host));
    private readonly List<Border> _layers = [];

    /// <summary>
    /// Applies the supplied effects in the order provided.
    /// The first effect processes the original scene; subsequent
    /// effects process the output of the preceding effect.
    /// </summary>
    public void SetEffects(IEnumerable<Effect?> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);

        Effect[] activeEffects = [.. effects
            .Where(effect => effect is not null)
            .Cast<Effect>()];

        // Detach the current visual tree from the host.
        _host.Content = null;

        // Release the old effect layers and their child relationships.
        foreach (Border layer in _layers)
            layer.Child = null;

        _layers.Clear();

        // Start with the original scene.
        UIElement current = _sceneContent;

        // Each new layer wraps the preceding output.
        foreach (Effect effect in activeEffects)
        {
            var layer = new Border
            {
                Child = current,
                Effect = effect
            };

            _layers.Add(layer);
            current = layer;
        }

        // Display the completed effect chain.
        _host.Content = current;
    }
}
