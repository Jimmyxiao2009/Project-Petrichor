using System;using System.Linq;using System.Collections.Generic;using System.Threading;using System.Threading.Tasks;using Petrichor.Core;using Windows.UI.Xaml;using Windows.UI.Xaml.Controls;using Windows.UI.Xaml.Navigation;using Windows.Storage;namespace Petrichor.App.Pages {public sealed partial class SettingsPage : MusicPage {public SettingsPage(){InitializeComponent();}
private bool ready;
protected override async Task LoadAsync(object p){Quality.SelectedIndex=App.Playback.Quality==AudioQuality.HiRes?4:App.Playback.Quality==AudioQuality.Lossless?3:App.Playback.Quality==AudioQuality.VeryHigh?2:App.Playback.Quality==AudioQuality.High?1:0;Repeat.SelectedIndex=(int)App.Playback.Queue.Repeat;Shuffle.IsOn=App.Playback.Queue.Shuffle;SleepStatus.Text=SleepTimerMenu.Describe();ready=true;await UpdateFolderAsync();await UpdateCacheAsync();}
protected override void OnNavigatedTo(NavigationEventArgs e){PendingLogic.SleepTimerChanged+=OnSleepChanged;base.OnNavigatedTo(e);}
protected override void OnNavigatedFrom(NavigationEventArgs e){PendingLogic.SleepTimerChanged-=OnSleepChanged;base.OnNavigatedFrom(e);}
private void OnSleepChanged(object s,EventArgs e)=>SleepStatus.Text=SleepTimerMenu.Describe();
private void SleepClick(object s,RoutedEventArgs e)=>SleepTimerMenu.ShowAt((FrameworkElement)s);
private async Task UpdateFolderAsync(){
 var folder=await PendingLogic.GetDownloadFolderAsync();bool custom=PendingLogic.HasCustomDownloadFolder;
 FolderPath.Text=folder!=null?(string.IsNullOrEmpty(folder.Path)?folder.DisplayName:folder.Path):custom?"所选文件夹不可用（SD 卡是否已拔出？）":"应用内部存储（默认）";
 ResetFolder.Visibility=custom?Visibility.Visible:Visibility.Collapsed;
 int pending=await PendingLogic.CountDownloadsOutsideFolderAsync();
 // 目标目录不可用时（如 SD 卡已拔出）不提供迁移，避免整批失败
 bool canMigrate=pending>0&&(folder!=null||!custom);
 MigrateFolder.Visibility=canMigrate?Visibility.Visible:Visibility.Collapsed;
 FolderHint.Text=canMigrate
  ?"有 "+pending+" 首已下载歌曲不在当前文件夹，可迁移到「"+(folder==null?"应用内部存储":folder.DisplayName)+"」。迁移前请先暂停播放。"
  :"可以选择手机或 SD 卡上的任意文件夹。已下载的歌曲会保存在这里。";
}
private async void ChooseFolderClick(object s,RoutedEventArgs e){
 var picker=new Windows.Storage.Pickers.FolderPicker{SuggestedStartLocation=Windows.Storage.Pickers.PickerLocationId.MusicLibrary,ViewMode=Windows.Storage.Pickers.PickerViewMode.List};
 picker.FileTypeFilter.Add("*");
 var folder=await picker.PickSingleFolderAsync();if(folder==null)return;
 await ApplyFolderAsync(folder);
}
private async void ResetFolderClick(object s,RoutedEventArgs e)=>await ApplyFolderAsync(null);
/// <summary>切换下载目录；已有下载在别处时询问是否一起迁移。</summary>
private async Task ApplyFolderAsync(StorageFolder folder){
 var target=folder==null?"应用内部存储":folder.DisplayName;
 int pending=await PendingLogic.CountDownloadsOutsideFolderAsync();
 if(pending>0){
  var dialog=new ContentDialog {Title="切换下载位置？",Content="新的下载会保存到「"+target+"」。已有 "+pending+" 首已下载歌曲，可以一起迁移过去。迁移前请先暂停播放。",PrimaryButtonText="切换并迁移",SecondaryButtonText="仅切换",CloseButtonText="取消"};
  var result=await dialog.ShowAsync();
  if(result==ContentDialogResult.None)return;
  PendingLogic.SetDownloadFolder(folder);
  if(result==ContentDialogResult.Primary){
   await ExecuteAsync(async()=>{
    var migration=await PendingLogic.MigrateDownloadsAsync(folder);
    await UpdateFolderAsync();
    Shell.Current.Notify(MigrationMessage(migration,target));
   });
   return;
  }
  await UpdateFolderAsync();
  Shell.Current.Notify("新的下载会保存到「"+target+"」，已下载歌曲保留在原位置。");
  return;
 }
 PendingLogic.SetDownloadFolder(folder);
 await UpdateFolderAsync();
 Shell.Current.Notify(folder==null?"已恢复为应用内部存储。":"新的下载会保存到「"+folder.DisplayName+"」。");
}
private async void MigrateFolderClick(object s,RoutedEventArgs e){
 var folder=await PendingLogic.GetDownloadFolderAsync();
 var target=folder==null?"应用内部存储":folder.DisplayName;
 await ExecuteAsync(async()=>{
  var migration=await PendingLogic.MigrateDownloadsAsync(folder);
  await UpdateFolderAsync();
  Shell.Current.Notify(MigrationMessage(migration,target));
 });
}
private static string MigrationMessage(DownloadMigration migration,string target)=>
 migration.Moved==0&&migration.Failed==0?"没有需要迁移的歌曲。"
 :"已迁移 "+migration.Moved+" 首到「"+target+"」"+(migration.Failed>0?"，"+migration.Failed+" 首失败（可能正在播放）。":"。");
private async Task UpdateCacheAsync(){var bytes=await App.Offline.CacheSizeAsync();CacheStatus.Text=(bytes/1048576d).ToString("0.0")+" MB";} private async void ClearCacheClick(object s,RoutedEventArgs e)=>await ExecuteAsync(async()=>{await App.Offline.ClearCacheAsync();await UpdateCacheAsync();Shell.Current.Notify("缓存已清理。");}); private void QualityChanged(object s,SelectionChangedEventArgs e){if(!ready)return;App.Playback.Quality=Quality.SelectedIndex==4?AudioQuality.HiRes:Quality.SelectedIndex==3?AudioQuality.Lossless:Quality.SelectedIndex==2?AudioQuality.VeryHigh:Quality.SelectedIndex==1?AudioQuality.High:AudioQuality.Standard;ApplicationData.Current.LocalSettings.Values["quality"]=(int)App.Playback.Quality;}
private void RepeatChanged(object s,SelectionChangedEventArgs e){if(ready){App.Playback.Queue.Repeat=(RepeatMode)Repeat.SelectedIndex;ApplicationData.Current.LocalSettings.Values["repeat"]=Repeat.SelectedIndex;}}
private void ShuffleChanged(object s,RoutedEventArgs e){if(ready){App.Playback.Queue.Shuffle=Shuffle.IsOn;ApplicationData.Current.LocalSettings.Values["shuffle"]=Shuffle.IsOn;}}}}

