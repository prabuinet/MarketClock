using System.Windows;
using MarketClock.Data;
using Media = System.Windows.Media;

namespace MarketClock.Panels
{
    // Daily Expenses panel: how much was spent on each day of the chosen period
    // (last week, month or year), shown either as a table or as a bar chart.
    // An expense is any money going out of an account, other than a daily MTM entry or a transfer.
    // Clicking a line of the table or a bar of the chart lists the expenses behind it;
    // Back returns to the table or chart.
    public partial class DailyExpensesControl : System.Windows.Controls.UserControl
    {
        /// <summary>One line of the table.</summary>
        public sealed record ExpenseRow(string Date, decimal Amount, DateTime Day);

        /// <summary>One bar of the chart: a day, or a month when the period is a year.</summary>
        /// <remarks>It covers the days from From up to, but not including, To.</remarks>
        private sealed record ExpenseBar(string Label, decimal Amount, DateTime From, DateTime To, string Title);

        private static readonly Media.Brush BarHoverBrush = Media.Brushes.LightCyan;

        private IReadOnlyList<Account> accounts = Array.Empty<Account>();
        private List<(Account Account, AccountTransaction Transaction)> expenses = new(); // in the chosen period
        private List<ExpenseBar> bars = new();
        private (DateTime From, DateTime To, string Title)? detail; // the day (or month) opened by a click, if any
        private string summary = "";
        private string? error;
        private bool chartMode;
        private bool loaded;   // false until the accounts have arrived
        private bool ready;    // false while the panel is still being built
        private int hoverIndex = -1;

        public DailyExpensesControl()
        {
            InitializeComponent();
            ready = true;
            UpdateModeButtons();
        }

        /// <summary>Restores the mode and period that were last chosen.</summary>
        public void LoadSettings()
        {
            var settings = AppSettings.Load();
            chartMode = settings.DailyExpensesChart;

            ready = false; // set the dropdown without saving the settings back
            foreach (System.Windows.Controls.ComboBoxItem item in RangeComboBox.Items)
            {
                if ((item.Tag as string) == settings.DailyExpensesRange)
                {
                    RangeComboBox.SelectedItem = item;
                }
            }

            ready = true;
            UpdateModeButtons();
            Refresh();
        }

        public void ShowAccounts(IReadOnlyList<Account> accounts)
        {
            this.accounts = accounts;
            error = null;
            loaded = true;
            Refresh();
        }

        public void ShowError(string message)
        {
            error = message;
            Refresh();
        }

        private string SelectedRange =>
            (RangeComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "Month";

        // ---- Mode and period ----

        private void TableMode_Click(object sender, RoutedEventArgs e) => SetChartMode(false);

        private void ChartMode_Click(object sender, RoutedEventArgs e) => SetChartMode(true);

        private void SetChartMode(bool chart)
        {
            if (chartMode == chart)
            {
                return;
            }

            chartMode = chart;
            UpdateModeButtons();
            Refresh();
            SaveSettings();
        }

        private void UpdateModeButtons()
        {
            Theme.SetAccent(TableModeButton, ForegroundProperty, !chartMode, Media.Brushes.White);
            Theme.SetAccent(ChartModeButton, ForegroundProperty, chartMode, Media.Brushes.White);
        }

        private void Range_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Also raised while the panel is still being built, and while the saved choice is restored.
            if (!ready)
            {
                return;
            }

            Refresh();
            SaveSettings();
        }

        private void SaveSettings()
        {
            try
            {
                var settings = AppSettings.Load();
                settings.DailyExpensesChart = chartMode;
                settings.DailyExpensesRange = SelectedRange;
                settings.Save();
            }
            catch (Exception)
            {
                // Not being able to remember the choice should not interrupt anything.
            }
        }

        // ---- Working out what to show ----

