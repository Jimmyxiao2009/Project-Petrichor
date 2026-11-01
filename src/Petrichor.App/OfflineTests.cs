#if DEBUG
using System;using System.Runtime.InteropServices.WindowsRuntime;using System.IO;using System.Linq;using System.Net;using System.Net.Http;using System.Threading;using System.Threading.Tasks;using Petrichor.Core;using Petrichor.Uwp;using Windows.Storage;
namespace Petrichor.App {
 internal static class OfflineTests {
  private sealed class LyricsHandler:HttpMessageHandler {
   public int Calls;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;if(!request.RequestUri.AbsolutePath.Contains("lyric"))throw new Exception("Offline playback attempted network");
    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"code\":200,\"lrc\":{\"lyric\":\"[00:00.00]First line\\n[00:02.00]Second line\"}}")});}
  }
  private sealed class ImageHandler:HttpMessageHandler {
   public int Calls;public byte[] Bytes;
   protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct){Calls++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(Bytes)});}
  }
  private static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
  public static async Task RunAsync(){
   string name="Diag-"+Guid.NewGuid().ToString("N");StorageFolder root=null;
   try{
    var handler=new LyricsHandler();
    var imageHandler=new ImageHandler {Bytes=(await FileIO.ReadBufferAsync(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Square44x44Logo.png")))).ToArray()};
    using(var client=new NeteaseClient(new WindowsCryptoProvider(),new MemorySessionStore(),handler))using(var http=new HttpClient(imageHandler)){
     var offline=new OfflineLibrary(client,name,http);await offline.InitializeAsync();root=await ApplicationData.Current.LocalFolder.GetFolderAsync(name);
     var track=new Track {Id=123,Name="Cache check"};
     var first=await offline.GetLyricsAsync(track);var second=await offline.GetLyricsAsync(track);
     Require(first.Lines.Count==2&&second.Lines.Count==2&&handler.Calls==1,"Lyrics cache made an extra request or lost timestamps");
     var uri=await offline.GetCoverAsync("https://test.invalid/cover.png");Require(uri?.Scheme=="ms-appdata"&&imageHandler.Calls==1,"Artwork did not cache");
     var restarted=new OfflineLibrary(client,name,http);var reused=await restarted.GetCoverAsync("https://test.invalid/cover.png");Require(reused==uri&&imageHandler.Calls==1,"Artwork cache failed across store instances");
     Require(await StorageFile.GetFileFromApplicationUriAsync(uri)!=null,"Cached artwork URI cannot resolve");
     byte[] audio;
     using(var memory=new MemoryStream())using(var writer=new BinaryWriter(memory)){
      const int rate=22050,samples=rate*8;writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples*2);
      for(int i=0;i<samples;i++)writer.Write((short)(Math.Sin(2*Math.PI*440*i/rate)*500));audio=memory.ToArray();
     }
     var fixture=await root.CreateFileAsync("fixture.wav",CreationCollisionOption.ReplaceExisting);await FileIO.WriteBytesAsync(fixture,audio);
     var imported=(await offline.ImportAsync(new[]{fixture})).Single();Require(imported.Id<0&&await offline.FindAudioAsync(imported)!=null,"Import did not create local track");
     var lrc=await root.CreateFileAsync("fixture.lrc",CreationCollisionOption.ReplaceExisting);await FileIO.WriteTextAsync(lrc,"[00:00.00]Local first line\n[00:02.00]Local second line");
     await offline.ImportLyricsAsync(imported,lrc);Require((await offline.GetLyricsAsync(imported)).Lines.Count==2,"Imported LRC lost timestamps");
     Require((await new OfflineLibrary(client,name,http).GetLocalSongsAsync()).Any(t=>t.Id==imported.Id),"Imported track not persisted");
     using(var player=new PlaybackService(client){LocalFileResolver=offline.FindAudioAsync}){
      await player.PlayAsync(new[]{imported});var until=DateTimeOffset.UtcNow.AddSeconds(10);while(player.Player.PlaybackSession.PlaybackState!=Windows.Media.Playback.MediaPlaybackState.Playing&&DateTimeOffset.UtcNow<until)await Task.Delay(100);
      await Task.Delay(500);Require(player.Player.PlaybackSession.Position.TotalMilliseconds>100&&player.CurrentStream==null&&handler.Calls==1,"Local playback used network or did not advance");
     }
     await offline.ClearCacheAsync();Require(await offline.FindAudioAsync(imported)!=null&&(await offline.GetLyricsAsync(imported)).Lines.Count==2,"Cache cleanup removed local music or imported lyrics");
     // The cleared online lyric entry must cause a fresh API request.
     await offline.GetLyricsAsync(track);Require(handler.Calls==2,"Online cache cleanup did not invalidate lyrics");
    }
   }finally{if(root!=null)await root.DeleteAsync();}
  }
 }
}
#endif


