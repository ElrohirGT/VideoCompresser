using System;

namespace VideoCompresser
{

    public readonly record struct Video(string Path, long TotalFrames, TimeSpan Duration, double FrameRate)
    {
        public string FileNameWithoutExtension => System.IO.Path.GetFileNameWithoutExtension(Path);
        public string FileName => System.IO.Path.GetFileName(Path);
        public override string ToString() => $"{FileName}: {TotalFrames}";
    }
}
