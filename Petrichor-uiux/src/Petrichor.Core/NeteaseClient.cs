using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Petrichor.Core
{
    /// <summary>Direct HTTPS NetEase Web API client. No gateway or Node.js runtime is used.</summary>
    public sealed partial class NeteaseClient : IDisposable
    {
        private static readonly Uri Origin = new Uri("https://music.163.com/");
        private readonly HttpClient http;
        private readonly WeapiCrypto crypto; private readonly IEapiCryptoProvider eapiProvider;
        private readonly ISessionStore store;
        private CookieContainer cookies = new CookieContainer();
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private bool disposed;
        public UserProfile CurrentUser { get; private set; }
        public bool IsLoggedIn => CurrentUser != null;
        public NeteaseClient(ICryptoProvider cryptoProvider, ISessionStore sessionStore, HttpMessageHandler handler = null)
        {
            crypto = new WeapiCrypto(cryptoProvider); eapiProvider=cryptoProvider as IEapiCryptoProvider; store = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
            http = new HttpClient(handler ?? new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
            http.Timeout = TimeSpan.FromSeconds(20);
        }
        private static Dictionary<string, object> Data(params object[] pairs) { var d = new Dictionary<string, object>(); for (int i = 0; i < pairs.Length; i += 2) d[(string)pairs[i]] = pairs[i + 1]; return d; }
        private static long ValidId(long id) { if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id)); return id; }
        private static void Page(int limit, int offset) { if (limit < 1 || limit > 100 || offset < 0) throw new ArgumentOutOfRangeException(nameof(limit)); }
        private void RequireLogin() { if (!IsLoggedIn) throw new MusicApiException(301, "请先登录网易云音乐"); }
        private Task<JObject> RequestAsync(string endpoint,Dictionary<string,object> data,CancellationToken ct,params int[] allowed)=>RequestInternalAsync(endpoint,data,ct,false,allowed);
        /// <summary>少数接口（如 playlist/delete）只接受 GET 加 querystring，这里复用同一套 cookie 与状态码处理。</summary>
        private async Task<JObject> RequestGetAsync(string pathAndQuery, CancellationToken ct) {
            if (disposed) throw new ObjectDisposedException(nameof(NeteaseClient));
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try {
                using (var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Origin, pathAndQuery))) {
                    request.Headers.Referrer = Origin;
                    request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0 Safari/537.36");
                    var cookie = cookies.GetCookieHeader(Origin);
                    request.Headers.TryAddWithoutValidation("Cookie", cookie + (cookie.Length > 0 ? "; " : "") + "os=pc; appver=2.10.13");
                    using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false)) {
                        ct.ThrowIfCancellationRequested();
                        if (!response.IsSuccessStatusCode) throw new MusicApiException((int)response.StatusCode, "网易云服务暂时无法响应请求");
                        var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        JObject json; try { json = JObject.Parse(text); } catch (JsonException) { throw new MusicApiException(502, "网易云返回了无法解析的数据"); }
                        var code = (int?)json["code"] ?? 200;
                        if (code == 301 || code == 302 || code == 401) { CurrentUser = null; cookies = new CookieContainer(); await store.ClearAsync().ConfigureAwait(false); throw new MusicApiException(code, "登录已失效，请重新登录"); }
                        return json;
                    }
                }
            }
            catch (HttpRequestException e) { throw new MusicApiException(503, "无法连接网易云，请检查网络", e); }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new MusicApiException(504, "网易云请求超时，请重试"); }
            finally { gate.Release(); }
        }
        private Task<JObject> RequestEapiAsync(string endpoint,Dictionary<string,object> data,CancellationToken ct)=>RequestInternalAsync(endpoint,data,ct,true,new int[0]);
        private async Task<JObject> RequestInternalAsync(string endpoint, Dictionary<string, object> data, CancellationToken ct, bool eapi, int[] allowed)
        {
            if (disposed) throw new ObjectDisposedException(nameof(NeteaseClient));
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try {
                var csrf = cookies.GetCookies(Origin)["__csrf"]?.Value ?? "";
                data["csrf_token"] = csrf;
                if(eapi){var header=Data("os","pc","appver","2.10.13","__csrf",csrf);foreach(Cookie item in cookies.GetCookies(Origin))header[item.Name]=item.Value;data["header"]=header;data["e_r"]=false;}
                var payload=eapi?new EapiCrypto(eapiProvider).Encrypt("/api/"+endpoint,JsonConvert.SerializeObject(data)):crypto.Encrypt(JsonConvert.SerializeObject(data));
                using (var request = new HttpRequestMessage(HttpMethod.Post, new Uri(eapi?new Uri("https://interfacepc.music.163.com/"):Origin, (eapi?"eapi/":"weapi/") + endpoint + "?csrf_token=" + Uri.EscapeDataString(csrf)))) {
                    request.Headers.Referrer = Origin;
                    request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0 Safari/537.36");
                    var cookie = cookies.GetCookieHeader(Origin);
                    request.Headers.TryAddWithoutValidation("Cookie", cookie + (cookie.Length > 0 ? "; " : "") + "os=pc; appver=2.10.13");
                    request.Content = new FormUrlEncodedContent(payload);
                    using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false)) {
                        ct.ThrowIfCancellationRequested();
                        if (!response.IsSuccessStatusCode) throw new MusicApiException((int)response.StatusCode, "网易云服务暂时无法响应请求");
                        JObject json; try { json = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false)); }
                        catch (JsonException) { throw new MusicApiException(502, "网易云返回了无法解析的数据"); }
                        var code = (int?)json["code"] ?? 0;
                        if (code != 200 && !allowed.Contains(code)) {
                            if (code == 301 || code == 302 || code == 401) { CurrentUser = null; cookies = new CookieContainer(); await store.ClearAsync().ConfigureAwait(false); }
                            throw new MusicApiException(code, code == 301 || code == 302 || code == 401 ? "登录已失效，请重新登录" : ((string)(json["msg"] ?? json["message"]) ?? "网易云未能完成请求"));
                        }
                        IEnumerable<string> headers;
                        if (response.Headers.TryGetValues("Set-Cookie", out headers)) foreach (var h in headers) { try { cookies.SetCookies(Origin, h); } catch (CookieException) { } }
                        // QR response may return the cookie in JSON as well as HTTP headers.
                        if (code == 803 && json["cookie"]?.Type == JTokenType.String) ImportCookieHeader((string)json["cookie"]);
                        if (cookies.GetCookies(Origin)["MUSIC_U"] != null) await store.SaveAsync(cookies.GetCookieHeader(Origin)).ConfigureAwait(false);
                        return json;
                    }
                }
            }
            catch (HttpRequestException e) { throw new MusicApiException(503, "无法连接网易云，请检查网络", e); }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new MusicApiException(504, "网易云请求超时，请重试"); }
            finally { gate.Release(); }
        }
        private void ImportCookieHeader(string header) {
            foreach (var pair in (header ?? "").Split(';')) { var at = pair.IndexOf('='); if (at < 1) continue; var name = pair.Substring(0, at).Trim();
                if (name != "MUSIC_U" && name != "MUSIC_A" && name != "__csrf" && name != "NMTID") continue;
                try { cookies.Add(Origin, new Cookie(name, pair.Substring(at + 1).Trim(), "/")); } catch (CookieException) { }
            }
        }
        public async Task<bool> RestoreSessionAsync(CancellationToken ct = default(CancellationToken)) {
            var value = await store.LoadAsync().ConfigureAwait(false); if (string.IsNullOrEmpty(value)) return false;
            await gate.WaitAsync(ct).ConfigureAwait(false); try { ImportCookieHeader(value); } finally { gate.Release(); }
            try { return await GetCurrentUserAsync(ct).ConfigureAwait(false) != null; }
            catch (MusicApiException e) when (e.RequiresLogin) { return false; }
        }
        public async Task<UserProfile> GetCurrentUserAsync(CancellationToken ct = default(CancellationToken)) {
            var j = await RequestAsync("w/nuser/account/get", Data(), ct).ConfigureAwait(false);
            CurrentUser = UserProfile.From(j["profile"] ?? j["data"]?["profile"]);
            if (CurrentUser == null) { await LogoutAsync().ConfigureAwait(false); throw new MusicApiException(301, "请先登录"); }
            return CurrentUser;
        }
        public async Task LogoutAsync() { await gate.WaitAsync().ConfigureAwait(false); try { cookies = new CookieContainer(); CurrentUser = null; await store.ClearAsync().ConfigureAwait(false); } finally { gate.Release(); } }
        public async Task<QrLoginChallenge> BeginQrLoginAsync(CancellationToken ct = default(CancellationToken)) {
            var j = await RequestAsync("login/qrcode/unikey", Data("type", 3), ct).ConfigureAwait(false);
            var key = (string)(j["unikey"] ?? j["data"]?["unikey"]);
            if (string.IsNullOrWhiteSpace(key)) throw new MusicApiException(502, "网易云未返回登录二维码标识");
            return new QrLoginChallenge { Key = key, LoginUrl = new Uri("https://music.163.com/login?codekey=" + Uri.EscapeDataString(key)), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(3) };
        }
        public async Task<QrLoginState> CheckQrLoginAsync(QrLoginChallenge challenge, CancellationToken ct = default(CancellationToken)) {
            if (challenge == null) throw new ArgumentNullException(nameof(challenge));
            if (DateTimeOffset.UtcNow >= challenge.ExpiresAt) return QrLoginState.Expired;
            if (DateTimeOffset.UtcNow < challenge.NextPollAt) await Task.Delay(challenge.NextPollAt - DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            challenge.NextPollAt = DateTimeOffset.UtcNow.AddMilliseconds(2500);
            var j = await RequestAsync("login/qrcode/client/login", Data("key", challenge.Key, "type", 3), ct, 800, 801, 802, 803).ConfigureAwait(false);
            var state = (QrLoginState)(int)j["code"];
            if (!Enum.IsDefined(typeof(QrLoginState), state)) throw new MusicApiException(502, "网易云返回了无效的扫码状态");
            if (state == QrLoginState.Authorized) await GetCurrentUserAsync(ct).ConfigureAwait(false);
            return state;
        }
        public async Task<UserProfile> WaitForQrLoginAsync(QrLoginChallenge challenge, IProgress<QrLoginState> progress = null, CancellationToken ct = default(CancellationToken)) {
            while (true) { ct.ThrowIfCancellationRequested(); var state = await CheckQrLoginAsync(challenge, ct).ConfigureAwait(false); progress?.Report(state);
                if (state == QrLoginState.Authorized) return CurrentUser;
                if (state == QrLoginState.Expired) throw new MusicApiException(800, "二维码已过期，请重新获取");
            }
        }
        public async Task<List<Playlist>> GetRecommendedPlaylistsAsync(int limit = 12, CancellationToken ct = default(CancellationToken)) {
            Page(limit, 0); var j = await RequestAsync("personalized/playlist", Data("limit", limit, "total", true, "n", 1000), ct).ConfigureAwait(false);
            return (j["result"] ?? new JArray()).Select(Playlist.From).ToList();
        }
        public async Task<List<Playlist>> GetChartsAsync(CancellationToken ct = default(CancellationToken)) {
            var j = await RequestAsync("toplist", Data(), ct).ConfigureAwait(false); return (j["list"] ?? new JArray()).Select(Playlist.From).ToList();
        }
        private async Task<JObject> SearchAsync(string query, int type, int limit, int offset, CancellationToken ct) {
            if (string.IsNullOrWhiteSpace(query) || query.Length > 100) throw new ArgumentException("请输入 1 至 100 字的搜索词", nameof(query)); Page(limit, offset);
            return await RequestAsync("cloudsearch/pc", Data("s", query.Trim(), "type", type, "limit", limit, "offset", offset, "total", true), ct).ConfigureAwait(false);
        }
        public async Task<SearchPage<Track>> SearchTracksAsync(string query, int limit = 30, int offset = 0, CancellationToken ct = default(CancellationToken)) {
            var j = await SearchAsync(query, 1, limit, offset, ct).ConfigureAwait(false); var r = j["result"];
            return new SearchPage<Track> { Items = (r?["songs"] ?? new JArray()).Select(Track.From).ToList(), Total = (int?)r?["songCount"] ?? 0, Limit = limit, Offset = offset };
        }
        public async Task<SearchPage<Playlist>> SearchPlaylistsAsync(string query, int limit = 30, int offset = 0, CancellationToken ct = default(CancellationToken)) {
            var j = await SearchAsync(query, 1000, limit, offset, ct).ConfigureAwait(false); var r = j["result"];
            return new SearchPage<Playlist> { Items = (r?["playlists"] ?? new JArray()).Select(Playlist.From).ToList(), Total = (int?)r?["playlistCount"] ?? 0, Limit = limit, Offset = offset };
        }
        public async Task<Playlist> GetPlaylistAsync(long id, CancellationToken ct = default(CancellationToken)) {
            var j = await RequestAsync("v6/playlist/detail", Data("id", ValidId(id), "n", 1000, "s", 0), ct).ConfigureAwait(false);
            if (j["playlist"] == null || j["playlist"].Type == JTokenType.Null) throw new MusicApiException(404, "歌单不存在"); return Playlist.From(j["playlist"]);
        }
        public async Task<List<Track>> GetPlaylistTracksAsync(Playlist playlist, int limit = 30, int offset = 0, CancellationToken ct = default(CancellationToken)) {
            if (playlist == null) throw new ArgumentNullException(nameof(playlist)); Page(limit, offset);
            var ids = playlist.TrackIds.Skip(offset).Take(limit).ToArray(); if (ids.Length == 0) return new List<Track>();
            return await GetTracksAsync(ids, ct).ConfigureAwait(false);
        }
        public async Task<List<Track>> GetAllPlaylistTracksAsync(Playlist playlist, CancellationToken ct = default(CancellationToken)) {
            if (playlist == null) throw new ArgumentNullException(nameof(playlist));
            ct.ThrowIfCancellationRequested();
            var ids = playlist.TrackIds.Count > 0 ? playlist.TrackIds : playlist.Tracks.Select(t => t.Id).ToList();
            var songs = playlist.Tracks.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());
            var missing = ids.Where(id => !songs.ContainsKey(id)).Distinct().ToArray();
            for (int offset = 0; offset < missing.Length; offset += 100) {
                ct.ThrowIfCancellationRequested();
                foreach (var track in await GetTracksAsync(missing.Skip(offset).Take(100), ct).ConfigureAwait(false)) songs[track.Id] = track;
            }
            return ids.Where(songs.ContainsKey).Select(id => songs[id]).ToList();
        }
        public async Task<List<Track>> GetTracksAsync(IEnumerable<long> ids, CancellationToken ct = default(CancellationToken)) {
            var values = ids?.Select(ValidId).ToArray() ?? throw new ArgumentNullException(nameof(ids)); if (values.Length > 100) throw new ArgumentException("单次最多加载 100 首歌曲"); if (values.Length == 0) return new List<Track>();
            var j = await RequestAsync("v3/song/detail", Data("c", JsonConvert.SerializeObject(values.Select(x => new { id = x })), "ids", JsonConvert.SerializeObject(values)), ct).ConfigureAwait(false);
            var songs = (j["songs"] ?? new JArray()).Select(Track.From).GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());
            return values.Where(songs.ContainsKey).Select(x => songs[x]).ToList();
        }
        public async Task<Lyrics> GetLyricsAsync(long id, CancellationToken ct = default(CancellationToken)) {
            var j = await RequestAsync("song/lyric", Data("id", ValidId(id), "tv", -1, "lv", -1, "rv", -1, "kv", -1, "_nmclfl", 1), ct).ConfigureAwait(false);
            var original = (string)j["lrc"]?["lyric"] ?? ""; var translation = (string)j["tlyric"]?["lyric"] ?? "";
            return new Lyrics { Original = original, Translation = translation, Lines = LrcParser.Parse(original), TranslatedLines = LrcParser.Parse(translation) };
        }
        public async Task<StreamInfo> GetStreamAsync(long id, AudioQuality quality = AudioQuality.Standard, CancellationToken ct = default(CancellationToken)) {
            if (!Enum.IsDefined(typeof(AudioQuality), quality)) throw new ArgumentOutOfRangeException(nameof(quality));
            // 无损/Hi-Res 需要同时带 level 参数（exhigh/lossless/hires），否则服务端只给 320k
            var level = quality == AudioQuality.HiRes ? "hires" : quality == AudioQuality.Lossless ? "lossless" : quality == AudioQuality.VeryHigh ? "exhigh" : quality == AudioQuality.High ? "higher" : "standard";
            var j = await RequestAsync("song/enhance/player/url", Data("ids", "[" + ValidId(id) + "]", "br", (int)quality, "level", level), ct).ConfigureAwait(false);
            var s = j["data"]?.FirstOrDefault(); Uri url;
            if (s == null || !Uri.TryCreate((string)s["url"], UriKind.Absolute, out url)) throw new MusicApiException(403, "当前账号无法播放这首歌，可能需要会员或暂不可用");
            if (url.Scheme != "http" && url.Scheme != "https") throw new MusicApiException(502, "无效的播放地址");
            return new StreamInfo { TrackId = (long?)s["id"] ?? id, Url = url, Format = (string)s["type"] ?? "mp3", Bitrate = (int?)s["br"] ?? (int)quality, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds((int?)s["expi"] ?? 1200), IsPreview = s["freeTrialInfo"] != null && s["freeTrialInfo"].Type != JTokenType.Null, Trial = s["freeTrialInfo"] as JObject };
        }
        public async Task<List<Track>> GetDailySongsAsync(CancellationToken ct = default(CancellationToken)) { RequireLogin(); var j = await RequestAsync("v3/discovery/recommend/songs", Data(), ct).ConfigureAwait(false); return (j["data"]?["dailySongs"] ?? new JArray()).Select(Track.From).ToList(); }
        public async Task<List<Track>> GetPrivateFmAsync(CancellationToken ct = default(CancellationToken)) { RequireLogin(); var j = await RequestAsync("v1/radio/get", Data(), ct).ConfigureAwait(false); return (j["data"] ?? new JArray()).Select(Track.From).ToList(); }
        public async Task<List<Playlist>> GetMyPlaylistsAsync(int limit = 30, int offset = 0, CancellationToken ct = default(CancellationToken)) { RequireLogin(); Page(limit, offset); var j = await RequestAsync("user/playlist", Data("uid", CurrentUser.Id, "limit", limit, "offset", offset), ct).ConfigureAwait(false); return (j["playlist"] ?? new JArray()).Select(Playlist.From).ToList(); }
        /// <summary>删除自己的歌单；接口是 GET，参数名 pid。</summary>
        public async Task DeletePlaylistAsync(long id, CancellationToken ct = default(CancellationToken)) {
            RequireLogin(); ValidId(id);
            // playlist/delete 走 GET querystring，RequestAsync 固定 POST，这里单独发一次带 query 的请求
            var j = await RequestGetAsync("playlist/delete?pid=" + id, ct).ConfigureAwait(false);
            if (j["code"] != null && (int)j["code"] != 200) throw new MusicApiException((int)j["code"], (string)(j["msg"] ?? j["message"]) ?? "无法删除歌单");
        }
        /// <summary>新建歌单，返回新歌单（含创建者信息）。</summary>
        public async Task<Playlist> CreatePlaylistAsync(string name, CancellationToken ct = default(CancellationToken)) {
            RequireLogin(); if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 40) throw new ArgumentException("歌单名需要 1 至 40 字", nameof(name));
            var j = await RequestAsync("playlist/create", Data("name", name.Trim()), ct).ConfigureAwait(false);
            var created = j["playlist"]; if (created != null && created.Type != JTokenType.Null) return Playlist.From(created);
            var id = (long?)j["id"] ?? 0; if (id <= 0) throw new MusicApiException(502, "网易云未返回新歌单");
            return new Playlist { Id = id, Name = name.Trim(), Creator = CurrentUser, TrackCount = 0 };
        }
        public async Task<List<long>> GetLikedTrackIdsAsync(CancellationToken ct = default(CancellationToken)) { RequireLogin(); var j = await RequestAsync("song/like/get", Data("uid", CurrentUser.Id), ct).ConfigureAwait(false); return (j["ids"] ?? new JArray()).Select(x => (long)x).ToList(); }
        public async Task SetLikedAsync(long id, bool liked, CancellationToken ct = default(CancellationToken)) { RequireLogin(); await RequestAsync("radio/like", Data("trackId", ValidId(id), "like", liked, "alg", "itembased", "time", 3), ct).ConfigureAwait(false); }
        public void Dispose() { if (disposed) return; disposed = true; http.Dispose(); }
    }
}





