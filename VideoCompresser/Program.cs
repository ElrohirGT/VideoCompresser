using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static ConsoleUtilitiesLite.ConsoleUtilitiesLite;

namespace VideoCompresser
{
    class Program
    {
        static readonly string[] Title = new string[]
        {
            "░█──░█ ─▀─ █▀▀▄ █▀▀ █▀▀█ ░█▀▀█ █▀▀█ █▀▄▀█ █▀▀█ █▀▀█ █▀▀ █▀▀ █▀▀ █▀▀ █▀▀█ ",
            "─░█░█─ ▀█▀ █──█ █▀▀ █──█ ░█─── █──█ █─▀─█ █──█ █▄▄▀ █▀▀ ▀▀█ ▀▀█ █▀▀ █▄▄▀ ",
            "──▀▄▀─ ▀▀▀ ▀▀▀─ ▀▀▀ ▀▀▀▀ ░█▄▄█ ▀▀▀▀ ▀───▀ █▀▀▀ ▀─▀▀ ▀▀▀ ▀▀▀ ▀▀▀ ▀▀▀ ▀─▀▀"
        };

        public static readonly string FFMPEG_PATH = Path.Combine(AppContext.BaseDirectory, @"ffmpeg 4.4\ffmpeg.exe");
        public static readonly string FFPLAY_PATH = Path.Combine(AppContext.BaseDirectory, @"ffmpeg 4.4\ffplay.exe");
        public static readonly string FFPROBE_PATH = Path.Combine(AppContext.BaseDirectory, @"ffmpeg 4.4\ffprobe.exe");

        static Program()
        {
            string baseDirectory = Path.Combine(AppContext.BaseDirectory, @"ffmpeg 4.4");
            FFMPEG_PATH = Path.Combine(baseDirectory, "ffmpeg");
            FFPLAY_PATH = Path.Combine(baseDirectory, "ffplay");
            FFPROBE_PATH = Path.Combine(baseDirectory, "ffprobe");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                FFMPEG_PATH += ".exe";
                FFPLAY_PATH += ".exe";
                FFPROBE_PATH += ".exe";
            }
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.Unicode;
            Console.Clear();

            //Console.WriteLine(FFMPEG_PATH);
            //Console.WriteLine();
            //Console.WriteLine(FFPLAY_PATH);
            //Console.WriteLine();
            //Console.WriteLine(FFPROBE_PATH);
            //Console.WriteLine();

            Console.Title = "Video Compresser";
            ShowTitle(Title);
            ShowVersion(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());

            string path;
            while (true)
            {
                Console.Write("Please insert a path: ");
                path = Console.ReadLine().Trim();
                if (Directory.Exists(path))
                    break;
                LogErrorMessage("Please write a valid path!");
            }

            Console.Write("Do you want to delete the file after compressing? (y/n): ");
            bool deleteFiles = Console.ReadLine().Trim().ToLower().Equals("y");
            if (deleteFiles)
                LogWarningMessage("Files will be deleted after compressing.");
            else
                LogWarningMessage("Files will not be deleted after compressing.");

            int maxNumberOfVideos = 5;
            while (true)
            {
                Console.Write("How many videos at a time can be converted? (more may slow the computer, default is 5): ");
                string answer = Console.ReadLine().Trim();
                if (string.IsNullOrEmpty(answer))
                    break;
                if (int.TryParse(answer, out maxNumberOfVideos))
                    break;
                LogErrorMessage("Please write a valid number!");
            }
            LogWarningMessage("{0} videos will be converted at the same time.", maxNumberOfVideos);

            //TODO Make compress videos recursively
            SubDivision();
            new VideoCompresser(maxNumberOfVideos).CompressAllVideos(path, deleteFiles);
        }

        #region Static Methods
        internal static Task<string> ExecuteCommandAsync(ConsoleCommand command)
        {
            try
            {
                TaskCompletionSource<string> taskSource = new TaskCompletionSource<string>();
                ProcessStartInfo procStartInfo =
                    new ProcessStartInfo(command.Command, command.Args)
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                        UseShellExecute = false,
                        CreateNoWindow = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                    };

                Process process = new Process() { StartInfo = procStartInfo, EnableRaisingEvents = true };
                process.Exited += (sender, args) =>
                {
                    taskSource.SetResult(process.StandardOutput.ReadToEnd());
                    process.Dispose();
                };

                process.Start();
                return taskSource.Task;
            }
            catch (Exception) { throw; }
        }
        internal static string ExecuteCommandSync(ConsoleCommand command)
        {
            try
            {
                ProcessStartInfo procStartInfo =
                    new ProcessStartInfo(command.Command, command.Args)
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                        UseShellExecute = false,
                        CreateNoWindow = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                    };

                using Process process = new Process() { StartInfo = procStartInfo };
                process.Start();
                process.WaitForExit();

                string result = process.StandardOutput.ReadToEnd();
                return result;
            }
            catch (Exception) { throw; }
        }
        #endregion
    }
}
