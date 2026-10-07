using System.Windows;

namespace MarketClock.Panels
{
    // To-Do List panel. Not implemented yet: it shows sample data,
    // which can be deleted once the real feature exists.
    public partial class TodoControl : System.Windows.Controls.UserControl
    {
        public sealed record TodoItemRow(string Glyph, string Title, string Percent, Thickness Indent, bool IsGroup);

        public TodoControl()
        {
            InitializeComponent();
            TodoList.ItemsSource = BuildSampleTodos();
        }

        private static List<TodoItemRow> BuildSampleTodos()
        {
            var groups = new (string Title, (string Title, bool Done)[] Tasks)[]
            {
                ("Home renovation", new[] { ("Get quotes", true), ("Pick contractor", true), ("Buy materials", false) }),
                ("Tax filing", new[] { ("Collect statements", true), ("Reconcile accounts", false), ("File return", false), ("Verify", false) }),
                ("Learning", new[] { ("Finish course", false), ("Practice project", false) }),
            };

            var rows = new List<TodoItemRow>();

            foreach (var group in groups)
            {
                var percent = 100 * group.Tasks.Count(t => t.Done) / group.Tasks.Length;
                rows.Add(new TodoItemRow("▾", group.Title, $"{percent}%", new Thickness(0), true));

                foreach (var task in group.Tasks)
                {
                    rows.Add(new TodoItemRow(
                        task.Done ? "☑" : "☐",
                        task.Title,
                        task.Done ? "100%" : "0%",
                        new Thickness(16, 0, 0, 0),
                        false));
                }
            }

            return rows;
        }
    }
}
