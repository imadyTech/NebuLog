namespace NebuLogBlazorServerSample
{
    public class NebuLogMessage
    {
        public string Description { get; set; }
        public decimal Amount { get; set; }


        public NebuLogMessage()
        {
            Description = "";
            Amount = 0.0m;
        }
    }
}