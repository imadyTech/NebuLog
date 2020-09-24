using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NebuLogBlazorServerSample
{
    public class AppState
    {
        private readonly List<Expense> _expenses = new List<Expense>();
        public IReadOnlyList<Expense> Expenses => _expenses;

        public event Action OnExpenseAdded;


        public void AddExpense(Expense expense)
        {
            _expenses.Add(expense);
            StateChanged();
        }


        private void StateChanged() => OnExpenseAdded?.Invoke();
    }
}
