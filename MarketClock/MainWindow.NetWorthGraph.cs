using System.Windows;
using MarketClock.Data;

namespace MarketClock
{
    // Net worth graph panel: one point per day, for the range picked in the dropdown.
    public partial class MainWindow
    {
        private List<Account> dashboardAccounts = new();
        private List<(DateTime Date, decimal Value)> netWorthSeries = new();

        private void NetWorthRange_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Also raised while the window is still being built; the first draw happens after loading.
            if (IsLoaded)
            {
                UpdateNetWorthGraph();
            }
        }

        private void NetWorthChart_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawNetWorthGraph();
        }

        /// <summary>Rebuilds the daily net worth series for the selected range and redraws it.</summary>
        private void UpdateNetWorthGraph()
        {
            var today = DateTime.Today;

            // Net worth on a day is every amount recorded up to that day, opening balances included.
            var transactions = dashboardAccounts
                .SelectMany(account => account.Transactions)
                .Where(t => t.Date.Date <= today)
                .OrderBy(t => t.Date)
                .ToList();

            var range = (NetWorthRangeComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string;
            var start = range switch
            {
                "15D" => today.AddDays(-14),
                "30D" => today.AddDays(-29),
                "3M" => today.AddMonths(-3),
                "1Y" => today.AddYears(-1),
                _ => transactions.Count > 0 ? transactions[0].Date.Date : today,
            };

            var series = new List<(DateTime Date, decimal Value)>();
            var running = 0m;
            var next = 0;

            for (var day = start; day <= today; day = day.AddDays(1))
            {
                while (next < transactions.Count && transactions[next].Date.Date <= day)
                {
                    running += transactions[next].Amount;
                    next++;
                }

                series.Add((day, running));
            }

            netWorthSeries = series;
            DrawNetWorthGraph();
        }

        private void DrawNetWorthGraph()
        {
            var width = NetWorthChartCanvas.ActualWidth;
            var height = NetWorthChartCanvas.ActualHeight;

            NetWorthLine.Points.Clear();
            HideNetWorthHover();

            if (netWorthSeries.Count == 0 || width <= 0 || height <= 0)
            {
                NetWorthMaxLabel.Text = "";
                NetWorthMinLabel.Text = "";
                NetWorthStartLabel.Text = "";
                NetWorthEndLabel.Text = "";
                return;
            }

            for (var i = 0; i < netWorthSeries.Count; i++)
            {
                NetWorthLine.Points.Add(NetWorthPoint(i, width, height));
            }

            if (netWorthSeries.Count == 1)
            {
                // A single day has no slope to draw, so show it as a level line.
                var y = NetWorthPoint(0, width, height).Y;
                NetWorthLine.Points.Clear();
                NetWorthLine.Points.Add(new System.Windows.Point(0, y));
                NetWorthLine.Points.Add(new System.Windows.Point(width, y));
            }

            NetWorthMaxLabel.Text = netWorthSeries.Max(p => p.Value).ToString("N0");
            NetWorthMinLabel.Text = netWorthSeries.Min(p => p.Value).ToString("N0");
            NetWorthStartLabel.Text = netWorthSeries[0].Date.ToString("dd MMM yyyy");
            NetWorthEndLabel.Text = netWorthSeries[^1].Date.ToString("dd MMM yyyy");
        }

        /// <summary>Canvas position of the series point at <paramref name="index"/>.</summary>
        private System.Windows.Point NetWorthPoint(int index, double width, double height)
        {
            const double inset = 3; // keeps the 2px line inside the plot at the extremes

            var min = netWorthSeries.Min(p => p.Value);
            var max = netWorthSeries.Max(p => p.Value);
            var span = max - min;

            var x = netWorthSeries.Count == 1 ? width / 2 : index * width / (netWorthSeries.Count - 1);
            var fraction = span == 0 ? 0.5 : (double)((netWorthSeries[index].Value - min) / span);
            var y = inset + (1 - fraction) * (height - 2 * inset);

            return new System.Windows.Point(x, y);
        }

        private void NetWorthChart_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var width = NetWorthChartCanvas.ActualWidth;
            var height = NetWorthChartCanvas.ActualHeight;

            if (netWorthSeries.Count == 0 || width <= 0 || height <= 0)
            {
                return;
            }

            // Snap to the nearest day.
            var mouseX = e.GetPosition(NetWorthChartCanvas).X;
            var index = netWorthSeries.Count == 1
                ? 0
                : (int)Math.Round(mouseX / width * (netWorthSeries.Count - 1));
            index = Math.Clamp(index, 0, netWorthSeries.Count - 1);

            var point = NetWorthPoint(index, width, height);

            NetWorthHoverLine.X1 = point.X;
            NetWorthHoverLine.X2 = point.X;
            NetWorthHoverLine.Y1 = 0;
            NetWorthHoverLine.Y2 = height;
            NetWorthHoverLine.Visibility = Visibility.Visible;

            System.Windows.Controls.Canvas.SetLeft(NetWorthHoverDot, point.X - NetWorthHoverDot.Width / 2);
            System.Windows.Controls.Canvas.SetTop(NetWorthHoverDot, point.Y - NetWorthHoverDot.Height / 2);
            NetWorthHoverDot.Visibility = Visibility.Visible;

            var (date, value) = netWorthSeries[index];
            NetWorthGraphReadout.Text = $"{date:dd MMM yyyy}   {value:N2}";
        }

        private void NetWorthChart_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            HideNetWorthHover();
        }

        /// <summary>Removes the hover marker and puts the range summary back in the readout.</summary>
        private void HideNetWorthHover()
        {
            NetWorthHoverLine.Visibility = Visibility.Collapsed;
            NetWorthHoverDot.Visibility = Visibility.Collapsed;

            if (netWorthSeries.Count == 0)
            {
                NetWorthGraphReadout.Text = "";
                return;
            }

            var change = netWorthSeries[^1].Value - netWorthSeries[0].Value;
            NetWorthGraphReadout.Text = $"{(change > 0 ? "+" : "")}{change:N2} over this period";
        }
    }
}
