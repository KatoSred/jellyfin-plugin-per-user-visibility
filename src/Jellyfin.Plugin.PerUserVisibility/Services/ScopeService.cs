using Jellyfin.Plugin.PerUserVisibility.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PerUserVisibility.Services;

/// <summary>
/// Translates per-user scopes into item tags plus the user's AllowedTags policy.
/// Jellyfin enforces AllowedTags server-side; only administrators can write that policy.
/// </summary>
public class ScopeService
{
    /// <summary>
    /// Prefix for every tag this plugin owns.
    /// </summary>
    public const string TagPrefix = "lscope-allow-";

    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<ScopeService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeService"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="userManager">User manager.</param>
    /// <param name="logger">Logger.</param>
    public ScopeService(ILibraryManager libraryManager, IUserManager userManager, ILogger<ScopeService> logger)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Builds the allow tag for a user.
    /// </summary>
    /// <param name="userId">User id.</param>
    /// <returns>The tag.</returns>
    public static string BuildTag(Guid userId)
        => TagPrefix + userId.ToString("N")[..8];

    /// <summary>
    /// Gets a value indicating whether a tag is managed by this plugin.
    /// </summary>
    /// <param name="tag">Tag to test.</param>
    /// <returns>True when the tag belongs to the plugin.</returns>
    public static bool IsPluginTag(string tag)
        => tag.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Applies every configured scope. Only writes the configuration file when something changed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when at least one scope changed.</returns>
    public async Task<bool> ApplyAllAsync(CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return false;
        }

        var changed = false;
        foreach (var scope in config.Scopes.ToList())
        {
            try
            {
                changed |= await ApplyScopeAsync(scope, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply scope for user {UserId}", scope.UserId);
            }
        }

        if (changed)
        {
            Plugin.Instance?.SaveConfiguration();
        }

        return changed;
    }

    /// <summary>
    /// Applies one scope: tags the desired items, un-tags the rest and rewrites AllowedTags.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the item set or the user policy actually changed.</returns>
    public async Task<bool> ApplyScopeAsync(UserScope scope, CancellationToken cancellationToken)
    {
        var user = _userManager.GetUserById(scope.UserId);
        if (user is null)
        {
            return false;
        }

        var dto = _userManager.GetUserDto(user);
        if (dto.Policy?.IsAdministrator == true)
        {
            _logger.LogWarning("Skipping user {UserId}: administrators are never restricted", scope.UserId);
            return false;
        }

        var policy = dto.Policy;
        if (policy is null)
        {
            return false;
        }

        var changed = false;

        if (!scope.BaselineCaptured)
        {
            scope.BaselineAllowedTags = (policy.AllowedTags ?? Array.Empty<string>())
                .Where(t => !IsPluginTag(t))
                .ToList();
            scope.BaselineCaptured = true;
            changed = true;
        }

        var tag = BuildTag(scope.UserId);
        var desired = new HashSet<Guid>();
        if (scope.Enabled)
        {
            foreach (var libraryId in scope.WholeLibraryIds)
            {
                desired.Add(libraryId);
            }

            foreach (var itemId in scope.ItemIds)
            {
                desired.Add(itemId);
            }
        }

        var previous = scope.AppliedItemIds.ToHashSet();
        foreach (var id in previous.Where(id => !desired.Contains(id)))
        {
            changed |= await RemoveTagAsync(id, tag, cancellationToken).ConfigureAwait(false);
        }

        foreach (var id in desired)
        {
            changed |= await AddTagAsync(id, tag, cancellationToken).ConfigureAwait(false);
        }

        if (!previous.SetEquals(desired))
        {
            scope.AppliedItemIds = desired.ToList();
            changed = true;
        }

        var newAllowed = new List<string>(scope.BaselineAllowedTags);
        if (desired.Count > 0)
        {
            newAllowed.Add(tag);
        }

        var current = policy.AllowedTags ?? Array.Empty<string>();
        var policyChanged = current.Length != newAllowed.Count
            || current.Except(newAllowed, StringComparer.OrdinalIgnoreCase).Any();

        if (policyChanged)
        {
            policy.AllowedTags = newAllowed.ToArray();
        }

        // Library access follows a whole-library grant. Granting one opens the library entry;
        // revoking it closes the entry again unless single items inside are still granted.
        if (!policy.EnableAllFolders)
        {
            var desiredLibraries = scope.Enabled
                ? scope.WholeLibraryIds.ToHashSet()
                : new HashSet<Guid>();
            var folders = (policy.EnabledFolders ?? Array.Empty<Guid>()).ToHashSet();

            foreach (var libraryId in desiredLibraries)
            {
                folders.Add(libraryId);
            }

            foreach (var id in previous)
            {
                if (desired.Contains(id) || desiredLibraries.Contains(id) || !IsLibraryRoot(id))
                {
                    continue;
                }

                if (!desired.Any(itemId => BelongsToLibrary(itemId, id)))
                {
                    folders.Remove(id);
                }
            }

            if (!folders.SetEquals(policy.EnabledFolders ?? Array.Empty<Guid>()))
            {
                policy.EnabledFolders = folders.ToArray();
                policyChanged = true;
            }
        }

        if (policyChanged)
        {
            await _userManager.UpdatePolicyAsync(scope.UserId, policy).ConfigureAwait(false);
            _logger.LogInformation(
                "User {UserId}: allowed tags set to [{Tags}], {Count} granted items, {LibraryCount} open libraries",
                scope.UserId,
                string.Join(", ", newAllowed),
                desired.Count,
                policy.EnabledFolders?.Length ?? 0);
            changed = true;
        }

        return changed;
    }

