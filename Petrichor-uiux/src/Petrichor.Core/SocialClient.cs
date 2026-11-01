using System;using System.Collections.Generic;using System.Linq;using System.Threading;using System.Threading.Tasks;using Newtonsoft.Json;using Newtonsoft.Json.Linq;
namespace Petrichor.Core {
 public sealed class MusicComment {
  public long Id {get;set;} public UserProfile User {get;set;} public string Content {get;set;} public DateTimeOffset Time {get;set;} public int LikeCount {get;set;} public bool Liked {get;set;}
  public long ParentCommentId {get;set;} public int ReplyCount {get;set;} public string Location {get;set;}
  /// <summary>楼中楼里被回复的那条评论（作者与内容），没有则为 null。</summary>
  public string RepliedNickname {get;set;} public string RepliedContent {get;set;}
  public string Nickname=>User?.Nickname??"云音乐用户";
  public string DateText=>FormatTime(Time,DateTimeOffset.Now);
  public static string FormatTime(DateTimeOffset time,DateTimeOffset now){
   var local=time.ToLocalTime();var span=now-local;
   if(span<TimeSpan.FromMinutes(1))return "刚刚";
   if(span<TimeSpan.FromHours(1))return (int)span.TotalMinutes+" 分钟前";
   if(local.Date==now.Date)return local.ToString("HH:mm");
   if(local.Date==now.Date.AddDays(-1))return "昨天 "+local.ToString("HH:mm");
   return local.Year==now.Year?local.ToString("M月d日"):local.ToString("yyyy年M月d日");
  }
  internal static MusicComment From(JToken j){
   var replied=(j["beReplied"] as JArray)?.FirstOrDefault() as JObject;var floor=j["showFloorComment"] as JObject;
   // ipLocation 在新接口是 {location}，旧接口可能直接是字符串
   var ip=j["ipLocation"];string location=ip is JObject ipObject?(string)ipObject["location"]??"":ip?.Type==JTokenType.String?(string)ip:"";
   var repliedUser=replied?["user"] as JObject;
   return new MusicComment {Id=(long?)j["commentId"]??0,User=UserProfile.From(j["user"]),Content=(string)j["content"]??"",Time=DateTimeOffset.FromUnixTimeMilliseconds((long?)j["time"]??0),LikeCount=(int?)j["likedCount"]??0,Liked=(bool?)j["liked"]??false,
    ParentCommentId=(long?)j["parentCommentId"]??0,ReplyCount=(int?)(floor?["replyCount"]??j["replyCount"])??0,Location=location,
    RepliedNickname=(string)repliedUser?["nickname"],RepliedContent=(string)replied?["content"]};
  }
 }
 public sealed class CommentPage {public List<MusicComment> Items {get;set;} public List<MusicComment> Hot {get;set;} public int Total {get;set;} public bool HasMore {get;set;}
  /// <summary>下一页的游标：最新排序是服务端返回的 cursor，楼中楼是最后一条的时间。</summary>
  public string Cursor {get;set;} public MusicComment Owner {get;set;}}
 /// <summary>数值与网易云 sortType 一致。</summary>
 public enum CommentSort {Recommended=99,Hot=2,Latest=3}
 public static class CommentThread {
  public static string ForTrack(Track track){if(track==null)throw new ArgumentNullException(nameof(track));return track.ProgramId>0?"A_DJ_1_"+track.ProgramId:"R_SO_4_"+track.Id;}
  internal static string Validate(string threadId){if(string.IsNullOrWhiteSpace(threadId)||!(threadId.StartsWith("R_SO_4_")||threadId.StartsWith("A_DJ_1_")))throw new ArgumentException("Unsupported comment thread",nameof(threadId));return threadId;}
 }
 public sealed partial class NeteaseClient {
  public async Task<CommentPage> GetCommentsAsync(string threadId,CommentSort sort,int pageNo=1,int pageSize=20,string cursor=null,CancellationToken ct=default(CancellationToken)){
   CommentThread.Validate(threadId);if(pageNo<1||pageSize<1||pageSize>50)throw new ArgumentOutOfRangeException(nameof(pageSize));
   if(!Enum.IsDefined(typeof(CommentSort),sort))throw new ArgumentOutOfRangeException(nameof(sort));
   object position=sort==CommentSort.Latest?(object)(string.IsNullOrEmpty(cursor)?"0":cursor):sort==CommentSort.Hot?(object)("normalHot#"+(pageNo-1)*pageSize):(pageNo-1)*pageSize;
   var j=await RequestEapiAsync("v2/resource/comments",Data("threadId",threadId,"pageNo",pageNo,"showInner",true,"pageSize",pageSize,"cursor",position,"sortType",(int)sort),ct).ConfigureAwait(false);
   var d=j["data"];
   return new CommentPage {Items=(d?["comments"]??new JArray()).Select(MusicComment.From).ToList(),Hot=new List<MusicComment>(),Total=(int?)d?["totalCount"]??0,HasMore=(bool?)d?["hasMore"]??false,Cursor=(string)d?["cursor"]};
  }
  // time: 上一页最后一条的时间（毫秒）；首页传 -1。
  public async Task<CommentPage> GetCommentRepliesAsync(string threadId,long parentCommentId,long time=-1,int limit=20,CancellationToken ct=default(CancellationToken)){
   CommentThread.Validate(threadId);ValidId(parentCommentId);if(limit<1||limit>50)throw new ArgumentOutOfRangeException(nameof(limit));
   var j=await RequestAsync("resource/comment/floor/get",Data("parentCommentId",parentCommentId,"threadId",threadId,"time",time,"limit",limit),ct).ConfigureAwait(false);
   var d=j["data"];var items=(d?["comments"]??new JArray()).Select(MusicComment.From).ToList();var owner=d?["ownerComment"];
   return new CommentPage {Items=items,Hot=new List<MusicComment>(),Total=(int?)d?["totalCount"]??items.Count,HasMore=(bool?)d?["hasMore"]??false,
    Cursor=items.Count>0?items[items.Count-1].Time.ToUnixTimeMilliseconds().ToString():null,Owner=owner==null||owner.Type==JTokenType.Null?null:MusicComment.From(owner)};
  }
  public Task PostCommentAsync(string threadId,string content,long replyTo=0,CancellationToken ct=default(CancellationToken))=>PostCommentCoreAsync(CommentThread.Validate(threadId),content,replyTo,ct);
  public Task SetCommentLikedAsync(string threadId,long commentId,bool liked,CancellationToken ct=default(CancellationToken))=>SetCommentLikedCoreAsync(CommentThread.Validate(threadId),commentId,liked,ct);
  public async Task<CommentPage> GetCommentsAsync(long trackId,int limit=30,int offset=0,CancellationToken ct=default(CancellationToken)){
   Page(limit,offset);var j=await RequestAsync("v1/resource/comments/R_SO_4_"+ValidId(trackId),Data("rid",trackId,"limit",limit,"offset",offset,"beforeTime",0),ct).ConfigureAwait(false);
   return new CommentPage {Items=(j["comments"]??new JArray()).Select(MusicComment.From).ToList(),Hot=(j["hotComments"]??new JArray()).Select(MusicComment.From).ToList(),Total=(int?)j["total"]??0,HasMore=(bool?)j["more"]??false};
  }
  public Task PostCommentAsync(long trackId,string content,long replyTo=0,CancellationToken ct=default(CancellationToken))=>PostCommentCoreAsync("R_SO_4_"+ValidId(trackId),content,replyTo,ct);
  public Task SetCommentLikedAsync(long trackId,long commentId,bool liked,CancellationToken ct=default(CancellationToken))=>SetCommentLikedCoreAsync("R_SO_4_"+ValidId(trackId),commentId,liked,ct);
  private async Task PostCommentCoreAsync(string threadId,string content,long replyTo,CancellationToken ct){
   RequireLogin();if(string.IsNullOrWhiteSpace(content)||content.Trim().Length>1000)throw new ArgumentException("Comment length must be 1..1000");
   var data=Data("threadId",threadId,"content",content.Trim());
   if(replyTo>0)data["commentId"]=replyTo;
   await RequestEapiAsync("resource/comments/"+(replyTo>0?"reply":"add"),data,ct).ConfigureAwait(false);
  }
  private async Task SetCommentLikedCoreAsync(string threadId,long commentId,bool liked,CancellationToken ct){
   RequireLogin();await RequestAsync("v1/comment/"+(liked?"like":"unlike"),Data("threadId",threadId,"commentId",ValidId(commentId)),ct).ConfigureAwait(false);
  }
  public Task RemoveTrackFromPlaylistAsync(long playlistId,long trackId,CancellationToken ct=default(CancellationToken))=>RemoveTracksFromPlaylistAsync(playlistId,new[]{trackId},ct);
  public async Task RemoveTracksFromPlaylistAsync(long playlistId,IEnumerable<long> trackIds,CancellationToken ct=default(CancellationToken)){
   RequireLogin();ValidId(playlistId);var ids=trackIds.Select(ValidId).Distinct().ToList();if(ids.Count==0)throw new ArgumentException("No tracks");
   for(int offset=0;offset<ids.Count;offset+=100){var batch=ids.Skip(offset).Take(100).ToArray();
    await RequestAsync("playlist/manipulate/tracks",Data("op","del","pid",playlistId,"trackIds",JsonConvert.SerializeObject(batch),"imme","true"),ct).ConfigureAwait(false);}
  }
  public Task AddTrackToPlaylistAsync(long playlistId,long trackId,CancellationToken ct=default(CancellationToken))=>AddTracksToPlaylistAsync(playlistId,new[]{trackId},ct);
  public async Task AddTracksToPlaylistAsync(long playlistId,IEnumerable<long> trackIds,CancellationToken ct=default(CancellationToken)){
   RequireLogin();ValidId(playlistId);var ids=trackIds.Select(ValidId).Distinct().ToList();if(ids.Count==0)throw new ArgumentException("No tracks");
   for(int offset=0;offset<ids.Count;offset+=100){var batch=ids.Skip(offset).Take(100).ToArray();var data=Data("op","add","pid",playlistId,"trackIds",JsonConvert.SerializeObject(batch),"imme","true");
    try{await RequestAsync("playlist/manipulate/tracks",data,ct).ConfigureAwait(false);}
    catch(MusicApiException error)when(error.Code==512){data["trackIds"]=JsonConvert.SerializeObject(batch.Concat(batch));await RequestAsync("playlist/manipulate/tracks",data,ct).ConfigureAwait(false);}
   }
  }
 }
}
