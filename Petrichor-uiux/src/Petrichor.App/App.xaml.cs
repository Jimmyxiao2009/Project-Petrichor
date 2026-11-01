using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Petrichor.Core;
using Petrichor.Uwp;
namespace Petrichor.App
{
 public sealed partial class App : Application {
  public static NeteaseClient Api { get; private set; }
  public static PlaybackService Playback { get; private set; }
  public static LibraryStore History { get; private set; }
  public static OfflineLibrary Offline { get; private set; }
  internal static bool IsDiagnosticsRunning { get; set; }
  public static event EventHandler AccountChanged;
  public static void NotifyAccountChanged() => AccountChanged?.Invoke(null, EventArgs.Empty);
  public App() { InitializeComponent(); Suspending += OnSuspending; EnteredBackground += (s,e) => Shell.Current?.SetBackground(true); LeavingBackground += (s,e) => Shell.Current?.SetBackground(false); UnhandledException += OnUnhandled; }
  private void EnsureWindow() {
   if (Api == null) { Api = new NeteaseClient(new WindowsCryptoProvider(), new VaultSessionStore()); History = new LibraryStore(); Offline = new OfflineLibrary(Api); Offline.Changed+=(s,e)=>MusicItem.RefreshCacheBadgesAsync(); Playback = new PlaybackService(Api, History) {LocalFileResolver=Offline.FindAudioAsync}; var settings=ApplicationData.Current.LocalSettings.Values; if(settings.ContainsKey("quality")) Playback.Quality=(AudioQuality)(int)settings["quality"]; if(settings.ContainsKey("repeat")) Playback.Queue.Repeat=(RepeatMode)(int)settings["repeat"]; if(settings.ContainsKey("shuffle")) Playback.Queue.Shuffle=(bool)settings["shuffle"]; }
   if (!(Window.Current.Content is Shell)) Window.Current.Content = new Shell();
   ApplicationView.PreferredLaunchViewSize = new Size(420,800); ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
   var view = ApplicationView.GetForCurrentView(); view.SetPreferredMinSize(new Size(360,540)); view.TryResizeView(new Size(420,800));
   view.TitleBar.BackgroundColor = Color.FromArgb(255,8,8,8); view.TitleBar.ForegroundColor = Colors.White; view.TitleBar.ButtonBackgroundColor = Color.FromArgb(255,8,8,8); view.TitleBar.ButtonForegroundColor = Colors.White;
   Window.Current.Activate();
  }
  protected override void OnLaunched(LaunchActivatedEventArgs e) { if(!e.PrelaunchActivated) EnsureWindow(); }
  protected override async void OnActivated(IActivatedEventArgs args) { EnsureWindow(); var p = args as ProtocolActivatedEventArgs; 
#if DEBUG
 if (p?.Uri.Host == "test") await RuntimeTests.RunAsync();
 if (p?.Uri.Host == "navigation-test") await NavigationTests.RunAsync();
#endif
 }
  private void OnSuspending(object sender, SuspendingEventArgs e) { /* Playback remains alive; history writes are already awaited. */ }
  private async void OnUnhandled(object sender, UnhandledExceptionEventArgs e) { e.Handled=true; Shell.Current?.Notify("出现错误，请重试当前操作。"); await LogAsync(e.Exception); }
  public static async Task LogAsync(Exception error) { try { var file=await ApplicationData.Current.LocalFolder.CreateFileAsync("errors.log",CreationCollisionOption.OpenIfExists); await FileIO.AppendTextAsync(file, DateTimeOffset.UtcNow.ToString("o")+" "+error.GetType().Name+" "+error.HResult+Environment.NewLine); } catch { } }
  /// <summary>下载完成后移动到所选目录失败（例如 SD 卡已拔出）时提示用户，文件仍在应用内部存储。</summary>
  public static void NotifyDownloadFallback(string trackName){ try { Shell.Current?.Notify("无法移动到所选文件夹，已保存在应用内部存储："+trackName); } catch { } }
 }
}




