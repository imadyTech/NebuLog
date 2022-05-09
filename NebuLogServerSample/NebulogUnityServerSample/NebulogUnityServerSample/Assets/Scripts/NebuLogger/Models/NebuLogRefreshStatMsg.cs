using imady.Message;

namespace imady.NebuLog
{
    public class NebuLogRefreshStatMsg : MadYMessageBase, INebuLogRequest
    {
        public string StatId { get; set; }
        public string StatValue { get; set; }

    }
}