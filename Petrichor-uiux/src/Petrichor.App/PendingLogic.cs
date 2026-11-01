using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Petrichor.Core;
using Windows.Storage;
namespace Petrichor.App {
 public enum CommentSort {Recommended,Hot,Latest}
 public enum PodcastFeed {Hot,Subscribed}
 /// <summary>播客列表行的 MusicItem.Value。</summary>
 public sealed class PodcastRef {public long Id {get;set;} public string Name {get;set;}}
 public sealed class PodcastHeader {public long Id {get;set;} public string Title {get;set;} public string Host {get;set;} public string CoverUrl {get;set;} public string Description {get;set;} public int EpisodeCount {get;set;} public long Subscribers {get;set;} public bool Subscribed {get;set;}}
 public sealed class Paged<T> {
  public List<T> Items {get;set;}=new List<T>(); public bool HasMore {get;set;}
  /// <summary>原样传回给下一次请求的游标；null 表示第一页。</summary>
  public string Cursor {get;set;} public int Total {get;set;}
 }
 public sealed class CommentThreadArgs {public Track Track {get;set;} public CommentEntry Parent {get;set;}}

 /// <summary>
 /// UI 与逻辑层的接缝。UI 只调用这里；方法体把 UI 数据契约映射到 Petrichor.Core / Petrichor.Uwp 的真实逻辑。
 /// </summary>
 internal static class PendingLogic {
  // ───────── 睡眠定时 ─────────
  public static bool SleepFinishTrackSupported=>true;
  public static event EventHandler SleepTimerChanged{add{App.Playback.SleepTimerChanged+=value;}remove{App.Playback.SleepTimerChanged-=value;}}
  public static bool SleepActive=>App.Playback.Sleep.IsActive;
  /// <summary>剩余时间；未开启或「播完当前歌曲」等待中返回 null。</summary>
  public static TimeSpan? SleepRemaining=>App.Playback.Sleep.Remaining(DateTimeOffset.UtcNow);
  public static bool SleepAfterTrack=>App.Playback.Sleep.WaitingForTrackEnd;
  public static void SetSleepTimer(TimeSpan? duration,bool finishTrack)=>App.Playback.SetSleepTimer(duration,finishTrack);
  public static void SetSleepAfterCurrentTrack()=>App.Playback.SetSleepAfterCurrentTrack();

  // ───────── 评论 ─────────
  public static bool CommentSortSupported(CommentSort sort)=>true;
  public static bool CommentRepliesSupported=>true;
  private const int CommentPageSize=20;
  public static async Task<Paged<CommentEntry>> GetCommentsAsync(Track track,CommentSort sort,string cursor,CancellationToken ct){
   int pageNo=1,offset=0;string latestCursor="0";
   if(!string.IsNullOrEmpty(cursor)){var parts=cursor.Split(':');pageNo=int.Parse(parts[0]);offset=int.Parse(parts[1]);latestCursor=parts[2];}
   var sortType=sort==CommentSort.Recommended?Core.CommentSort.Recommended:sort==CommentSort.Hot?Core.CommentSort.Hot:Core.CommentSort.Latest;
   var page=await App.Api.GetCommentsAsync(CommentThread.ForTrack(track),sortType,pageNo,CommentPageSize,latestCursor,ct);
   var items=page.Items.Select(CommentEntry.From).ToList();
   string next=sort==CommentSort.Latest?"0:0:"+(page.Cursor??"0"):(pageNo+1)+":"+(offset+items.Count)+":0";
   return new Paged<CommentEntry>{Items=items,HasMore=page.HasMore,Cursor=next,Total=page.Total};
  }
  public static async Task<Paged<CommentEntry>> GetRepliesAsync(Track track,CommentEntry parent,string cursor,CancellationToken ct){
   long time=string.IsNullOrEmpty(cursor)?-1:long.Parse(cursor);
   var page=await App.Api.GetCommentRepliesAsync(CommentThread.ForTrack(track),parent.Id,time,CommentPageSize,ct);
   var items=page.Items.Select(CommentEntry.From).ToList();
   return new Paged<CommentEntry>{Items=items,HasMore=page.HasMore,Cursor=page.Cursor,Total=page.Total};
  }
  public static Task PostCommentAsync(Track track,string content,long replyTo,CancellationToken ct)=>App.Api.PostCommentAsync(CommentThread.ForTrack(track),content,replyTo,ct);
  public static Task SetCommentLikedAsync(Track track,CommentEntry comment,bool liked,CancellationToken ct)=>App.Api.SetCommentLikedAsync(CommentThread.ForTrack(track),comment.Id,liked,ct);

