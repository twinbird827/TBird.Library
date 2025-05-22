using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TBird.Core
{
    /// <summary>
    /// ｽﾄﾘｰﾑのWrapperクラス
    /// </summary>
    /// <remarks>
    /// Dispose 時に、内部ｽﾄﾘｰﾑの参照を外します
    /// </remarks>
    public class WrappingStream : Stream
    {
        Stream? m_streamBase;

        public WrappingStream(Stream streamBase)
        {
            if (streamBase == null)
            {
                throw new ArgumentNullException("streamBase");
            }
            m_streamBase = streamBase; //渡したStreamを内部ストリームとして保持
        }

        /// ****************************************************************************************************
        /// override 定義
        /// ****************************************************************************************************

        public override bool CanRead
        {
            get => GetStreamBase().CanRead;
        }

        public override bool CanSeek
        {
            get => GetStreamBase().CanSeek;
        }

        public override bool CanWrite
        {
            get => GetStreamBase().CanWrite;
        }

        public override long Length
        {
            get => GetStreamBase().Length;
        }

        public override long Position
        {
            get => GetStreamBase().Position;
            set => GetStreamBase().Position = value;
        }

        public override int ReadTimeout
        {
            get => GetStreamBase().ReadTimeout;
            set => GetStreamBase().ReadTimeout = value;
        }

        public override int WriteTimeout
        {
            get => GetStreamBase().WriteTimeout;
            set => GetStreamBase().WriteTimeout = value;
        }

        public override bool CanTimeout
        {
            get => GetStreamBase().CanTimeout;
        }

        public override void Flush() => GetStreamBase().Flush();

        public override int Read(byte[] buffer, int offset, int count) => GetStreamBase().Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            //ThrowIfDisposed(); return m_streamBase.ReadAsync(buffer, offset, count, cancellationToken);

            var tcs = new TaskCompletionSource<int>();
            var callback = new AsyncCallback(ar =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested || m_streamBase == null || !m_streamBase.CanRead || !ar.IsCompleted)
                    {
                        tcs.SetCanceled();
                        return;
                    }

                    var response = m_streamBase.EndRead(ar);
                    tcs.TrySetResult(response);
                }
                catch (Exception ex)
                {
                    MessageService.Exception(ex);
                    tcs.TrySetResult(0);
                    //tcs.TrySetException(ex);
                }
            });

            GetStreamBase().BeginRead(buffer, offset, count, callback, null);

            return tcs.Task;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            var callback = new AsyncCallback(ar =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested || m_streamBase == null || !ar.IsCompleted)
                    {
                        tcs.SetCanceled();
                        return;
                    }

                    m_streamBase.EndWrite(ar);
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            GetStreamBase().BeginWrite(buffer, offset, count, callback, null);

            return tcs.Task;
        }

        public override long Seek(long offset, SeekOrigin origin) => GetStreamBase().Seek(offset, origin);

        public override void SetLength(long value) => GetStreamBase().SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => GetStreamBase().Write(buffer, offset, count);

        public override void Close() => GetStreamBase().Close();

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => GetStreamBase().CopyToAsync(destination, bufferSize, cancellationToken);

        public override bool Equals(object obj) => GetStreamBase().Equals(obj);

        public override Task FlushAsync(CancellationToken cancellationToken) => GetStreamBase().FlushAsync(cancellationToken);

        public override int GetHashCode() => GetStreamBase().GetHashCode();

        public override object InitializeLifetimeService() => GetStreamBase().InitializeLifetimeService();

        public override int ReadByte() => GetStreamBase().ReadByte();

        public override void WriteByte(byte value) => GetStreamBase().WriteByte(value);

        public override string ToString() => GetStreamBase().ToString();

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
            GetStreamBase().BeginRead(buffer, offset, count, callback, state);

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
            GetStreamBase().BeginWrite(buffer, offset, count, callback, state);

        public override int EndRead(IAsyncResult asyncResult) => GetStreamBase().EndRead(asyncResult);

        public override void EndWrite(IAsyncResult asyncResult) => GetStreamBase().EndWrite(asyncResult);

        /// ****************************************************************************************************
        /// new 定義 (override不可ﾒｿｯﾄﾞの隠蔽)
        /// ****************************************************************************************************

        public new Task<int> ReadAsync(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count, CancellationToken.None);

        public new Task WriteAsync(byte[] buffer, int offset, int count) => WriteAsync(buffer, offset, count, CancellationToken.None);

        public new Task CopyToAsync(Stream destination) => CopyToAsync(destination, 81920);

        public new Task CopyToAsync(Stream destination, int bufferSize) => CopyToAsync(destination, bufferSize, CancellationToken.None);

        public new Task FlushAsync() => FlushAsync(CancellationToken.None);

        /// ****************************************************************************************************
        /// 内部Stream固有の処理
        /// ****************************************************************************************************

        /// <summary>
        /// ｽﾄﾘｰﾑの内容をﾊﾞｲﾄ配列に書き込みます。
        /// </summary>
        /// <returns></returns>
        public virtual byte[] ToArray()
        {
            var ms = m_streamBase as MemoryStream;
            if (ms != null)
            {
                return ms.ToArray();
            }
            else
            {
                throw new NotSupportedException("Inner stream is not MemoryStream");
            }
        }

        /// <summary>
        /// 破棄済かどうか
        /// </summary>
        public bool IsDisposed => m_streamBase == null;

        /// <summary>
        /// WrappingStreamによって使用されている全てのﾘｿｰｽを開放します。
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (m_streamBase != null && CanRead) m_streamBase.Dispose();
                m_streamBase = null;  //disposeしたら内部ストリームをnullにして参照を外す
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 内部Streamがすでに破棄されている場合、例外を発生させます。
        /// </summary>
        private Stream GetStreamBase()
        {
            return m_streamBase ?? throw new ObjectDisposedException(GetType().Name);
        }

    }
}