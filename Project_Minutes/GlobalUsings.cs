// Evita ambigüedades al usar UseWindowsForms junto con WPF (MessageBox, Button, etc.).
global using System.Net.Http;
global using MessageBox = System.Windows.MessageBox;
global using WpfApplication = System.Windows.Application;
global using Button = System.Windows.Controls.Button;
global using Brushes = System.Windows.Media.Brushes;
global using TaskDialog = Project_Minutes.Dialogs.TaskDialog;
