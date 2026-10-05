using System.Windows;
using MarketClock.Data;

namespace MarketClock
{
    /// <summary>A list of categories with an add / rename form. Used by the Settings pages.</summary>
    public partial class CategoryEditor : System.Windows.Controls.UserControl
    {
        private Func<IEnumerable<ICategory>>? loadCategories;
        private Action<string>? addCategory;
        private Action<int, string>? renameCategory;
        private ICategory? editingCategory;

        public CategoryEditor()
        {
            InitializeComponent();
        }

        /// <summary>Connects the editor to the data layer and loads the list.</summary>
        public void Initialize(
            Func<IEnumerable<ICategory>> load,
            Action<string> add,
            Action<int, string> rename)
        {
            loadCategories = load;
            addCategory = add;
            renameCategory = rename;
            Reload();
        }

        private void Reload(string? selectName = null)
        {
            if (loadCategories == null)
            {
                return;
            }

            var categories = loadCategories().ToList();
            CategoryListBox.ItemsSource = categories;

            if (selectName != null)
            {
                CategoryListBox.SelectedItem = categories.FirstOrDefault(c => c.Name == selectName);
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            ShowForm(true);
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (CategoryListBox.SelectedItem is not ICategory category)
            {
                MessageTextBlock.Text = "Select a category to edit.";
                return;
            }

            ShowForm(true, category);
        }

        private void CategoryListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (CategoryListBox.SelectedItem is ICategory category)
            {
                ShowForm(true, category);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            ShowForm(false);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (addCategory == null || renameCategory == null)
            {
                MessageTextBlock.Text = "Categories are not loaded.";
                return;
            }

            var name = NameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageTextBlock.Text = "Enter a category name.";
                return;
            }

            var existing = CategoryListBox.Items.OfType<ICategory>();
            if (existing.Any(c => c.Id != editingCategory?.Id &&
                                  string.Equals(c.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            {
                MessageTextBlock.Text = "A category with that name already exists.";
                return;
            }

            try
            {
                if (editingCategory == null)
                {
                    addCategory(name);
                }
                else
                {
                    renameCategory(editingCategory.Id, name);
                }
            }
            catch (Exception ex)
            {
                MessageTextBlock.Text = $"Could not save the category: {ex.Message}";
                return;
            }

            ShowForm(false);
            Reload(selectName: name);
        }

        private void ShowForm(bool show, ICategory? categoryToEdit = null)
        {
            editingCategory = show ? categoryToEdit : null;

            FormPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            ListActionsPanel.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            MessageTextBlock.Text = "";

            if (show)
            {
                FormTitle.Text = categoryToEdit == null ? "New Category" : "Edit Category";
                SaveButton.Content = categoryToEdit == null ? "+ Add" : "Save";
                NameTextBox.Text = categoryToEdit?.Name ?? "";
                NameTextBox.Focus();
            }
        }
    }
}
