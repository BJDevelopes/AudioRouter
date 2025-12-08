using System;
using System.Diagnostics;
using System.Security.Principal;

namespace AudioRouter.Helpers
{
    public static class AdminHelper
    {
        public static bool IsRunningAsAdmin()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static void RestartAsAdmin()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(processInfo);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to restart as admin: {ex.Message}");
            }
        }

        public static void SetProcessPriority()
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                process.PriorityClass = ProcessPriorityClass.High;
                Debug.WriteLine("Process priority set to High");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set process priority: {ex.Message}");
            }
        }
    }
}
