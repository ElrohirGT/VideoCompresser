using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace VideoCompresser;

public class CompressingReportBuilder
{
    private int _compressedVideosCount = 0;
    private int _videosCount = 0;
    private string _currentDirectory = string.Empty;
    private readonly ConcurrentDictionary<string, double> _dictionary = new();

    public CompressingReportBuilder(string currentDirectory) => _currentDirectory = currentDirectory;

    public CompressingReport AsReadonly() => new(_compressedVideosCount, _videosCount, _dictionary, _currentDirectory);

    public void IncrementVideosCount() => Interlocked.Increment(ref _videosCount);
    public void IncrementCompressedVideosCount() => Interlocked.Increment(ref _compressedVideosCount);
    public void ChangePercentage(string fileName, double percentage)
    {
        _dictionary.TryAdd(fileName, percentage);
        _dictionary[fileName] = percentage;
    }
    public void RemovePercentage(string fileName) => _dictionary.TryRemove(fileName, out _);
}

public readonly record struct CompressingReport(int CompressedVideosCount, int VideosCount, IDictionary<string, double> Percentages, string CurrentDirectory);