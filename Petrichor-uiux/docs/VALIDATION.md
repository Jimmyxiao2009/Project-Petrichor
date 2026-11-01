# 验证记录

日期：2026-10-06。应用版本：1.0.0.0（本页下方为历史记录）。开发机：Windows 11 26200。

## 1.0.1.0：下载删除与目录迁移

- 新增功能：下载页删除下载（长按/右键菜单 + 多选批量删除，删除前确认，进行中与失败条目也可删），设置页切换下载目录时可选「切换并迁移」，以及独立的「迁移已下载歌曲」按钮。
- 逻辑：`OfflineLibrary.DeleteAsync`（音频 + 歌词缓存 + index 条目；下载中条目先暂停并解除传输句柄）、`MigrateDownloadsAsync`（移动到所选目录或应用内部存储，重命名并更新 `FolderToken` / `FileName` / `LocalFilePath`）、`CountOutsideDownloadFolderAsync`；`PendingLogic` 转发，接缝签名未变。
- 顺带加固：`ObserveAsync` 在下载期间条目被删除时不再搬运文件或刷新歌词封面；传输句柄只在仍是自己的任务时移除，避免误删同名歌曲重新下载后的新任务。
- 新增运行时用例 `Downloaded track migration and deletion`：等后台传输收尾 → 迁移到本地临时目录 → 校验文件落在新目录且可解析 → 迁回 → 校验指向与可解析 → 删除 → 校验不再可解析。
- 桌面 x86 运行时测试 **30/30 PASS**；Core 单测 **57/57**。
- Debug / x86 与 Release / ARM / .NET Native 均构建通过；ARM 包用 `CN=Petrichor.Development`（指纹 `C2537162A6CA792F64FFE59232FA37020B9187AE`）签名。
- **已部署到手机真机**：设备门户 `https://192.168.3.159`，Windows 10 Mobile ARM `15254.603`，安装并启动 `Petrichor.Music_1.0.1.0_arm__ppvmpn1qcca64`（`artifacts\device\deployment.json`）。真机上的 SD 卡选取与迁移流程仍需手动验收。

## 1.0.0.0：改名与签名

- 应用显示名由「雨声 · Petrichor」改为 `Petrichor`：`Properties\DisplayName`、`VisualElements DisplayName`、协议 `DisplayName` 三处。包标识 `Petrichor.Music`、Publisher 与协议名 `petrichor` 未变，包族名仍为 `Petrichor.Music_ppvmpn1qcca64`，本机属覆盖升级，登录凭据与 LocalState 保留。
- 版本号 0.4.5.2 → 1.0.0.0。
- Debug / x86 与 Release / ARM / .NET Native 均构建通过，并用现有自签名证书 `CN=Petrichor.Development`（指纹 `C2537162A6CA792F64FFE59232FA37020B9187AE`，2029-10-05 到期）签名，两个包均导出 `Petrichor.Development.cer`。
- 解包读回核对：两个包的 Identity 均为 `Petrichor.Music` / `1.0.0.0` / `CN=Petrichor.Development`，显示名与协议显示名均为 `Petrichor`。
- `signtool verify /pa` 对两个包只报「证书链终止于不受信任的根」，即自签名证书未装入本机受信任根存储；签名主体与清单 Publisher 一致。
- 本轮未做：本机注册与前台启动、手机真机部署。本机当前注册版本仍为 0.4.5.2。

## 0.4.5.1：逻辑层接入

- `PendingLogic` 的「即将上线」全部替换为真实逻辑：睡眠定时、播客、评论排序与楼中楼、下载目录。`*Supported` 均为 true。
- Core 单测 **57/57**（新增睡眠定时状态机、eapi 评论排序与游标、楼中楼、电台/节目解析共 22 项）。
- 桌面 x86 运行时测试 **29/29**。三条原 `(sample data)` 用例改为真实数据：楼中楼打开真实评论的回复、播客列表读真实热门电台、播客详情读真实节目。
- Debug / x86 与 Release / ARM / .NET Native 均构建通过（`Petrichor.App_0.4.5.1_ARM.appx`，未签名）。
- 修复真实缺陷：`MusicComment.From` 在旧接口 `ipLocation` 为字符串、`beReplied` 为空/非对象时会抛 `JValue` 子节点异常；楼中楼因此显示空。已改为防御式读取并有单测锁定。
- `--live` 模式补上 `IEapiCryptoProvider`，可直接验证 eapi 接口（推荐排序、楼中楼实测有数据）。

部署注意：同版本号 + 松散文件注册会跳过更新。本次还遇到旧注册残留导致应用无法启动，用 `Remove-AppxPackage -PreserveApplicationData` 重装后恢复；改完务必升 `Package.appxmanifest` 版本号。

## 本轮通过

- Debug / x86 完整解决方案构建通过；Release / ARM / .NET Native 构建通过。
- 新版 UWP 已注册并启动到可见前台，通过 Computer Use 查看发现、我的、设置、歌单详情与播放器。实际点击歌单「播放全部」后播放《The Devil Incarnate》，观察到进度推进和暂停状态。
- 17 项原生集成测试全部通过：真实页面和公开数据、二维码生成、设置、MediaPlayer 网络音频、暂停/恢复/跳转、同步歌词、队列、360×640 双列、下一首/上一首、不可用歌曲清理音源与重试、播放历史。
- 24 项协议与业务测试、5 项真实公开接口请求全部通过。
- 已检查 13 个原生页面及公共错误入口。删除设置里的工程版本/技术栈说明、登录凭据存储说明、搜索与收藏的加载项数、测试横幅。接口和系统错误改为用户提示；保留加载、登录、试听、缓冲等有助于用户操作的反馈。
- 首页没有 Hero 横幅。「播放全部」读取完整歌单，界面不再暴露已加载曲目这样的实现限制。

## 查看记录

[本轮前台回归 JSON](../artifacts/test-results/latest-foreground-results.json)；[原生 XAML 截图集](../artifacts/test-results/index.html)。PNG 由真实前台 UWP 进程的 RenderTargetBitmap 产生。前一轮后台执行的 14/17 结果保留为历史记录，不作为本轮结果。

本机入口：`Petrichor.Music_ppvmpn1qcca64!Music`。Debug 的测试入口不会在界面展示测试名称、结果或报告路径，Release 不开放测试入口。工程说明留在文档中。

本机代理为 127.0.0.1:7897，仅给本应用添加回环例外，没有修改全局代理。部署脚本从源清单读取版本和入口。

## 待验证

真实扫码授权与收藏写入尚未进行受控验收；观察到现有账号可展示歌单，不等同于完整登录回归。Windows 10 Mobile 真机 TLS、后台/锁屏音频、耳机/蓝牙媒体键与手机内存限制仍待测试。ARM 包已构建并签名，但未部署到手机；最低目标系统为 15063。
