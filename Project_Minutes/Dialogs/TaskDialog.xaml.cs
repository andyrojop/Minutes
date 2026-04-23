using System.Windows;
using Project_Minutes.Models;

namespace Project_Minutes.Dialogs;

public partial class TaskDialog : Window
{
    public int SelectedMinuteId { get; private set; }
    public string TaskTitle { get; private set; } = "";
    public int? ResponsibleUserId { get; private set; }
    public DateTime? DueDate { get; private set; }

    public TaskDialog(IReadOnlyList<MinutePickItem> minutes, IReadOnlyList<UserRecord> users, int? preselectMinuteId = null)
    {
        InitializeComponent();
        MinuteCombo.ItemsSource = minutes;
        UserCombo.ItemsSource = users;
        if (preselectMinuteId is { } id)
        {
            var match = minutes.FirstOrDefault(m => m.MinuteId == id);
            if (match is not null)
                MinuteCombo.SelectedItem = match;
        }

        if (MinuteCombo.SelectedItem is null && MinuteCombo.Items.Count > 0)
            MinuteCombo.SelectedIndex = 0;
        if (UserCombo.Items.Count > 0)
            UserCombo.SelectedIndex = 0;
        DuePicker.SelectedDate = DateTime.Today.AddDays(7);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (MinuteCombo.SelectedItem is not MinutePickItem m)
        {
            MessageBox.Show(this, "Selecciona una minuta.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var t = TitleInput.Text.Trim();
        if (t.Length == 0)
        {
            MessageBox.Show(this, "Escribe el nombre de la tarea.", Title, MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SelectedMinuteId = m.MinuteId;
        TaskTitle = t;
        ResponsibleUserId = UserCombo.SelectedItem is UserRecord u ? u.UserId : null;
        DueDate = DuePicker.SelectedDate?.Date;
        DialogResult = true;
    }
}
