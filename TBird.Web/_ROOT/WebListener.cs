using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using TBird.Core;
using System.Net.Http;
using System.Threading.Tasks;

namespace TBird.Web
{
    public partial class WebListener : TBirdObject
    {
        public WebListener(string prefix, int port)
        {
            Port = port;

            _listener = new HttpListener();
            _listener.Prefixes.Clear();
            _listener.Prefixes.Add($"{prefix}:{port}");
            _listener.Start();

            AddDisposed((sender, e) =>
            {
                _listener.Stop();
                _listener = null;
            });
        }

        public WebListener(int port) : this(@"http://localhost", port)
        {

        }

        public WebListener() : this(ListenerUtil.GetAvailablePort(50000))
        {

        }

        public int Port { get; private set; }

        private HttpListener _listener;

        public HttpListenerContext GetContext()
        {
            return _listener.GetContext();
        }

        public Task<HttpListenerContext> GetContextAsync()
        {
            return _listener.GetContextAsync();
        }
    }
}