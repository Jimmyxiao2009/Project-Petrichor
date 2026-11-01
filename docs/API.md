# 网易云接口与验证范围

调查日期：2026-10-04。采用网易云网页端 HTTPS 请求协议，C# 独立实现，不引入 Node SDK。社区实现用于核对协议和参数，不代表网易云官方开放 API 承诺。

来源：

- [社区维护的 API Enhanced](https://github.com/NeteaseCloudMusicApiEnhanced/api-enhanced)
- [WEAPI 算法](https://github.com/NeteaseCloudMusicApiEnhanced/api-enhanced/blob/main/util/crypto.js)：AES-CBC/PKCS7 两次加密、随机 16 字符密钥、反序密钥的原始 RSA 公钥运算。
- [社区 C# 版本](https://github.com/wwh1004/NeteaseCloudMusicApi)：可供参考，但其 .NET Standard 2.0 目标不适合本项目的 Mobile 版本，未直接引用。
- [微软 .NET Standard 支持表](https://learn.microsoft.com/en-us/dotnet/standard/net-standard)
- [微软 UWP 单进程后台音频](https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/background-audio)

请求路径以 `https://music.163.com/weapi/` 开头。所有请求发送加密表单 `params` / `encSecKey`，统一 Referer、Cookie 与 CSRF。每次请求生成随机密钥；固定密钥只存在于测试提供程序。

| 页面或功能 | C# 方法 |
| --- | --- |
| 登录 | BeginQrLoginAsync / CheckQrLoginAsync / WaitForQrLoginAsync / RestoreSessionAsync / LogoutAsync |
| 发现 | GetRecommendedPlaylistsAsync / GetDailySongsAsync / GetChartsAsync / GetPrivateFmAsync |
| 搜索 | SearchTracksAsync / SearchPlaylistsAsync / SearchArtistsAsync / SearchAlbumsAsync |
| 歌手 | GetArtistAsync / GetArtistBiographyAsync / GetArtistAlbumsAsync |
| 专辑 | GetAlbumAsync |
| 我的 | GetCurrentUserAsync / GetMyPlaylistsAsync / GetLikedTrackIdsAsync / GetSavedArtistsAsync / GetSavedAlbumsAsync |
| 歌单 | GetPlaylistsAsync / GetPlaylistAsync / GetPlaylistTracksAsync |
| 歌词 | GetLyricsAsync / LrcParser.Parse / LrcParser.FindActive |
| 播放 | GetStreamAsync / PlaybackService / PlaybackQueue |
| 收藏 | SetLikedAsync / SetArtistSavedAsync / SetAlbumSavedAsync |
| 最近播放 | LibraryStore.ReadAsync / RecordAsync / ClearAsync |

歌单分页根据 TrackIds 获取歌曲详情并恢复原始顺序。歌单模型保留创建者及收藏状态，用于「我创建的」与「我收藏的」分类。歌单订阅接口近期引入额外校验，尚未纳入直连实现；设计稿的收藏歌单按钮属于概念交互，不能宣称已完成写入功能。

标准／高／极高音质请求 128／192／320 kbps 的兼容音频。会员、试听、无播放地址等状态遵从网易云响应。播放 URL 只在内存使用，每次切歌重新获取；过期暂停恢复时重新获取。不提供音源解锁或会员绕过。服务变更时更新 C# 请求层，无需在手机上运行服务端程序。

验证区分：协议与业务测试覆盖加密、模型、分页、二维码响应、会话失效、歌词、队列、试听、不可用歌曲和取消操作；公开接口可以通过 `--live` 实测。测试中的二维码授权响应为模拟，未使用用户账号，不能当作真实授权通过的证据。原生 UWP 已在本机运行，公开接口、二维码生成、真实 MediaPlayer 播放、歌词、队列及窄屏布局通过集成测试；Release ARM / .NET Native 已构建成功。尚未在 Windows 10 Mobile 真机验证 TLS、锁屏/后台播放或真实账号授权和收藏写入。

