using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static ConsoleUtilitiesLite.ConsoleUtilitiesLite;

namespace VideoCompresser
{
    public partial class VideoCompresser : IDisposable
    {
        const int CRF = 30;
        const string COMMAND_FORMAT = "-hide_banner -loglevel fatal -i \"{0}\" -c:v libx265 -c:a copy -crf {2} \"{1}\"";
        int _videosCount = 0;
        Video[] _videos;
        bool _disposed;
        readonly BlockingCollection<string> _errors = new BlockingCollection<string>();
        readonly Semaphore _gate;
        readonly HashSet<string> _processedPaths = new HashSet<string>();

        public int ErrorProbability => _errors.Count / (_videosCount == 0 ? 1 : _videosCount);
        public float SuccessRate => 1 - ErrorProbability;
        public VideoCompresser(int maxVideosAtATime) => _gate = new Semaphore(maxVideosAtATime, maxVideosAtATime);

        public void CompressAllVideos(string path, bool deleteFiles = false)
        {
            StopWatch stopWatch = new StopWatch();
            stopWatch.StartRecording();

            CompressVideosInFolderRecursively(path, deleteFiles);

            stopWatch.StopRecording();
            ShowStats(stopWatch);
            if (_errors.Count != 0)
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadLine();
            }
        }

        void CompressVideosInFolderRecursively(string path, bool deleteFiles = false)
        {
            foreach (var directory in Directory.GetDirectories(path))
                CompressVideosInFolderRecursively(directory, deleteFiles);

            CompressVideosInfolder(path, deleteFiles);
        }

        public void CompressVideosInfolder(string path, bool deleteFiles = false, bool showStats = false)
        {
            StopWatch stopWatch = new StopWatch();
            stopWatch.StartRecording();

            LogInfoMessage("Gathering information...");
            IEnumerable<string> videosPaths = VideoPathFinder.FindVideosPaths(path);

            _videos = VideoInfoGatherer.GetAllVideosInfo(videosPaths);

            LogInfoMessage("Sorting paths...");
            Array.Sort(_videos, BynumberOfFramesASC);

            LogInfoMessage("Finished! Starting compression...");
            try
            {
                string outputDirectory = Directory.CreateDirectory(Path.Combine(path, "Done")).FullName;

                Task.WaitAll(
                    Task.Run(() => CompressLargeVideos(deleteFiles, outputDirectory)),
                    Task.Run(() => CompressShortVideos(deleteFiles, outputDirectory))
                );

                if (deleteFiles)
                    MoveProcessedFilesToMainPath(outputDirectory, path);

            }
            catch (Exception ex) { LogErrorMessage(ex.ToString()); }

            stopWatch.StopRecording();
            Console.Beep();
            if (showStats)
                ShowStats(stopWatch);
        }

        private void MoveProcessedFilesToMainPath(string processedFilesDirectory, string mainPath)
        {
            foreach (var filePath in VideoPathFinder.FindVideosPaths(processedFilesDirectory))
            {
                try
                {
                    File.Move(filePath, GetEndPath(filePath, mainPath));
                }
                catch (IOException)
                {
                    File.Delete(filePath);
                    LogWarningMessage($"Deleted erroneos compression of: {Path.GetFileName(filePath)}");
                }
                catch (Exception) { }
            }

            try
            {
                Directory.Delete(processedFilesDirectory);
            }
            catch (Exception) { }

            LogSuccessMessage($"Succesfully moved all files to {mainPath}");
        }

        int BynumberOfFramesASC(Video video1, Video video2) => video1.NumberOfFrames.CompareTo(video2.NumberOfFrames);

        private Task CompressShortVideos(bool deleteFiles, string outputDirectory)
        {
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < _videos.Length; i++)
            {
                if (VideoHasBeenProcessed(_videos[i].Path))
                    continue;
                _gate.WaitOne();
                tasks.Add(CompressVideo(_videos[i], deleteFiles, outputDirectory));
            }

            Task.WaitAll(tasks.ToArray());
            return Task.CompletedTask;
        }
        private Task CompressLargeVideos(bool deleteFiles, string outputDirectory)
        {
            for (int i = _videos.Length - 1; i >= 0; i--)
            {
                if (VideoHasBeenProcessed(_videos[i].Path))
                    continue;
                _gate.WaitOne();
                CompressVideo(_videos[i], deleteFiles, outputDirectory).Wait();
            }

            return Task.CompletedTask;
        }
        private bool VideoHasBeenProcessed(string videoPath)
        {
            lock (_processedPaths) { return !_processedPaths.Add(videoPath); }
        }
        void ShowStats(StopWatch timer)
        {
            Division();
            LogSuccessMessage("FINISHED!");

            LogWarningMessage("It took {0} time to compress {1} videos.", timer.GetTimeSpan(), _videosCount);
            LogWarningMessage("Videos Compressed: {0}", _videosCount);
            LogWarningMessage("Success rate: {0:P1}", SuccessRate);

            foreach (var error in _errors)
                LogErrorMessage(error);
        }

        async Task CompressVideo(Video video, bool deleteFiles, string outputDirectory)
        {
            if (!File.Exists(video.Path))
            {
                _gate.Release();
                return;
            }

            string endPath = GetEndPath(video.Path, outputDirectory);
            string fileName = Path.GetFileName(video.Path);
            ConsoleCommand command = new ConsoleCommand()
            {
                Command = Program.FFMPEG_PATH,
                Args = string.Format(COMMAND_FORMAT, video.Path, endPath, CRF)
            };

            try
            {
                LogSuccessMessage($"Compressing {fileName}:");
                await Program.ExecuteCommandAsync(command);

                Video convertedVideo = Video.Factory.Create(endPath);
                if (!CloseEquals(video.DurationInSeconds, convertedVideo.DurationInSeconds))
                {
                    LogWarningMessage($"Durations different of {Path.GetFileName(video.Path)}.");
                    _errors.Add($"Duration different on: {video.Path}");
                    return;
                }

                if (deleteFiles)
                    File.Delete(video.Path);

                SubDivision();
                LogSuccessMessage($"Finished compressing: {fileName}");
                SubDivision();
            }
            catch (Exception ex)
            {
                _errors.Add($"Error on: {Path.GetFileName(video.Path)}");
                LogErrorMessage(ex.ToString());
            }
            finally
            {
                Interlocked.Increment(ref _videosCount);
                _gate.Release();
            }
        }

        private bool CloseEquals(double durationInSeconds1, double durationInSeconds2) => Math.Abs(durationInSeconds1 - durationInSeconds2) < 1;

        string GetEndPath(string filePath, string outputDirectory)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string endPath = Path.Combine(outputDirectory, $"{fileName}.mp4");
            return endPath;
        }

        ~VideoCompresser()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _errors.Dispose();
                    _gate.Dispose();
                }
                _gate.Close();
            }
            _disposed = true;
        }
    }
}
