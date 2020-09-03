﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife.Data;
using NewLife.Security;
using NewLife.Serialization;

namespace NewLife.Log
{
    /// <summary>性能跟踪片段。轻量级APM</summary>
    public interface ISpan : IDisposable
    {
        /// <summary>唯一标识。随线程上下文、Http、Rpc传递，作为内部片段的父级</summary>
        String Id { get; set; }

        /// <summary>父级片段标识</summary>
        String ParentId { get; set; }

        /// <summary>跟踪标识。可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        String TraceId { get; set; }

        /// <summary>开始时间。Unix毫秒</summary>
        Int64 StartTime { get; set; }

        /// <summary>结束时间。Unix毫秒</summary>
        Int64 EndTime { get; set; }

        /// <summary>数据标签。记录一些附加数据</summary>
        String Tag { get; set; }

        /// <summary>错误信息</summary>
        String Error { get; set; }

        /// <summary>设置错误信息</summary>
        /// <param name="ex">异常</param>
        /// <param name="tag">标签</param>
        void SetError(Exception ex, Object tag);
    }

    /// <summary>性能跟踪片段。轻量级APM</summary>
    /// <remarks>
    /// spanId/traceId采用W3C标准，https://www.w3.org/TR/trace-context/
    /// </remarks>
    public class DefaultSpan : ISpan
    {
        #region 属性
        /// <summary>构建器</summary>
        [XmlIgnore, ScriptIgnore, IgnoreDataMember]
        public ISpanBuilder Builder { get; }

        /// <summary>唯一标识。随线程上下文、Http、Rpc传递，作为内部片段的父级</summary>
        public String Id { get; set; }

        /// <summary>父级片段标识</summary>
        public String ParentId { get; set; }

        /// <summary>跟踪标识。可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public String TraceId { get; set; }

        /// <summary>开始时间。Unix毫秒</summary>
        public Int64 StartTime { get; set; }

        /// <summary>结束时间。Unix毫秒</summary>
        public Int64 EndTime { get; set; }

        /// <summary>数据标签。记录一些附加数据</summary>
        public String Tag { get; set; }

        /// <summary>错误信息</summary>
        public String Error { get; set; }

#if NET40
        [ThreadStatic]
        private static ISpan _Current;
        /// <summary>当前线程正在使用的上下文</summary>
        public static ISpan Current { get => _Current; set => _Current = value; }
#elif NET45
        private static readonly String FieldKey = typeof(DefaultSpan).FullName;
        /// <summary>当前线程正在使用的上下文</summary>
        public static ISpan Current
        {
            get => ((System.Runtime.Remoting.ObjectHandle)System.Runtime.Remoting.Messaging.CallContext.LogicalGetData(FieldKey))?.Unwrap() as ISpan;
            set => System.Runtime.Remoting.Messaging.CallContext.LogicalSetData(FieldKey, new System.Runtime.Remoting.ObjectHandle(value));
        }
#else
        private static readonly System.Threading.AsyncLocal<ISpan> _Current = new System.Threading.AsyncLocal<ISpan>();
        /// <summary>当前线程正在使用的上下文</summary>
        public static ISpan Current { get => _Current.Value; set => _Current.Value = value; }
#endif

        private ISpan _parent;
        private Boolean _finished;
        //private static Int64 _gid;
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public DefaultSpan() { }

        /// <summary>实例化</summary>
        /// <param name="builder"></param>
        public DefaultSpan(ISpanBuilder builder)
        {
            Builder = builder;
            StartTime = DateTime.UtcNow.ToLong();
        }

        /// <summary>释放资源</summary>
        public void Dispose() => Finish();
        #endregion

        #region 方法
        /// <summary>设置跟踪标识</summary>
        public virtual void Start()
        {
            if (Id.IsNullOrEmpty()) Id = Rand.NextBytes(8).ToHex().ToLower();

            // 设置父级
            var span = Current;
            if (span != null && span != this)
            {
                _parent = span;

                ParentId = span.Id;
                TraceId = span.TraceId;
            }

            // 否则创建新的跟踪标识
            if (TraceId.IsNullOrEmpty()) TraceId = Rand.NextBytes(16).ToHex().ToLower();

            // 设置当前片段
            Current = this;
        }

        /// <summary>完成跟踪</summary>
        protected virtual void Finish()
        {
            if (_finished) return;
            _finished = true;

            EndTime = DateTime.UtcNow.ToLong();

            // 从本线程中清除跟踪标识
            Current = _parent;

            Builder.Finish(this);
        }

