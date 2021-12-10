using System;

namespace VideoCompresser
{

    public readonly record struct Video(string? Path, long TotalFrames, double TotalDurationInSeconds, double FrameRate)
    {
        public override string ToString() => $"{System.IO.Path.GetFileName(Path)}: {TotalFrames}";
    }

    //public record Video
    //{
    //    public string? Path { get; }
    //    public long NumberOfFrames { get; }
    //    public double DurationInSeconds { get; }
    //    public double FrameRate { get; }
    //    public override string ToString() => $"{System.IO.Path.GetFileName(Path)}: {NumberOfFrames}";
    //}
}
