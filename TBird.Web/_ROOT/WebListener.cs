using System.Net;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Web
{
    public partial class WebListener : TBirdObject
    {
        public WebListener(string prefix, int port)
        {
            Port = port;
            Prefix = $"{prefix}:{port}/";

            _listener = new HttpListener();
            _listener.Prefixes.Clear();
            _listener.Prefixes.Add(Prefix);
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

        public WebListener() : this(ListenerUtil.GetUnusedPort())
        {

        }

        public int Port { get; private set; }

        public string Prefix { get; private set; }

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