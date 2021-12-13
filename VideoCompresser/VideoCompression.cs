﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using FFMpegCore;

namespace VideoCompresser
{
    public class VideoCompression
    {
        readonly Channel<CompressingReport> _channel = Channel.CreateUnbounded<CompressingReport>(new UnboundedChannelOptions() { SingleWriter = true });
        readonly object _lock = new();

        public string? InitialPath { get; init; }
        public bool DeleteFiles { get; init; }
        public CancellationToken? SoftToken { get; init; }
        public CancellationToken? InstantToken { get; init; }
        public int MaxDegreeOfParalelism { get; init; }
        public IEnumerable<string>? ValidExtensions { get; init; }
        public int CRF { get; set; }
        public ChannelReader<CompressingReport> ReportChannel => _channel.Reader;

        public IDictionary<string, List<string>> Start()
        {
            IDictionary<string, List<string>> errors = CompressVideosRecursively(InitialPath, DeleteFiles, SoftToken, InstantToken);
            _channel.Writer.Complete();
            return errors;
        }

        private IDictionary<string, List<string>> CompressVideosRecursively(string path, bool deleteFiles, CancellationToken? softToken, CancellationToken? instantToken)
        {
            ConcurrentDictionary<string, List<string>> errors = new();
            foreach (var subDirectory in Directory.GetDirectories(path))
            {
                var subErrors = CompressVideosRecursively(subDirectory, deleteFiles, softToken, instantToken);
                foreach (var error in subErrors)
                    AddError(errors, error.Key, error.Value);
            }
            CompressVideosInFolder(path, deleteFiles, errors, softToken, instantToken);
            return errors;
        }

        private void CompressVideosInFolder(string path, bool deleteFiles, ConcurrentDictionary<string, List<string>> errors, CancellationToken? soft, CancellationToken? instantToken)
        {
            CompressingReportBuilder reportInstance = new(path);
            string outputPath = Path.Combine(path, "Done");
            Directory.CreateDirectory(outputPath);

            var sortedVideos = GetSortedVideos(path, errors, reportInstance);
            ParallelOptions configuration = new() { MaxDegreeOfParallelism = MaxDegreeOfParalelism };
            Parallel.ForEach(sortedVideos, configuration, (video) =>
            {
                string outputFilePath = Path.Combine(outputPath, video.FileNameWithoutExtension + ".mp4");
                try
                {
                    soft?.ThrowIfCancellationRequested();
                    instantToken?.ThrowIfCancellationRequested();
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
        private void CompressVideo(Video video, string outputFilePath, CancellationToken? token, CompressingReportBuilder reportInstance)
        {
            var args = FFMpegArguments
                .FromFileInput(video.Path)
                .OutputToFile(
                    outputFilePath,
                    false,
                    options => options.WithVideoCodec("libx265").WithConstantRateFactor(CRF).WithFastStart())
                .NotifyOnProgress(p => OnReport(p, video.FileName, reportInstance), video.Duration);
            if (token is not null)
                args.CancellableThrough(token.Value);
            args.ProcessSynchronously();
        }
        private void ReportVideoCompleted(Video video, CompressingReportBuilder reportInstance)
        {
            reportInstance.IncrementCompressedVideosCount();
            OnReport(100, video.FileName, reportInstance);
            reportInstance.RemovePercentage(video.FileName);
        }
        private void OnReport(double percentage, string fileName, CompressingReportBuilder reportInstance)
        {
            reportInstance.ChangePercentage(fileName, percentage);
            lock (_lock)
                _channel.Writer.TryWrite(reportInstance.AsReadonly());
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
                    Video video = new(videoPath, totalFrames.Value, mediaInfo.Duration, videoStream.AvgFrameRate);
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
                foreach (var validExtension in ValidExtensions)
                    if (filePath.EndsWith(validExtension))
                        videoPaths.Add(filePath);
            return videoPaths;
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
    }
}