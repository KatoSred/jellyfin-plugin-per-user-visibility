using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.PerUserVisibility.Configuration;
using Jellyfin.Plugin.PerUserVisibility.Services;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.PerUserVisibility.Api;

/// <summary>
/// Administration endpoints. Every action requires elevation, so normal users cannot reach them.
/// </summary>
[ApiController]
[Route("PerUserVisibility")]
[Authorize(Policy = Policies.RequiresElevation)]
public class ScopesController : ControllerBase
{
    private readonly ScopeService _service;
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopesController"/> class.
    /// </summary>
    /// <param name="service">Scope service.</param>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="userManager">User manager.</param>
    public ScopesController(ScopeService service, ILibraryManager libraryManager, IUserManager userManager)
    {
        _service = service;
        _libraryManager = libraryManager;
        _userManager = userManager;
    }

    /// <summary>
    /// Lists users with their current scope.
    /// </summary>
    /// <returns>User list.</returns>
    [HttpGet("Users")]
    public ActionResult GetUsers()
    {
        var config = Plugin.Instance!.Configuration;
        var result = _userManager.GetUsers().Select(u => new
        {
            Id = u.Id,
            Name = u.Username,
            IsAdministrator = _userManager.GetUserDto(u).Policy?.IsAdministrator ?? false,
            Scope = BuildScopeDto(config.Scopes.FirstOrDefault(s => s.UserId == u.Id))
        });
        return Ok(result);
    }

    /// <summary>
    /// Lists media libraries.
    /// </summary>
    /// <returns>Library list.</returns>
    [HttpGet("Libraries")]
    public ActionResult GetLibraries()
    {
        var result = _libraryManager.GetVirtualFolders()
            .Where(v => !string.IsNullOrEmpty(v.ItemId) && Guid.TryParse(v.ItemId, out _))
            .Select(v => new
            {
                Id = Guid.Parse(v.ItemId),
                Name = v.Name,
                ItemCount = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    ParentId = Guid.Parse(v.ItemId),
                    Recursive = true,
                    IncludeItemTypes = new[] { BaseItemKind.Series, BaseItemKind.Movie },
                    EnableTotalRecordCount = false
                }).Count
            });
        return Ok(result);
    }

    /// <summary>
    /// Searches series and movies inside one library.
    /// </summary>
    /// <param name="libraryId">Library folder id.</param>
    /// <param name="query">Optional search term.</param>
    /// <returns>Matching items.</returns>
    [HttpGet("Search")]
    public ActionResult Search([FromQuery] Guid libraryId, [FromQuery] string? query)
    {
        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            ParentId = libraryId,
            Recursive = true,
            IncludeItemTypes = new[] { BaseItemKind.Series, BaseItemKind.Movie },
            SearchTerm = string.IsNullOrWhiteSpace(query) ? null : query,
            Limit = 300,
            EnableTotalRecordCount = false,
            OrderBy = new[] { (ItemSortBy.SortName, SortOrder.Ascending) }
        });

        return Ok(items.Select(ToItemDto));
    }

    /// <summary>
    /// Resolves names for a set of ids (used to show current grants).
    /// </summary>
    /// <param name="ids">Comma separated ids.</param>
    /// <returns>Items.</returns>
    [HttpGet("Items")]
    public ActionResult GetItems([FromQuery] string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
        {
            return Ok(Array.Empty<object>());
        }

        var result = new List<object>();
        foreach (var raw in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(raw, out var id))
            {
                continue;
            }

            var item = _libraryManager.GetItemById(id);
            if (item is not null)
            {
                result.Add(ToItemDto(item));
            }
        }

        return Ok(result);
    }

    /// <summary>
    /// Saves a user's scope and applies it immediately.
    /// </summary>
    /// <param name="request">The scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpPost("Save")]
    public async Task<ActionResult> Save([FromBody] SaveScopeRequest request, CancellationToken cancellationToken)
    {
        var user = _userManager.GetUserById(request.UserId);
        if (user is null)
        {
            return NotFound("Unknown user.");
        }

        if (_userManager.GetUserDto(user).Policy?.IsAdministrator == true)
        {
            return BadRequest("Administrators cannot be restricted.");
        }

        var scope = _service.GetOrCreateScope(request.UserId);
        scope.Enabled = request.Enabled;
        scope.WholeLibraryIds = request.WholeLibraryIds?.Distinct().ToList() ?? new List<Guid>();
        scope.ItemIds = request.ItemIds?.Distinct().ToList() ?? new List<Guid>();

        await _service.ApplyScopeAsync(scope, cancellationToken).ConfigureAwait(false);
        Plugin.Instance!.SaveConfiguration();

        return Ok(new { ok = true, granted = scope.AppliedItemIds.Count });
    }

    /// <summary>
    /// Removes a user's scope entirely and restores their original policy.
    /// </summary>
    /// <param name="userId">User id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpPost("Remove")]
    public async Task<ActionResult> Remove([FromQuery] Guid userId, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance!.Configuration;
        var scope = config.Scopes.FirstOrDefault(s => s.UserId == userId);
        if (scope is not null)
        {
            scope.Enabled = false;
            scope.WholeLibraryIds.Clear();
            scope.ItemIds.Clear();
            await _service.ApplyScopeAsync(scope, cancellationToken).ConfigureAwait(false);
            config.Scopes.Remove(scope);
            Plugin.Instance.SaveConfiguration();
        }

        return Ok(new { ok = true });
    }

    /// <summary>
    /// Re-applies every scope.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpPost("ApplyAll")]
    public async Task<ActionResult> ApplyAll(CancellationToken cancellationToken)
    {
        var changed = await _service.ApplyAllAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new { ok = true, changed });
    }

    private object? BuildScopeDto(UserScope? scope)
    {
        if (scope is null)
        {
            return null;
        }

        return new
        {
            Enabled = scope.Enabled,
            WholeLibraryIds = scope.WholeLibraryIds,
            ItemIds = scope.ItemIds,
            AppliedCount = scope.AppliedItemIds.Count,
            Items = scope.ItemIds.Select(id =>
            {
                var item = _libraryManager.GetItemById(id);
                var library = item is null
                    ? null
                    : _libraryManager.GetCollectionFolders(item).FirstOrDefault();
                return new
                {
                    Id = id,
                    Name = item?.Name,
                    Year = item?.ProductionYear,
                    Type = item?.GetType().Name,
                    LibraryId = library?.Id
                };
            }).ToList()
        };
    }

    private static object ToItemDto(BaseItem item) => new
    {
        Id = item.Id,
        Name = item.Name,
        Year = item.ProductionYear,
        Type = item.GetType().Name,
        SeriesName = item is Episode episode ? episode.SeriesName : null
    };
}

/// <summary>
/// Request body for saving a scope.
/// </summary>
public class SaveScopeRequest
{
    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the scope is active.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets fully visible library ids.
    /// </summary>
    public List<Guid>? WholeLibraryIds { get; set; }

    /// <summary>
    /// Gets or sets granted series or movie ids.
    /// </summary>
    public List<Guid>? ItemIds { get; set; }
}
