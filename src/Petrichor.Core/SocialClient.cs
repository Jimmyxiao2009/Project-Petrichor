using System;using System.Collections.Generic;using System.Linq;using System.Threading;using System.Threading.Tasks;using Newtonsoft.Json;using Newtonsoft.Json.Linq;
namespace Petrichor.Core {
 public sealed class MusicComment {
  public long Id {get;set;} public UserProfile User {get;set;} public string Content {get;set;} public DateTimeOffset Time {get;set;} public int LikeCount {get;set;} public bool Liked {get;set;}
  public string Nickname=>User?.Nickname??"云音乐用户";
  public string DateText=>Time.ToLocalTime().ToString("yyyy.MM.dd");
  internal static MusicComment From(JToken j)=>new MusicComment {Id=(long?)j["commentId"]??0,User=UserProfile.From(j["user"]),Content=(string)j["content"]??"",Time=DateTimeOffset.FromUnixTimeMilliseconds((long?)j["time"]??0),LikeCount=(int?)j["likedCount"]??0,Liked=(bool?)j["liked"]??false};
 }
 public sealed class CommentPage {public List<MusicComment> Items {get;set;} public List<MusicComment> Hot {get;set;} public int Total {get;set;} public bool HasMore {get;set;}}
 public sealed partial class NeteaseClient {
  public async Task<CommentPage> GetCommentsAsync(long trackId,int limit=30,int offset=0,CancellationToken ct=default(CancellationToken)){
   Page(limit,offset);var j=await RequestAsync("v1/resource/comments/R_SO_4_"+ValidId(trackId),Data("rid",trackId,"limit",limit,"offset",offset,"beforeTime",0),ct).ConfigureAwait(false);
   return new CommentPage {Items=(j["comments"]??new JArray()).Select(MusicComment.From).ToList(),Hot=(j["hotComments"]??new JArray()).Select(MusicComment.From).ToList(),Total=(int?)j["total"]??0,HasMore=(bool?)j["more"]??false};
  }
  public async Task PostCommentAsync(long trackId,string content,long replyTo=0,CancellationToken ct=default(CancellationToken)){
   RequireLogin();if(string.IsNullOrWhiteSpace(content)||content.Trim().Length>1000)throw new ArgumentException("Comment length must be 1..1000");
   var data=Data("threadId","R_SO_4_"+ValidId(trackId),"content",content.Trim());
   if(replyTo>0)data["commentId"]=replyTo;
   await RequestEapiAsync("resource/comments/"+(replyTo>0?"reply":"add"),data,ct).ConfigureAwait(false);
  }
  public async Task SetCommentLikedAsync(long trackId,long commentId,bool liked,CancellationToken ct=default(CancellationToken)){
   RequireLogin();await RequestAsync("v1/comment/"+(liked?"like":"unlike"),Data("threadId","R_SO_4_"+ValidId(trackId),"commentId",ValidId(commentId)),ct).ConfigureAwait(false);
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
