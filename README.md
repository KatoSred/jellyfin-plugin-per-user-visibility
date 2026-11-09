# Per-User Visibility

English | [简体中文](README.zh-CN.md)

Plugin 1.0.1.0 · tested against Jellyfin 12.4.0 · updated 2026-11-09

Jellyfin grants access per library. This plugin grants it per user, per library: an administrator ticks the titles a user may see, and the decision sits on the server. A hidden title returns `404` even on a direct request.

## Contents

- [Quick start](#quick-start)
- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Usage](#usage)
- [Limitations](#limitations)
- [How this was built](#how-this-was-built)
- [Build from source](#build-from-source)

## Quick start

1. Add this URL under **Dashboard → Plugins → Repositories**:

   ```text
   https://raw.githubusercontent.com/KatoSred/jellyfin-plugin-per-user-visibility/main/manifest.json
   ```

2. Install **Per-User Visibility** from the **Catalog** tab.
3. Restart Jellyfin, open **Plugins → Per-User Visibility**, select a user and tick the titles that user may see.

If you would rather not add a repository, the manual route works too — see [Installation](#installation).

## Features

- Scope visibility per user, per library.
- Whole-library grants: one switch makes a whole library visible and opens its entry; turning it off closes the entry again.
- Filtering happens in the query path: listings, search, search suggestions, latest media, direct item requests and episode lists all return `404` for hidden titles.
- Survives a metadata refresh; the tag field is locked.
- Enforced server-side through the user policy, out of reach of a normal account.
- Administrator accounts are never filtered, and users without a scope keep the access they had.
- No background task. Saves apply immediately; the *re-apply all users* button is the manual fallback.

## Requirements

| Item | Value |
| --- | --- |
| Jellyfin | 12.4.0 or newer (`targetAbi` 12.4.0.0) |
| Runtime | net10.0, shipped with the Jellyfin 12 server image |
| Architecture | any (x64, arm64); no native dependency |
| Account | an administrator account to configure scopes |

It does not run on 10.9 or 11 — the plugin is built against `12.4.0.0`, and the catalog hides versions whose `targetAbi` is above the running server.

## Installation

### Option 1 — Add the plugin repository

1. Open **Dashboard → Plugins → Repositories**.
2. Press **Add**, give it a name such as `Per-User Visibility`, and paste this URL:

   ```text
   https://raw.githubusercontent.com/KatoSred/jellyfin-plugin-per-user-visibility/main/manifest.json
   ```

3. Save, then switch to the **Catalog** tab.
4. Find **Per-User Visibility**, select it and press **Install**.
5. Restart Jellyfin when prompted.

### Option 2 — Manual install

Use this when the server cannot reach GitHub, or when you want to pin an exact build.

1. Download the package: [`dist/PerUserVisibility_1.0.1.0.zip`](dist/PerUserVisibility_1.0.1.0.zip).
2. Create a folder named `PerUserVisibility_1.0.1.0` inside the server's plugin directory, and extract the zip into it. The result must look like this:

   ```text
   <config>/plugins/PerUserVisibility_1.0.1.0/
   ├── Jellyfin.Plugin.PerUserVisibility.dll
   └── meta.json
   ```

   `<config>` is the Jellyfin configuration directory — `/config` in the official Docker image, `/var/lib/jellyfin` for a package install on Linux, `%LOCALAPPDATA%\jellyfin` on Windows.
3. Keep both files at the top level of that folder. Do not nest them one level deeper; the server loads plugins from the folder root.
4. Restart Jellyfin.

### Verify the install

Open **Dashboard → Plugins**. The list should contain **Per-User Visibility**, version `1.0.1.0`, status *Active*. If it is missing, search the server log for `Loaded assembly Jellyfin.Plugin.PerUserVisibility` — a load failure almost always means the folder is one level too deep, or `meta.json` is missing.

## Usage

Open **Dashboard → Plugins → Per-User Visibility**. The same page is also in the sidebar under **Plugins**.

1. Select a user on the left. The header shows whether that user has a scope yet, and how many titles it grants.
2. Pick a library tab, then search or scroll the list.
3. Tick the titles the user may see. Ticked titles move into the summary panel on the right, grouped by library.
4. If a library should be visible in full, turn on **whole library visible** on that library's tab. Individual selection for that library is disabled while the switch is on, and the library is added to the user's *Library access*.
5. Press **Save and apply**. The status bar shows whether there are unsaved changes; leaving the page with unsaved changes asks for confirmation first.

Changes apply to new requests immediately. The client may need a page refresh, or a sign-out and sign-in, before the list looks right.

### How the whitelist behaves

The selection is a whitelist: anything unticked is invisible, and that holds for the rest of the library.

- A user with no scope at all is **not** restricted. The plugin takes over only after you save a scope for that user.
- New series and movies arrive without a tag, so they stay hidden until you grant them. New episodes of an already granted series are the exception — episodes inherit the series tag and appear on their own.
- Removing a grant strips the tag and the lock from that item. The plugin records what it applied, so cleanup does not depend on the current selection.

## Limitations

- **The tag box is taken over.** Once a scope is applied, tags added by hand under *Parental Control → Allowed tags* are overwritten on the next save and maintained by the plugin, so `AllowedTags` holds only `lscope-allow-<first 8 characters of the user id>`. Grant access through the plugin page.
- **Libraries granted item by item stay under *Library access*.** The plugin grants items, not library entries, so if a user is given a few series and sees none of them, check that the library is in their *Library access* list. Whole-library grants look after this themselves — the entry is added on and removed again when the switch goes off.
- **Tags become read-only for granted items.** The tag field is locked, so manual tag edits on those items are ignored until the grant is removed.
- **One tag per user per item.** Scopes add up across libraries; they are not per-library namespaces. An item granted to a user counts as granted everywhere that user can see it.

## How this was built

This plugin was written by an AI coding assistant under the maintainer's direction. The design, the tag-based approach and the API surface were the maintainer's calls; the implementation and this documentation were drafted with AI assistance and reviewed before release.

Everything described above was verified on a live Jellyfin 12.4.0 server: installing from the repository URL, granting and revoking scopes, confirming that hidden items return `404`, refreshing metadata, and uninstalling. Beyond the MIT licence there is no warranty.

## Build from source

Requires the .NET SDK 10.

```bash
git clone https://github.com/KatoSred/jellyfin-plugin-per-user-visibility.git
cd jellyfin-plugin-per-user-visibility/src/Jellyfin.Plugin.PerUserVisibility
dotnet publish -c Release -o ../../build
cd ../..
python3 tools/package.py
```

`tools/package.py` writes `dist/PerUserVisibility_<version>.zip` and updates the `checksum` in `manifest.json`. Packaging is reproducible (fixed entry order, fixed timestamps, fixed compression); the manifest checksum and the archive MD5 must match, or the install is refused.
