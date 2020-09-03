﻿using System;
using System.Threading;
using NewLife.Log;
using NewLife.Threading;

namespace NewLife.Caching
{
    /// <summary>分布式锁</summary>
    public class CacheLock : DisposeBase
    {
        private ICache Client { get; set; }

        /// <summary>键</summary>
        public String Key { get; set; }

        /// <summary>实例化</summary>
        /// <param name="client"></param>
        /// <param name="key"></param>
        public CacheLock(ICache client, String key)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (key.IsNullOrEmpty()) throw new ArgumentNullException(nameof(key));

            Client = client;
            Key = "lock:" + key;
        }

        /// <summary>申请锁</summary>
        /// <param name="msTimeout">锁等待时间</param>
        /// <returns></returns>
        public Boolean Acquire(Int32 msTimeout)
        {
            var ch = Client;
            var now = DateTime.Now;
            var sTimeout = msTimeout / 1000;

            // 循环等待
            var end = now.AddMilliseconds(msTimeout);
            while (now < end)
            {
                var expire = now.AddMilliseconds(msTimeout);

                // 申请加锁。没有冲突时可以直接返回
                var rs = ch.Add(Key, expire, sTimeout);
                if (rs) return true;

                // 死锁超期检测
                var dt = ch.Get<DateTime>(Key);
                if (dt <= now)
                {
#if DEBUG
                    XTrace.WriteLine("[{0}]抢超期死锁，{1}=>{2}", Key, dt, expire);
#endif
                    // 开抢死锁。所有竞争者都会修改该锁的时间戳，但是只有一个能拿到旧的超时的值
                    expire = now.AddMilliseconds(msTimeout);
                    var old = ch.Replace(Key, expire);
                    // 如果拿到超时值，说明抢到了锁。其它线程会抢到一个为超时的值
                    if (old <= now)
                    {
                        ch.SetExpire(Key, TimeSpan.FromMilliseconds(msTimeout));
                        return true;
                    }
                }

                // 没抢到，继续
                Thread.Sleep(200);

                now = DateTime.Now;
            }

            return false;
        }

        /// <summary>销毁</summary>
        /// <param name="disposing"></param>
        protected override void Dispose(Boolean disposing)
        {
            base.Dispose(disposing);

            // 如果客户端已释放，则不删除
            if (Client is DisposeBase db && db.Disposed)
            {
            }
            else
            {
                Client.Remove(Key);
            }
        }
    }
}