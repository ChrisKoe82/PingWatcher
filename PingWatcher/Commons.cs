namespace PingWatcher
{
    class Commons
    {
        public class PingInformation
        {
            public int GatewayTimeouts { get; set; }
            public int GatewayPing { get; set; }
            public int TargetTimeouts { get; set; }
            public int TargetPing { get; set; }
        }
    }
}
