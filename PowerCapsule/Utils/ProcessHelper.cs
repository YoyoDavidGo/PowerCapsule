using System.Diagnostics;
using System;

namespace PowerCapsule.Utils
{
    public static class ProcessHelper
    {
        public static ProcessResult RunCommand(string fileName, string arguments, bool asAdmin = false)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = asAdmin,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = !asAdmin,
                    RedirectStandardError = !asAdmin
                };

                if (asAdmin)
                {
                    psi.Verb = "runas";
                }

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return new ProcessResult { Success = false, Error = "无法启动进程" };

                    if (!asAdmin)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        var error = process.StandardError.ReadToEnd();
                        process.WaitForExit(5000);
                        return new ProcessResult
                        {
                            Success = process.ExitCode == 0,
                            Output = output,
                            Error = error,
                            ExitCode = process.ExitCode
                        };
                    }

                    process.WaitForExit(5000);
                    return new ProcessResult
                    {
                        Success = process.ExitCode == 0,
                        ExitCode = process.ExitCode
                    };
                }
            }
            catch (Exception ex)
            {
                return new ProcessResult { Success = false, Error = ex.Message };
            }
        }
    }

    public class ProcessResult
    {
        public bool Success { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public int ExitCode { get; set; }
    }
}
