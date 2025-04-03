namespace ProtocolInterop
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    public delegate void BufCallbackFn(ByteBuffer respBuf);
    public delegate void CallbackFn(string respStr);

    [StructLayout(LayoutKind.Sequential)]
    public struct ByteBuffer
    {
        public IntPtr Data;
        public UIntPtr Len;
    }

    public static partial class Rust
    {
        [LibraryImport("protocol_interop", EntryPoint = "init_sys")]
        public static partial void InitSys();

        [LibraryImport("protocol_interop", EntryPoint = "invoke", StringMarshalling = StringMarshalling.Utf8)]
        public static partial void Invoke(string reqStr, CallbackFn callback);
    }

    class InvokerAwaitable
    {
        private InvokerAwaiter awaiter;

        public InvokerAwaitable(string reqValue)
        {
            this.awaiter = new InvokerAwaiter();
            Rust.Invoke(reqValue, this.awaiter.Complete);
        }

        public InvokerAwaiter GetAwaiter()
        {
            return this.awaiter;
        }
    }

    class InvokerAwaiter : INotifyCompletion
    {
        private readonly SemaphoreSlim semaphore;
        private readonly ExecutionContext? executionContext;
        private string respValue;
        private Action? continuation;

        public InvokerAwaiter()
        {
            semaphore = new SemaphoreSlim(1);

            executionContext = ExecutionContext.Capture();
            respValue = string.Empty;
            continuation = null;

            IsCompleted = false;
        }

        public bool IsCompleted { get; private set; }

        public string GetResult()
        {
            try
            {
                semaphore.Wait();
                return respValue;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public void OnCompleted(Action continuation)
        {
            try
            {
                semaphore.Wait();
                this.continuation = continuation;
            }
            finally
            {
                semaphore.Release();
            }
        }

        internal void Complete(string respStr)
        {
            try
            {
                semaphore.Wait();
                this.respValue = respStr;
                this.IsCompleted = true;

                if (this.continuation != null)
                {
                    if (this.executionContext != null)
                    {
                        ThreadPool.QueueUserWorkItem((_) => { ExecutionContext.Run(this.executionContext, (_) => { this.continuation(); }, null); }, null);
                    }
                    else
                    {
                        ThreadPool.QueueUserWorkItem((_) => { this.continuation(); }, null);
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        }
    }

    public class Invoker
    {
        public Invoker()
        {
            Thread invokerThread = new Thread(Rust.InitSys);
            invokerThread.IsBackground = true;
            invokerThread.Start();
        }

        public async Task<string> Invoke(string reqValue)
        {
            return await new InvokerAwaitable(reqValue);
        }
    }
}
