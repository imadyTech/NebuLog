using imady.Message;

namespace imady.NebuLog
{
    public class NebuLogStatMsg : MadYMessageBase, INebuLogRequest
    {
        public string StatId { get; set; }
        public string StatTitle { get; set; }
        public string StatValue { get; set; }
        public string StatColor { get; set; }
    }
}