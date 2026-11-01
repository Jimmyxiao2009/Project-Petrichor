# 逻辑交接：定时器 / 播客 / 评论排序与楼中楼 / 下载目录

> **状态（0.4.5.1）：四项逻辑已全部接入，`PendingLogic` 不再有「即将上线」分支。**
> - 睡眠定时：`Petrichor.Core.SleepSchedule`（纯状态机）+ `PlaybackService` 计时与 `OnEnded` 拦截。
> - 播客：`Petrichor.Core.PodcastClient.cs`（`DjRadio` / `DjProgram` + 热门 / 订阅 / 详情 / 节目 / 订阅写入）；`Track.ProgramId` 让节目评论走 `A_DJ_1_{id}` 线程。
> - 评论：`Petrichor.Core.SocialClient.cs` 增加 eapi `v2/resource/comments`（推荐 99 / 最热 2 / 最新 3，游标编码见下）与 `resource/comment/floor/get` 楼中楼；`MusicComment` 补齐 `ReplyCount` / `Location` / `Replied*` / `Owner`。
> - 下载目录：`OfflineLibrary` 读取 `LocalSettings["downloadFolderToken"]`，`DownloadEntry.FolderToken` 持久化；下载完成后 `MoveAsync` 到所选目录，`FindAudioAsync` 先查所选目录再回退应用目录。
> - 下载删除与目录迁移（新增）：`OfflineLibrary.DeleteAsync` / `MigrateDownloadsAsync` / `CountOutsideDownloadFolderAsync`；下载页支持长按菜单与批量删除，设置页切换目录时可选择一并迁移，见第 5 节。
> - 验证：Core 单测 57/57；桌面 x86 运行时 29/29（含三条真实数据用例）；Release / ARM / .NET Native 构建通过。**真机（锁屏后台音频、SD 卡选取、手机 TLS）仍未验收。**
>
> 与本文档原设计的差异：
> - 评论游标用 `"pageNo:offset:serverCursor"` 三段编码，因为 UI 只透传一个 `Cursor` 字符串，而三种排序的翻页参数不同。
> - 下载目录逻辑放在 `OfflineLibrary`（而非 `PendingLogic`），`PendingLogic` 只转发；`DownloadEntry.FolderToken` 加在 `Petrichor.App` 的 `OfflineLibrary.cs`。
> - `MusicComment.From` 做了防御：`ipLocation` 在旧接口是字符串、`beReplied` 可能是空/非对象，原来会抛 `JValue` 子节点异常（已修，并有单测锁定）。
> - 运行时测试里三条 `(sample data)` 用例改为真实数据断言；`LiveCrypto` 补上 `IEapiCryptoProvider` 后 `--live` 可测 eapi 接口。
>
> 以下为原始交接设计，保留作参考。

## 原始交接（0.4.4.0）

UI 已全部完成并在桌面 x86 上通过 29 项运行时测试。逻辑层（Core / Uwp / OfflineLibrary）当时**没有改动**，是 0.4.3.0 的状态。

## 怎么接

- UI 只调用 `src\Petrichor.App\PendingLogic.cs`，**这是唯一的接缝**。逻辑侧：
  1. 在 Core / Uwp 里实现真正的逻辑；
  2. 替换 `PendingLogic` 里对应方法体；
  3. 把对应的 `*Supported` 改成 `true`，UI 会自动去掉「即将上线」提示并启用对应选项。
- 不需要改任何 XAML 或页面代码。如果确实要改返回类型，先看「UI 数据契约」一节，页面依赖这些字段。
- `PendingLogic` 里 `Sample`（= `App.IsDiagnosticsRunning`）分支返回样例数据，只在 `petrichor://test` 运行时测试期间生效，用于截图。接入真实逻辑后：
  - 能拿到真实数据的，删掉样例分支；
  - 相应运行时测试（`RuntimeTests.cs` 里带 `(sample data)` 的三项）改成用真实数据断言。
- 草稿：之前写过一版未编译、未验证的实现，放在 `docs\handoff\drafts\*.cs.txt`，可以参考也可以不用。里面的接口路径来自 NeteaseCloudMusicApi 的公开实现，**没有对真实服务器验证过**。

## 1. 睡眠定时

