using System;
namespace Petrichor.Core {
 public enum SleepAction {None,PauseNow,PauseAfterTrack}
 /// <summary>睡眠定时的纯状态；计时与暂停由播放服务驱动。</summary>
 public sealed class SleepSchedule {
  private readonly object sync=new object();
  public DateTimeOffset? Deadline {get;private set;}
  /// <summary>到点时若正在播放，等当前曲目播完再停。</summary>
  public bool FinishTrack {get;private set;}
  public bool WaitingForTrackEnd {get;private set;}
  public bool IsActive {get{lock(sync)return Deadline.HasValue||WaitingForTrackEnd;}}
  public void Start(TimeSpan duration,bool finishTrack,DateTimeOffset now){
   if(duration<=TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(duration));
   lock(sync){Deadline=now+duration;FinishTrack=finishTrack;WaitingForTrackEnd=false;}
  }
  public void StopAfterCurrentTrack(){lock(sync){Deadline=null;FinishTrack=true;WaitingForTrackEnd=true;}}
  public void Cancel(){lock(sync){Deadline=null;FinishTrack=false;WaitingForTrackEnd=false;}}
  public TimeSpan? Remaining(DateTimeOffset now){lock(sync){if(!Deadline.HasValue)return null;var left=Deadline.Value-now;return left<TimeSpan.Zero?TimeSpan.Zero:left;}}
  public SleepAction OnDeadline(DateTimeOffset now,bool playing){
   lock(sync){
    if(!Deadline.HasValue||now<Deadline.Value)return SleepAction.None;
    Deadline=null;
    if(FinishTrack&&playing){WaitingForTrackEnd=true;return SleepAction.PauseAfterTrack;}
    FinishTrack=false;WaitingForTrackEnd=false;return SleepAction.PauseNow;
   }
  }
  /// <returns>true 表示应停在这里，不再切到下一首。</returns>
  public bool OnTrackEnded(){lock(sync){if(!WaitingForTrackEnd)return false;Deadline=null;FinishTrack=false;WaitingForTrackEnd=false;return true;}}
 }
}
