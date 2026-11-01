# UWP 接入

`src/Petrichor.App` 是实际可运行的原生 UWP 宿主，最低版本 10.0.15063.0；`Petrichor.sln` 提供 Debug/x86 与 Release/ARM 配置。所有页面使用 XAML Frame、Pivot、ListView、GridView 与原生输入控件。

## 服务生命周期

App 持有一份 NeteaseClient、PlaybackService 与 LibraryStore。页面退出取消页面请求，播放器跨页面保留。后台停止歌词/UI 时钟，媒体播放依靠清单中的 backgroundMediaPlayback 能力与 MediaPlayer 的系统媒体集成。

```csharp
var api = new NeteaseClient(new WindowsCryptoProvider(), new VaultSessionStore());
var history = new LibraryStore();
var playback = new PlaybackService(api, history);
await api.RestoreSessionAsync();
var results = await api.SearchTracksAsync("晴天");
await playback.PlayAsync(results.Items, 0);
```

播放器事件可能来自系统线程，Shell 与歌词页通过 CoreDispatcher 更新 UI。页面捕获 MusicApiException 并展示提示；页面导航取消请求，搜索用请求序号避免旧结果覆盖新词。不可用歌曲会清除旧音源，点击播放会重新请求 URL，避免恢复播放上一首残留音频。

## 登录

LoginPage 使用 QRCoder 1.4.3 在本机把真实 challenge.LoginUrl 编成 PNG；不调用第三方二维码网站。刷新取消原轮询，退出页面停止轮询，授权成功读取用户资料。Cookie 保存在 PasswordVault，日志不记录 Cookie 或二维码 URL。注销只清除本机会话。

扫码授权需要用户在另一台设备上的网易云音乐确认，自动化测试只验证挑战生成和等待扫码界面。

## 播放与布局

MediaPlayer.CommandManager 保持系统控制启用；Next/Previous 由 PlaybackService 处理，默认系统命令处理播放与暂停。每次切歌重新获取音源，恢复过期 URL 时再次请求。权限不足会显示错误；试听片段显示试听说明。

歌词使用原文/译文时间戳匹配和 LrcParser.FindActive，同步高亮滚动；点击歌词可跳转。歌单按 TrackIds 分页取详情并保持原始顺序。歌单播放按钮明确只播放已加载曲目。

发现和歌单广场动态计算封面尺寸，360×640 窗口保持双列，宽窗口可展示更多列。Shell 的内容宽度限制为 820，页面列表独立滚动，播放条与导航常驻。

## 构建与部署

```powershell
msbuild Petrichor.sln /restore /t:Build /p:Configuration=Debug /p:Platform=x86
.\scripts\Deploy-Desktop.ps1
msbuild Petrichor.sln /restore /t:Build /p:Configuration=Release /p:Platform=ARM
```

本机使用 SDK 15063 的 XAML 编译器，并显式声明 x86/ARM 与 AOT RuntimeIdentifiers。Release/ARM 的 .NET Native 1.7 构建已通过，Default.rd.xml 保留业务模型和二维码反射信息。手机 AppX 当前未签名，需要开发者签名后再进行手机部署。

Debug 本机运行可用脚本的 -Test 选项执行原生集成测试。真实账号授权、手机锁屏音频、耳机/蓝牙媒体键、后台内存限制和手机网络兼容性尚需真机验证。

