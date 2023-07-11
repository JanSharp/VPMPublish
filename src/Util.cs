using System.Diagnostics;
using System.Text;

namespace VPMPublish
{
    public static class Util
    {
        public static bool DidAbort { private set; get; }
        private static ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
        };

        // I seriously dislike these Abort and MayAbort functions,
        // but it's at least somewhat better than not having them.

        public static Exception Abort(string message)
        {
            DidAbort = true;
            return new Exception(message);
        }

        public static T Abort<T>(T exception) where T : Exception
        {
            DidAbort = true;
            return exception;
        }

        public static T MayAbort<T>(Func<T> func)
        {
            DidAbort = true;
            T result = func();
            DidAbort = false;
            return result;
        }

        public static void Info(string msg) => Console.WriteLine(msg);

        public static void SetChildProcessWorkingDirectory(string workingDIrectory) => startInfo.WorkingDirectory = workingDIrectory;

        public static List<string> CheckRunProcess(string? errorMsgPrefix, string fileName, params string[] args)
        {
            startInfo.FileName = fileName;
            startInfo.ArgumentList.Clear();
            foreach (string arg in args)
                startInfo.ArgumentList.Add(arg);
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            using Process? process = Process.Start(startInfo);
            if (process == null)
                throw new Exception($"Unable to start a '{fileName}' process even "
                    + $"though their availability has been validated already."
                );

            List<string> lines = new List<string>();
            process.OutputDataReceived += (object o, DataReceivedEventArgs e) => {
                if (e.Data != null)
                    lines.Add(e.Data);
            };
            process.BeginOutputReadLine();

            List<string> errorLines = new List<string>();
            process.ErrorDataReceived += (object o, DataReceivedEventArgs e) => {
                if (e.Data != null)
                    errorLines.Add(e.Data);
            };
            process.BeginErrorReadLine();

            process.WaitForExit();

            if (process.ExitCode != 0)
                throw Abort((errorMsgPrefix == null ? "" : errorMsgPrefix + "\n\n")
                    + $"The process '{fileName}' exited with the exit code {process.ExitCode}.\n"
                    + $"The arguments were:\n{string.Join('\n', args.Select(a => $"'{a}'"))}\n\n"
                    + $"The process had the following error output:\n{string.Join('\n', errorLines)}"
                );

            process.Close();

            return lines;
        }

        public static List<string> RunProcess(string fileName, params string[] args)
        {
            return CheckRunProcess(null, fileName, args);
        }
    }
}
