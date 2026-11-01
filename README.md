# 雨声 / Petrichor

原生 **C# / UWP** 网易云音乐第三方客户端，面向 Windows 10 Mobile 15063 及以上。直接使用 HTTPS 和 WEAPI 访问网易云；手机端不依赖 Node.js 或独立 API 网关。

## 运行应用

打开 [Petrichor.sln](Petrichor.sln)，选择 `Petrichor.App` 为启动项目。

- 本机：Debug / x86，已在这台 Windows 11 电脑注册、启动并测试。
- Windows 10 Mobile：Release / ARM，使用 .NET Native；已成功构建 ARM AppX，尚未部署到手机真机。
- 应用最低版本为 15063，业务库保持 .NET Standard 1.4，避免引入手机不支持的 .NET Standard 2.0 API。

```powershell
msbuild Petrichor.sln /restore /t:Build /p:Configuration=Debug /p:Platform=x86
.\scripts\Deploy-Desktop.ps1
# 更新本机应用并启动进程内测试：
.\scripts\Deploy-Desktop.ps1 -Build -Test
# Windows 10 Mobile 原生发布构建：
msbuild Petrichor.sln /restore /t:Build /p:Configuration=Release /p:Platform=ARM
```

本机部署使用现有开发者模式与解包注册。脚本目前匹配本机 Visual Studio 18 Community 和 Windows SDK 的安装路径。AppX 尚未签名；手机安装需要匹配发布者的签名和手机部署环境。

本机系统代理为 127.0.0.1:7897，已只为 `Petrichor.Music_ppvmpn1qcca64` 添加 UWP 回环例外。其他电脑仅在使用本地代理且需要时，以管理员身份运行 `Deploy-Desktop.ps1 -EnableLocalProxy`；脚本不修改全局代理。

## 页面

原生 XAML 包含发现、搜索、扫码登录、歌手、专辑、歌单广场、歌单详情、我的、账号收藏、播放器、同步歌词、队列和设置，共 13 页。首页按要求移除了 Hero 横幅，直接呈现四个入口和推荐歌单。

搜索支持单曲、歌单、歌手与专辑；歌单可分页加载，「播放全部」会读取完整歌单后建立播放队列。扫码二维码在本机生成，授权状态保存在 PasswordVault。播放支持原生 MediaPlayer、系统媒体控制、上一首/下一首、进度跳转、循环、随机和睡眠定时，最近播放保存在本机。音质设置应用到下一次加载音源。

「喜欢歌曲」「收藏歌手」「收藏专辑」接入真实账号接口；需要用户扫码授权。歌单远程订阅尚未实现，原生界面没有伪造收藏成功的按钮。

## 项目

| 项目 | 用途 |
| --- | --- |
| Petrichor.App | UWP XAML 宿主、页面交互、二维码、原生集成测试 |
| Petrichor.Core | WEAPI、API 模型、会话、内容、歌词与队列；.NET Standard 1.4 |
| Petrichor.Uwp | Windows 加密、凭据保险箱、媒体与本机历史 |
| tests/Petrichor.Core.Tests | 开发机协议和业务测试；.NET 10，不随手机应用运行 |

## 设计与验证

- [HTML 总览设计稿](design/index.html)、[七页补充设计稿](design/pages.html)：概念数据。
- [本机原生 UWP 运行截图](artifacts/test-results/index.html)：真实接口和真实 XAML 渲染。
- [验证记录](docs/VALIDATION.md)、[UWP 接入](docs/UWP-INTEGRATION.md)、[接口说明](docs/API.md)。

```powershell
dotnet run --project tests/Petrichor.Core.Tests
dotnet run --project tests/Petrichor.Core.Tests -- --live
```

Debug 包的 `petrichor://test` 入口会在真实 UWP 进程中执行页面与媒体测试，暂时切换页面、播放测试歌曲，结束后暂停并回到发现页。报告与 PNG 保存在应用 LocalState/TestResults；Release 包不开放这个入口。运行截图也已复制到仓库 artifacts/test-results。

账号真实扫码授权、收藏写入、手机锁屏/后台音频及手机 TLS 仍需真机验收。


