﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ConsoleUtilitiesLite.ConsoleUtilities;

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
            ShowVersion(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty);

            string path;
            while (true)
            {
                Console.Write("Please insert a path: ");
                path = ReadConsoleLine().Trim();
                if (Directory.Exists(path))
                    break;
                LogErrorMessage("Please write a valid path!");
            }

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
            LogWarningMessage("{0} videos will be converted at the same time.", maxNumberOfVideos);

            SubDivision();
            LogInfoMessage("Press s, to cancel after the current compression finished.");
            LogInfoMessage("Press q, force quit and cancel all compressions.");

            CancellationTokenSource softCTS = new();
            CancellationTokenSource instantCTS = new();

            Task.Run(() =>
            {
                while (true)
                {
                    string command = ReadConsoleLine();
                    if (command == "s")
                        softCTS.Cancel();
                    if (command == "q")
                        instantCTS.Cancel();
                }
            });

            var stopWatch = new StopWatch();
            stopWatch.StartRecording();
            VideoCompresser videoCompresser = new(maxNumberOfVideos);
            int previousLogLength = 1;
            LogInfoMessage("Current: 0.00%; 0/0 videos.");
            videoCompresser.Report += (r) =>
            {
                ClearPreviousLog(previousLogLength);
                
                StringBuilder builder = new(previousLogLength);
                foreach (var item in r.Percentages)
                    builder.AppendLine($"{item.Key}: {item.Value:N2}%");
                builder.AppendLine($"{r.CompressedVideosCount}/{r.VideosCount} videos.");
                
                previousLogLength = LogInfoMessage(builder.ToString());
            };
            var errors = videoCompresser.CompressAllVideos(path, !notDeleteFiles, softCTS.Token, instantCTS.Token);
            stopWatch.StopRecording();

            Division();
            LogInfoMessage($"Time: {stopWatch.RecordedTime}.");

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
        }

        private static string ReadConsoleLine() => (Console.ReadLine() ?? string.Empty);
    }
}
