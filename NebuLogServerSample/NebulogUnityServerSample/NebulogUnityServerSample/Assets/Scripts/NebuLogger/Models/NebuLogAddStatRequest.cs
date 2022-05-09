namespace imady.NebuLog
{
    public class NebuLogAddStatRequest: INebuLogRequest
    {
        public string StatId { get; set; }
        public string StatTitle { get; set; }
        public string StatValue { get; set; }
        public string StatColor { get; set; }
    }
}