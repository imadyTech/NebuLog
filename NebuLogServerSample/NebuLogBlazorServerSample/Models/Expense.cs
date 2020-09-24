namespace NebuLogBlazorServerSample
{
    public class Expense
    {
        public string Description { get; set; }
        public decimal Amount { get; set; }


        public Expense()
        {
            Description = "";
            Amount = 0.0m;
        }
    }
}