# UI/UX 重构交接文档（uiux-redesign）

## 位置与版本

- 工作目录：`H:\项目\UWP\Petrichor-uiux`（主仓 `Project-Petrichor` 不是 git 仓库，本目录是 robocopy 副本，充当 worktree；主仓代码未动）
- 当前版本：0.4.2.0（Debug/x86），已注册安装，InstallLocation 指向本目录 `artifacts\desktop-x86-0.4.2.0`（详见下方「对齐打磨」）
- ⚠️ 部署陷阱：同版本号 + 松散文件注册时系统会跳过更新。改完必须升 `Package.appxmanifest` 版本号，或先 `Remove-AppxPackage` 再部署。

## 验证

- `.\scripts\Deploy-Desktop.ps1 -Test` 进程内测试 23/23 PASS（最近一轮在 0.4.0.0 上）
- 截图：`artifacts\test-results\*.png`（旧仓 artifacts 里有改版前截图可对比）

## 已完成的改动（对照设计计划）

### 导航骨架（Shell.xaml / Shell.xaml.cs）
- 底部四栏 tab bar（iOS 范式）→ 顶部 Pivot：发现 / 搜索 / 我的，各持有独立 Frame（DiscoverFrame/SearchFrame/LibraryFrame）
- 详情页（歌单/专辑/播放器/设置等 10 页）走覆盖层 `ContentFrame`；**必须带 Background**（否则 Pivot 透底，已修过一次）
- `Shell.Navigate`：三个主页类型路由到 Pivot 并清空详情层；其余导航到 ContentFrame
- 返回逻辑：详情可见时硬件返回 = GoBack/关闭详情层；主页时不拦截（系统挂起）
- MiniPlayer：单层 58px，顶部 2px `PetrichorAccent` 进度细条（MiniProgress），只留打开+播放/暂停；队列按钮移到播放页

### 设计令牌（App.xaml）
- 移除 `RequestedTheme="Dark"` 和 `SystemAccentColor` 覆盖（恢复用户系统强调色）
- 新增 `ThemeDictionaries`：Dark / Light 各一套 `PageBackground / Surface / SurfaceAlt / SecondaryText / Divider`
- `PetrichorAccent` #E84757 品牌红，仅用于主行动点（播放键、搜索按钮、喜欢、进度条、AccentButton）
- 字号 ramp：PageTitle 20 / SectionTitle 16 / Caption 11（去掉 220 字距）；MetroButton 触控目标 ≥40
- 封面模板标题固定 Height=36（两行动态高度导致 GridView 卡片不平齐，已修）

### 页面
- 搜索页：分类 ComboBox → 四页签 Pivot（Results0-3，code-behind 里 `Results` 字段动态指向当前页签列表）；搜索按钮列宽 Auto + MinWidth 76（修文字裁切）；输入框 VerticalContentAlignment=Center
- 播放页：64px 圆形 PetrichorAccent 主播放按钮（自定义 ControlTemplate）；循环/随机从设置页提到传输行（RepeatClick/ShuffleClick，直接写 App.Playback.Queue + LocalSettings）；下载/收藏到歌单/评论收进「⋯」MenuFlyout；喜欢填充态用品牌红
- 登录页：二维码收进 Surface 卡片（白底 quiet zone 保留），状态字 18→14
- 我的页：去掉大标题，账户行加设置齿轮（SettingsClick → SettingsPage）
- 各页 Margin 20→12，`{StaticResource SecondaryText/Surface}` 全部改 `{ThemeResource ...}`

### 功能修复
- 歌单批量下载：`CacheAsync` 原来用页面 `Token`（离开页面即取消 → 整单中断）。已改为 `CancellationToken.None`；BackgroundDownloader + TransferGroup 本身支持进程退出后续传，重启时 `InitializeCoreAsync` 用 `GetCurrentDownloadsForTransferGroupAsync` 自动重接。

### 后台音频（原本就有，README 标注待真机验收）
- Manifest 有 `backgroundMediaPlayback`；MediaPlayer AudioCategory=Media + CommandManager（SMTC）全开；进后台只停 UI 时钟不停音频。

## 对齐打磨（0.4.2.0）

- 当前版本：0.4.2.0，InstallLocation `artifacts\desktop-x86-0.4.2.0`；测试 23/23 PASS
- 截图对比：改前 `artifacts\ui-before-0.4.0.0`，改后 `artifacts\ui-after-0.4.2.0`（测试实际写入 `%LOCALAPPDATA%\Packages\Petrichor.Music_ppvmpn1qcca64\LocalState\TestResults`，`artifacts\test-results` 是旧拷贝）
- 改前源码备份：`artifacts\backup-src-0.4.0.0`
- 新增样式（App.xaml）：
  - `IconButton`：48×48、Padding 0、格内水平/垂直居中。UWP Button 默认 `HorizontalAlignment=Left`，放进等分列会贴左，播放页传输行/操作行、歌单多选栏原来都是这个问题
  - `TextButton`：左对齐文字按钮去掉水平内边距，文字与内容左缘对齐（播放页歌手/专辑、设置页清理缓存）
  - `FooterButton`：「加载更多」统一整行宽
  - `SubPivot`：二级 Pivot 标题 16px（顶层 24px），`Margin=-12,0` 抵消 PivotItem 自带 12 边距
