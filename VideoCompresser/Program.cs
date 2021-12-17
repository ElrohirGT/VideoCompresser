using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static ConsoleUtilitiesLite.ConsoleUtilities;

namespace VideoCompresser
{
    class Program
    {
        static readonly string[] _title =
        {
            "░█──░█ ─▀─ █▀▀▄ █▀▀ █▀▀█ ░█▀▀█ █▀▀█ █▀▄▀█ █▀▀█ █▀▀█ █▀▀ █▀▀ █▀▀ █▀▀ █▀▀█ ",
            "─░█░█─ ▀█▀ █──█ █▀▀ █──█ ░█─── █──█ █─▀─█ █──█ █▄▄▀ █▀▀ ▀▀█ ▀▀█ █▀▀ █▄▄▀ ",
            "──▀▄▀─ ▀▀▀ ▀▀▀─ ▀▀▀ ▀▀▀▀ ░█▄▄█ ▀▀▀▀ ▀───▀ █▀▀▀ ▀─▀▀ ▀▀▀ ▀▀▀ ▀▀▀ ▀▀▀ ▀─▀▀"
        };

        static Program()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                _title = new string[] { "VIDEO COMPRESSER" };
        }

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.Unicode;
            Console.Clear();
            Console.Title = "Video Compresser";

            ShowTitle(_title);
            ShowVersion(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty);

            string path;
            while (true)
            {
                Console.Write("Please insert a path: ");
                path = ReadConsoleLine().Trim();
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    path = Regex.Unescape(path);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    path = path.Replace(@"""", string.Empty);
                if (Directory.Exists(path))
                    break;
                LogErrorMessage("Please write a valid path!");
            }
            LogWarningMessage($"The path that will be used is: {path}");

            Console.Write("Do you want to delete the file after compressing? (y/n, default is yes): ");
            bool notDeleteFiles = ReadConsoleLine().Trim().ToLower().Equals("n");
            if (notDeleteFiles)
                LogWarningMessage("Files will not be deleted after compressing.");
            else
                LogWarningMessage("Files will be deleted after compressing.");

            int maxNumberOfVideos = 2;
            while (true)
            {
                Console.Write($"How many videos at a time can be converted? (more may slow the computer, default is {maxNumberOfVideos}): ");
                string answer = ReadConsoleLine().Trim();
                if (string.IsNullOrEmpty(answer))
                    break;
                if (int.TryParse(answer, out maxNumberOfVideos))
                    break;
                LogErrorMessage("Please write a valid number!");
            }
            LogWarningMessage($"{maxNumberOfVideos} videos will be converted at the same time.");

            SubDivision();
            LogInfoMessage("Press s, to cancel after the current compression finished.");
            LogInfoMessage("Press q, force quit and cancel all compressions.");
            SubDivision();

            using CancellationTokenSource softCTS = new();
            using CancellationTokenSource instantCTS = new();


            int previousLogLength = LogInfoMessage($"Gathering information...");
            VideoCompresser videoCompresser = new();
            var compression = videoCompresser.CompressAllVideos(path, !notDeleteFiles, maxNumberOfVideos, softCTS.Token, instantCTS.Token);
            var loggingTask = Task.Run(async () =>
            {
                await foreach (var report in compression.ReportChannel.ReadAllAsync())
                {
                    //INFO: The Console.CursorTop property doesn't work on the mac terminal.
                    //TODO: Find out why we can't use ClearPreviousLog on Mac OS.
                    //if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        ClearPreviousLog(previousLogLength);

                    StringBuilder builder = new(previousLogLength);
                    builder.AppendLine($"Folder: {report.CurrentDirectory}");
                    foreach (var item in report.Percentages)
                        builder.AppendLine($"{item.Key}: {item.Value:N2}%");
                    builder.Append($"Count: {report.CompressedVideosCount}/{report.VideosCount} videos.");

                    previousLogLength = LogInfoMessage(builder.ToString());
                }
            });
            var commandTask = Task.Run(() =>
            {
                while (true)
                {
                    ConsoleKeyInfo command = Console.ReadKey(true);
                    if (command.KeyChar == 's')
                        softCTS.Cancel();
                    if (command.KeyChar == 'q')
                        instantCTS.Cancel();
                }
            }, instantCTS.Token);
            
            var stopWatch = new StopWatch();
            stopWatch.StartRecording();
            var errors = compression.Start();
            stopWatch.StopRecording();

            await loggingTask;

            Division();
            if (errors.Count == 0)
                LogSuccessMessage("NO ERRORS!");
            else
            {
                LogErrorMessage("ERRORS:");
                foreach (var error in errors)
                {
                    LogErrorMessage(error.Key);
                    foreach (var item in error.Value)
                        LogInfoMessage($"\t- {item}");
                }
            }
            SubDivision();
            LogInfoMessage($"Time: {stopWatch.RecordedTime}.");
            Console.ReadLine();
        }

        private static string ReadConsoleLine() => (Console.ReadLine() ?? string.Empty);
    }
}
