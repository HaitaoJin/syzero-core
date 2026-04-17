using FreeRedis;
using System;
using System.Threading.Tasks;
using SyZero.Cache;
using SyZero.Serialization;

namespace SyZero.Redis
{
    public class Cache : ICache
    {
        private readonly RedisClient _cache;
        private readonly IJsonSerialize _jsonSerialize;

        public Cache(RedisClient cache, IJsonSerialize jsonSerialize)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _jsonSerialize = jsonSerialize ?? throw new ArgumentNullException(nameof(jsonSerialize));
        }

        public bool Exist(string key)
        {
            ValidateKey(key);
            return _cache.Exists(key);
        }

        public T Get<T>(string key)
        {
            ValidateKey(key);
            var jsonStr = _cache.Get<string>(key);
            if (string.IsNullOrEmpty(jsonStr))
            {
                return default(T);
            }

            return _jsonSerialize.JSONToObject<T>(jsonStr);
        }

        public string[] GetKeys(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return Array.Empty<string>();
            }

            return _cache.Keys(pattern);
        }

        public void Refresh(string key)
        {
            ValidateKey(key);
            _cache.Exists(key);
        }

        public Task RefreshAsync(string key)
        {
            Refresh(key);
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            ValidateKey(key);
            _cache.Del(key);
        }

        public Task RemoveAsync(string key)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public void Set<T>(string key, T value, int exprireTime = 24 * 60 * 60)
        {
            ValidateKey(key);
            _cache.Set(key, _jsonSerialize.ObjectToJSON(value), exprireTime);
        }

        public Task SetAsync<T>(string key, T value, int exprireTime = 24 * 60 * 60)
        {
            Set(key, value, exprireTime);
            return Task.CompletedTask;
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("缓存键不能为空", nameof(key));
            }
        }
    }
}
