using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
namespace Petrichor.Core
{
    public sealed class LyricLine { public TimeSpan Position { get; set; } public string Text { get; set; } }
    public static class LrcParser
    {
        public static List<LyricLine> Parse(string text)
        {
            var list = new List<LyricLine>(); long offset = 0;
            var offsetMatch = Regex.Match(text ?? "", @"\[offset:([+-]?\d+)\]", RegexOptions.IgnoreCase);
            if (offsetMatch.Success) long.TryParse(offsetMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out offset);
            foreach (var row in (text ?? "").Split('\n')) {
                var words = Regex.Replace(row, @"\[[^\]]*\]", "").Trim();
                foreach (Match m in Regex.Matches(row, @"\[(\d+):(\d{1,2})(?:\.(\d{1,3}))?\]")) {
                    var minutes = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    var seconds = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                    if (seconds >= 60) continue;
                    var fraction = m.Groups[3].Value.PadRight(3, '0');
                    var ms = Math.Max(0, minutes * 60000 + seconds * 1000 + int.Parse(fraction) + offset);
                    if (ms <= TimeSpan.MaxValue.TotalMilliseconds) list.Add(new LyricLine { Position = TimeSpan.FromMilliseconds(ms), Text = words });
                }
            }
            // Stable ordering preserves translations or repeated timestamps in the source.
            return System.Linq.Enumerable.ToList(System.Linq.Enumerable.OrderBy(list, x => x.Position));
        }
        public static int FindActive(IReadOnlyList<LyricLine> lines, TimeSpan position) {
            int lo = 0, hi = lines.Count - 1, result = -1;
            while (lo <= hi) { int mid = lo + (hi - lo) / 2; if (lines[mid].Position <= position) { result = mid; lo = mid + 1; } else hi = mid - 1; } return result;
        }
    }
    public enum RepeatMode { Off, All, One }
    public sealed class PlaybackQueue
    {
        private readonly List<Track> tracks = new List<Track>(); private readonly Random random = new Random();
        public IReadOnlyList<Track> Tracks => tracks.AsReadOnly(); public int Index { get; private set; } = -1;
        public Track Current => Index >= 0 && Index < tracks.Count ? tracks[Index] : null;
        public RepeatMode Repeat { get; set; } public bool Shuffle { get; set; }
        public void Replace(IEnumerable<Track> items, int start = 0) {
            if (items == null) throw new ArgumentNullException(nameof(items)); var next = new List<Track>(items);
            if (next.Count != 0 && (start < 0 || start >= next.Count)) throw new ArgumentOutOfRangeException(nameof(start));
            tracks.Clear(); tracks.AddRange(next); Index = tracks.Count == 0 ? -1 : start;
        }
        public void Append(IEnumerable<Track> items){if(items==null)throw new ArgumentNullException(nameof(items));tracks.AddRange(items);if(Index<0&&tracks.Count>0)Index=0;}
        public void InsertNext(IEnumerable<Track> items){if(items==null)throw new ArgumentNullException(nameof(items));tracks.InsertRange(Math.Max(0,Index+1),items);if(Index<0&&tracks.Count>0)Index=0;}
        public bool MoveNext(bool userInitiated = false) {
            if (tracks.Count == 0) return false; if (Repeat == RepeatMode.One && !userInitiated) return true;
            if (Shuffle && tracks.Count > 1) { Index = (Index + 1 + random.Next(tracks.Count - 1)) % tracks.Count; return true; }
            if (Index + 1 < tracks.Count) { Index++; return true; }
            if (Repeat == RepeatMode.All) { Index = 0; return true; } return false;
        }
        public bool MovePrevious() { if (tracks.Count == 0) return false; if (Index > 0) { Index--; return true; } if (Repeat == RepeatMode.All) { Index = tracks.Count - 1; return true; } return false; }
    }
}

