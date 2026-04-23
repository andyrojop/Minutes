using System.Windows;
using System.Windows.Input;
using Project_Minutes.Configuration;
using Project_Minutes.Services;

namespace Project_Minutes;

public partial class LoginWindow : Window
{
    private readonly UserRepository _users;
    private bool _hasAdministrators;
    private bool _databaseReady;

    public LoginWindow()
    {
        InitializeComponent();
        _ = ClientConfiguration.Load();
        _users = new UserRepository();

        Loaded += async (_, _) => await InitializeAsync().ConfigureAwait(true);
        PasswordBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && LoginSection.Visibility == Visibility.Visible)
                _ = TryLoginAsync();
        };
    }

    private async Task InitializeAsync()
    {
        try
        {
            var res = await ApiHttp.Instance.GetAsync("api/health/ready").ConfigureAwait(true);
            res.EnsureSuccessStatusCode();

            var count = await _users.CountActiveAdministratorsAsync().ConfigureAwait(true);
            _hasAdministrators = count > 0;
            _databaseReady = true;

            StatusBanner.Text = _hasAdministrators
                ? "Inicie sesión o registre una cuenta de administrador adicional."
                : "Cree el primer administrador para continuar. También podrá añadir más cuentas después desde aquí.";

            RegisterSection.Visibility = Visibility.Visible;
            ExitOnlyButton.Visibility = Visibility.Collapsed;

            if (_hasAdministrators)
            {
                LoginSection.Visibility = Visibility.Visible;
                RegisterFirstButton.Visibility = Visibility.Collapsed;
                RegisterAdditionalButton.Visibility = Visibility.Visible;
                RegisterHelpText.Text =
                    "Use este apartado para dar de alta otra cuenta de administrador (por ejemplo otro miembro del equipo) sin cerrar esta ventana.";
                UsernameBox.Focus();
            }
            else
            {
                LoginSection.Visibility = Visibility.Collapsed;
                RegisterFirstButton.Visibility = Visibility.Visible;
                RegisterAdditionalButton.Visibility = Visibility.Collapsed;
                RegisterHelpText.Text =
                    "No hay ningún administrador con contraseña. Cree la primera cuenta. Más adelante podrá registrar otros desde esta misma sección.";
            }
        }
        catch (Exception ex)
        {
            _databaseReady = false;
            StatusBanner.Text = "No se pudo conectar al backend (API REST).";
            LoginSection.Visibility = Visibility.Collapsed;
            RegisterSection.Visibility = Visibility.Collapsed;
            ExitOnlyButton.Visibility = Visibility.Visible;
            MessageBox.Show(this,
                "Inicie el proyecto «Project_Minutes.Api» y revise Api:BaseUrl en appsettings.json del cliente.\n\n" +
                ex.Message,
                "Conexión",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e) => await TryLoginAsync().ConfigureAwait(true);

    private async Task TryLoginAsync()
    {
        if (!_hasAdministrators)
            return;

        var user = UsernameBox.Text?.Trim() ?? "";
        var pass = PasswordBox.Password ?? "";

        LoginButton.IsEnabled = false;
        try
        {
            var session = await _users.LoginAdministratorAsync(user, pass).ConfigureAwait(true);
            if (session is null)
            {
                MessageBox.Show(this,
                    "Usuario o contraseña incorrectos, o la cuenta no es de administrador.",
                    "Inicio de sesión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            AuthSession.SetUser(session);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Inicio de sesión", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void RegisterFirstButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_databaseReady)
            return;

        var dlg = new RegisterAdminWindow(_users, firstAdministratorOnly: true) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.RegisteredUser is { } u)
        {
            AuthSession.SetUser(u);
            DialogResult = true;
        }
    }

    private async void RegisterAdditionalButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_databaseReady)
            return;

        var dlg = new RegisterAdminWindow(_users, firstAdministratorOnly: false, allowRegisterWithoutAdminSession: true)
        {
            Owner = this
        };
        if (dlg.ShowDialog() != true)
            return;

        MessageBox.Show(this,
            "Administrador registrado. La nueva cuenta ya puede usarse para iniciar sesión.",
            "Registro",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        try
        {
            var count = await _users.CountActiveAdministratorsAsync().ConfigureAwait(true);
            _hasAdministrators = count > 0;
        }
        catch
        {
            // ignorar
        }
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
