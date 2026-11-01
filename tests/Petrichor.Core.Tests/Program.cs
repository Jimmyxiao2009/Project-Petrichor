using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Petrichor.Core;
internal sealed class DesktopCrypto : ICryptoProvider, IEapiCryptoProvider {
    public byte[] Md5(byte[] bytes){using var hash=MD5.Create();return hash.ComputeHash(bytes);}
    public byte[] AesEcbPkcs7(byte[] input,byte[] key){using var aes=Aes.Create();aes.Key=key;aes.Mode=CipherMode.ECB;return aes.CreateEncryptor().TransformFinalBlock(input,0,input.Length);}
    public static string DecryptEapi(string hex){using var aes=Aes.Create();aes.Key=Encoding.UTF8.GetBytes("e82ckenh8dichen8");aes.Mode=CipherMode.ECB;var bytes=Convert.FromHexString(hex);return Encoding.UTF8.GetString(aes.CreateDecryptor().TransformFinalBlock(bytes,0,bytes.Length));}
    public byte[] RandomBytes(int length) => new byte[length]; // Fixed secret 'a' for deterministic protocol test.
    public byte[] AesCbcPkcs7(byte[] input, byte[] key, byte[] iv) { using var aes = Aes.Create(); aes.Key = key; aes.IV = iv; return aes.CreateEncryptor().TransformFinalBlock(input, 0, input.Length); }
    public static string Decrypt(string input, string key) { using var aes = Aes.Create(); aes.Key = Encoding.UTF8.GetBytes(key); aes.IV = Encoding.UTF8.GetBytes("0102030405060708"); var bytes = Convert.FromBase64String(input); return Encoding.UTF8.GetString(aes.CreateDecryptor().TransformFinalBlock(bytes, 0, bytes.Length)); }
}
internal sealed class FakeHandler : HttpMessageHandler {
    public readonly Queue<string> Bodies = new Queue<string>(); public JObject LastPayload; public string LastCookie; public Uri LastUri; public bool LoginCookie;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
        ct.ThrowIfCancellationRequested(); LastUri = request.RequestUri; LastCookie = request.Headers.GetValues("Cookie").Single();
        var form = await request.Content.ReadAsStringAsync(); var fields = form.Split('&').Select(p => p.Split('=')).ToDictionary(x => x[0], x => WebUtility.UrlDecode(x[1]));
        if (request.RequestUri.AbsolutePath.StartsWith("/eapi/")){var decoded=DesktopCrypto.DecryptEapi(fields["params"]).Split("-36cd479b6b5-");LastPayload=JObject.Parse(decoded[1]);var digest=Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes("nobody"+decoded[0]+"use"+decoded[1]+"md5forencrypt"))).ToLowerInvariant();if(decoded[2]!=digest||decoded[0]!="/api/"+request.RequestUri.AbsolutePath.Substring(6))throw new Exception("EAPI signature mismatch");}else {
        if (!fields.ContainsKey("encSecKey") || fields["encSecKey"].Length != 256) throw new Exception("Missing RSA key");
        LastPayload = JObject.Parse(DesktopCrypto.Decrypt(DesktopCrypto.Decrypt(fields["params"], new string('a',16)), "0CoJUm6Qyw8W8jud"));
        }
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Bodies.Dequeue()) };
        if (LoginCookie) { response.Headers.TryAddWithoutValidation("Set-Cookie", new[] { "MUSIC_U=test-session; Path=/; Secure; HttpOnly", "__csrf=csrf-test; Path=/; Secure" }); LoginCookie = false; }
        return response;
    }
}
internal static class Program {
    private static int count;
    private static void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); Console.WriteLine("PASS: " + label); count++; }
    private static async Task ExpectApi(Func<Task> action, int code, string label) { try { await action(); throw new Exception("No exception: " + label); } catch (MusicApiException e) { Check(e.Code == code, label); } }
    private static async Task Main(string[] args) {
        var crypto = new DesktopCrypto();
        var fields = new WeapiCrypto(crypto).Encrypt("{\"s\":\"雨\"}");
        Check(DesktopCrypto.Decrypt(DesktopCrypto.Decrypt(fields["params"], new string('a',16)), "0CoJUm6Qyw8W8jud") == "{\"s\":\"雨\"}", "WEAPI two AES-CBC/PKCS7 layers preserve UTF-8");
        using (var rsa = RSA.Create()) {
            rsa.ImportFromPem("-----BEGIN PUBLIC KEY-----\nMIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDgtQn2JZ34ZC28NWYpAUd98iZ37BUrX/aKzmFbt7clFSs6sXqHauqKWqdtLkF2KexO40H1YTX8z2lSgBBOAxLsvaklV8k4cBFK9snQXE9/DDaFt6Rr7iVZMldczhC0JNgTz+SHXT6CBHuX3e9SdB1Ua44oncaTWz7OBGLbCiK45wIDAQAB\n-----END PUBLIC KEY-----");
            var pub = rsa.ExportParameters(false);
            var expected = BigInteger.ModPow(new BigInteger(Encoding.ASCII.GetBytes(new string('a',16)), true, true), new BigInteger(pub.Exponent, true, true), new BigInteger(pub.Modulus, true, true)).ToString("x").TrimStart('0').PadLeft(256,'0');
            Check(fields["encSecKey"] == expected, "WEAPI raw RSA matches independently parsed SPKI public key");
        }
        var lyrics = LrcParser.Parse("[offset:-50]\n[00:01.2][00:03.456]雨\n[00:02.01]风\n[00:99.1]invalid");
        Check(lyrics.Count == 3 && lyrics[0].Position.TotalMilliseconds == 1150 && lyrics[2].Position.TotalMilliseconds == 3406, "LRC multiple timestamps, fractions, offset and sorting");
        Check(LrcParser.FindActive(lyrics, TimeSpan.FromMilliseconds(100)) == -1 && LrcParser.FindActive(lyrics, TimeSpan.FromSeconds(2)) == 1, "Lyric binary search handles before-first and between-lines");
        var queue = new PlaybackQueue(); queue.Replace(new[] { new Track {Id=1}, new Track {Id=2} }); queue.Repeat = RepeatMode.One;
        Check(queue.MoveNext() && queue.Index == 0 && queue.MoveNext(true) && queue.Index == 1, "Repeat-one distinguishes song end from user next");
        queue.Repeat = RepeatMode.Off; Check(!queue.MoveNext() && queue.Index == 1, "Queue stops at end without indexing past bounds");
        queue.Repeat = RepeatMode.All; Check(queue.MoveNext() && queue.Index == 0 && queue.MovePrevious() && queue.Index == 1, "Repeat-all wraps both directions");
        queue.Replace(Array.Empty<Track>()); Check(queue.Current == null && !queue.MoveNext(), "Empty queue is safe");
        var fake = new FakeHandler(); var store = new MemorySessionStore(); using var client = new NeteaseClient(crypto, store, fake);
        fake.Bodies.Enqueue("{\"code\":200,\"result\":{\"songCount\":60,\"songs\":[{\"id\":5000000001,\"name\":\"雨\",\"ar\":[{\"id\":3,\"name\":\"歌手\"}],\"al\":{\"id\":4,\"picUrl\":\"https://example.com/cover.jpg\"},\"dt\":12345}]}}");
        var search = await client.SearchTracksAsync(" 雨 ", 30, 30);
        Check(search.Items[0].Id == 5000000001 && search.Items[0].ArtistText == "歌手" && search.Offset == 30 && search.HasMore, "Track normalization preserves 64-bit IDs and pagination");
        Check(fake.LastUri.AbsolutePath == "/weapi/cloudsearch/pc" && (string)fake.LastPayload["s"] == "雨" && (int)fake.LastPayload["offset"] == 30, "Direct HTTPS search sends encrypted payload with pagination");
        await ExpectApi(() => client.GetDailySongsAsync(), 301, "Personal recommendations require login before network");
        fake.Bodies.Enqueue("{\"code\":200,\"unikey\":\"test-key\"}"); var challenge = await client.BeginQrLoginAsync();
        Check(challenge.LoginUrl.Query.Contains("test-key"), "QR challenge exposes login URL without a Node gateway");
        fake.Bodies.Enqueue("{\"code\":803}"); fake.LoginCookie = true;
        fake.Bodies.Enqueue("{\"code\":200,\"profile\":{\"userId\":123,\"nickname\":\"用户\"}}");
        Check(await client.CheckQrLoginAsync(challenge) == QrLoginState.Authorized && client.IsLoggedIn, "QR authorization verifies profile and establishes native session");
        Check((await store.LoadAsync()).Contains("MUSIC_U=test-session") && fake.LastCookie.Contains("MUSIC_U=test-session") && (string)fake.LastPayload["csrf_token"] == "csrf-test", "Session store and CSRF token carry through authenticated requests");
        fake.Bodies.Enqueue("{\"code\":200,\"data\":[{\"id\":1,\"url\":\"https://example.com/music.mp3\",\"expi\":1200,\"freeTrialInfo\":{\"start\":0,\"end\":30}}]}");
        Check((await client.GetStreamAsync(1)).IsPreview && (int)fake.LastPayload["br"] == 128000, "Playback defaults to compatible bitrate and preserves trial status");
        fake.Bodies.Enqueue("{\"code\":200,\"data\":[{\"id\":1,\"url\":null}]}"); await ExpectApi(() => client.GetStreamAsync(1), 403, "Unavailable songs remain unavailable");
        fake.Bodies.Enqueue("{\"code\":200,\"songs\":[{\"id\":2,\"name\":\"two\"},{\"id\":1,\"name\":\"one\"}]}");
        var ordered = await client.GetTracksAsync(new long[] {1,2}); Check(ordered[0].Id == 1 && ordered[1].Id == 2, "Batch details preserve playlist order");
        fake.Bodies.Enqueue("{\"code\":200,\"artist\":{\"id\":3,\"name\":\"歌手\",\"albumSize\":2},\"hotSongs\":[{\"id\":1,\"name\":\"热门\"}]}");
        var artist = await client.GetArtistAsync(3); Check(artist.HotTracks.Count == 1 && artist.AlbumCount == 2 && fake.LastUri.AbsolutePath == "/weapi/v1/artist/3", "Artist detail normalizes hot tracks and catalog counts");
        fake.Bodies.Enqueue("{\"code\":200,\"album\":{\"id\":4,\"name\":\"专辑\",\"publishTime\":1000,\"artists\":[{\"id\":3,\"name\":\"歌手\"}]},\"songs\":[{\"id\":1,\"name\":\"第一首\"},{\"id\":2,\"name\":\"第二首\"}]}");
        var album = await client.GetAlbumAsync(4); Check(album.Tracks.Count == 2 && album.Artists[0].Id == 3 && album.PublishedAt.ToUnixTimeSeconds() == 1, "Album details preserve release date, artists and track order");
        fake.Bodies.Enqueue("{\"code\":200,\"hotAlbums\":[{\"id\":4,\"name\":\"专辑\",\"picUrl\":\"https://example.com/cover.jpg\"}],\"more\":true}");
        var albums = await client.GetArtistAlbumsAsync(3, 10, 10); Check(albums.HasMore && albums.Offset == 10 && albums.Items[0].Id == 4, "Artist album pagination preserves has-more and offset");
        fake.Bodies.Enqueue("{\"code\":200,\"playlists\":[{\"id\":5,\"name\":\"歌单\",\"creator\":{\"userId\":123},\"subscribed\":true}],\"total\":21}");
        var lists = await client.GetPlaylistsAsync("华语", 10, 0); Check(lists.Items[0].Creator.Id == 123 && lists.Items[0].Subscribed && (string)fake.LastPayload["cat"] == "华语", "Playlist category browsing preserves creator and collection state");
                  fake.Bodies.Enqueue("{\"code\":200,\"playlist\":{\"id\":5,\"trackCount\":2,\"trackIds\":[{\"id\":2},{\"id\":1}],\"tracks\":[{\"id\":1,\"name\":\"one\"},{\"id\":2,\"name\":\"two\"}]}}");
          var completePlaylist=await client.GetPlaylistAsync(5);var completeTracks=await client.GetAllPlaylistTracksAsync(completePlaylist);
          Check(completeTracks.Select(t=>t.Id).SequenceEqual(new long[]{2,1})&&fake.Bodies.Count==0&&fake.LastUri.AbsolutePath=="/weapi/v6/playlist/detail","Complete playlist reuses detail tracks without redundant requests");
          var largePlaylist=new Playlist {TrackIds=Enumerable.Range(1,205).Select(i=>(long)i).ToList(),Tracks=new List<Track>{new Track{Id=1,Name="embedded"}}};
          for(int offset=2;offset<=205;offset+=100)fake.Bodies.Enqueue(new JObject {["code"]=200,["songs"]=new JArray(Enumerable.Range(offset,Math.Min(100,206-offset)).Reverse().Select(i=>new JObject {["id"]=i,["name"]="song"+i}))}.ToString());
          var largeTracks=await client.GetAllPlaylistTracksAsync(largePlaylist);
          Check(largeTracks.Count==205&&largeTracks.Select(t=>t.Id).SequenceEqual(largePlaylist.TrackIds)&&fake.Bodies.Count==0,"Complete large playlist fetches every missing batch in original order");
          using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{await client.GetAllPlaylistTracksAsync(largePlaylist,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(true,"Complete playlist respects cancellation before reusing detail");}}

queue.Replace(new[]{new Track{Id=1},new Track{Id=4}});queue.InsertNext(new[]{new Track{Id=2},new Track{Id=3}});queue.Append(new[]{new Track{Id=5}});Check(queue.Current.Id==1&&queue.Tracks.Select(t=>t.Id).SequenceEqual(new long[]{1,2,3,4,5}),"Batch queue insert/append preserves current track and order");
        fake.Bodies.Enqueue("{\"code\":200,\"comments\":[{\"commentId\":8000000000,\"content\":\"雨\",\"user\":{\"userId\":123,\"nickname\":\"听众\"},\"time\":1000,\"likedCount\":5}],\"hotComments\":[],\"total\":40,\"more\":true}");
        var comments=await client.GetCommentsAsync(1,10,20);Check(comments.Items[0].Id==8000000000&&comments.Items[0].LikeCount==5&&comments.HasMore&&(int)fake.LastPayload["offset"]==20,"Comment pagination normalizes 64-bit IDs and likes");
        fake.Bodies.Enqueue("{\"code\":512}");fake.Bodies.Enqueue("{\"code\":200}");await client.AddTrackToPlaylistAsync(5,1);Check(JArray.Parse((string)fake.LastPayload["trackIds"]).Count==2&&(long)fake.LastPayload["pid"]==5,"Playlist add handles upstream 512 with duplicate ID retry");
        fake.Bodies.Enqueue("{\"code\":200}");fake.Bodies.Enqueue("{\"code\":200}");await client.AddTracksToPlaylistAsync(5,Enumerable.Range(1,101).Select(i=>(long)i));Check(JArray.Parse((string)fake.LastPayload["trackIds"]).Single().Value<long>()==101,"Batch playlist collection splits at 100 tracks");
        fake.Bodies.Enqueue("{\"code\":200}");await client.SetLikedAsync(1,false);Check(!(bool)fake.LastPayload["like"]&&(long)fake.LastPayload["trackId"]==1,"Unlike sends false without changing track identity");
        fake.Bodies.Enqueue("{\"code\":200}");await client.PostCommentAsync(1," 雨声 ");Check(fake.LastUri.AbsolutePath=="/eapi/resource/comments/add"&&(string)fake.LastPayload["content"]=="雨声"&&(string)fake.LastPayload["threadId"]=="R_SO_4_1","EAPI comment encrypts signed UTF-8 payload and preserves thread");
        fake.Bodies.Enqueue("{\"code\":200}");await client.PostCommentAsync(1,"回复",99);Check(fake.LastUri.AbsolutePath.EndsWith("/reply")&&(long)fake.LastPayload["commentId"]==99,"Comment reply targets original comment");
        fake.Bodies.Enqueue("{\"code\":200}");await client.SetCommentLikedAsync(1,99,false);Check(fake.LastUri.AbsolutePath=="/weapi/v1/comment/unlike"&&(long)fake.LastPayload["commentId"]==99,"Comment unlike uses the correct comment endpoint");
        fake.Bodies.Enqueue("{\"code\":301}"); await ExpectApi(() => client.GetLikedTrackIdsAsync(), 301, "Expired login is surfaced");
        Check(!client.IsLoggedIn && await store.LoadAsync() == null, "Expired login removes persisted credentials");
        using (var cts = new CancellationTokenSource()) { cts.Cancel(); try { await client.SearchTracksAsync("x",ct:cts.Token); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { Check(true, "Caller cancellation is preserved"); } }
        Console.WriteLine($"All {count} checks passed.");
        if (args.Contains("--live")) {
            using var live = new NeteaseClient(new LiveCrypto(), new MemorySessionStore());
            try { var page = await live.SearchTracksAsync("晴天", 3); Check(page.Items.Count > 0, "LIVE: direct public NetEase search"); Console.WriteLine("Live tracks returned: " + page.Items.Count);
                var song = page.Items[0];
                if (song.Artists.Count > 0) { var liveArtist = await live.GetArtistAsync(song.Artists[0].Id); Check(liveArtist.HotTracks.Count > 0, "LIVE: artist hot tracks"); }
                if (song.Album.Id > 0) { var liveAlbum = await live.GetAlbumAsync(song.Album.Id); Check(liveAlbum.Tracks.Count > 0, "LIVE: album detail"); }
                var liveComments=await live.GetCommentsAsync(song.Id,5);Check(liveComments.Items.Count>0||liveComments.Hot.Count>0,"LIVE: actual song comments");
                var lyric = await live.GetLyricsAsync(song.Id); Check(lyric.Lines.Count > 0, "LIVE: timed lyrics");
                var qr = await live.BeginQrLoginAsync(); Check(!string.IsNullOrEmpty(qr.Key), "LIVE: QR login challenge (no user login performed)"); }
            catch (MusicApiException e) { Console.Error.WriteLine($"LIVE failed: {e.Code} {e.Message}"); Environment.ExitCode = 2; }
        }
    }
    private sealed class LiveCrypto : ICryptoProvider {
        public byte[] Md5(byte[] bytes){using var hash=MD5.Create();return hash.ComputeHash(bytes);}
    public byte[] AesEcbPkcs7(byte[] input,byte[] key){using var aes=Aes.Create();aes.Key=key;aes.Mode=CipherMode.ECB;return aes.CreateEncryptor().TransformFinalBlock(input,0,input.Length);}
    public static string DecryptEapi(string hex){using var aes=Aes.Create();aes.Key=Encoding.UTF8.GetBytes("e82ckenh8dichen8");aes.Mode=CipherMode.ECB;var bytes=Convert.FromHexString(hex);return Encoding.UTF8.GetString(aes.CreateDecryptor().TransformFinalBlock(bytes,0,bytes.Length));}
    public byte[] RandomBytes(int length) => RandomNumberGenerator.GetBytes(length);
        public byte[] AesCbcPkcs7(byte[] input, byte[] key, byte[] iv) => new DesktopCrypto().AesCbcPkcs7(input, key, iv);
    }
}





