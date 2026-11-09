using Jellyfin.Plugin.PerUserVisibility.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.PerUserVisibility;

/// <summary>
/// Per-User Visibility: per-user, per-library visibility enforced through Jellyfin's own tag filters.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// The plugin id.
    /// </summary>
    public static readonly Guid PluginId = new("8f4a1c72-3d95-4f60-9b21-7c5ade0f6b13");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Per-User Visibility";

    /// <inheritdoc />
    public override string Description => "Restrict what each user can see inside a library, enforced by the server.";

    /// <inheritdoc />
    public override Guid Id => PluginId;

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        var ns = GetType().Namespace;
        return new[]
        {
            new PluginPageInfo
            {
                Name = "PerUserVisibility",
                DisplayName = "Per-User Visibility",
                EmbeddedResourcePath = ns + ".Web.configPage.html",
                EnableInMainMenu = true,
                MenuIcon = "security"
            }
        };
    }
}