        private void Refresh()
        {
            if (!ready)
            {
                return;
            }

            // The header shows the filters, or the opened day with a Back button.
            var inDetail = detail != null;
            TitleText.Text = detail?.Title ?? "Daily Expenses";
            ModeButtons.Visibility = inDetail ? Visibility.Collapsed : Visibility.Visible;
            RangeComboBox.Visibility = inDetail ? Visibility.Collapsed : Visibility.Visible;
            BackButton.Visibility = inDetail ? Visibility.Visible : Visibility.Collapsed;

            if (error != null || !loaded)
            {
                ShowMessage(error ?? "Loading...");
                SummaryText.Text = "";
                return;
            }

            var today = DateTime.Today;
            var range = SelectedRange;
            var start = range switch
            {
                "Week" => today.AddDays(-6),
                "Year" => new DateTime(today.Year, today.Month, 1).AddMonths(-11),
                _ => today.AddDays(-29),
            };

            // Spent per day. Money going out is stored as a negative amount; opening balances
            // daily MTM entries and transfers between accounts are not expenses.
            expenses = DashboardTransactions.Activity(accounts)
                .Where(x => x.Transaction.Amount < 0 && !x.Transaction.IsDailyMtm && !x.Transaction.IsTransfer
                    && x.Transaction.Date.Date >= start && x.Transaction.Date.Date <= today)
                .ToList();

            if (detail != null)
            {
                ShowDetail(detail.Value.From, detail.Value.To);
                return;
            }

            var byDay = expenses
                .Select(x => x.Transaction)
                .GroupBy(t => t.Date.Date)
                .ToDictionary(group => group.Key, group => -group.Sum(t => t.Amount));

            var total = byDay.Values.Sum();
            var days = (today - start).Days + 1;
            var period = range switch
            {
                "Week" => "last 7 days",
                "Year" => "last 12 months",
                _ => "last 30 days",
            };
            summary = $"{total:N2} in the {period}  ·  {total / days:N2} a day";
            SummaryText.Text = summary;

            if (byDay.Count == 0)
            {
                bars = new();
                ExpenseRowsList.ItemsSource = null;
                ShowMessage("No expenses in this period.");
                return;
            }

            ExpenseRowsList.ItemsSource = byDay
                .OrderByDescending(day => day.Key)
                .Select(day => new ExpenseRow(day.Key.ToString(range == "Year" ? "dd MMM yyyy" : "ddd, dd MMM"), day.Value, day.Key))
                .ToList();

            bars = new();
            if (range == "Year")
            {
                // 365 bars would be too thin to read, so a year is drawn month by month.
                for (var month = start; month <= today; month = month.AddMonths(1))
                {
                    var amount = byDay.Where(day => day.Key.Year == month.Year && day.Key.Month == month.Month).Sum(day => day.Value);
                    bars.Add(new ExpenseBar(month.ToString("MMM yyyy"), amount, month, month.AddMonths(1), month.ToString("MMMM yyyy")));
                }
            }
            else
            {
                for (var day = start; day <= today; day = day.AddDays(1))
                {
                    bars.Add(new ExpenseBar(
                        day.ToString("ddd, dd MMM"), byDay.GetValueOrDefault(day), day, day.AddDays(1), day.ToString("ddd, dd MMM yyyy")));
                }
            }

            EmptyText.Visibility = Visibility.Collapsed;
            DetailView.Visibility = Visibility.Collapsed;
            TableView.Visibility = chartMode ? Visibility.Collapsed : Visibility.Visible;
            ChartView.Visibility = chartMode ? Visibility.Visible : Visibility.Collapsed;
            DrawChart();
        }

        private void ShowMessage(string message)
        {
            EmptyText.Text = message;
            EmptyText.Visibility = Visibility.Visible;
            TableView.Visibility = Visibility.Collapsed;
            ChartView.Visibility = Visibility.Collapsed;
            DetailView.Visibility = Visibility.Collapsed;
        }

        // ---- One day's expenses ----

