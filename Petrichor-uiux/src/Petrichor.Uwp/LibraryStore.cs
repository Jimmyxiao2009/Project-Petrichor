using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Petrichor.Core;
using Windows.Storage;
namespace Petrichor.Uwp
{
    public sealed class RecentPlay { public Track Track { get; set; } public DateTimeOffset PlayedAt { get; set; } }
    /// <summary>Bounded local history, kept on device and independent of account credentials.</summary>
    public sealed class LibraryStore
    {
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private const string FileName = "recent-plays.json";
        private async Task<List<RecentPlay>> ReadInternalAsync() {
            var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileName) as StorageFile;
            if (item == null) return new List<RecentPlay>();
            try { return JsonConvert.DeserializeObject<List<RecentPlay>>(await FileIO.ReadTextAsync(item)) ?? new List<RecentPlay>(); }
            catch (JsonException) { return new List<RecentPlay>(); }
        }
        public async Task<List<RecentPlay>> ReadAsync() { await gate.WaitAsync(); try { return await ReadInternalAsync(); } finally { gate.Release(); } }
        public async Task RecordAsync(Track track) {
            if (track == null) throw new ArgumentNullException(nameof(track)); await gate.WaitAsync();
            try {
                var items = await ReadInternalAsync(); items.RemoveAll(x => x.Track == null || x.Track.Id == track.Id);
                items.Insert(0, new RecentPlay { Track = track, PlayedAt = DateTimeOffset.UtcNow });
                var pending = await ApplicationData.Current.LocalFolder.CreateFileAsync(FileName + ".pending", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(pending, JsonConvert.SerializeObject(items.Take(100)));
                await pending.RenameAsync(FileName, NameCollisionOption.ReplaceExisting);
            } finally { gate.Release(); }
        }
        public async Task ClearAsync() { await gate.WaitAsync(); try { var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileName); if (item != null) await item.DeleteAsync(); } finally { gate.Release(); } }
    }
}
