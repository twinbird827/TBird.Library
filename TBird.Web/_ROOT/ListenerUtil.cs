using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Web
{
    public static class ListenerUtil
    {
        public static IEnumerable<int> GetActivePorts()
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();

            return properties.GetActiveTcpConnections().Select(x => x.LocalEndPoint)
                .Concat(properties.GetActiveTcpListeners())
                .Concat(properties.GetActiveUdpListeners())
                .Select(x => x.Port);
        }

        public static int GetAvailablePort(int begin)
        {
            var actives = new HashSet<int>(GetActivePorts());

            for (var i = begin; i <= 65535; i++)
            {
                if (!actives.Contains(i)) return i;
            }

            return -1;
        }

        public static int GetUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            using (listener.Disposer(x => x.Stop()))
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
        }

        public static async Task WriteAutoClose(this HttpListenerResponse response)
        {
            //var html = @"<!DOCTYPE html><html><body onload=""open(location, '_self').close();""></body></html>";
            //var html = @"<!DOCTYPE html><html><head><script>window.onload = function(){ document.getElementById('d').innerHTML = 'xxxxxxxxxx'; window.open(""about:blank"",""_self"").close(); };</script></head><body><p>closed</p><p id=""d""></p></body></html>";
            var html = @"<!DOCTYPE html><html><head><script>function winclose() { open('about:blank', '_self').close(); } window.onload = function(){ setTimeout('winclose()', 100); };</script></head><body>closed</body></html>";

            var buffer = Encoding.UTF8.GetBytes(html);
            response.StatusCode = 200;
            response.ContentLength64 = buffer.Length;
            using (var stream = response.OutputStream)
            {
                await stream.WriteAsync(buffer, 0, buffer.Length);
            }
            response.Close();
        }
    }
}