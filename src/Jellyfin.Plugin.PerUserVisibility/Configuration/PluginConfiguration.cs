using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.PerUserVisibility.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the configured per-user scopes.
    /// </summary>
    public List<UserScope> Scopes { get; set; } = new();
}

/// <summary>
/// One restricted user and what they are allowed to see.
/// </summary>
public class UserScope
{
    /// <summary>
    /// Gets or sets the Jellyfin user id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the scope is active.
    /// Disabled scopes leave the user completely untouched.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets library folder ids that are fully visible to this user.
    /// The library root item is tagged, so every item inside inherits it.
    /// </summary>
    public List<Guid> WholeLibraryIds { get; set; } = new();

    /// <summary>
    /// Gets or sets series or movie ids explicitly granted to this user.
    /// </summary>
    public List<Guid> ItemIds { get; set; } = new();

    /// <summary>
    /// Gets or sets the last successfully applied item id set, including library roots.
    /// Used to compute removals without scanning the whole library by tag.
    /// </summary>
    public List<Guid> AppliedItemIds { get; set; } = new();

    /// <summary>
    /// Gets or sets the AllowedTags the user had before the plugin took over.
    /// Restored when the scope is disabled or emptied.
    /// </summary>
    public List<string> BaselineAllowedTags { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the baseline has been captured.
    /// </summary>
    public bool BaselineCaptured { get; set; }
}
