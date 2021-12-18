using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace VideoCompresser;

public class CompressionReportBuilder
{
    private int _compressedVideosCount = 0;
    private int _videosCount = 0;
    private readonly string _currentDirectory;
    private readonly ConcurrentDictionary<string, double> _dictionary = new();

    public CompressionReportBuilder(string currentDirectory) => _currentDirectory = currentDirectory;

    public CompressionReport AsReadonly() => new(_compressedVideosCount, _videosCount, _dictionary, _currentDirectory);

    public void IncrementVideosCount() => Interlocked.Increment(ref _videosCount);

    public void IncrementCompressedVideosCount() => Interlocked.Increment(ref _compressedVideosCount);

    public void ChangePercentage(string fileName, double percentage)
    {
        _dictionary.TryAdd(fileName, percentage);
        _dictionary[fileName] = percentage;
    }

    public void RemovePercentage(string fileName) => _dictionary.TryRemove(fileName, out _);
}

public readonly record struct CompressionReport(int CompressedVideosCount, int VideosCount, IDictionary<string, double> Percentages, string CurrentDirectory);