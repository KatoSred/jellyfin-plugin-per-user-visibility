# Per-User Visibility

[English](README.md) | 简体中文

插件 1.0.1.0 · 已在 Jellyfin 12.4.0 上验证 · 更新于 2026-11-09

Jellyfin 只能按媒体库授权。这个插件按用户、按媒体库勾选可见的剧集，限制落在服务端：被藏起来的条目连直链都返回 `404`。

## 目录

- [快速开始](#快速开始)
- [功能](#功能)
- [环境要求](#环境要求)
- [安装](#安装)
- [使用](#使用)
- [已知限制](#已知限制)
- [开发说明](#开发说明)
- [从源码构建](#从源码构建)

## 快速开始

1. 在**控制台 → 插件 → 仓库**里添加这个地址：

   ```text
   https://raw.githubusercontent.com/KatoSred/jellyfin-plugin-per-user-visibility/main/manifest.json
   ```

2. 在**目录**标签页安装 **Per-User Visibility**。
3. 重启 Jellyfin，打开**插件 → Per-User Visibility**，选一个用户并勾选他可以看到的剧集。

不想加仓库的，手动解压也能装，步骤见[安装](#安装)。

## 功能

- 按用户、按媒体库圈定可见范围。
- 整库授权：一个开关让整个库可见，同时开放该库入口；关掉时一并收回。
- 隐藏落在查询层：列表、搜索、搜索建议、最新媒体、按 ID 直取、单集列表一律 `404`。
- 元数据刷新不掉，标签字段已锁定。
- 限制写在用户策略里，普通账户改不了。
- 管理员账户不过滤；没配过范围的用户不受影响。
- 无定时任务，保存即生效。要兜底就点"重新应用全部用户"。

## 环境要求

| 项目 | 值 |
| --- | --- |
| Jellyfin | 12.4.0 或更高（`targetAbi` 12.4.0.0） |
| 运行时 | net10.0，Jellyfin 12 服务端镜像已自带 |
| 架构 | 任意（x64、arm64），无原生依赖 |
| 账户 | 需要管理员账户来配置范围 |

10.9 和 11 用不了——插件按 `12.4.0.0` 编译，目录页会自动隐藏 `targetAbi` 高于服务端版本的条目。

## 安装

### 方式一：添加插件仓库

1. 打开**控制台 → 插件 → 仓库**。
2. 点**添加**，名称随意（例如 `Per-User Visibility`），地址填入：

   ```text
   https://raw.githubusercontent.com/KatoSred/jellyfin-plugin-per-user-visibility/main/manifest.json
   ```

3. 保存后切到**目录**标签页。
4. 找到 **Per-User Visibility**，选中并点**安装**。
5. 按提示重启 Jellyfin。

### 方式二：手动解压安装

服务器访问不了 GitHub，或者要钉死某个版本，用这条路。

1. 下载安装包：[`dist/PerUserVisibility_1.0.1.0.zip`](dist/PerUserVisibility_1.0.1.0.zip)。
2. 在服务器的插件目录下新建文件夹 `PerUserVisibility_1.0.1.0`，把压缩包解压进去，最终结构必须是：

   ```text
   <配置目录>/plugins/PerUserVisibility_1.0.1.0/
   ├── Jellyfin.Plugin.PerUserVisibility.dll
   └── meta.json
   ```

   `<配置目录>` 指 Jellyfin 的配置目录：官方 Docker 镜像里是 `/config`，Linux 包安装是 `/var/lib/jellyfin`，Windows 是 `%LOCALAPPDATA%\jellyfin`。
3. 两个文件直接放这一层，别再套子文件夹，否则服务端加载不到。
4. 重启 Jellyfin。

### 验证安装

打开**控制台 → 插件**，列表里该有 **Per-User Visibility**，版本 `1.0.1.0`，状态"活动"。没出现就去服务端日志搜 `Loaded assembly Jellyfin.Plugin.PerUserVisibility`——加载失败基本都是目录层级放错，或者缺了 `meta.json`。

## 使用

打开**控制台 → 插件 → Per-User Visibility**，侧边栏的"插件"分组里也有同一个入口。

1. 在左侧选择用户。顶部会显示该用户有没有配过范围、已授权多少部。
2. 选一个媒体库标签页，搜索或者直接往下翻。
3. 勾选该用户可见的剧集。勾上的会进右侧汇总面板，按媒体库分组。
4. 某个库该整库可见，就打开该库标签页右侧的**整库可见**开关；开关一开，该库的逐条勾选会被禁用，用户的"媒体库访问"里也会加上这个库。
5. 点**保存并应用**。底部状态条会提示有没有未保存的改动；带着改动离开页面，会先弹确认。

保存后新请求立即生效。客户端可能要刷新页面，或者退出来重新登录，列表才干净。

### 白名单的行为

授权是白名单制：勾了才可见，同一个库里没勾的一律隐藏。

- 完全没配过范围的用户**不受限制**。插件只在这个用户存过一次范围之后才接手。
- 新入库的剧集和电影没有标签，会一直藏着，直到你授权。已授权剧集的新单集是例外——单集继承剧集的标签，自动可见。
- 移除授权会一起摘掉标签和锁定。插件记录了自己应用过哪些条目，所以清理不看你现在选了哪些。

## 已知限制

- **家长控制的标签框被接管。** 保存范围后，在"用户 → 家长控制 → 允许的标签"里手工加的内容会在下次保存时被覆盖，改由插件维护，`AllowedTags` 里只剩 `lscope-allow-<用户 ID 前 8 位>`。要放开可见范围，走插件页面。
- **逐条授权的库要用户自己开。** 插件只按条目授权，不改"媒体库访问"。所以给了某个用户几部剧集、他却看不到东西时，先确认他的"媒体库访问"里有这个库。整库授权不用管这一条——开的时候会一并开放库入口，关掉时收回。
- **已授权条目的标签变只读。** 标签字段被锁，这些条目上手动改标签会被忽略，直到移除授权。
- **一个用户一个标签。** 范围在多个库之间是叠加的，不是按库隔离的命名空间。某条目对某用户授权后，在该用户可见的所有位置都算授权。

## 开发说明

本插件由 AI 编程助手在维护者指导下编写。设计方案、标签机制与接口由维护者确定，实现与本文档在 AI 辅助下成稿，发版前审阅过。

上文描述的行为都在真实的 Jellyfin 12.4.0 服务器上验证过：从仓库地址安装、授权与撤销、确认被隐藏条目返回 `404`、刷新元数据、卸载插件。除 MIT 许可证外不作任何担保。

## 从源码构建

需要 .NET SDK 10。

```bash
git clone https://github.com/KatoSred/jellyfin-plugin-per-user-visibility.git
cd jellyfin-plugin-per-user-visibility/src/Jellyfin.Plugin.PerUserVisibility
dotnet publish -c Release -o ../../build
cd ../..
python3 tools/package.py
```

`tools/package.py` 生成 `dist/PerUserVisibility_<版本>.zip`，并回写 `manifest.json` 里的 `checksum`。打包可复现（固定条目顺序、时间戳、压缩方式），清单与压缩包的 MD5 必须一致，否则安装会被拒。
