using System.Windows;
using Project_Minutes.Models;
using Project_Minutes.Services;

namespace Project_Minutes;

public partial class RegisterAdminWindow : Window
{
    private readonly UserRepository _users;
    private readonly bool _firstAdministratorOnly;
    /// <summary>Solo para flujo desde la ventana de login: permite registrar otro admin sin sesión previa.</summary>
    private readonly bool _allowRegisterWithoutAdminSession;

    public AdminSessionUser? RegisteredUser { get; private set; }

    public RegisterAdminWindow(UserRepository users, bool firstAdministratorOnly,
        bool allowRegisterWithoutAdminSession = false)
    {
        InitializeComponent();
        _users = users;
        _firstAdministratorOnly = firstAdministratorOnly;
        _allowRegisterWithoutAdminSession = allowRegisterWithoutAdminSession;

        if (firstAdministratorOnly)
        {
            Title = "Primer administrador";
            HeaderText.Text = "Crear primer administrador";
        }
        else if (allowRegisterWithoutAdminSession)
        {
            Title = "Nuevo administrador";
            HeaderText.Text = "Registrar administrador";
        }
    }

    private async void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var name = DisplayNameBox.Text ?? "";
        var email = string.IsNullOrWhiteSpace(EmailBox.Text) ? null : EmailBox.Text.Trim();
        var username = UsernameBox.Text ?? "";
        var pass = PasswordBox.Password ?? "";
        var confirm = ConfirmPasswordBox.Password ?? "";

        if (pass != confirm)
        {
            MessageBox.Show(this, "Las contraseñas no coinciden.", "Registrar", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        OkButton.IsEnabled = false;
        try
        {
            AdminSessionUser user;
            if (_firstAdministratorOnly)
                user = await _users.RegisterFirstAdministratorAsync(name, email, username, pass).ConfigureAwait(true);
            else
            {
                if (!AuthSession.IsAdministratorLoggedIn && !_allowRegisterWithoutAdminSession)
                {
                    MessageBox.Show(this, "Debe iniciar sesión como administrador para registrar otro.",
                        "Registrar", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                user = await _users.RegisterAdministratorAsync(name, email, username, pass).ConfigureAwait(true);
            }

            RegisteredUser = user;
            DialogResult = true;
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(this, ex.Message, "Registrar", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, "Registrar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Registrar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            OkButton.IsEnabled = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