UI：播放页第二行中间的月亮按钮（`PlayerPage`）、设置页「睡眠定时」→ 共用 `SleepTimerMenu`。菜单项：关闭 / 15 / 30 / 45 / 60 / 90 分钟 / 播完当前歌曲 / 自定义（5–180 分钟 + 「到点后播完当前歌曲再停」）。倒计时显示在播放页进度条下方中间，月亮图标变品牌红。

现状：`SetSleepTimer(duration, finishTrack)` 已调用现有 `PlaybackService.SetSleepTimer(TimeSpan?)`，倒计时由 `PendingLogic` 自己记 deadline（UI 侧临时实现）。`finishTrack` 被忽略，「播完当前歌曲」菜单禁用。

需要实现：

| 成员 | 行为 |
|---|---|
| `SetSleepTimer(TimeSpan? d, bool finishTrack)` | null = 取消。`finishTrack=true` 时到点若正在播放，等当前曲目自然结束再暂停，**不切下一首** |
| `SetSleepAfterCurrentTrack()` | 当前曲目结束后暂停，不切下一首 |
| `SleepRemaining` | 剩余时间；未开启或「播完当前歌曲」等待中返回 null |
| `SleepAfterTrack` | 正在等当前曲目结束时为 true（含 finishTrack 到点后的等待阶段） |
| `SleepActive` | 已有实现：`SleepRemaining.HasValue || SleepAfterTrack` |
| `SleepTimerChanged` | 开启、取消、到点、曲目结束触发暂停时都要触发（设置页订阅它刷新文字；播放页靠 Shell 400ms 时钟轮询） |
| `SleepFinishTrackSupported` | 实现后改 true |

要点：
- 状态应放进 `PlaybackService`（它在后台播放时仍存活），`PendingLogic` 只转发。
- 「播完再停」需要在 `PlaybackService.OnEnded` 里拦截：命中时暂停并 return，不调用 `HandleMoveAsync`。
- 草稿：`SleepSchedule.cs.txt`（纯状态机，可单测）+ `PlaybackService.cs.txt`（接线）。
- 事件可能在非 UI 线程触发；设置页的处理函数直接改 TextBlock，接入时请在 `PendingLogic` 里切回 UI 线程再 raise，或改设置页走 Dispatcher。

## 2. 播客

UI：
- 发现页第 5 个入口「播客」→ `PodcastsPage`：页签「热门」「我的订阅」（未登录显示登录按钮），行模板 `PodcastRowTemplate`。
- `PodcastPage`（参数 `PodcastRef`）：封面、标题、主播、「N 期 · M 人订阅」、简介、播放全部、订阅/已订阅、排序切换（最新在前/最早在前）、节目列表（`EpisodeRowTemplate`）、加载更多。
- 搜索页新增第 5 个页签「播客」（`Results4`）。

UI 数据契约：

| 方法 | 返回 | 说明 |
|---|---|---|
| `GetPodcastsAsync(feed, cursor, ct)` | `Paged<MusicItem>` | `Value=PodcastRef{Id,Name}`，`Title`=名称，`Subtitle`=「主播 · N 期」，`Detail`=一句简介（可空），`CoverUrl` |
| `SearchPodcastsAsync(q, cursor, ct)` | 同上 | |
| `GetPodcastAsync(id, ct)` | `PodcastHeader` | 不支持时抛 `NotSupportedException`，页面据此显示「即将上线」 |
| `GetEpisodesAsync(id, oldestFirst, cursor, ct)` | `Paged<MusicItem>` | `Value=Track`（**可直接交给播放器**），`Title`=节目名，`Badge`=期数，`Subtitle`=用 `FormatEpisodeMeta(发布时间, 时长, 播放量)` |
| `SetPodcastSubscribedAsync(id, bool, ct)` | | 需要登录 |

`cursor`：第一页传 null，之后原样传回上一页的 `Paged.Cursor`；`HasMore` 控制「加载更多」按钮。

参考接口（weapi，未验证）：
- 热门 `djradio/hot/v1` {limit, offset} → `djRadios[]`, `hasMore`
- 我的订阅 `djradio/get/subed` {limit, offset, total:true} → `djRadios[]`, `hasMore`（需登录）
- 详情 `djradio/v2/get` {id} → `data`（旧版 `djradio/get` → `djRadio`）
- 节目 `dj/program/byradio` {radioId, limit, offset, asc} → `programs[]`, `more`
- 订阅 `djradio/sub` / `djradio/unsub` {id}
- 搜索 `cloudsearch/pc` type=1009 → `result.djRadios`, `djRadiosCount`
- 节目音频：`program.mainSong.id` 走现有 `song/enhance/player/url` 即可播放。

