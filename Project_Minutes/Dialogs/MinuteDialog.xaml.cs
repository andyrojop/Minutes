using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Project_Minutes.Helpers;
using Project_Minutes.Models;
using Project_Minutes.Services;

namespace Project_Minutes.Dialogs;

public partial class MinuteDialog : Window
{
    private readonly ParticipantRepository _participants;
    private readonly SignatureRepository _signatures;
    private readonly ObservableCollection<AttendeeRow> _attendees = new();

    public int? EditingMinuteId { get; }
    public int SelectedMeetingId { get; private set; }
    public string CombinedContent { get; private set; } = "";

    public MinuteDialog(
        IReadOnlyList<MeetingPickItem> meetings,
        ParticipantRepository participants,
        SignatureRepository signatures,
        MinuteListItem? edit = null)
    {
        InitializeComponent();
        _participants = participants;
        _signatures = signatures;

        MeetingCombo.ItemsSource = meetings;
        AttendeesItems.ItemsSource = _attendees;

        if (edit is null)
        {
            EditingMinuteId = null;
            if (meetings.Count > 0)
                MeetingCombo.SelectedIndex = 0;
        }
        else
        {
            EditingMinuteId = edit.MinuteId;
            Title = "Editar minuta";
            HeaderTitleBlock.Text = "Editar minuta";

            var match = meetings.FirstOrDefault(m => m.MeetingId == edit.MeetingId);
            if (match is not null)
                MeetingCombo.SelectedItem = match;

            var (t, b) = MinuteContentFormat.SplitTitleBody(edit.Content);
            TitleInput.Text = t;
            BodyInput.Text = b;

            MeetingCombo.IsEnabled = false;
        }

        Loaded += MinuteDialog_Loaded;
    }

    private async void MinuteDialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ReloadAttendeesAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void MeetingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        try
        {
            await ReloadAttendeesAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ReloadAttendeesAsync()
    {
        _attendees.Clear();
        if (MeetingCombo.SelectedItem is not MeetingPickItem mp)
            return;

        var parts = await _participants.GetByMeetingAsync(mp.MeetingId).ConfigureAwait(true);
        IReadOnlyDictionary<int, byte[]> sigMap = new Dictionary<int, byte[]>();
        if (EditingMinuteId is int mid)
            sigMap = await _signatures.GetAllPngByUserForMinuteAsync(mid).ConfigureAwait(true);

        foreach (var p in parts)
        {
            sigMap.TryGetValue(p.UserId, out var png);
            var row = new AttendeeRow { UserId = p.UserId, Name = p.ListCaption };
            row.ApplyDbSignature(png);
            _attendees.Add(row);
        }
    }

    private AttendeeRow? FindRow(int userId)
    {
        return _attendees.FirstOrDefault(a => a.UserId == userId);
    }

    private void CaptureAttendee_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not int userId)
            return;

        var row = FindRow(userId);
        if (row is null)
            return;

        var dlg = new CaptureSignatureDialog { Owner = this };
        if (dlg.ShowDialog() != true || dlg.SignaturePng is not { Length: > 0 } png)
            return;

        row.PendingPng = png;
    }

    private void RemoveAttendee_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not int userId)
            return;

        var row = FindRow(userId);
        if (row is null)
            return;

        row.PendingPng = null;
        row.PendingRemove = row.HasDbSignature;
        row.RefreshStatusAndPreview();
    }

    private async void RemoveFromMeeting_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not int userId)
            return;

        if (MeetingCombo.SelectedItem is not MeetingPickItem mp)
            return;

        if (MessageBox.Show(this,
                "¿Quitar a esta persona como participante de la reunión? (No borra la minuta ya guardada hasta que guardes cambios en la firma.)",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await _participants.RemoveAsync(mp.MeetingId, userId).ConfigureAwait(true);
            if (EditingMinuteId is int mid)
                await _signatures.DeleteMinuteUserAsync(mid, userId).ConfigureAwait(true);
            await ReloadAttendeesAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public IReadOnlyList<(int UserId, byte[] Png)> GetMinuteSignatureUpserts() =>
        _attendees.Where(a => a.PendingPng is { Length: > 0 }).Select(a => (a.UserId, a.PendingPng!)).ToList();

    public IReadOnlyList<int> GetMinuteSignatureRemovals() =>
        _attendees.Where(a => a.PendingRemove && a.HasDbSignature).Select(a => a.UserId).ToList();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (MeetingCombo.SelectedItem is not MeetingPickItem mp)
        {
            MessageBox.Show(this, "Selecciona una reunión.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        CombinedContent = MinuteContentFormat.CombineTitleBody(TitleInput.Text, BodyInput.Text);
        SelectedMeetingId = mp.MeetingId;

        static bool EfectivamenteFirmado(AttendeeRow a) =>
            (a.PendingPng is { Length: > 0 }) || (a.HasDbSignature && !a.PendingRemove);

        var sinFirma = _attendees.Count(a => !EfectivamenteFirmado(a));
        if (sinFirma > 0 && _attendees.Count > 0)
        {
            if (MessageBox.Show(
                    this,
                    $"Hay {sinFirma} asistente(s) sin firma. ¿Deseas guardar la minuta de todas formas?",
                    Title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
        }

        DialogResult = true;
    }
}
