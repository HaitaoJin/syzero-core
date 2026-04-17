using FreeRedis;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using SyZero.Util;

namespace SyZero.Redis
{
    public class LockUtil : ILockUtil
    {
        private const string ReleaseLockScript = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
        private readonly RedisClient _redis;
        private readonly ConcurrentDictionary<string, string> _lockTokens = new ConcurrentDictionary<string, string>();
        //默认过期时间10s
        private readonly int _defaultExpires = 10;
        //等待时重试时间间隔：毫秒，默认60毫秒
        private readonly int _retryInterval = 60;
        //1970年
        private readonly DateTime _time1970 = new DateTime(1970, 1, 1, 0, 0, 0, 0);

        public LockUtil(RedisClient cach)
        {
            _redis = cach ?? throw new ArgumentNullException(nameof(cach));
        }

        public bool GetLock(string lockKey, int expiresSenconds = 10, int waitTimeSenconds = 10)
        {
            ValidateLockKey(lockKey);

            if (expiresSenconds <= 0)
            {
                expiresSenconds = _defaultExpires;
            }

            var lockToken = Guid.NewGuid().ToString("N");
            if (waitTimeSenconds <= 0)
            {
                return TryAcquire(lockKey, lockToken, expiresSenconds);
            }

            var now = CurrentTimeStamp();
            var waitEndTime = now + waitTimeSenconds * 1000;
            var result = false;

            while (!result && now <= waitEndTime)
            {
                result = TryAcquire(lockKey, lockToken, expiresSenconds);
                if (!result)
                {
                    Thread.Sleep(_retryInterval);
                    now = CurrentTimeStamp();
                }
            }

            return result;
        }

        public async Task<bool> GetLockAsync(string lockKey, int expiresSenconds = 10, int waitTimeSenconds = 10)
        {
            ValidateLockKey(lockKey);

            if (expiresSenconds <= 0)
            {
                expiresSenconds = _defaultExpires;
            }

            var lockToken = Guid.NewGuid().ToString("N");
            if (waitTimeSenconds <= 0)
            {
                return TryAcquire(lockKey, lockToken, expiresSenconds);
            }

            var now = CurrentTimeStamp();
            var waitEndTime = now + waitTimeSenconds * 1000;
            long leftTime;
            var result = false;

            while (!result && now <= waitEndTime)
            {
                result = TryAcquire(lockKey, lockToken, expiresSenconds);
                if (!result)
                {
                    leftTime = waitEndTime - now;
                    await Task.Delay(leftTime >= _retryInterval ? _retryInterval : (int)leftTime);
                    now = CurrentTimeStamp();
                }
            }

            return result;
        }

        public void Release(string lockKey)
        {
            ValidateLockKey(lockKey);

            if (!_lockTokens.TryRemove(lockKey, out var lockToken))
            {
                return;
            }

            _redis.Eval(ReleaseLockScript, new[] { lockKey }, new object[] { lockToken });
        }

        /// <summary>
        /// 获取时间戳
        /// </summary>
        /// <returns></returns>
        private long CurrentTimeStamp()
        {
            var ts = DateTime.UtcNow - _time1970;
            return Convert.ToInt64(ts.TotalSeconds * 1000);
        }

        private bool TryAcquire(string lockKey, string lockToken, int expiresSenconds)
        {
            var acquired = _redis.SetNx(lockKey, lockToken, expiresSenconds);
            if (acquired)
            {
                _lockTokens[lockKey] = lockToken;
            }

            return acquired;
        }

        private static void ValidateLockKey(string lockKey)
        {
            if (string.IsNullOrWhiteSpace(lockKey))
            {
                throw new ArgumentException("锁键不能为空", nameof(lockKey));
            }
        }
    }
}
