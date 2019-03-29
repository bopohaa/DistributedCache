using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace DistributedCache.Common
{
    public class DistributedCacheConnectionString
    {
        public struct Options
        {
            public readonly TimeSpan KeepaliveInterval;
            public readonly TimeSpan ReconnectInterval;
            public readonly TimeSpan ConnectTimeout;
            public readonly int ReconnectCount;

            public Options(TimeSpan keepalive_interval, TimeSpan reconnect_interval, int reconnect_count, TimeSpan connect_timeout)
            {
                KeepaliveInterval = keepalive_interval;
                ReconnectInterval = reconnect_interval;
                ReconnectCount = reconnect_count;
                ConnectTimeout = connect_timeout;
            }

            public static Options Parse(string query_string, TimeSpan keepalive_interval, TimeSpan reconnect_interval, int reconnect_count, TimeSpan connect_timeout)
            {
                var parts = query_string.Split('&');
                foreach (var part in parts)
                {
                    var data = part.Split('=');
                    if (data.Length > 1)
                    {
                        switch (data[0])
                        {
                            case "keepalive_interval":
                                keepalive_interval = TimeSpan.FromMilliseconds(int.Parse(WebUtility.UrlDecode(data[1])));
                                break;
                            case "reconnect_interval":
                                reconnect_interval = TimeSpan.FromMilliseconds(int.Parse(WebUtility.UrlDecode(data[1])));
                                break;
                            case "connect_timeout":
                                connect_timeout = TimeSpan.FromMilliseconds(int.Parse(WebUtility.UrlDecode(data[1])));
                                break;
                            case "reconnect_count":
                                reconnect_count = int.Parse(WebUtility.UrlDecode(data[1]));
                                break;
                            default:
                                break;
                        }
                    }
                }
                return new Options(keepalive_interval, reconnect_interval, reconnect_count, connect_timeout);
            }
        }

        public readonly string Host;
        public readonly string Path;
        public readonly int Port;
        public readonly Options ConnectionOptions;

        public DistributedCacheConnectionString(string host, string path, int port, Options options)
        {
            Host = host;
            Path = path;
            Port = port;
            ConnectionOptions = options;
        }

        public static DistributedCacheConnectionString Parse(string connection_string, TimeSpan keepalive_interval, TimeSpan reconnect_interval, int reconnect_count, TimeSpan connect_timeout)
        {
            var s = new Uri(connection_string, UriKind.Absolute);

            return new DistributedCacheConnectionString(s.Host, s.AbsolutePath, s.Port, Options.Parse(s.Query, keepalive_interval, reconnect_interval, reconnect_count, connect_timeout));
        }
    }
}
