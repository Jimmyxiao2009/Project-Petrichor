using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Petrichor.Core
{
    public sealed class MusicApiException : Exception
    {
        public int Code { get; }
        public MusicApiException(int code, string message, Exception innerException = null) : base(message, innerException) { Code = code; }
        public bool RequiresLogin => Code == 301 || Code == 302 || Code == 401;
    }
    public sealed class Artist { public long Id { get; set; } public string Name { get; set; } }
    public sealed class Album { public long Id { get; set; } public string Name { get; set; } public string CoverUrl { get; set; } }
    public sealed class Track
    {
        public long Id { get; set; } public string Name { get; set; }
        /// <summary>播客节目（dj/program）的主曲目会带上节目 id；普通歌曲为 0。</summary>
        public long ProgramId { get; set; }
        public string LocalFilePath { get; set; } public List<Artist> Artists { get; set; } = new List<Artist>();
        public Album Album { get; set; } = new Album();
        public int DurationMs { get; set; } public int Fee { get; set; }
        public string ArtistText => string.Join(" / ", Artists.Select(a => a.Name));
        internal static Track From(JToken t) {
            var a = t["al"] ?? t["album"];
            return new Track { Id = (long?)t["id"] ?? 0, Name = (string)t["name"] ?? "",
                Artists = (t["ar"] ?? t["artists"] ?? new JArray()).Select(x => new Artist { Id = (long?)x["id"] ?? 0, Name = (string)x["name"] ?? "" }).ToList(),
                Album = new Album { Id = (long?)a?["id"] ?? 0, Name = (string)a?["name"] ?? "", CoverUrl = (string)a?["picUrl"] ?? "" },
                DurationMs = (int?)(t["dt"] ?? t["duration"]) ?? 0, Fee = (int?)t["fee"] ?? 0 };
        }
    }
    public sealed class UserProfile {
        public long Id { get; set; } public string Nickname { get; set; } public string AvatarUrl { get; set; }
        internal static UserProfile From(JToken p) => p == null || p.Type == JTokenType.Null ? null : new UserProfile { Id = (long?)p["userId"] ?? 0, Nickname = (string)p["nickname"] ?? "", AvatarUrl = (string)p["avatarUrl"] ?? "" };
    }
    public sealed class Playlist {
        public long Id { get; set; } public string Name { get; set; } public string CoverUrl { get; set; }
        public UserProfile Creator { get; set; } public bool Subscribed { get; set; }
        /// <summary>5 = 用户的「我喜欢的音乐」歌单。</summary>
        public int SpecialType { get; set; }
        public string Description { get; set; } public int TrackCount { get; set; } public long PlayCount { get; set; }
        public List<long> TrackIds { get; set; } = new List<long>(); public List<Track> Tracks { get; set; } = new List<Track>();
        internal static Playlist From(JToken p) => new Playlist { Id = (long?)p["id"] ?? 0, Name = (string)p["name"] ?? "", CoverUrl = (string)(p["coverImgUrl"] ?? p["picUrl"]) ?? "", Creator = UserProfile.From(p["creator"]), Subscribed = (bool?)p["subscribed"] ?? false, SpecialType = (int?)p["specialType"] ?? 0, Description = (string)p["description"] ?? "", TrackCount = (int?)p["trackCount"] ?? 0, PlayCount = (long?)p["playCount"] ?? 0, Tracks = (p["tracks"] ?? new JArray()).Select(Track.From).ToList(), TrackIds = (p["trackIds"] ?? new JArray()).Select(x => (long)x["id"]).ToList() };
    }
    public sealed class SearchPage<T> { public List<T> Items { get; set; } public int Total { get; set; } public int Offset { get; set; } public int Limit { get; set; } public bool HasMore => Offset + Items.Count < Total; }
    public enum AudioQuality { Standard = 128000, High = 192000, VeryHigh = 320000, Lossless = 999000, HiRes = 1900000 }
    public sealed class StreamInfo { public long TrackId { get; set; } public Uri Url { get; set; } public int Bitrate { get; set; } public string Format { get; set; } public DateTimeOffset ExpiresAt { get; set; } public bool IsPreview { get; set; } public JObject Trial { get; set; } }
    public enum QrLoginState { Expired = 800, Waiting = 801, Scanned = 802, Authorized = 803 }
    public sealed class QrLoginChallenge { public string Key { get; internal set; } public Uri LoginUrl { get; internal set; } public DateTimeOffset ExpiresAt { get; internal set; } internal DateTimeOffset NextPollAt { get; set; } }
    public sealed class Lyrics { public string Original { get; set; } public string Translation { get; set; } public List<LyricLine> Lines { get; set; } public List<LyricLine> TranslatedLines { get; set; } }
}




