using System;using System.Collections.Generic;using System.Text;namespace Petrichor.Core {
 public interface IEapiCryptoProvider {byte[] AesEcbPkcs7(byte[] input,byte[] key);byte[] Md5(byte[] input);}
 public sealed class EapiCrypto {
  private readonly IEapiCryptoProvider provider;
  public EapiCrypto(IEapiCryptoProvider provider){this.provider=provider??throw new ArgumentNullException(nameof(provider));}
  public Dictionary<string,string> Encrypt(string path,string json){
   string digest=BitConverter.ToString(provider.Md5(Encoding.UTF8.GetBytes("nobody"+path+"use"+json+"md5forencrypt"))).Replace("-","").ToLowerInvariant();
   string data=path+"-36cd479b6b5-"+json+"-36cd479b6b5-"+digest;
   var bytes=provider.AesEcbPkcs7(Encoding.UTF8.GetBytes(data),Encoding.UTF8.GetBytes("e82ckenh8dichen8"));
   return new Dictionary<string,string>{{"params",BitConverter.ToString(bytes).Replace("-","")}};
  }
 }
}
