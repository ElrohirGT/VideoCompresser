using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FFMpegCore;

namespace VideoCompresser
{
    public partial class VideoCompresser
    {
        private const int CRF = 30;
        private readonly int _maxNumberOfVideos;
        private readonly IEnumerable<string> _validExtensions = new string[] { ".mp4", ".webm" };
        private static object _lock = new();

        public event Action<CompressingReport> Report;

        public VideoCompresser(int maxNumberOfVideos) => _maxNumberOfVideos = maxNumberOfVideos;

        public IDictionary<string, List<string>> CompressAllVideos(string path, bool deleteFiles, CancellationToken softToken, CancellationToken instantToken)
        {
            ConcurrentDictionary<string, List<string>> errors = new();
            foreach (var subDirectory in Directory.GetDirectories(path))
            {
                var subErrors = CompressAllVideos(subDirectory, deleteFiles, softToken, instantToken);
                foreach (var error in subErrors)
                    AddError(errors, error.Key, error.Value);
            }
            CompressVideosInFolder(path, deleteFiles, errors, softToken, instantToken);
            return errors;
        }

        private static void AddError(ConcurrentDictionary<string, List<string>> errors, string key, List<string> value)
        {
            errors.TryAdd(key, new List<string>());
            errors[key].AddRange(value);
        }
        private static void AddError(ConcurrentDictionary<string, List<string>> errors, string key, string value)
        {
            errors.TryAdd(key, new List<string>());
            errors[key].Add(value);
        }

        private void CompressVideosInFolder(string path, bool deleteFiles, ConcurrentDictionary<string, List<string>> errors, CancellationToken soft, CancellationToken instantToken)
        {
            CompressingReportBuilder reportInstance = new();
            string outputPath = Path.Combine(path, "Done");
            Directory.CreateDirectory(outputPath);

            var sortedVideos = GetSortedVideos(path, errors, reportInstance);
            ParallelOptions configuration = new() { MaxDegreeOfParallelism = _maxNumberOfVideos };
            Parallel.ForEach(sortedVideos, configuration, (video) =>
            {
                string outputFilePath = Path.Combine(outputPath, video.FileNameWithoutExtension+".mp4");
                try
                {
                    soft.ThrowIfCancellationRequested();
                    instantToken.ThrowIfCancellationRequested();
                    CompressVideo(video, outputFilePath, instantToken, reportInstance);

                    var newVideoInfo = FFProbe.Analyse(outputFilePath);
                    if ((int)newVideoInfo.Duration.TotalSeconds != (int)video.Duration.TotalSeconds)
                        AddError(errors, video.Path, $"Durations of output video and original video are different, please check them manually.");
                    else if (deleteFiles)
                        File.Delete(video.Path);
                    ReportVideoCompleted(video, reportInstance);
                }
                catch (OperationCanceledException) { }
                catch (Exception e)
                {
                    AddError(errors, video.Path, $"Error compressing the video: {e.Message}");
                    File.Delete(outputFilePath);
                    ReportVideoCompleted(video, reportInstance);
                }
            });

            if (!deleteFiles)
                return;

            Parallel.ForEach(Directory.GetFiles(outputPath, "*.mp4"), newVideoPath =>
            {
                string destFilePath = Path.Combine(path, Path.GetFileName(newVideoPath));
                File.Move(newVideoPath, destFilePath);
            });

            try
            {
                Directory.Delete(outputPath);
            }
            catch (Exception)
            {
                AddError(errors, outputPath, "Couldn't clean the directory, please check it and delete it manually.");
            }
        }

        private void ReportVideoCompleted(Video video, CompressingReportBuilder reportInstance)
        {
            reportInstance.IncrementCompressedVideosCount();
            OnReport(100, video.FileName, reportInstance);
            reportInstance.RemovePercentage(video.FileName);
        }

        private void CompressVideo(Video video, string outputFilePath, CancellationToken token, CompressingReportBuilder reportInstance)
        {
            FFMpegArguments
                .FromFileInput(video.Path)
                .OutputToFile(
                    outputFilePath,
                    false,
                    options => options.WithVideoCodec("libx265").WithConstantRateFactor(CRF).WithFastStart())
                .NotifyOnProgress(p=>OnReport(p, video.FileName, reportInstance), video.Duration)
                .CancellableThrough(token)
                .ProcessSynchronously();
        }

        private void OnReport(double percentage, string fileName, CompressingReportBuilder reportInstance)
        {
            reportInstance.ChangePercentage(fileName, percentage);
            lock (_lock)
                Report?.Invoke(reportInstance.AsReadonly());
        }

        private IEnumerable<Video> GetSortedVideos(string path, ConcurrentDictionary<string, List<string>> errors, CompressingReportBuilder reportInstance)
        {
            BlockingSortedSet<Video> videos = new(Comparer<Video>.Create((v1, v2) => v1.TotalFrames.CompareTo(v2.TotalFrames)));
            Parallel.ForEach(GetVideoPaths(path), videoPath =>
            {
                IMediaAnalysis mediaInfo = FFProbe.Analyse(videoPath);
                VideoStream? videoStream = mediaInfo.PrimaryVideoStream;
                long? totalFrames = (long?)(videoStream?.Duration.TotalSeconds * videoStream?.AvgFrameRate);
                if (totalFrames is null)
                {
                    errors.TryAdd(videoPath, new List<string>());
                    errors[videoPath].Add("Couldn't get the number of frames!");
                }
                else
                {
                    Video video = new(videoPath, totalFrames.Value, videoStream.Duration, videoStream.AvgFrameRate);
                    if (videos.TryAdd(video))
                        reportInstance.IncrementVideosCount();
                    else
                    {
                        errors.TryAdd(videoPath, new List<string>());
                        errors[videoPath].Add("Sorry, but something went wrong trying to create a video instance.");
                    }
                }
            });
            return videos;
        }

        private IEnumerable<string> GetVideoPaths(string path)
        {
            string[] filePaths = Directory.GetFiles(path);
            List<string> videoPaths = new(filePaths.Length);
            
            foreach (var filePath in filePaths)
                foreach (var validExtension in _validExtensions)
                    if (filePath.EndsWith(validExtension))
                        videoPaths.Add(filePath);
            return videoPaths;
        }
    }
}
