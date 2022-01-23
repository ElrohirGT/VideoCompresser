using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using static ConsoleUtilitiesLite.ConsoleUtilities;

namespace VideoCompresser
{
    internal class Program
    {
        private static readonly string[] _title =
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

        private static async Task Main(string[] args)
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
            LogInfoMessage("Press s to cancel after the current compression finished.");
            LogInfoMessage("Press q force quit and cancel all compressions.");
            LogInfoMessage("Press + to increment the logging severity.");
            LogInfoMessage("Press - to decrement the logging severity.");
            SubDivision();

            using CancellationTokenSource softCTS = new();
            using CancellationTokenSource instantCTS = new();

            var previousLogLength = LogInfoMessage($"Gathering information...");
            var compression = VideoCompresser.CompressAllVideos(path, !notDeleteFiles, maxNumberOfVideos, softCTS.Token, instantCTS.Token);
            int loggingLevel = (int)LoggingLevel.ShowProgress;
            var loggingTask = Task.Run(async () =>
            {
                await foreach (var report in compression.ReportChannel.ReadAllAsync())
                {
                    //INFO: The Console.CursorTop property doesn't work on the mac terminal.
                    //TODO: Find out why we can't use ClearPreviousLog on Mac OS.
                    //if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    ClearPreviousLog(previousLogLength);
                    if (loggingLevel == (int)LoggingLevel.None)
                    {
                        previousLogLength = Array.Empty<int>();
                        continue;
                    }

                    StringBuilder builder = new();
                    if (loggingLevel >= (int)LoggingLevel.ShowFolder)
                        builder.AppendLine($"Folder: {report.CurrentDirectory}");
                    if (loggingLevel >= (int)LoggingLevel.ShowProgress)
                        foreach (var item in report.Percentages)
                            builder.AppendLine($"{item.Key}: {item.Value:N2}%");
                    builder.Append($"Count: {report.CompressedVideosCount}/{report.VideosCount} videos.");

                    previousLogLength = LogInfoMessage(builder.ToString());
                }
            });

            CommandObserver commandObserver = new();
            commandObserver.Add(new ConsoleCommand(ConsoleKey.S, softCTS.Cancel));
            commandObserver.Add(new ConsoleCommand(ConsoleKey.Q, instantCTS.Cancel));
            commandObserver.Add(new ConsoleCommand(ConsoleKey.OemMinus, () => DecrementLoggingLevel(ref loggingLevel)));
            commandObserver.Add(new ConsoleCommand(ConsoleKey.OemPlus, () => IncrementLoggingLevel(ref loggingLevel)));
            commandObserver.StartObserving(instantCTS.Token);

            StopWatch stopWatch = new();
            stopWatch.StartRecording();
            var errors = compression.Start();
            stopWatch.StopRecording();

            commandObserver.StopObserving();
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
            Console.WriteLine("Press any key to close the window...");
            Console.ReadLine();
        }

        private static void IncrementLoggingLevel(ref int loggingLevel) => loggingLevel = Math.Min(++loggingLevel, (int)LoggingLevel.ShowProgress);

        private static void DecrementLoggingLevel(ref int loggingLevel) => loggingLevel = Math.Max(--loggingLevel, (int)LoggingLevel.None);
        private static string ReadConsoleLine() => (Console.ReadLine() ?? string.Empty);
    }

    public enum LoggingLevel
    {
        None,
        OnlyVideoCount,
        ShowFolder,
        ShowProgress
    }
}