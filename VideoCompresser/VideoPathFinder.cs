using System.Collections.Generic;
using System.IO;

namespace VideoCompresser
{
    public partial class VideoCompresser
    {
        public static class VideoPathFinder
        {
            static readonly List<string> _validExtensions = new List<string>()
        {
            ".mp4", ".mkv",
            ".mov", ".m4v",
            ".webm"
        };
            public static IEnumerable<string> FindVideosPaths(string pathToSearchForVideos)
            {
                List<string> videosPaths = new List<string>(5);

                foreach (var filePath in Directory.EnumerateFiles(pathToSearchForVideos))
                    if (IsVideoAndNotHidden(filePath))
                        videosPaths.Add(filePath);

                return videosPaths;
            }
            static bool IsVideoAndNotHidden(string filePath)
            {
                string extension = Path.GetExtension(filePath);
                bool isHidden = File.GetAttributes(filePath).HasFlag(FileAttributes.Hidden);
                return _validExtensions.Contains(extension) && !isHidden;
            }
        }
    }
}
