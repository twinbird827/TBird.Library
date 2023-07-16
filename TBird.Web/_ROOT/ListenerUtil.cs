using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;

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
    }
}