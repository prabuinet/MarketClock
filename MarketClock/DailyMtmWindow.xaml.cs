using System.Globalization;
using System.Windows;
using MarketClock.Data;

namespace MarketClock
{
    /// <summary>
    /// Daily MTM form: one line per trading account, where the day's profit or loss is typed in.
    /// Picking a date that already has entries shows them, so they can be corrected and saved again.
    /// </summary>
    public partial class DailyMtmWindow : System.Windows.Window
    {
        /// <summary>One trading account on the form.</summary>
        public sealed class Row
        {
            public int AccountId { get; init; }

            public string AccountName { get; init; } = "";

            public string BalanceText { get; init; } = "";

            /// <summary>What is typed in the MTM box; blank means no entry for the day.</summary>
            public string MtmText { get; set; } = "";
        }

        private AccountStore? accountStore;
        private List<Row> rows = new();

        public DailyMtmWindow()
        {
            InitializeComponent();
            MtmDatePicker.SelectedDate = DateTime.Today;

            Loaded += async (_, _) => await LoadRowsAsync();
            MtmDatePicker.SelectedDateChanged += async (_, _) =>
            {
                if (IsLoaded)
                {
                    await LoadRowsAsync();
                }
            };
        }

        /// <summary>Shows the trading accounts with whatever is already recorded for the chosen date.</summary>
        private async Task LoadRowsAsync(string message = "")
        {
            if (MtmDatePicker.SelectedDate is not DateTime date)
            {
                return;
            }

            SaveButton.IsEnabled = false;

            try
            {
                var existingStore = accountStore;

                // Opening the database is slow the first time, so keep it off the UI thread.
                var (store, entries) = await Task.Run(() =>
                {
                    var loadedStore = existingStore ?? new AccountStore();
                    return (loadedStore, loadedStore.GetDailyMtm(date));
                });

                accountStore = store;

                if (MtmDatePicker.SelectedDate != date)
                {
                    return; // the date was changed again while loading; that load will fill the form
                }

                rows = entries
                    .Select(entry => new Row
                    {
                        AccountId = entry.AccountId,
                        AccountName = entry.AccountName,
                        BalanceText = entry.Balance.ToString("N2"),
                        MtmText = entry.Mtm?.ToString("0.##", CultureInfo.CurrentCulture) ?? "",
                    })
                    .ToList();

                RowsList.ItemsSource = rows;
                UpdateTotal();

                if (rows.Count == 0)
                {
                    MessageTextBlock.Text = "No trading accounts yet. Give an account the \"Trading account\" category to see it here.";
                    return;
                }

                MessageTextBlock.Text = message;
                SaveButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageTextBlock.Text = $"Could not load accounts: {ex.Message}";
            }
        }

        private void Mtm_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox { DataContext: Row row } textBox)
            {
                row.MtmText = textBox.Text;
                UpdateTotal();
            }
        }

        private void UpdateTotal()
        {
            var total = 0m;
            foreach (var row in rows)
            {
                if (TryParseAmount(row.MtmText, out var amount))
                {
                    total += amount;
                }
            }

            TotalText.Text = total.ToString("N2");
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (accountStore == null || MtmDatePicker.SelectedDate is not DateTime date)
            {
                MessageTextBlock.Text = "Pick a date.";
                return;
            }

            // A blank box means "no MTM for this account on this day" (and removes an earlier entry).
            var amounts = new Dictionary<int, decimal?>();
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.MtmText))
                {
                    amounts[row.AccountId] = null;
                }
                else if (TryParseAmount(row.MtmText, out var amount))
                {
                    amounts[row.AccountId] = amount;
                }
                else
                {
                    MessageTextBlock.Text = $"Enter a number for {row.AccountName} (use a minus sign for a loss).";
                    return;
                }
            }

            var store = accountStore;
            SaveButton.IsEnabled = false;

            try
            {
                await Task.Run(() => store.SaveDailyMtm(date, amounts));
            }
            catch (Exception ex)
            {
                MessageTextBlock.Text = $"Could not save: {ex.Message}";
                SaveButton.IsEnabled = true;
                return;
            }

            // Reload so the balances on the form show the reconciled figures.
            await LoadRowsAsync($"Saved MTM for {date:dd MMM yyyy}.");
        }

        private static bool TryParseAmount(string text, out decimal amount)
        {
            return decimal.TryParse(text, NumberStyles.Currency, CultureInfo.CurrentCulture, out amount) ||
                   decimal.TryParse(text, NumberStyles.Currency, CultureInfo.InvariantCulture, out amount);
        }
    }
}
