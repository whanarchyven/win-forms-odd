using System;
using System.IO;
using System.Text;
using System.Windows;

namespace KioskStand
{
    public static class App
    {
        static string logFile;

        public static void Log(string msg)
        {
            try { File.AppendAllText(logFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine, Encoding.UTF8); }
            catch { }
        }

        [STAThread]
        public static int Main(string[] args)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            logFile = Path.Combine(baseDir, "error.log");
            string configPath = Path.Combine(baseDir, args.Length > 0 ? args[0] : "config.json");

            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += (s, e) =>
            {
                // стенд не должен «падать» на глазах у посетителей — пишем в лог и продолжаем
                Log("Необработанная ошибка: " + e.Exception);
                e.Handled = true;
            };

            AppConfig cfg;
            try { cfg = AppConfig.Load(configPath); }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось прочитать конфигурацию:\n" + configPath + "\n\n" + ex.Message, "Клинические задачи", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }

            string resultsPath = cfg.Resolve(cfg.ResultsFile);
            Storage store;
            try { store = new Storage(resultsPath); }
            catch (Exception ex)
            {
                // файл результатов повреждён: сохраняем копию и начинаем новый, ничего не теряя
                string backup = resultsPath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                try { File.Move(resultsPath, backup); } catch { }
                Log("Файл результатов повреждён (" + ex.Message + "), перенесён в " + backup);
                MessageBox.Show("Файл результатов повреждён и сохранён как:\n" + backup + "\n\nСоздан новый файл.", "Клинические задачи", MessageBoxButton.OK, MessageBoxImage.Warning);
                store = new Storage(resultsPath);
            }

            var w = new MainWindow(cfg, store);
            return app.Run(w);
        }
    }
}
