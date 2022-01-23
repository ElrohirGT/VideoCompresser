using System.Collections.Generic;
using System.Threading;

using FFMpegCore;

namespace VideoCompresser
{
    public static class VideoCompresser
    {
        private static readonly IEnumerable<string> _validExtensions = new string[] { ".mp4", ".webm" };

        static VideoCompresser()
        {
            GlobalFFOptions.Configure((opt) =>
            {
                opt.BinaryFolder = "./ffmpeg 5.0/";
                opt.WorkingDirectory = "./";
            });
        }

        public static VideoCompression CompressAllVideos(string path, bool deleteFiles = true, int maxDegreeOfParalelism = 1, CancellationToken? softToken = null, CancellationToken? instantToken = null, int crf = 30)
        {
            return new VideoCompression
            {
                InitialPath = path,
                DeleteFiles = deleteFiles,
                SoftToken = softToken,
                InstantToken = instantToken,
                CRF = crf,
                MaxDegreeOfParalelism = maxDegreeOfParalelism,
                ValidExtensions = _validExtensions
            };
        }
    }
}