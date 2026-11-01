using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Petrichor.Core;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
namespace Petrichor.Uwp
{
    public sealed class PlaybackErrorEventArgs : EventArgs { public Exception Error { get; } public PlaybackErrorEventArgs(Exception error) { Error = error; } }
    /// <summary>Native single-process player. Add backgroundMediaPlayback to the host UWP app manifest.</summary>
    public sealed class PlaybackService : IDisposable
    {
        private readonly NeteaseClient api;
        private readonly LibraryStore history;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private CancellationTokenSource sleepTimer;
        private MediaPlaybackItem currentItem;
        private int revision;
        private bool disposed;
        public MediaPlayer Player { get; }
        public PlaybackQueue Queue { get; } = new PlaybackQueue();
        public AudioQuality Quality { get; set; } = AudioQuality.Standard;
        public StreamInfo CurrentStream { get; private set; }
        public Func<Track,Task<Windows.Storage.StorageFile>> LocalFileResolver { get; set; }
        public event EventHandler TrackChanged;
        public event EventHandler<PlaybackErrorEventArgs> Error;
        public PlaybackService(NeteaseClient api, LibraryStore history = null) {
            this.api = api ?? throw new ArgumentNullException(nameof(api)); this.history = history;
            Player = new MediaPlayer { AudioCategory = MediaPlayerAudioCategory.Media };
            // Keep the default SMTC integration enabled, including system play/pause and background playback.
            Player.CommandManager.IsEnabled = true;
            Player.CommandManager.NextBehavior.EnablingRule = MediaCommandEnablingRule.Always;
            Player.CommandManager.PreviousBehavior.EnablingRule = MediaCommandEnablingRule.Always;
            Player.CommandManager.NextReceived += OnNext;
            Player.CommandManager.PreviousReceived += OnPrevious;
            Player.MediaEnded += OnEnded; Player.MediaFailed += OnFailed;
        }
        public async Task PlayAsync(IEnumerable<Track> tracks, int start = 0, CancellationToken ct = default(CancellationToken)) {
            ThrowIfDisposed(); var expected = Interlocked.Increment(ref revision);
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token)) {
                await gate.WaitAsync(linked.Token);
                try { if (expected != revision) return; Player.Pause(); Queue.Replace(tracks, start); if (Queue.Current == null) { ClearSource(); return; } await LoadCurrentAsync(expected, linked.Token); }
                finally { gate.Release(); }
            }
        }
        private async Task LoadCurrentAsync(int expected, CancellationToken ct) {
            var track = Queue.Current; if (track == null) return;
            StreamInfo stream=null; MediaSource source=null;
            try {
                var file=LocalFileResolver==null?null:await LocalFileResolver(track);
                ct.ThrowIfCancellationRequested();
                if(file!=null)source=MediaSource.CreateFromStorageFile(file);
                else { if(track.Id<=0)throw new MusicApiException(404,"Local file unavailable"); stream=await api.GetStreamAsync(track.Id,Quality,ct);ct.ThrowIfCancellationRequested();source=MediaSource.CreateFromUri(stream.Url); }
            }
            catch { if (expected == revision && !disposed) { ClearSource(); TrackChanged?.Invoke(this, EventArgs.Empty); } throw; }
            if (expected != revision || disposed) return;
            // Fetch a new URL for every load; signed playback URLs are never persisted.
            var item = new MediaPlaybackItem(source);
            var properties = item.GetDisplayProperties(); properties.Type = MediaPlaybackType.Music;
            properties.MusicProperties.Title = track.Name; properties.MusicProperties.Artist = track.ArtistText;
            properties.MusicProperties.AlbumTitle = track.Album.Name; item.ApplyDisplayProperties(properties);
            var old = currentItem; currentItem = item; CurrentStream = stream; Player.Source = item; old?.Source.Dispose();
            Player.Play(); TrackChanged?.Invoke(this, EventArgs.Empty);
            if (history != null) { try { await history.RecordAsync(track); } catch (Exception e) { Error?.Invoke(this, new PlaybackErrorEventArgs(e)); } }
        }
        private async Task MoveAsync(bool forward, bool automatic) {
            ThrowIfDisposed(); var expected = revision; await gate.WaitAsync(lifetime.Token);
            try {
                if (expected != revision) return;
                bool moved = forward ? Queue.MoveNext(!automatic) : Queue.MovePrevious();
                if (!moved) { if (automatic) Player.Pause(); return; }
                Player.Pause(); await LoadCurrentAsync(expected, lifetime.Token);
            } finally { gate.Release(); }
        }
        public Task NextAsync() => MoveAsync(true, false);
        public Task PreviousAsync() => MoveAsync(false, false);
        public void Pause() { ThrowIfDisposed(); Player.Pause(); }
        public async Task ResumeAsync() {
            ThrowIfDisposed();
            if ((currentItem == null && Queue.Current != null) || (CurrentStream != null && CurrentStream.ExpiresAt <= DateTimeOffset.UtcNow)) { var position = currentItem == null ? TimeSpan.Zero : Player.PlaybackSession.Position; var expected = revision; await gate.WaitAsync(lifetime.Token); try { if (expected != revision) return; await LoadCurrentAsync(expected, lifetime.Token); Player.PlaybackSession.Position = position; } finally { gate.Release(); } }
            else if (currentItem != null) Player.Play();
        }
        public void Seek(TimeSpan position) { ThrowIfDisposed(); var duration = Player.PlaybackSession.NaturalDuration; if (position < TimeSpan.Zero) position = TimeSpan.Zero; if (duration > TimeSpan.Zero && position > duration) position = duration; Player.PlaybackSession.Position = position; }
        public void SetSleepTimer(TimeSpan? duration) {
            ThrowIfDisposed(); sleepTimer?.Cancel(); sleepTimer?.Dispose(); sleepTimer = null;
            if (!duration.HasValue) return; if (duration.Value <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
            sleepTimer = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); RunSleepTimer(duration.Value, sleepTimer.Token);
        }
        private async void RunSleepTimer(TimeSpan duration, CancellationToken ct) { try { await Task.Delay(duration, ct); if (!disposed) Player.Pause(); } catch (OperationCanceledException) { } }
        private async void OnNext(MediaPlaybackCommandManager sender, MediaPlaybackCommandManagerNextReceivedEventArgs args) { args.Handled = true; await HandleMoveAsync(true, false); }
        private async void OnPrevious(MediaPlaybackCommandManager sender, MediaPlaybackCommandManagerPreviousReceivedEventArgs args) { args.Handled = true; await HandleMoveAsync(false, false); }
        private async void OnEnded(MediaPlayer sender, object args) { await HandleMoveAsync(true, true); }
        private async Task HandleMoveAsync(bool forward, bool automatic) { try { await MoveAsync(forward, automatic); } catch (OperationCanceledException) { } catch (Exception e) { Error?.Invoke(this, new PlaybackErrorEventArgs(e)); } }
        private void OnFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args) { if (!disposed) Error?.Invoke(this, new PlaybackErrorEventArgs(new MusicApiException(503, "音频播放失败：" + args.ErrorMessage))); }
        private void ClearSource() { Player.Source = null; currentItem?.Source.Dispose(); currentItem = null; CurrentStream = null; }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(PlaybackService)); }
        public void Dispose() {
            if (disposed) return; disposed = true; Interlocked.Increment(ref revision); lifetime.Cancel(); sleepTimer?.Cancel(); sleepTimer?.Dispose();
            Player.MediaEnded -= OnEnded; Player.MediaFailed -= OnFailed; Player.CommandManager.NextReceived -= OnNext; Player.CommandManager.PreviousReceived -= OnPrevious;
            Player.Pause(); ClearSource(); Player.Dispose(); lifetime.Dispose();
        }
    }
}