- 栅格：所有内容左缘统一 12px（与 Pivot 标题文字对齐）
  - 首页三页（发现/搜索/我的）位于 PivotItem 内，PivotItem 已有 12 边距，页面水平 Margin 改为 0
  - 行首/封面/头像固定宽度元素加 `HorizontalAlignment=Left`（原来在稍宽的列里居中，偏 2–5px）
  - 标题栏右侧图标按钮 `Margin` 右 -14，使图标右缘与内容右缘对齐；与标题同行时两者都 `VerticalAlignment=Center`
- 字号：11px 全部提到 12px（Win10 Caption 下限）；歌单/专辑/歌手/歌词标题 22 → 20 Semilight
- 搜索框：15063 TextBox 不响应 VerticalContentAlignment，改用 `Height=40 Padding=10,9,6,0` 让文字与 40 高的搜索按钮垂直居中
- 其它：
  - 发现页空状态文字为空时折叠，消除标题与封面间的空行
  - 播放页缓冲/试听状态移到时间行中间，少占一行
  - 歌词高亮行原为硬编码白色，浅色主题下看不见 → 改用 `SystemControlForegroundBaseHighBrush` / `SecondaryText`
  - 0.4.0.0 截图里详情层透底，是部署包早于 Shell.xaml 的 Background 修复；0.4.2.0 已确认不透底

## 登录态与喜欢列表修复（0.4.3.0）

- 「我的」页登录后仍显示「登录云音乐」、没有歌单：Pivot 改版后三个首页在 Shell 构造时就加载，早于 `OnLoaded` 里的 `RestoreSessionAsync`；`App.AccountChanged` 事件之前没人订阅，`Shell.Navigate(LibraryPage)` 无参数时也不重建页面，所以一直停在未登录。现在 LibraryPage 订阅 `AccountChanged` 自动重载（登录/恢复/退出都走这条），切到「我的」时刷新最近播放
- 「我喜欢的音乐」乱序：原来用 `song/like/get` 的 id 顺序（不按收藏时间）。现在取用户自建的 specialType=5 歌单的 trackIds（与官方客户端一致），取不到时退回旧接口。`Playlist` 模型新增 `SpecialType`
- 新增运行时测试 2 项，共 25/25 PASS；Core 测试 35/35 PASS

## 新功能 UI（0.4.4.0；0.4.5.1 已接入逻辑）

- 逻辑已在 0.4.5.1 接入，见 `docs\LOGIC-HANDOFF.md` 顶部状态；UI 与逻辑的唯一接缝是 `src\Petrichor.App\PendingLogic.cs`，接缝签名未变。
- 睡眠定时：播放页第二行新增月亮按钮（5 列），倒计时显示在时间行中间；设置页改为状态 + 「设置定时」；共用 `SleepTimerMenu`
- 播客：发现页第 5 个入口 → `PodcastsPage`（热门 / 我的订阅）→ `PodcastPage`；搜索页新增「播客」页签；模板 `PodcastRowTemplate` / `EpisodeRowTemplate`
- 评论：`CommentsPage` 改为推荐 / 最热 / 最新三页签；卡片抽成 `Controls\CommentCard`（宿主页实现 `ICommentHost`）；新增楼中楼页 `CommentRepliesPage`；评论页开启页面缓存，`MusicPage` 对 Back 导航跳过重载
- 下载目录：设置页「下载位置」（FolderPicker + 恢复默认）；切换目录时可选择「切换并迁移」，并有独立的「迁移已下载歌曲」按钮。下载页支持长按/右键菜单与多选批量删除已下载歌曲（进行中与失败条目也可删）。
- 运行时测试 29/29；新截图在 `artifacts\ui-after-0.4.4.0`
- 已知：冷启动后第一张发现页截图偶尔还没渲染出封面（重跑正常），是截图时机问题

## ARM 打包与签名

- 打包：`MSBuild Petrichor.App.csproj /t:Build /p:Configuration=Release /p:Platform=ARM` → `AppPackages\Petrichor.App_<版本>_ARM_Test\Petrichor.App_<版本>_ARM.appx`
- 签名：`.\scripts\Sign-Package.ps1`（默认 ARM）。首次运行会在 `Cert:\CurrentUser\My` 创建自签名证书 `CN=Petrichor.Development`（必须与清单 Publisher 一致），之后复用；公钥导出为同目录 `Petrichor.Development.cer`。私钥只在本机证书存储里，换机器需重新生成（包族名不变，但设备上要重新信任新证书）
- 每次重新 Build 都会生成未签名的新 appx，需要重跑签名脚本
- `Deploy-Device.ps1` 在 `.cer` 存在时随包上传（form 字段 `certificate`），尚未在真机上验证

## 已知缺口 / 下一步候选
- **嵌套 Pivot**：搜索页、我的页在顶层 Pivot 里又套了 Pivot，Win10M 规范不建议（横滑手势冲突，内层吞掉滑动）。本轮只做了视觉层级区分；要彻底解决需把内层换成非手势的页签条。
- ~~**播客/电台节目**：Core 里完全没有 dj/program API~~ 已在 0.4.5.1 接入（`Petrichor.Core.PodcastClient.cs`）。
- 封面取色/模糊背景：min 15063 无 Acrylic，需 Composition 手写，未做。
- SemanticZoom（长列表分组跳转）：未做，接口数据需先分组。
- 歌词页/评论等页的细节打磨（Phase 4 剩余项）。
- 真机验收清单：锁屏后台音频、TLS、扫码登录、收藏写入。

## 回滚
主仓未动。回滚 = 卸掉 0.4.0.0，从主仓重新 Deploy。
