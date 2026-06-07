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
                        // 异步读两个流，避免“先读满 stdout 再读 stderr”在管道缓冲写满时死锁
                        var outputTask = process.StandardOutput.ReadToEndAsync();
                        var errorTask = process.StandardError.ReadToEndAsync();

                        if (!process.WaitForExit(5000))
                        {
                            try { process.Kill(); } catch { }
                            return new ProcessResult { Success = false, Error = "命令执行超时" };
                        }

                        return new ProcessResult
                        {
                            Success = process.ExitCode == 0,
                            Output = outputTask.Result,
                            Error = errorTask.Result,
                            ExitCode = process.ExitCode
                        };
                    }

                    if (!process.WaitForExit(5000))
                    {
                        try { process.Kill(); } catch { }
                        return new ProcessResult { Success = false, Error = "命令执行超时" };
                    }
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