要点：
- `mainSong` 的 artists/album 常为空或占位，展示时用主播名和电台封面覆盖（草稿 `PodcastClient.cs.txt` 里 `DjProgram.From` 有处理）。
- 节目评论线程是 `A_DJ_1_{programId}` 而不是 `R_SO_4_{songId}`；`Track` 需要带上 programId（草稿里加了 `Track.ProgramId`），评论页才能拿到节目评论。
- 节目也会进入最近播放（`LibraryStore` 直接序列化 Track），新增字段要能被 Json 往返。

## 3. 评论排序与楼中楼

UI：
- `CommentsPage`：页签「推荐」「最热」「最新」，各自独立分页（列表底部「加载更多」）；右上角「共 N 条」。评论卡片 `Controls\CommentCard`：圆头像、昵称、时间 · IP 属地、点赞数 + 心形按钮（直接点即点赞，乐观更新）、正文、被回复引用块、「N 条回复 ›」入口。点卡片空白处弹菜单：回复 / 查看回复 / 点赞 / 复制。
- `CommentRepliesPage`（楼中楼，参数 `CommentThreadArgs{Track, Parent}`）：顶部楼主评论（灰底），「全部回复 · N」，回复列表；默认回复楼主，点某条回复改为回复它。
- `CommentsPage` 开了 `NavigationCacheMode=Enabled`，从楼中楼返回时保留列表与滚动位置（`MusicPage.OnNavigatedTo` 对 Back 导航跳过重载）。

现状：「最热」「最新」用旧接口 `v1/resource/comments` 已能用；「推荐」显示即将上线；楼中楼列表为空（但回复楼主已可用，走现有 `resource/comments/reply`）。卡片上的回复数、IP 属地、被回复引用目前没有数据，所以不显示。

需要实现：

| 成员 | 行为 |
|---|---|
| `GetCommentsAsync(track, sort, cursor, ct)` | 三种排序都支持；填满 `CommentEntry` 的 `ReplyCount`、`Location`、`Total`；分页用 cursor |
| `GetRepliesAsync(track, parent, cursor, ct)` | 楼中楼分页；回复里如果是「回复某人」，填 `RepliedNickname` / `RepliedContent` |
| `PostCommentAsync` / `SetCommentLikedAsync` | 已可用；节目评论需要按线程 id 区分（见上） |
| `CommentSortSupported(Recommended)`、`CommentRepliesSupported` | 实现后改 true |

参考接口（未验证）：
- 列表 `v2/resource/comments`（**eapi**）{threadId, pageNo, pageSize, showInner:true, sortType, cursor}
  - sortType：推荐 99、最热 2、最新 3
  - cursor：推荐 = `(pageNo-1)*pageSize`；最热 = `"normalHot#" + (pageNo-1)*pageSize`；最新 = 上一页返回的 `data.cursor`（首页 "0"）
  - 返回 `data.comments[]`、`data.totalCount`、`data.hasMore`、`data.cursor`；每条评论 `showFloorComment.replyCount`、`ipLocation.location`
- 楼中楼 `resource/comment/floor/get`（weapi）{parentCommentId, threadId, time, limit}
  - time：首页 -1，之后传上一页最后一条的 `time`
  - 返回 `data.comments[]`、`data.hasMore`、`data.totalCount`、`data.ownerComment`；每条 `beReplied[0].user.nickname` / `content`
- 草稿：`SocialClient.cs.txt`。

测试提示：`tests\Petrichor.Core.Tests` 的 `FakeHandler` 能解密 eapi 请求，可断言 sortType/cursor；`--live` 模式的 `LiveCrypto` 目前没实现 `IEapiCryptoProvider`（它已有 `AesEcbPkcs7`/`Md5`，补上接口声明即可），否则 live 测不了 eapi 接口。

## 4. 下载目录

UI：设置页「下载位置」：显示当前路径 / 「应用内部存储（默认）」/「所选文件夹不可用（SD 卡是否已拔出？）」，「选择文件夹」（系统 FolderPicker，可选手机或 SD 卡任意文件夹）、「恢复默认」。

现状：选择会被记住——`FutureAccessList.AddOrReplace(token, folder)`，token 存在 `LocalSettings["downloadFolderToken"]`（常量 `PendingLogic.DownloadFolderSetting`）。**下载仍写入应用内部存储**，设置页提示「即将上线」。

