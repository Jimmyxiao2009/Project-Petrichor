using System;
using System.ComponentModel;
using Petrichor.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
namespace Petrichor.App {
 /// <summary>评论卡片的视图模型。逻辑侧补齐 ReplyCount / Location / Replied* 后卡片自动显示楼中楼入口和引用。</summary>
 public sealed class CommentEntry : INotifyPropertyChanged {
  public long Id {get;set;} public string Nickname {get;set;} public string AvatarUrl {get;set;} public string Content {get;set;}
  public DateTimeOffset Time {get;set;} public string Location {get;set;}
  public int ReplyCount {get;set;}
  public string RepliedNickname {get;set;} public string RepliedContent {get;set;}
  private int likeCount;private bool liked;
  public int LikeCount {get=>likeCount;set{likeCount=value;Raise(nameof(LikeText));}}
  public bool Liked {get=>liked;set{liked=value;Raise(nameof(LikeGlyph));Raise(nameof(LikeBrush));}}
  /// <summary>楼中楼页顶部的楼主评论不再显示「N 条回复」入口。</summary>
  public bool IsThreadHead {get;set;}
  private BitmapImage avatar;
  public BitmapImage Avatar=>avatar??(avatar=MusicItem.Image(AvatarUrl));
  public string MetaText=>FormatTime(Time,DateTimeOffset.Now)+(string.IsNullOrEmpty(Location)?"":" · "+Location);
  public string LikeText=>likeCount<=0?"":PendingLogic.FormatCount(likeCount);
  public string LikeGlyph=>liked?"\uEB52":"\uEB51";
  public Brush LikeBrush=>(Brush)Application.Current.Resources[liked?"PetrichorAccent":"SecondaryText"];
  public string ReplyText=>ReplyCount+" 条回复";
  public Visibility ReplyVisibility=>ReplyCount>0&&!IsThreadHead?Visibility.Visible:Visibility.Collapsed;
  public string RepliedText=>"@"+RepliedNickname+"："+RepliedContent;
  public Visibility RepliedVisibility=>string.IsNullOrEmpty(RepliedNickname)?Visibility.Collapsed:Visibility.Visible;
  public event PropertyChangedEventHandler PropertyChanged;
  private void Raise(string name)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
  /// <summary>楼中楼页顶部用的副本，不影响列表里原条目的「N 条回复」入口。</summary>
  public CommentEntry AsThreadHead()=>new CommentEntry{Id=Id,Nickname=Nickname,AvatarUrl=AvatarUrl,Content=Content,Time=Time,Location=Location,ReplyCount=ReplyCount,LikeCount=LikeCount,Liked=Liked,IsThreadHead=true};
  public static CommentEntry From(MusicComment c)=>new CommentEntry{Id=c.Id,Nickname=c.Nickname,AvatarUrl=c.User?.AvatarUrl,Content=c.Content,Time=c.Time,Location=c.Location,ReplyCount=c.ReplyCount,RepliedNickname=c.RepliedNickname,RepliedContent=c.RepliedContent,LikeCount=c.LikeCount,Liked=c.Liked};
  public static string FormatTime(DateTimeOffset time,DateTimeOffset now){
   var local=time.ToLocalTime();var span=now-local;
   if(span<TimeSpan.FromMinutes(1))return "刚刚";
   if(span<TimeSpan.FromHours(1))return (int)span.TotalMinutes+" 分钟前";
   if(local.Date==now.Date)return local.ToString("HH:mm");
   if(local.Date==now.Date.AddDays(-1))return "昨天 "+local.ToString("HH:mm");
   return local.Year==now.Year?local.ToString("M月d日"):local.ToString("yyyy年M月d日");
  }
 }
}
