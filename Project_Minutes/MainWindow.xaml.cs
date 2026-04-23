using System.Windows;
using System.Windows.Controls;
using Project_Minutes.Configuration;
using Project_Minutes.Dialogs;
using Project_Minutes.Helpers;
using Project_Minutes.Models;
using Project_Minutes.Services;

namespace Project_Minutes;

public partial class MainWindow : Window
{
    private readonly UserRepository _users;
    private readonly MeetingRepository _meetings;
    private readonly MinuteRepository _minutes;
    private readonly TaskRepository _tasks;
    private readonly SignatureRepository _signatures;
    private readonly ParticipantRepository _participants;
    private readonly TaskSignatureRepository _taskSignatures;

    public MainWindow()
    {
        InitializeComponent();

        _ = ClientConfiguration.Load();
        _users = new UserRepository();
        _meetings = new MeetingRepository();
        _minutes = new MinuteRepository();
        _tasks = new TaskRepository();
        _signatures = new SignatureRepository();
        _participants = new ParticipantRepository();
        _taskSignatures = new TaskSignatureRepository();

        Loaded += async (_, _) =>
        {
            try
            {
                UpdateSessionUi();
                await OnLoadedAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Error al iniciar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
    }

    private void UpdateSessionUi()
    {
        var u = AuthSession.Current;
        SessionUserText.Text = u is null
            ? "Administrador: —"
            : $"Administrador: {u.DisplayName} ({u.Username})";
    }

    private void MenuRegisterAdmin_Click(object sender, RoutedEventArgs e)
    {
        if (!AuthSession.IsAdministratorLoggedIn)
        {
            MessageBox.Show(this, "No hay una sesión de administrador activa.", "Registrar administrador",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var dlg = new RegisterAdminWindow(_users, firstAdministratorOnly: false) { Owner = this };
            if (dlg.ShowDialog() == true)
                MessageBox.Show(this, "Se registró el nuevo administrador.", "Registrar administrador",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Registrar administrador", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MenuLogout_Click(object sender, RoutedEventArgs e)
    {
        AuthSession.Clear();
        UpdateSessionUi();
        Hide();

        var login = new LoginWindow();
        if (login.ShowDialog() == true)
        {
            UpdateSessionUi();
            Show();
            _ = RefreshAllAsync();
        }
        else
        {
            Close();
            WpfApplication.Current.Shutdown();
        }
    }

    private async Task OnLoadedAsync()
    {
        var connected = await TestConnectionAsync().ConfigureAwait(true);
        if (connected)
            await EnsureDatabaseSchemaAsync().ConfigureAwait(true);
        await RefreshAllAsync().ConfigureAwait(true);
    }

    /// <summary>Indica al backend que aplique el esquema SQL si hace falta.</summary>
    private async Task EnsureDatabaseSchemaAsync()
    {
        try
        {
            var res = await ApiHttp.Instance.GetAsync("api/health/ready").ConfigureAwait(true);
            res.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "No se pudo preparar la base de datos vía API. Compruebe que el backend esté en ejecución y la cadena de conexión del servidor.\n\n" +
                ex.Message,
                "Esquema de base de datos",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void ReconnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (await TestConnectionAsync().ConfigureAwait(true))
            await EnsureDatabaseSchemaAsync().ConfigureAwait(true);
        await RefreshAllAsync().ConfigureAwait(true);
    }

    private async void MenuUsers_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var initial = await _users.GetAllAsync().ConfigureAwait(true);
            var dlg = new UserManagementDialog(
                initial,
                async () => await _users.GetAllAsync().ConfigureAwait(true),
                async (n, em) => await _users.AddAsync(n, em).ConfigureAwait(true))
            {
                Owner = this
            };
            dlg.ShowDialog();
            await RefreshAllAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Usuarios", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<bool> TestConnectionAsync()
    {
        var api = ClientConfiguration.Load().ApiBaseUrl;
        StatusText.Text = "Comprobando conexión con la API…";
        DataPathText.Text = "Datos guardados en: —";
        try
        {
            var res = await ApiHttp.Instance.GetAsync("api/health").ConfigureAwait(true);
            res.EnsureSuccessStatusCode();
            StatusText.Text = "Conectado al backend REST.";
            DataPathText.Text = $"API: {api.TrimEnd('/')} (SQL en el servidor)";
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Sin conexión al backend. Inicie la API y revise Api:BaseUrl.";
            DataPathText.Text = "Datos: (API no disponible)";
            MessageBox.Show(this, ex.Message, "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private async Task RefreshAllAsync()
    {
        try
        {
            await RefreshMeetingsUiAsync().ConfigureAwait(true);
            await PopulateMinuteFilterAsync().ConfigureAwait(true);
            await RefreshMinutesUiAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error al cargar datos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RefreshMeetingsUiAsync()
    {
        var prevId = MeetingsList.SelectedItem is MeetingRow mr ? mr.Record.MeetingId : (int?)null;
        var list = await _meetings.GetAllAsync().ConfigureAwait(true);
        var rows = list.Select(m => new MeetingRow
        {
            Record = m,
            Line = $"{(string.IsNullOrWhiteSpace(m.Title) ? "Reunión" : m.Title.Trim())} — {m.MeetingDate:d} {TimeParse.FormatHhMm(m.MeetingTime)}"
        }).ToList();

        MeetingsList.ItemsSource = rows;

        if (rows.Count == 0)
        {
            MeetingsList.SelectedItem = null;
            UpdateMeetingContextHeader();
            ParticipantsList.ItemsSource = Array.Empty<ParticipantRecord>();
            return;
        }

        var pick = prevId is { } id ? rows.FirstOrDefault(r => r.Record.MeetingId == id) : null;
        pick ??= rows[0];
        MeetingsList.SelectedItem = pick;
    }

    private void UpdateMeetingContextHeader()
    {
        if (MeetingsList.SelectedItem is MeetingRow row)
        {
            MeetingContextTitle.Text = row.Line;
            MeetingContextSubtitle.Text = "Participantes que pueden firmar la minuta de esta reunión.";
        }
        else
        {
            MeetingContextTitle.Text = MeetingsList.Items.Count == 0
                ? "No hay reuniones"
                : "Selecciona una reunión";
            MeetingContextSubtitle.Text = MeetingsList.Items.Count == 0
                ? "Crea una reunión desde el panel izquierdo para comenzar."
                : "Elige una fila en la lista de reuniones.";
        }
    }

    private async void MeetingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        UpdateMeetingContextHeader();
        try
        {
            await RefreshParticipantsListAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Participantes", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RefreshParticipantsListAsync()
    {
        if (MeetingsList.SelectedItem is not MeetingRow row)
        {
            ParticipantsList.ItemsSource = Array.Empty<ParticipantRecord>();
            return;
        }

        var parts = await _participants.GetByMeetingAsync(row.Record.MeetingId).ConfigureAwait(true);
        ParticipantsList.ItemsSource = parts;
    }

    private async void RegisterNewParticipant_Click(object sender, RoutedEventArgs e)
    {
        if (MeetingsList.SelectedItem is not MeetingRow meetingRow)
        {
            MessageBox.Show(this, "Selecciona una reunión en la lista.", "Participantes", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var name = NewParticipantNameBox.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show(this, "Escribe el nombre del participante.", "Participantes",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var posRaw = NewParticipantPositionBox.Text.Trim();
        string? position = posRaw.Length == 0 ? null : posRaw;

        try
        {
            var userId = await _users.AddAsync(name, null).ConfigureAwait(true);
            await _participants.AddIfNotExistsAsync(meetingRow.Record.MeetingId, userId, position).ConfigureAwait(true);
            NewParticipantNameBox.Clear();
            NewParticipantPositionBox.Clear();
            await RefreshParticipantsListAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Participantes", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RemoveParticipant_Click(object sender, RoutedEventArgs e)
    {
        if (MeetingsList.SelectedItem is not MeetingRow meetingRow)
            return;

        if (ParticipantsList.SelectedItem is not ParticipantRecord row)
        {
            MessageBox.Show(this, "Selecciona un participante en la lista.", "Participantes",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this, $"¿Quitar a «{row.ListCaption}» de esta reunión?", "Participantes",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await _participants.RemoveAsync(meetingRow.Record.MeetingId, row.UserId).ConfigureAwait(true);
            await RefreshParticipantsListAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Participantes", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task PopulateMinuteFilterAsync()
    {
        var meetings = await _meetings.GetAllAsync().ConfigureAwait(true);
        var opts = new List<FilterMeetingOption>
        {
            new() { MeetingId = null, DisplayText = "Todas las reuniones" }
        };
        opts.AddRange(meetings.Select(m => new FilterMeetingOption
        {
            MeetingId = m.MeetingId,
            DisplayText = string.IsNullOrWhiteSpace(m.Title)
                ? $"Reunión #{m.MeetingId} — {m.MeetingDate:d}"
                : $"{m.Title} — {m.MeetingDate:d}"
        }));
        MinuteFilterCombo.ItemsSource = opts;
        MinuteFilterCombo.SelectedIndex = 0;
    }

    private int? GetSelectedFilterMeetingId()
    {
        if (MinuteFilterCombo.SelectedItem is not FilterMeetingOption f)
            return null;
        return f.MeetingId;
    }

    private async Task RefreshMinutesUiAsync()
    {
        var filter = GetSelectedFilterMeetingId();
        var prevMinuteId = MinutesList.SelectedItem is MinuteListItem mi ? mi.MinuteId : (int?)null;
        var items = await _minutes.GetListItemsAsync(filter).ConfigureAwait(true);
        MinutesList.ItemsSource = items;

        if (items.Count == 0)
        {
            MinutesList.SelectedItem = null;
            UpdateMinuteContextHeader();
            TasksList.ItemsSource = Array.Empty<TaskRecord>();
            return;
        }

        var pick = prevMinuteId is { } mid ? items.FirstOrDefault(x => x.MinuteId == mid) : null;
        pick ??= items[0];
        MinutesList.SelectedItem = pick;
    }

    private void UpdateMinuteContextHeader()
    {
        if (MinutesList.SelectedItem is MinuteListItem m)
        {
            var preview = MinuteListItem.ExtractTitlePreview(m.Content);
            MinuteContextTitle.Text = $"{preview} · Minuta #{m.MinuteId}";
            var meetingLabel = string.IsNullOrWhiteSpace(m.MeetingTitle)
                ? $"Reunión #{m.MeetingId}"
                : m.MeetingTitle.Trim();
            if (m.ParticipantCount == 0)
                MinuteContextSubtitle.Text = $"{meetingLabel} · Sin participantes en la reunión";
            else if (m.SignatureCount >= m.ParticipantCount)
                MinuteContextSubtitle.Text =
                    $"{meetingLabel} · Firmas completas ({m.SignatureCount}/{m.ParticipantCount})";
            else
                MinuteContextSubtitle.Text =
                    $"{meetingLabel} · Firmas {m.SignatureCount}/{m.ParticipantCount} asistentes";
        }
        else
        {
            MinuteContextTitle.Text = MinutesList.Items.Count == 0
                ? "No hay minutas"
                : "Selecciona una minuta";
            MinuteContextSubtitle.Text = MinutesList.Items.Count == 0
                ? "Cambia el filtro de reunión o crea una minuta nueva."
                : "Elige una fila en la lista de la izquierda.";
        }
    }

    private async void MinutesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        UpdateMinuteContextHeader();
        try
        {
            await RefreshTasksForSelectedMinuteAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Compromisos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RefreshTasksForSelectedMinuteAsync()
    {
        if (MinutesList.SelectedItem is not MinuteListItem item)
        {
            TasksList.ItemsSource = Array.Empty<TaskRecord>();
            return;
        }

        var tasks = await _tasks.GetByMinuteIdAsync(item.MinuteId).ConfigureAwait(true);
        TasksList.ItemsSource = tasks;
    }

    private async void MinuteFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        try
        {
            await RefreshMinutesUiAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Minutas", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void NewMeeting_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new MeetingDialog { Owner = this };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            await _meetings.AddAsync(dlg.ResultTitle, dlg.ResultDate, dlg.ResultTime).ConfigureAwait(true);
            await RefreshAllAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Reuniones", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void EditMeeting_Click(object sender, RoutedEventArgs e)
    {
        if (MeetingsList.SelectedItem is not MeetingRow row)
        {
            MessageBox.Show(this, "Selecciona una reunión.", "Reuniones", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dlg = new MeetingDialog(row.Record) { Owner = this };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            await _meetings.UpdateAsync(row.Record.MeetingId, dlg.ResultTitle, dlg.ResultDate, dlg.ResultTime)
                .ConfigureAwait(true);
            await RefreshAllAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Reuniones", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DeleteMeeting_Click(object sender, RoutedEventArgs e)
    {
        if (MeetingsList.SelectedItem is not MeetingRow row)
        {
            MessageBox.Show(this, "Selecciona una reunión.", "Reuniones", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this,
                "¿Eliminar esta reunión y sus minutas, firmas y compromisos vinculados?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            await _meetings.DeleteAsync(row.Record.MeetingId).ConfigureAwait(true);
            await RefreshAllAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Reuniones", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private IReadOnlyList<MeetingPickItem> BuildMeetingPickList()
    {
        return MeetingsList.ItemsSource is IEnumerable<MeetingRow> rows
            ? rows.Select(r => new MeetingPickItem
            {
                MeetingId = r.Record.MeetingId,
                DisplayText = r.Line
            }).ToList()
            : Array.Empty<MeetingPickItem>();
    }

    private async void NewMinute_Click(object sender, RoutedEventArgs e)
    {
        var meetings = BuildMeetingPickList();
        if (meetings.Count == 0)
        {
            MessageBox.Show(this, "Crea primero una reunión.", "Minutas", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dlg = new MinuteDialog(meetings, _participants, _signatures) { Owner = this };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            var id = await _minutes.AddAsync(dlg.SelectedMeetingId, dlg.CombinedContent).ConfigureAwait(true);
            foreach (var (userId, png) in dlg.GetMinuteSignatureUpserts())
                await _signatures.UpsertMinuteUserAsync(id, userId, png).ConfigureAwait(true);

            await RefreshAllAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Minutas", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void EditMinute_Click(object sender, RoutedEventArgs e)
    {
        if (MinutesList.SelectedItem is not MinuteListItem item)
        {
            MessageBox.Show(this, "Selecciona una minuta.", "Minutas", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var meetings = BuildMeetingPickList();

        var dlg = new MinuteDialog(meetings, _participants, _signatures, item) { Owner = this };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            await _minutes.UpdateAsync(item.MinuteId, dlg.CombinedContent).ConfigureAwait(true);
            foreach (var uid in dlg.GetMinuteSignatureRemovals())
                await _signatures.DeleteMinuteUserAsync(item.MinuteId, uid).ConfigureAwait(true);
            foreach (var (userId, png) in dlg.GetMinuteSignatureUpserts())
                await _signatures.UpsertMinuteUserAsync(item.MinuteId, userId, png).ConfigureAwait(true);

            await RefreshAllAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Minutas", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DeleteMinute_Click(object sender, RoutedEventArgs e)
    {
        if (MinutesList.SelectedItem is not MinuteListItem item)
        {
            MessageBox.Show(this, "Selecciona una minuta.", "Minutas", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this, "¿Eliminar esta minuta y sus firmas y compromisos?", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            await _minutes.DeleteAsync(item.MinuteId).ConfigureAwait(true);
            await RefreshAllAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Minutas", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void NewTask_Click(object sender, RoutedEventArgs e)
    {
        var minutes = await _minutes.GetAllAsync().ConfigureAwait(true);
        var users = await _users.GetAllAsync().ConfigureAwait(true);
        if (minutes.Count == 0)
        {
            MessageBox.Show(this, "Crea primero una minuta.", "Compromisos", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var minuteItems = minutes.Select(m => new MinutePickItem
        {
            MinuteId = m.MinuteId,
            DisplayText = $"{m.MeetingTitle} — #{m.MinuteId}"
        }).ToList();

        var preMinute = MinutesList.SelectedItem is MinuteListItem sel ? sel.MinuteId : (int?)null;
        var dlg = new TaskDialog(minuteItems, users, preMinute) { Owner = this };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            await _tasks.AddAsync(dlg.SelectedMinuteId, dlg.TaskTitle, dlg.ResponsibleUserId, dlg.DueDate)
                .ConfigureAwait(true);
            await RefreshTasksForSelectedMinuteAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Compromisos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SignTask_Click(object sender, RoutedEventArgs e)
    {
        if (TasksList.SelectedItem is not TaskRecord task)
        {
            MessageBox.Show(this, "Selecciona un compromiso.", "Compromisos", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (task.ResponsibleUserId is null)
        {
            MessageBox.Show(this, "Asigna un responsable al compromiso antes de pedir la firma.", "Compromisos",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        TaskSignDialog dlg;
        try
        {
            dlg = new TaskSignDialog(task) { Owner = this };
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Compromisos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (dlg.ShowDialog() != true || dlg.SignaturePng is not { Length: > 0 } png)
            return;

        try
        {
            await _taskSignatures.UpsertAsync(task.TaskId, dlg.ResponsibleUserId, png).ConfigureAwait(true);
            await RefreshTasksForSelectedMinuteAsync().ConfigureAwait(true);
            MessageBox.Show(this, "Firma del responsable guardada.", "Compromisos", MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Compromisos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (TasksList.SelectedItem is not TaskRecord task)
        {
            MessageBox.Show(this, "Selecciona un compromiso.", "Compromisos", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this, "¿Eliminar este compromiso?", "Confirmar", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await _tasks.DeleteAsync(task.TaskId).ConfigureAwait(true);
            await RefreshTasksForSelectedMinuteAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Compromisos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private sealed class MeetingRow
    {
        public required MeetingRecord Record { get; init; }
        public string Line { get; init; } = "";
    }

    private sealed class FilterMeetingOption
    {
        public int? MeetingId { get; init; }
        public string DisplayText { get; init; } = "";
    }
}
