using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
namespace Petrichor.Core
{
    // Platform AES and randomness come from Windows.Security.Cryptography on the phone.
    public interface ICryptoProvider { byte[] RandomBytes(int length); byte[] AesCbcPkcs7(byte[] input, byte[] key, byte[] iv); }
    public sealed class WeapiCrypto
    {
        private readonly ICryptoProvider provider;
        private static readonly byte[] Iv = Encoding.UTF8.GetBytes("0102030405060708");
        private static readonly byte[] Preset = Encoding.UTF8.GetBytes("0CoJUm6Qyw8W8jud");
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        // NetEase Web client's public RSA modulus, exponent 65537, raw RSA (no padding).
        // Actual DER public key is parsed below to avoid transcription-sensitive numeric constants.
        private const string PublicDer = "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDgtQn2JZ34ZC28NWYpAUd98iZ37BUrX/aKzmFbt7clFSs6sXqHauqKWqdtLkF2KexO40H1YTX8z2lSgBBOAxLsvaklV8k4cBFK9snQXE9/DDaFt6Rr7iVZMldczhC0JNgTz+SHXT6CBHuX3e9SdB1Ua44oncaTWz7OBGLbCiK45wIDAQAB";
        public WeapiCrypto(ICryptoProvider provider) { this.provider = provider ?? throw new ArgumentNullException(nameof(provider)); }
        public Dictionary<string, string> Encrypt(string json)
        {
            var key = new StringBuilder();
            while (key.Length < 16) foreach (var b in provider.RandomBytes(32)) { if (b < 248) key.Append(Alphabet[b % 62]); if (key.Length == 16) break; }
            var secret = key.ToString();
            var first = Convert.ToBase64String(provider.AesCbcPkcs7(Encoding.UTF8.GetBytes(json), Preset, Iv));
            var second = Convert.ToBase64String(provider.AesCbcPkcs7(Encoding.UTF8.GetBytes(first), Encoding.UTF8.GetBytes(secret), Iv));
            // ASN.1 SubjectPublicKeyInfo above: modulus starts at byte 29, 128 bytes (preceded by a sign byte).
            var der = Convert.FromBase64String(PublicDer); var modulus = new byte[129];
            for (int i = 0; i < 128; i++) modulus[i] = der[156 - i];
            // BigInteger is little endian: this is the reversed ASCII secret interpreted as big endian.
            var secretBytes = new byte[17]; Encoding.UTF8.GetBytes(secret).CopyTo(secretBytes, 0);
            var value = BigInteger.ModPow(new BigInteger(secretBytes), new BigInteger(65537), new BigInteger(modulus));
            var hex = value.ToString("x").TrimStart('0').PadLeft(256, '0');
            return new Dictionary<string, string> { ["params"] = second, ["encSecKey"] = hex };
        }
    }
}