    private bool IsLibraryRoot(Guid id)
        => _libraryManager.GetItemById(id) is CollectionFolder;

    private bool BelongsToLibrary(Guid itemId, Guid libraryId)
    {
        var item = _libraryManager.GetItemById(itemId);
        return item is not null
            && _libraryManager.GetCollectionFolders(item).Any(folder => folder.Id == libraryId);
    }

    /// <summary>
    /// Adds the tag to an item and locks the Tags field so metadata refreshes keep it.
    /// </summary>
    /// <param name="itemId">Item id.</param>
    /// <param name="tag">Tag to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the item was written.</returns>
    public async Task<bool> AddTagAsync(Guid itemId, string tag, CancellationToken cancellationToken)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is null)
        {
            return false;
        }

        var tags = (item.Tags ?? Array.Empty<string>()).ToList();
        var changed = false;

        if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            tags.Add(tag);
            item.Tags = tags.ToArray();
            changed = true;
        }

        var locked = (item.LockedFields ?? Array.Empty<MetadataField>()).ToList();
        if (!locked.Contains(MetadataField.Tags))
        {
            locked.Add(MetadataField.Tags);
            item.LockedFields = locked.ToArray();
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        await _libraryManager
            .UpdateItemAsync(item, item.GetParent() ?? item, ItemUpdateType.MetadataEdit, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Removes the tag from an item and unlocks the Tags field when no plugin tags remain.
    /// </summary>
    /// <param name="itemId">Item id.</param>
    /// <param name="tag">Tag to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the item was written.</returns>
    public async Task<bool> RemoveTagAsync(Guid itemId, string tag, CancellationToken cancellationToken)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is null)
        {
            return false;
        }

        var tags = (item.Tags ?? Array.Empty<string>()).ToList();
        if (tags.RemoveAll(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)) == 0)
        {
            return false;
        }

        item.Tags = tags.ToArray();

        if (!tags.Any(IsPluginTag))
        {
            var locked = item.LockedFields ?? Array.Empty<MetadataField>();
            if (locked.Contains(MetadataField.Tags))
            {
                item.LockedFields = locked.Where(f => f != MetadataField.Tags).ToArray();
            }
        }

        await _libraryManager
            .UpdateItemAsync(item, item.GetParent() ?? item, ItemUpdateType.MetadataEdit, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Finds or creates the scope for a user.
    /// </summary>
    /// <param name="userId">User id.</param>
    /// <returns>The scope.</returns>
    public UserScope GetOrCreateScope(Guid userId)
    {
        var config = Plugin.Instance!.Configuration;
        var scope = config.Scopes.FirstOrDefault(s => s.UserId == userId);
        if (scope is null)
        {
            scope = new UserScope { UserId = userId };
            config.Scopes.Add(scope);
        }

        return scope;
    }
}
