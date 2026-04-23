using System.Windows;
using System.Windows.Threading;

namespace Project_Minutes;

public partial class App : WpfApplication
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Evita que, al cerrar el diálogo de login, WPF interprete que no queda ninguna ventana y
        // arranque el cierre antes de abrir MainWindow (lo que impediría asignar ShutdownMode).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        base.OnStartup(e);

        var login = new LoginWindow();
        if (login.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        var main = new MainWindow();
        MainWindow = main;
        main.Closed += (_, _) => Shutdown();
        main.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.ToString(), "Error no controlado", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            MessageBox.Show(ex.ToString(), "Error fatal", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