需要实现（`OfflineLibrary`）：
- 开始下载时读取当前 token，记在 `DownloadEntry` 上（需要新增并持久化到 index.json 的字段），以便下载过程中改设置也不影响已开始的任务、应用重启后续传仍知道目标。
- `BackgroundDownloader` 不保证能直接写可移动存储上通过选取器得到的目录。建议：仍下到应用目录，**完成后**再 `StorageFile.MoveAsync` 到目标目录，文件名用可读的「歌手 - 歌名.ext」（非法字符替换、重名 `GenerateUniqueName`）。移动失败（SD 卡拔出等）则留在应用目录并提示用户。
- `FindAudioAsync`：先按 token 打开目录找文件，再回退应用目录。注意：SD 卡上的文件用 `StorageFile.GetFileFromPathAsync(path)` 打开会抛 `UnauthorizedAccessException`（不是 FileNotFound），现有 catch 只接 FileNotFound，会直接崩到上层。
- 恢复默认时**不要**从 FutureAccessList 移除旧 token，已下载到旧目录的歌曲还要能播放。
- 草稿：`OfflineLibrary.cs.txt`（`MoveToChosenFolderAsync` / `OpenFolderAsync` / 改过的 `FindAudioAsync`）。
- 实现后把 `DownloadFolderSupported` 改 true。

## 5. 已下载歌曲删除与下载目录迁移（本轮新增）

UI：
- 下载页「离线音乐」：长按/右键单曲 → 「播放」「多选」「从设备删除」；标题栏「多选」进入批量模式，底部工具条为 已选 N / 删除所选 / 全选 / 退出多选。删除前有确认对话框。
- 设置页「下载位置」：切换文件夹（含「恢复默认」）时若已有下载在别处，弹「切换下载位置？」→「切换并迁移」/「仅切换」/「取消」；另外在存在未迁移歌曲时显示「迁移已下载歌曲」按钮。

逻辑（都在 `OfflineLibrary`，`PendingLogic` 只转发）：

| 成员 | 行为 |
|---|---|
| `DeleteAsync(trackId)` | 删除本地音频与 `lyrics/{id}.json`，从 `downloads` 移除并保存 index.json。文件删除失败（正在播放等）时返回 false 并保留条目；下载中的条目不可删。封面缓存按 URL 哈希共享，不删除。 |
| `CountOutsideDownloadFolderAsync()` | 已下载歌曲中 `FolderToken` 与当前生效目录不一致的数量。 |
| `MigrateDownloadsAsync(folder)` | 把已下载歌曲移到 `folder`（null = 应用内部存储）：文件名统一为「歌手 - 歌名.ext」+ `GenerateUniqueName`，成功后更新 `FileName` / `FolderToken` / `Track.LocalFilePath` 并保存；失败条目留在原位置。返回 `DownloadMigration{Moved,Failed}`。调用前需先用 `SetDownloadFolder` 切到同一目录。 |
| `TokenFor(folder)` | 目录令牌由路径（或 `FolderRelativeId`）哈希派生，同一目录恒定，`SetDownloadFolder` 与迁移共用。 |

- `FindStoredAsync` / `OpenByPathAsync` 抽出后，`FindAudioAsync`、删除、迁移共用同一套定位回退：所选目录 → 应用内部目录 → `Track.LocalFilePath`。
- 删除进行中的条目会先 `Pause()` 并解除传输句柄（后台传输没有取消 API）；`ObserveAsync` 在条目已被删除时跳过搬运文件与刷新歌词封面，且只在传输句柄仍属于自己时移除，避免误删同名歌曲重新下载后的新任务。
- 迁移正在播放的文件会失败，UI 提示「可能正在播放」；设置页在目标目录不可用（SD 卡拔出）时不提供迁移，避免整批失败。

## 验证现状

- Core 单元测试 57/57。
- 运行时测试 **30/30**（1.0.1.0，桌面 x86），新增 `Downloaded track migration and deletion`（迁移 → 迁回 → 删除）。截图目录见 `docs\VALIDATION.md`。
- 已部署到手机真机：`https://192.168.3.159`（Windows 10 Mobile ARM 15254.603）安装并启动 `Petrichor.Music_1.0.1.0_arm__ppvmpn1qcca64`。
- 真机上 FolderPicker 选 SD 卡的流程、以及跨存储迁移仍需手动验收。