  // ───────── 播客 ─────────
  public static bool PodcastsSupported=>true;
  public static async Task<Paged<MusicItem>> GetPodcastsAsync(PodcastFeed feed,string cursor,CancellationToken ct){
   int offset=string.IsNullOrEmpty(cursor)?0:int.Parse(cursor);
   var page=feed==PodcastFeed.Subscribed?await App.Api.GetSubscribedRadiosAsync(30,offset,ct):await App.Api.GetHotRadiosAsync(30,offset,ct);
   return new Paged<MusicItem>{Items=page.Items.Select(ToPodcastItem).ToList(),HasMore=page.HasMore,Cursor=page.HasMore?(offset+page.Items.Count).ToString():null};
  }
  public static async Task<Paged<MusicItem>> SearchPodcastsAsync(string query,string cursor,CancellationToken ct){
   int offset=string.IsNullOrEmpty(cursor)?0:int.Parse(cursor);
   var page=await App.Api.SearchRadiosAsync(query,30,offset,ct);
   return new Paged<MusicItem>{Items=page.Items.Select(ToPodcastItem).ToList(),HasMore=page.HasMore,Cursor=page.HasMore?(offset+page.Items.Count).ToString():null,Total=page.Total};
  }
  public static async Task<PodcastHeader> GetPodcastAsync(long id,CancellationToken ct){
   var radio=await App.Api.GetRadioAsync(id,ct);
   return new PodcastHeader{Id=radio.Id,Title=radio.Name,Host=radio.DjName,CoverUrl=radio.CoverUrl,Description=radio.Description,EpisodeCount=radio.ProgramCount,Subscribers=radio.SubCount,Subscribed=radio.Subscribed};
  }
  public static async Task<Paged<MusicItem>> GetEpisodesAsync(long podcastId,bool oldestFirst,string cursor,CancellationToken ct){
   int offset=string.IsNullOrEmpty(cursor)?0:int.Parse(cursor);
   var page=await App.Api.GetRadioProgramsAsync(podcastId,30,offset,oldestFirst,ct);
   return new Paged<MusicItem>{Items=page.Items.Select(ToEpisodeItem).ToList(),HasMore=page.HasMore,Cursor=page.HasMore?(offset+page.Items.Count).ToString():null};
  }
  public static Task SetPodcastSubscribedAsync(long podcastId,bool subscribed,CancellationToken ct)=>App.Api.SetRadioSubscribedAsync(podcastId,subscribed,ct);
  private static MusicItem ToPodcastItem(DjRadio r)=>new MusicItem{Value=new PodcastRef{Id=r.Id,Name=r.Name},Title=r.Name,Subtitle=PodcastSubtitle(r),Detail=r.Description,CoverUrl=r.CoverUrl};
  private static string PodcastSubtitle(DjRadio r)=>string.IsNullOrWhiteSpace(r.DjName)?r.ProgramCount+" 期":r.DjName+" · "+r.ProgramCount+" 期";
  private static MusicItem ToEpisodeItem(DjProgram p)=>new MusicItem{Value=p.Track,Title=p.Name,Badge=p.SerialNumber>0?p.SerialNumber.ToString():"",Subtitle=FormatEpisodeMeta(p.CreatedAt,TimeSpan.FromMilliseconds(p.DurationMs),p.ListenerCount)};
  public static string FormatEpisodeMeta(DateTimeOffset published,TimeSpan duration,long plays)=>(published.Year==DateTimeOffset.Now.Year?published.ToLocalTime().ToString("M月d日"):published.ToLocalTime().ToString("yyyy年M月d日"))+" · "+(int)Math.Max(1,Math.Round(duration.TotalMinutes))+" 分钟 · "+FormatCount(plays)+" 次播放";
  public static string FormatCount(long n)=>n>=100000000?(n/100000000d).ToString("0.#")+" 亿":n>=10000?(n/10000d).ToString("0.#")+" 万":n.ToString();

  // ───────── 下载目录 ─────────
  /// <summary>下载完成后会移动到所选目录；目录不可用时留在应用内部存储。</summary>
  public static bool DownloadFolderSupported=>true;
  public static Task<StorageFolder> GetDownloadFolderAsync()=>App.Offline.GetDownloadFolderAsync();
  public static bool HasCustomDownloadFolder=>App.Offline.HasCustomDownloadFolder;
  /// <param name="folder">null 恢复默认。旧目录的授权不移除，已下载到那里的歌曲仍需能访问。</param>
  public static void SetDownloadFolder(StorageFolder folder)=>App.Offline.SetDownloadFolder(folder);
  /// <summary>已下载歌曲中有多少首不在当前下载目录。</summary>
  public static Task<int> CountDownloadsOutsideFolderAsync()=>App.Offline.CountOutsideDownloadFolderAsync();
  /// <summary>把已下载歌曲迁移到指定目录；folder=null 表示迁回应用内部存储。调用前需先用 SetDownloadFolder 切到同一目录。</summary>
  public static Task<DownloadMigration> MigrateDownloadsAsync(StorageFolder folder)=>App.Offline.MigrateDownloadsAsync(folder);
 }
}
