using System;
using System.Net;
using System.Net.NetworkInformation;

namespace MyPing
{
    public enum PingStatus
    {
        Ok,
        Error
    };

    public class PingResult
    {
        internal PingResult(long ping)
        {
            Ping = ping;
            Status = PingStatus.Ok;
        }

        internal PingResult(PingStatus status)
        {
            Ping = null;
            Status = status;
        }

        public long? Ping { get; internal set; }
        public PingStatus Status { get; internal set; }
    }


    public abstract class PingChecker
    {
        public abstract PingResult Get();

        public String HostString { get; protected set; }

        public virtual bool SetHost(String host)
        {
            HostString = host;

            if (HostChanged != null)
                HostChanged(host);

            return true;
        }
        public event Action<String> HostChanged;
    }

    class RandomPingChecker : PingChecker
    {
        private readonly Random _rnd = new Random();

        public override PingResult Get()
        {
            var p =_rnd.Next(400);
            return (p > 300) ? new PingResult(PingStatus.Error) : new PingResult(p);
        }
    }

    class RealPingChecker : PingChecker
    {
        protected IPAddress HostIpAddress;
        private readonly Ping  _pingSender = new Ping();

        public RealPingChecker(String host = "8.8.8.8")
        {
            SetHost(host);
        }

        public override bool SetHost(String host)
        {
            try
            {
                HostIpAddress = IPAddress.Parse(host);
                return base.SetHost(host);
            }
            catch (Exception)
            {
                HostIpAddress = null;
                base.SetHost("");
            }
            return false;
        }

        public override PingResult Get()
        {
            if (HostIpAddress != null)
            {
                try
                {
                    PingReply reply = _pingSender.Send(HostIpAddress);

                    if (reply != null && reply.Status == IPStatus.Success)
                        return new PingResult(reply.RoundtripTime);
                }
                catch (Exception)
                {
                }
            }

            return new PingResult(PingStatus.Error);
        }
    }
}
