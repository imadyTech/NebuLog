
namespace imady.NebuLog.DataModel
{
    public class NebuLogRefreshStatRequest : INebuLogRequest
    {
        public string StatId { get; set; }
        public string StatValue { get; set; }

    }
}