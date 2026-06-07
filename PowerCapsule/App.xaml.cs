using System;
using System.Windows;
using System.Windows.Threading;
using PowerCapsule.Views;

namespace PowerCapsule
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += (s, args) =>
            {
                LogError(args.Exception);
                MessageBox.Show(
                    $"发生未处理的异常：\n{args.Exception.Message}",
                    "PowerCapsule 错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    LogError(ex);
                    MessageBox.Show(
                        $"发生严重错误：\n{ex.Message}",
                        "PowerCapsule 错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };

            try
            {
                var window = new CapsuleWindow();
                window.Show();
            }
            catch (Exception ex)
            {
                LogError(ex);
                MessageBox.Show(
                    $"启动失败：\n{ex}",
                    "PowerCapsule 启动错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LogError(Exception ex)
        {
            try
            {
                var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\PowerCapsule";
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(
                    dir + "\\error.log",
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
            }
            catch { }
        }
    }
}
