using System;using System.Collections.Generic;using System.Linq;using System.Threading;using System.Threading.Tasks;using Petrichor.Core;using Windows.UI.Xaml.Controls;namespace Petrichor.App {
 internal static class PlaylistChooser {
  public static async Task AddAsync(IReadOnlyList<Track> tracks,CancellationToken ct){
   if(tracks.Count==0)return;if(!App.Api.IsLoggedIn){Shell.Current.Navigate(typeof(Pages.LoginPage));return;}
   var playlists=new List<Playlist>();
   for(int offset=0;;offset+=50){var page=await App.Api.GetMyPlaylistsAsync(50,offset,ct);playlists.AddRange(page.Where(x=>x.Creator?.Id==App.Api.CurrentUser.Id));if(page.Count<50)break;}
   if(playlists.Count==0){Shell.Current.Notify("还没有可添加歌曲的歌单，请先在云音乐创建歌单。");return;}
   var list=new ListView {ItemsSource=playlists.Select(MusicItem.From).ToList(),ItemTemplate=(Windows.UI.Xaml.DataTemplate)Windows.UI.Xaml.Application.Current.Resources["MusicRowTemplate"],SelectionMode=ListViewSelectionMode.Single,MaxHeight=340};
   var dialog=new ContentDialog {Title="收藏到歌单",Content=list,PrimaryButtonText="收藏",CloseButtonText="取消",IsPrimaryButtonEnabled=false};
   list.SelectionChanged+=(s,e)=>dialog.IsPrimaryButtonEnabled=list.SelectedItem!=null;
   if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
   var playlist=(Playlist)((MusicItem)list.SelectedItem).Value;
   await App.Api.AddTracksToPlaylistAsync(playlist.Id,tracks.Select(x=>x.Id),ct);Shell.Current.Notify("已收藏到「"+playlist.Name+"」。");
  }
 }
}
