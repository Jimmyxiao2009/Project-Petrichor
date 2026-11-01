using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
namespace Petrichor.Core
{
    public sealed class ArtistDetails {
        public long Id { get; set; } public string Name { get; set; } public string CoverUrl { get; set; }
        public string Biography { get; set; } public int SongCount { get; set; } public int AlbumCount { get; set; }
        public List<Track> HotTracks { get; set; }
    }
    public sealed class AlbumDetails { public Album Album { get; set; } public List<Artist> Artists { get; set; } public string Description { get; set; } public DateTimeOffset PublishedAt { get; set; } public List<Track> Tracks { get; set; } }
    public sealed class BiographySection { public string Title { get; set; } public string Content { get; set; } }
    public sealed class ArtistBiography { public string Summary { get; set; } public List<BiographySection> Sections { get; set; } }
    public sealed class CollectionPage<T> { public List<T> Items { get; set; } public bool HasMore { get; set; } public int Offset { get; set; } }
    public sealed partial class NeteaseClient
    {
        private static Album ParseAlbum(JToken a) => new Album { Id = (long?)a["id"] ?? 0, Name = (string)a["name"] ?? "", CoverUrl = (string)(a["picUrl"] ?? a["blurPicUrl"]) ?? "" };
        private static Artist ParseArtist(JToken a) => new Artist { Id = (long?)a["id"] ?? 0, Name = (string)a["name"] ?? "" };
        public async Task<SearchPage<Artist>> SearchArtistsAsync(string query, int limit = 30, int offset = 0, CancellationToken ct = default(CancellationToken)) {
            var j = await SearchAsync(query, 100, limit, offset, ct).ConfigureAwait(false); var r = j["result"];
            return new SearchPage<Artist> { Items = (r?["artists"] ?? new JArray()).Select(ParseArtist).ToList(), Total = (int?)r?["artistCount"] ?? 0, Offset = offset, Limit = limit };
        }
        public async Task<SearchPage<Album>> SearchAlbumsAsync(string query, int limit = 30, int offset = 0, CancellationToken ct = default(CancellationToken)) {
            var j = await SearchAsync(query, 10, limit, offset, ct).ConfigureAwait(false); var r = j["result"];
            return new SearchPage<Album> { Items = (r?["albums"] ?? new JArray()).Select(ParseAlbum).ToList(), Total = (int?)r?["albumCount"] ?? 0, Offset = offset, Limit = limit };
        }
        public async Task<ArtistDetails> GetArtistAsync(long id, CancellationToken ct = default(CancellationToken)) {
            var j = await RequestAsync("v1/artist/" + ValidId(id), Data(), ct).ConfigureAwait(false); var a = j["artist"];
            if (a == null || a.Type == JTokenType.Null) throw new MusicApiException(404, "歌手不存在");
            return new ArtistDetails { Id = (long?)a["id"] ?? id, Name = (string)a["name"] ?? "", CoverUrl = (string)(a["picUrl"] ?? a["img1v1Url"]) ?? "", Biography = (string)a["briefDesc"] ?? "", SongCount = (int?)a["musicSize"] ?? 0, AlbumCount = (int?)a["albumSize"] ?? 0, HotTracks = (j["hotSongs"] ?? new JArray()).Select(Track.From).ToList() };
        }
        public async Task<ArtistBiography> GetArtistBiographyAsync(long id, CancellationToken ct = default(CancellationToken)) {
            var j = await RequestAsync("artist/introduction", Data("id", ValidId(id)), ct).ConfigureAwait(false);
            return new ArtistBiography { Summary = (string)j["briefDesc"] ?? "", Sections = (j["introduction"] ?? new JArray()).Select(x => new BiographySection { Title = (string)x["ti"] ?? "", Content = (string)x["txt"] ?? "" }).ToList() };
        }
        public async Task<CollectionPage<Album>> GetArtistAlbumsAsync(long id, int limit = 30, int offset = 0, CancellationToken ct = default(CancellationToken)) {
            Page(limit, offset); var j = await RequestAsync("artist/albums/" + ValidId(id), Data("limit", limit, "offset", offset, "total", true), ct).ConfigureAwait(false);
            return new CollectionPage<Album> { Items = (j["hotAlbums"] ?? new JArray()).Select(ParseAlbum).ToList(), HasMore = (bool?)j["more"] ?? false, Offset = offset };
        }
        public async Task<AlbumDetails> GetAlbumAsync(long id, CancellationToken ct = default(CancellationToken)) {
            var j = await RequestAsync("v1/album/" + ValidId(id), Data(), ct).ConfigureAwait(false); var a = j["album"];
            if (a == null || a.Type == JTokenType.Null) throw new MusicApiException(404, "专辑不存在");
            return new AlbumDetails { Album = ParseAlbum(a), Artists = (a["artists"] ?? new JArray()).Select(ParseArtist).ToList(), Description = (string)a["description"] ?? "", PublishedAt = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMilliseconds((long?)a["publishTime"] ?? 0), Tracks = (j["songs"] ?? new JArray()).Select(Track.From).ToList() };
        }
        public async Task<SearchPage<Playlist>> GetPlaylistsAsync(string category = "全部", int limit = 30, int offset = 0, CancellationToken ct = default(CancellationToken)) {
            Page(limit, offset); if (string.IsNullOrWhiteSpace(category) || category.Length > 30) throw new ArgumentException("无效的歌单分类", nameof(category));
            var j = await RequestAsync("playlist/list", Data("cat", category, "order", "hot", "limit", limit, "offset", offset, "total", true), ct).ConfigureAwait(false);
            return new SearchPage<Playlist> { Items = (j["playlists"] ?? new JArray()).Select(Playlist.From).ToList(), Total = (int?)j["total"] ?? 0, Limit = limit, Offset = offset };
        }
        public async Task<CollectionPage<Artist>> GetSavedArtistsAsync(int limit = 30, int offset = 0, CancellationToken ct = default(CancellationToken)) {
            RequireLogin(); Page(limit, offset); var j = await RequestAsync("artist/sublist", Data("limit", limit, "offset", offset, "total", true), ct).ConfigureAwait(false);
            return new CollectionPage<Artist> { Items = (j["data"] ?? new JArray()).Select(ParseArtist).ToList(), HasMore = (bool?)j["hasMore"] ?? false, Offset = offset };
        }
        public async Task<CollectionPage<Album>> GetSavedAlbumsAsync(int limit = 30, int offset = 0, CancellationToken ct = default(CancellationToken)) {
            RequireLogin(); Page(limit, offset); var j = await RequestAsync("album/sublist", Data("limit", limit, "offset", offset, "total", true), ct).ConfigureAwait(false);
            return new CollectionPage<Album> { Items = (j["data"] ?? new JArray()).Select(ParseAlbum).ToList(), HasMore = (bool?)j["hasMore"] ?? false, Offset = offset };
        }
        public async Task SetArtistSavedAsync(long id, bool saved, CancellationToken ct = default(CancellationToken)) { RequireLogin(); await RequestAsync("artist/" + (saved ? "sub" : "unsub"), Data("artistId", ValidId(id), "artistIds", "[" + id + "]"), ct).ConfigureAwait(false); }
        public async Task SetAlbumSavedAsync(long id, bool saved, CancellationToken ct = default(CancellationToken)) { RequireLogin(); await RequestAsync("album/" + (saved ? "sub" : "unsub"), Data("id", ValidId(id)), ct).ConfigureAwait(false); }
    }
}