        /// <summary>设置错误信息</summary>
        /// <param name="ex">异常</param>
        /// <param name="tag">标签</param>
        public virtual void SetError(Exception ex, Object tag)
        {
            Error = ex?.GetMessage();
            if (tag is String str)
                Tag = str?.Cut(1024);
            else if (tag != null)
                Tag = tag?.ToJson().Cut(1024);
        }

        /// <summary>已重载。</summary>
        /// <returns></returns>
        public override String ToString() => $"00-{TraceId}-{Id}-00";
        #endregion
    }

    /// <summary>跟踪片段扩展</summary>
    public static class SpanExtension
    {
        #region 扩展方法
        private static String GetAttachParameter(ISpan span)
        {
            var builder = (span as DefaultSpan)?.Builder;
            var tracer = (builder as DefaultSpanBuilder)?.Tracer;
            return tracer?.AttachParameter;
        }

        /// <summary>把片段信息附加到http请求头上</summary>
        /// <param name="span">片段</param>
        /// <param name="request">http请求</param>
        /// <returns></returns>
        public static HttpRequestMessage Attach(this ISpan span, HttpRequestMessage request)
        {
            if (span == null || request == null) return request;

            // 注入参数名
            var name = GetAttachParameter(span);
            if (name.IsNullOrEmpty()) return request;

            var headers = request.Headers;
            if (!headers.Contains(name)) headers.Add(name, span.ToString());

            return request;
        }

        /// <summary>把片段信息附加到api请求头上</summary>
        /// <param name="span">片段</param>
        /// <param name="args">api请求参数</param>
        /// <returns></returns>
        public static Object Attach(this ISpan span, Object args)
        {
            if (span == null || args == null || args is Packet || args is Byte[] || args is IAccessor) return args;
            if (Type.GetTypeCode(args.GetType()) != TypeCode.Object) return args;

            // 注入参数名
            var name = GetAttachParameter(span);
            if (name.IsNullOrEmpty()) return args;

            var headers = args.ToDictionary();
            if (!headers.ContainsKey(name)) headers.Add(name, span.ToString());

            return headers;
        }

        /// <summary>从http请求头释放片段信息</summary>
        /// <param name="span">片段</param>
        /// <param name="headers">http请求头</param>
        public static void Detach(this ISpan span, NameValueCollection headers)
        {
            if (span == null || headers == null || headers.Count == 0) return;

            if (headers.AllKeys.Contains("traceparent"))
            {
                var tid = headers["traceparent"];
                var ss = (tid + "").Split("-");
                if (ss.Length > 1) span.TraceId = ss[1];
                if (ss.Length > 2) span.ParentId = ss[2];
            }
            else if (headers.AllKeys.Contains("Request-Id"))
            {
                // HierarchicalId编码取最后一段作为父级
                var tid = headers["Request-Id"];
                var ss = (tid + "").Split(".", "_");
                if (ss.Length > 0) span.TraceId = ss[0].TrimStart('|');
                if (ss.Length > 1) span.ParentId = ss[ss.Length - 1];
            }
        }

        /// <summary>从api请求释放片段信息</summary>
        /// <param name="span">片段</param>
        /// <param name="parameters">参数</param>
        public static void Detach(this ISpan span, IDictionary<String, Object> parameters)
        {
            if (span == null || parameters == null || parameters.Count == 0) return;

            if (parameters.TryGetValue("traceparent", out var tid))
            {
                var ss = (tid + "").Split("-");
                if (ss.Length > 1) span.TraceId = ss[1];
                if (ss.Length > 2) span.ParentId = ss[2];
            }
            else if (parameters.TryGetValue("Request-Id", out tid))
            {
                // HierarchicalId编码取最后一段作为父级
                var ss = (tid + "").Split(".", "_");
                if (ss.Length > 0) span.TraceId = ss[0].TrimStart('|');
                if (ss.Length > 1) span.ParentId = ss[ss.Length - 1];
            }
        }

        /// <summary>从api请求释放片段信息</summary>
        /// <param name="span">片段</param>
        /// <param name="parameters">参数</param>
        public static void Detach<T>(this ISpan span, IDictionary<String, T> parameters)
        {
            if (span == null || parameters == null || parameters.Count == 0) return;

            if (parameters.TryGetValue("traceparent", out var tid))
            {
                var ss = (tid + "").Split("-");
                if (ss.Length > 1) span.TraceId = ss[1];
                if (ss.Length > 2) span.ParentId = ss[2];
            }
            else if (parameters.TryGetValue("Request-Id", out tid))
            {
                // HierarchicalId编码取最后一段作为父级
                var ss = (tid + "").Split(".", "_");
                if (ss.Length > 0) span.TraceId = ss[0].TrimStart('|');
                if (ss.Length > 1) span.ParentId = ss[ss.Length - 1];
            }
        }
        #endregion
    }
}