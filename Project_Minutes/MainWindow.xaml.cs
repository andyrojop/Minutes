using System.Windows;
using System.Windows.Controls;
using Project_Minutes.Configuration;
using Project_Minutes.Data;
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

        var appConfig = AppConfiguration.Load();
        var db = new SqlDatabase(appConfig);
        _users = new UserRepository(db);
        _meetings = new MeetingRepository(db);
        _minutes = new MinuteRepository(db);
        _tasks = new TaskRepository(db);
        _signatures = new SignatureRepository(db);
        _participants = new ParticipantRepository(db);
        _taskSignatures = new TaskSignatureRepository(db);

        Loaded += async (_, _) =>
        {
            try
            {
                await OnLoadedAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Error al iniciar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
    }

    private async Task OnLoadedAsync()
    {
        var connected = await TestConnectionAsync().ConfigureAwait(true);
        if (connected)
            await EnsureDatabaseSchemaAsync().ConfigureAwait(true);
        await RefreshAllAsync().ConfigureAwait(true);
    }

    /// <summary>Crea TaskSignatures (y el índice de firmas por minuta/usuario) si aún no existen.</summary>
    private async Task EnsureDatabaseSchemaAsync()
    {
        try
        {
            var cfg = AppConfiguration.Load();
            await using var c = new Microsoft.Data.SqlClient.SqlConnection(cfg.MeetingMinutesConnectionString);
            await c.OpenAsync().ConfigureAwait(true);
            await DatabaseSchemaInitializer.EnsureExtendedSchemaAsync(c).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "La app funcionará con limitaciones hasta que la base permita crear la tabla TaskSignatures.\n\n" +
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
        StatusText.Text = "Comprobando conexión con SQL Server…";
        DataPathText.Text = "Datos guardados en: —";
        try
        {
            var cfg = AppConfiguration.Load();
            await using var c = new Microsoft.Data.SqlClient.SqlConnection(cfg.MeetingMinutesConnectionString);
            await c.OpenAsync().ConfigureAwait(true);
            StatusText.Text = $"Conectado a «{c.Database}» en {c.DataSource}.";
            DataPathText.Text = $"Datos guardados en: SQL Server · {c.Database} · {c.DataSource}";
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Sin conexión. Revisa appsettings.json.";
            DataPathText.Text = "Datos guardados en: (no conectado)";
            MessageBox.Show(this, ex.Message, "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private async Task RefreshAllAsync()
    {
        try
        {
            await RefreshMeetingsUiAsync().ConfigureAwait(true);
            await RefreshParticipantMeetingsComboAsync().ConfigureAwait(true);
            await RefreshParticipantsListAsync().ConfigureAwait(true);
            await PopulateMinuteFilterAsync().ConfigureAwait(true);
            await RefreshMinutesUiAsync().ConfigureAwait(true);
            await PopulateTaskMinuteComboAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error al cargar datos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RefreshMeetingsUiAsync()
    {
        var list = await _meetings.GetAllAsync().ConfigureAwait(true);
        MeetingsList.ItemsSource = list.Select(m => new MeetingRow
        {
            Record = m,
            Line = $"{(string.IsNullOrWhiteSpace(m.Title) ? "Reunión" : m.Title.Trim())} — {m.MeetingDate:d} {TimeParse.FormatHhMm(m.MeetingTime)}"
        }).ToList();
    }

    private async Task RefreshParticipantMeetingsComboAsync()
    {
        var list = await _meetings.GetAllAsync().ConfigureAwait(true);
        var items = list.Select(m => new MeetingPickItem
        {
            MeetingId = m.MeetingId,
            DisplayText =
                $"{(string.IsNullOrWhiteSpace(m.Title) ? "Reunión" : m.Title.Trim())} — {m.MeetingDate:d} {TimeParse.FormatHhMm(m.MeetingTime)}"
        }).ToList();

        var prevId = ParticipantMeetingCombo.SelectedItem is MeetingPickItem p ? p.MeetingId : (int?)null;
        ParticipantMeetingCombo.ItemsSource = items;

        if (items.Count == 0)
        {
            ParticipantsList.ItemsSource = Array.Empty<ParticipantRecord>();
            return;
        }

        if (prevId is { } id)
        {
            var match = items.FirstOrDefault(x => x.MeetingId == id);
            if (match is not null)
            {
                ParticipantMeetingCombo.SelectedItem = match;
                return;
            }
        }

        ParticipantMeetingCombo.SelectedIndex = 0;
    }

    private async Task RefreshParticipantsListAsync()
    {
        if (ParticipantMeetingCombo.SelectedItem is not MeetingPickItem mp)
        {
            ParticipantsList.ItemsSource = Array.Empty<ParticipantRecord>();
            return;
        }

        var parts = await _participants.GetByMeetingAsync(mp.MeetingId).ConfigureAwait(true);
        ParticipantsList.ItemsSource = parts;
    }

    private async void ParticipantMeetingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        try
        {
            await RefreshParticipantsListAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Participantes", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RegisterNewParticipant_Click(object sender, RoutedEventArgs e)
    {
        if (ParticipantMeetingCombo.SelectedItem is not MeetingPickItem mp)
        {
            MessageBox.Show(this, "Elige una reunión.", "Participantes", MessageBoxButton.OK,
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
            await _participants.AddIfNotExistsAsync(mp.MeetingId, userId, position).ConfigureAwait(true);
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
        if (ParticipantMeetingCombo.SelectedItem is not MeetingPickItem mp)
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
            await _participants.RemoveAsync(mp.MeetingId, row.UserId).ConfigureAwait(true);
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
        var items = await _minutes.GetListItemsAsync(filter).ConfigureAwait(true);
        MinutesList.ItemsSource = items;
    }

    private async Task PopulateTaskMinuteComboAsync()
    {
        var list = await _minutes.GetAllAsync().ConfigureAwait(true);
        var opts = list.Select(m => new MinutePickItem
        {
            MinuteId = m.MinuteId,
            DisplayText = $"{m.MeetingTitle} — minuta #{m.MinuteId}"
        }).ToList();
        TaskMinuteCombo.ItemsSource = opts;
        if (TaskMinuteCombo.Items.Count > 0)
            TaskMinuteCombo.SelectedIndex = 0;
        await RefreshTasksForSelectedMinuteAsync().ConfigureAwait(true);
    }

    private async Task RefreshTasksForSelectedMinuteAsync()
    {
        if (TaskMinuteCombo.SelectedItem is not MinutePickItem opt)
        {
            TasksList.ItemsSource = Array.Empty<TaskRecord>();
            return;
        }

        var tasks = await _tasks.GetByMinuteIdAsync(opt.MinuteId).ConfigureAwait(true);
        TasksList.ItemsSource = tasks;
    }

    private async void MinuteFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            await RefreshMinutesUiAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Minutas", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void TaskMinuteCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            await RefreshTasksForSelectedMinuteAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Compromisos", MessageBoxButton.OK, MessageBoxImage.Error);
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

        var dlg = new TaskDialog(minuteItems, users) { Owner = this };
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
