using System;using System.Linq;using System.Collections.Generic;using System.Threading;using System.Threading.Tasks;using Petrichor.Core;using Windows.UI.Xaml;using Windows.UI.Xaml.Controls;using Windows.UI.Xaml.Navigation;using Windows.Storage;namespace Petrichor.App.Pages {public sealed partial class PlaylistListPage : MusicPage {public PlaylistListPage(){InitializeComponent(); Items.SizeChanged+=(s,e)=>MusicItem.FitCovers(Items);}
private readonly List<MusicItem> items=new List<MusicItem>();private int revision;public int ItemCount=>Items.Items.Count;
protected override Task LoadAsync(object p)=>FetchAsync(false);
private async Task FetchAsync(bool next){int request=++revision;var offset=next?items.Count:0;var category=((ComboBoxItem)Category.SelectedItem).Content.ToString();var result=await App.Api.GetPlaylistsAsync(category,30,offset,Token);if(request!=revision||!IsCurrent)return;if(!next)items.Clear();items.AddRange(result.Items.Select(MusicItem.From));Items.ItemsSource=items.ToList();MusicItem.FitCovers(Items);More.Visibility=result.HasMore?Visibility.Visible:Visibility.Collapsed;}
private async void CategoryChanged(object s,SelectionChangedEventArgs e){if(Items!=null&&Frame!=null)await ExecuteAsync(()=>FetchAsync(false));}
private async void MoreClick(object s,RoutedEventArgs e)=>await ExecuteAsync(()=>FetchAsync(true));}}

