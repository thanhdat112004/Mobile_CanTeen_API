using System;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace WEB_API_CANTEEN.Services
{
    public interface IOtpService
    {
        string GenerateAndSave(string key);    // key = $"register:{email}" hoặc $"reset:{email}"
        bool Verify(string key, string code, bool consume = true);
        bool CanSend(string key);              // rate-limit 60s/lần
    }

    public class OtpService : IOtpService
    {
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _cfg;
        private readonly int _len;
        private readonly TimeSpan _ttl;
        private readonly TimeSpan _rl;

        public OtpService(IMemoryCache cache, IConfiguration cfg)
        {
            _cache = cache;
            _cfg = cfg;
            _len = int.Parse(_cfg["Otp:Length"] ?? "6");
            _ttl = TimeSpan.FromMinutes(int.Parse(_cfg["Otp:TtlMinutes"] ?? "5"));
            _rl = TimeSpan.FromSeconds(int.Parse(_cfg["Otp:RateLimitSeconds"] ?? "60"));
        }

        public bool CanSend(string key)
        {
            var lockKey = $"otp:lock:{key}";
            if (_cache.TryGetValue(lockKey, out _)) return false;
            _cache.Set(lockKey, true, _rl);
            return true;
        }

        public string GenerateAndSave(string key)
        {
            var code = GenerateDigits(_len);
            _cache.Set($"otp:{key}", code, _ttl);
            return code;
        }

        public bool Verify(string key, string code, bool consume = true)
        {
            if (_cache.TryGetValue($"otp:{key}", out string? cached) && cached == code)
            {
                if (consume) _cache.Remove($"otp:{key}");
                return true;
            }
            return false;
        }

        private static string GenerateDigits(int length)
        {
            var bytes = new byte[length];
            RandomNumberGenerator.Fill(bytes);
            var chars = new char[length];
            for (int i = 0; i < length; i++) chars[i] = (char)('0' + (bytes[i] % 10));
            return new string(chars);
        }
    }
}
