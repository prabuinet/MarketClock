using System.Windows;
using MarketClock.Data;

namespace MarketClock
{
    // Editing and deleting a saved transaction, the same way wherever it is listed
    // (the Recent Transactions panel and the Accounts window).
    internal static class TransactionActions
    {
        /// <summary>Opens the edit form. True when the transaction was changed or deleted.</summary>
        public static bool Edit(Window? owner, int transactionId)
        {
            var window = new ExpenseWindow(transactionId)
            {
                Owner = owner
            };

            return window.ShowDialog() == true;
        }

        /// <summary>Asks first, then deletes. True when the transaction was deleted.</summary>
        public static bool Delete(Window? owner, int transactionId, string summary, bool isTransfer)
        {
            var question = $"Delete this transaction?\n\n{summary}";
            if (isTransfer)
            {
                question += "\n\nIt is one side of a transfer: the matching entry in the other account is deleted too.";
            }

            if (Show(owner, question, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return false;
            }

            try
            {
                new AccountStore().DeleteTransaction(transactionId);
                return true;
            }
            catch (Exception ex)
            {
                Show(owner, $"Could not delete the transaction: {ex.Message}", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
        }

        private static MessageBoxResult Show(Window? owner, string text, MessageBoxButton buttons, MessageBoxImage image)
        {
            const string caption = "Delete Transaction";

            return owner == null
                ? System.Windows.MessageBox.Show(text, caption, buttons, image, MessageBoxResult.No)
                : System.Windows.MessageBox.Show(owner, text, caption, buttons, image, MessageBoxResult.No);
        }
    }
}
