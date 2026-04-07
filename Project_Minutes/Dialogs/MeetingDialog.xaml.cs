using System.Windows;
using Project_Minutes.Helpers;
using Project_Minutes.Models;

namespace Project_Minutes.Dialogs;

public partial class MeetingDialog : Window
{
    public string? ResultTitle { get; private set; }
    public DateTime ResultDate { get; private set; }
    public TimeSpan ResultTime { get; private set; }

    public int? EditingMeetingId { get; }

    public MeetingDialog(MeetingRecord? existing = null)
    {
        InitializeComponent();
        EditingMeetingId = existing?.MeetingId;

        if (existing is not null)
        {
            Title = "Editar reunión";
            HeaderTitleBlock.Text = "Editar reunión";
            TitleInput.Text = existing.Title ?? "";
            DatePickerControl.SelectedDate = existing.MeetingDate;
            TimeInput.Text = TimeParse.FormatHhMm(existing.MeetingTime);
        }
        else
        {
            DatePickerControl.SelectedDate = DateTime.Today;
            TimeInput.Text = "10:00";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var date = DatePickerControl.SelectedDate?.Date;
        if (date is null)
        {
            MessageBox.Show(this, "Elige una fecha.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TimeParse.TryHhMm(TimeInput.Text, out var time))
        {
            MessageBox.Show(this, "Hora no válida. Usa HH:mm.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ResultTitle = string.IsNullOrWhiteSpace(TitleInput.Text) ? null : TitleInput.Text.Trim();
        ResultDate = date.Value;
        ResultTime = time;
        DialogResult = true;
        Close();
    }
}
