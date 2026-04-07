using System.Windows;
using Project_Minutes.Models;

namespace Project_Minutes.Dialogs;

public partial class UserManagementDialog : Window
{
    private readonly Func<Task<IReadOnlyList<UserRecord>>> _reload;

    private readonly Func<string, string?, Task> _addUser;

    public UserManagementDialog(
        IEnumerable<UserRecord> initial,
        Func<Task<IReadOnlyList<UserRecord>>> reload,
        Func<string, string?, Task> addUser)
    {
        InitializeComponent();
        _reload = reload;
        _addUser = addUser;
        UsersGrid.ItemsSource = initial;
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var name = NameInput.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show(this, "Escribe un nombre.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var email = string.IsNullOrWhiteSpace(EmailInput.Text) ? null : EmailInput.Text.Trim();
            await _addUser(name, email).ConfigureAwait(true);
            NameInput.Clear();
            EmailInput.Clear();
            var list = await _reload().ConfigureAwait(true);
            UsersGrid.ItemsSource = list;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void CloseOk_Click(object sender, RoutedEventArgs e) => Close();
}