        private void ExpenseRow_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is ExpenseRow row)
            {
                OpenDetail(row.Day, row.Day.AddDays(1), row.Day.ToString("ddd, dd MMM yyyy"));
            }
        }

        private void OpenDetail(DateTime from, DateTime to, string title)
        {
            detail = (from, to, title);
            Refresh();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            detail = null;
            Refresh();
        }

        /// <summary>Lists every expense dated from <paramref name="from"/> up to, but not including, <paramref name="to"/>.</summary>
        private void ShowDetail(DateTime from, DateTime to)
        {
            var items = expenses
                .Where(x => x.Transaction.Date.Date >= from && x.Transaction.Date.Date < to)
                .OrderByDescending(x => x.Transaction.Date)
                .ThenByDescending(x => x.Transaction.Id)
                .ToList();

            var total = -items.Sum(x => x.Transaction.Amount);
            SummaryText.Text = items.Count == 1 ? $"{total:N2} in 1 expense" : $"{total:N2} in {items.Count} expenses";

            if (items.Count == 0)
            {
                ShowMessage("No expenses here.");
                return;
            }

            DetailList.ItemsSource = items.Select(x => DashboardTransactions.ToRow(x.Account, x.Transaction)).ToList();
            EmptyText.Visibility = Visibility.Collapsed;
            TableView.Visibility = Visibility.Collapsed;
            ChartView.Visibility = Visibility.Collapsed;
            DetailView.Visibility = Visibility.Visible;
        }

        // ---- Chart ----

        private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawChart();
        }

        private void DrawChart()
        {
            var width = ChartCanvas.ActualWidth;
            var height = ChartCanvas.ActualHeight;

            ChartCanvas.Children.Clear();
            hoverIndex = -1;

            if (bars.Count == 0 || width <= 0 || height <= 0)
            {
                return;
            }

            var max = bars.Max(bar => bar.Amount);
            ChartMaxLabel.Text = max.ToString("N0");
            ChartStartLabel.Text = bars[0].Label;
            ChartEndLabel.Text = bars[^1].Label;

            var slot = width / bars.Count;
            var gap = Math.Min(4, slot * 0.25);
            var barWidth = Math.Max(1, slot - gap);

            // One rectangle per bar, in order, so the bar under the mouse can be found by position.
            foreach (var (bar, index) in bars.Select((bar, index) => (bar, index)))
            {
                var barHeight = max > 0 && bar.Amount > 0
                    ? Math.Max(1, (double)(bar.Amount / max) * height)
                    : 0;

                var rectangle = new System.Windows.Shapes.Rectangle
                {
                    Width = barWidth,
                    Height = barHeight,
                    IsHitTestVisible = false,
                };
                rectangle.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, Theme.AccentKey);
                System.Windows.Controls.Canvas.SetLeft(rectangle, index * slot + gap / 2);
                System.Windows.Controls.Canvas.SetTop(rectangle, height - barHeight);
                ChartCanvas.Children.Add(rectangle);
            }
        }

        private void ChartCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var width = ChartCanvas.ActualWidth;
            if (bars.Count == 0 || width <= 0 || ChartCanvas.Children.Count != bars.Count)
            {
                return;
            }

            var index = Math.Clamp((int)(e.GetPosition(ChartCanvas).X / width * bars.Count), 0, bars.Count - 1);
            SetHover(index);
            SummaryText.Text = $"{bars[index].Label}   {bars[index].Amount:N2}";
        }

        private void ChartCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Handled here so the click does not also start dragging the window.
            e.Handled = true;

            var width = ChartCanvas.ActualWidth;
            if (bars.Count == 0 || width <= 0)
            {
                return;
            }

            var bar = bars[Math.Clamp((int)(e.GetPosition(ChartCanvas).X / width * bars.Count), 0, bars.Count - 1)];
            if (bar.Amount > 0)
            {
                SetHover(-1);
                OpenDetail(bar.From, bar.To, bar.Title);
            }
        }

        private void ChartCanvas_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            SetHover(-1);
            SummaryText.Text = summary;
        }

        /// <summary>Highlights the bar under the mouse.</summary>
        private void SetHover(int index)
        {
            if (index == hoverIndex)
            {
                return;
            }

            if (hoverIndex >= 0 && hoverIndex < ChartCanvas.Children.Count)
            {
                ((System.Windows.Shapes.Rectangle)ChartCanvas.Children[hoverIndex]).SetResourceReference(System.Windows.Shapes.Shape.FillProperty, Theme.AccentKey);
            }

            if (index >= 0 && index < ChartCanvas.Children.Count)
            {
                ((System.Windows.Shapes.Rectangle)ChartCanvas.Children[index]).Fill = BarHoverBrush;
            }

            hoverIndex = index;
        }
    }
}
