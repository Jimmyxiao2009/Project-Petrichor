using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
namespace Petrichor.App {
 /// <summary>播放页与设置页共用的睡眠定时菜单。</summary>
 internal static class SleepTimerMenu {
  private static readonly int[] Presets={15,30,45,60,90};
  public static string Describe(){
   if(PendingLogic.SleepAfterTrack)return "本首播完后暂停";
   var left=PendingLogic.SleepRemaining;
   return left.HasValue?Format(left.Value)+" 后暂停":"未开启";
  }
  public static string Format(TimeSpan left)=>left.TotalHours>=1?((int)left.TotalHours)+":"+left.ToString(@"mm\:ss"):left.ToString(@"mm\:ss");
  public static void ShowAt(FrameworkElement target){
   var menu=new MenuFlyout();
   if(PendingLogic.SleepActive){
    menu.Items.Add(new MenuFlyoutItem{Text=Describe(),IsEnabled=false});
    var off=new MenuFlyoutItem{Text="关闭定时"};off.Click+=(s,e)=>Apply(null,false);menu.Items.Add(off);
    menu.Items.Add(new MenuFlyoutSeparator());
   }
   foreach(var minutes in Presets){var item=new MenuFlyoutItem{Text=minutes+" 分钟"};item.Click+=(s,e)=>Apply(TimeSpan.FromMinutes(minutes),false);menu.Items.Add(item);}
   var afterTrack=new MenuFlyoutItem{Text="播完当前歌曲",IsEnabled=PendingLogic.SleepFinishTrackSupported};
   afterTrack.Click+=(s,e)=>{PendingLogic.SetSleepAfterCurrentTrack();Shell.Current.Notify("当前歌曲播完后暂停。");};
   menu.Items.Add(afterTrack);
   menu.Items.Add(new MenuFlyoutSeparator());
   var custom=new MenuFlyoutItem{Text="自定义…"};custom.Click+=async(s,e)=>await ShowCustomAsync();menu.Items.Add(custom);
   menu.ShowAt(target);
  }
  private static void Apply(TimeSpan? duration,bool finishTrack){
   PendingLogic.SetSleepTimer(duration,finishTrack);
   Shell.Current.Notify(duration.HasValue?(int)duration.Value.TotalMinutes+" 分钟后"+(finishTrack?"播完当前歌曲再":"")+"暂停播放。":"已关闭睡眠定时。");
  }
  private static async System.Threading.Tasks.Task ShowCustomAsync(){
   var value=new TextBlock{FontSize=34,FontFamily=new Windows.UI.Xaml.Media.FontFamily("Segoe UI Light"),HorizontalAlignment=HorizontalAlignment.Center};
   var slider=new Slider{Minimum=5,Maximum=180,StepFrequency=5,SmallChange=5,LargeChange=15,Value=30,Margin=new Thickness(0,8,0,0)};
   Windows.UI.Xaml.Automation.AutomationProperties.SetName(slider,"定时分钟数");
   void Update(){var m=(int)slider.Value;value.Text=m>=60?(m/60)+" 小时"+(m%60==0?"":" "+(m%60)+" 分钟"):m+" 分钟";}
   slider.ValueChanged+=(s,e)=>Update();Update();
   var finish=new CheckBox{Content="到点后播完当前歌曲再停",IsEnabled=PendingLogic.SleepFinishTrackSupported,Margin=new Thickness(0,12,0,0)};
   var panel=new StackPanel();panel.Children.Add(value);panel.Children.Add(slider);panel.Children.Add(finish);
   var dialog=new ContentDialog{Title="睡眠定时",Content=panel,PrimaryButtonText="开始",CloseButtonText="取消"};
   if(await dialog.ShowAsync()==ContentDialogResult.Primary)Apply(TimeSpan.FromMinutes(slider.Value),finish.IsChecked==true);
  }
 }
}
