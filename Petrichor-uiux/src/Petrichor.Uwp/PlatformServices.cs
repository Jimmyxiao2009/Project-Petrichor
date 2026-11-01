using System;
using System.Threading.Tasks;
using Petrichor.Core;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
namespace Petrichor.Uwp
{
    public sealed class WindowsCryptoProvider : ICryptoProvider, IEapiCryptoProvider
    {
        public byte[] Md5(byte[] input){byte[] value;var provider=HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);CryptographicBuffer.CopyToByteArray(provider.HashData(CryptographicBuffer.CreateFromByteArray(input)),out value);return value;}
        public byte[] AesEcbPkcs7(byte[] input,byte[] key){byte[] value;var algorithm=SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesEcbPkcs7);var cryptoKey=algorithm.CreateSymmetricKey(CryptographicBuffer.CreateFromByteArray(key));CryptographicBuffer.CopyToByteArray(CryptographicEngine.Encrypt(cryptoKey,CryptographicBuffer.CreateFromByteArray(input),null),out value);return value;}
        public byte[] RandomBytes(int length) { byte[] value; CryptographicBuffer.CopyToByteArray(CryptographicBuffer.GenerateRandom((uint)length), out value); return value; }
        public byte[] AesCbcPkcs7(byte[] input, byte[] key, byte[] iv) {
            var algorithm = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesCbcPkcs7);
            var cryptoKey = algorithm.CreateSymmetricKey(CryptographicBuffer.CreateFromByteArray(key));
            byte[] value; CryptographicBuffer.CopyToByteArray(CryptographicEngine.Encrypt(cryptoKey, CryptographicBuffer.CreateFromByteArray(input), CryptographicBuffer.CreateFromByteArray(iv)), out value); return value;
        }
    }
    public sealed class VaultSessionStore : ISessionStore
    {
        private const string Resource = "Petrichor.NetEase.Session";
        private const string Account = "current";
        private readonly PasswordVault vault = new PasswordVault();
        public Task<string> LoadAsync() { try { var item = vault.Retrieve(Resource, Account); item.RetrievePassword(); return Task.FromResult(item.Password); } catch (Exception e) when ((uint)e.HResult == 0x80070490 || (uint)e.HResult == 0x80070525) { return Task.FromResult<string>(null); } }
        public Task SaveAsync(string cookie) { ClearAsync(); if (!string.IsNullOrEmpty(cookie)) vault.Add(new PasswordCredential(Resource, Account, cookie)); return Task.FromResult(0); }
        public Task ClearAsync() { try { vault.Remove(vault.Retrieve(Resource, Account)); } catch (Exception e) when ((uint)e.HResult == 0x80070490 || (uint)e.HResult == 0x80070525) { } return Task.FromResult(0); }
    }
}

