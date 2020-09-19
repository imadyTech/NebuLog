namespace imady.NebuLog
{
    public class NebuLogRefreshStatRequest : INebuLogRequest
    {
        public string StatId { get; set; }
        public string StatValue { get; set; }

    }
}