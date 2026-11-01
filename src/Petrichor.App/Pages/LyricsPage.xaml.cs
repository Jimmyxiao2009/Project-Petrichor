using System;using System.Linq;using System.Collections.Generic;using System.ComponentModel;using System.Threading.Tasks;using Petrichor.Core;using Windows.UI;using Windows.UI.Text;using Windows.UI.Xaml;using Windows.UI.Xaml.Controls;using Windows.UI.Xaml.Media;using Windows.UI.Xaml.Navigation;namespace Petrichor.App.Pages {
 public sealed partial class LyricsPage : MusicPage {
  public LyricsPage(){InitializeComponent();}
  public sealed class LineView:INotifyPropertyChanged {
   public TimeSpan Position {get;set;}public string Text {get;set;}public string Translation {get;set;}
   private bool active;public bool Active {get=>active;set{if(active==value)return;active=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Foreground)));PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Weight)));}}
   public SolidColorBrush Foreground=>new SolidColorBrush(Active?Colors.White:Color.FromArgb(255,115,115,115));
   public double FontSize=>22;public FontWeight Weight=>Active?FontWeights.SemiBold:FontWeights.Normal;
   public event PropertyChangedEventHandler PropertyChanged;
  }
  private List<LyricLine> lyrics=new List<LyricLine>();private List<LineView> views=new List<LineView>();private long shown;private int active=-1;
  public int LineCount=>lyrics.Count;public int ActiveIndex=>active;
  protected override async Task LoadAsync(object p){App.Playback.TrackChanged+=Changed;await FetchAsync();}
  private async Task FetchAsync(){
   var track=App.Playback.Queue.Current;if(track==null){Status.Text="播放歌曲后，这里会随音乐显示歌词。";return;}
   shown=track.Id;Title.Text=track.Name;Status.Text=track.ArtistText;
   var result=await App.Offline.GetLyricsAsync(track,Token);if(!IsCurrent||shown!=track.Id)return;
   lyrics=result.Lines??new List<LyricLine>();var translations=(result.TranslatedLines??new List<LyricLine>()).GroupBy(x=>x.Position).ToDictionary(x=>x.Key,x=>x.First().Text);
   views=lyrics.Select(l=>new LineView {Position=l.Position,Text=l.Text,Translation=translations.ContainsKey(l.Position)?translations[l.Position]:""}).ToList();Lines.ItemsSource=views;
   if(lyrics.Count==0)Status.Text=string.IsNullOrWhiteSpace(result.Original)?"暂无歌词，享受音乐吧。":System.Text.RegularExpressions.Regex.Replace(result.Original,@"\[[^\]]*\]","").Trim();
   active=-1;UpdatePosition();
  }
  private async void Changed(object s,EventArgs e)=>await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,async()=>await ExecuteAsync(FetchAsync));
  public void UpdatePosition(){
   int index=LrcParser.FindActive(lyrics,App.Playback.Player.PlaybackSession.Position);if(index==active)return;
   if(active>=0&&active<views.Count)views[active].Active=false;active=index;
   if(active>=0&&active<views.Count){views[active].Active=true;Lines.ScrollIntoView(Lines.Items[Math.Max(0,index-2)],ScrollIntoViewAlignment.Leading);}
  }
  private void LineClick(object s,ItemClickEventArgs e)=>App.Playback.Seek(((LineView)e.ClickedItem).Position);
  private void PlayerClick(object s,RoutedEventArgs e)=>Shell.Current.Navigate(typeof(PlayerPage));
  private void OptionsClick(object s,RoutedEventArgs e){
   var flyout=new MenuFlyout();var item=new ToggleMenuFlyoutItem {Text="显示播放控制栏",IsChecked=Shell.Current.LyricsControlsVisible};
   item.Click+=(sender,args)=>Shell.Current.SetLyricsControlsVisible(item.IsChecked);
   flyout.Items.Add(item);flyout.ShowAt((FrameworkElement)s);
  }
  protected override void OnNavigatedFrom(NavigationEventArgs e){App.Playback.TrackChanged-=Changed;base.OnNavigatedFrom(e);}
 }
}

