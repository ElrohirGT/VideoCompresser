using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VideoCompresser
{
    public static class VideoInfoGatherer
    {
        internal static Video[] GetAllVideosInfo(IEnumerable<string> videoPaths)
        {
            using BlockingCollection<Video> concurrentVideos = new BlockingCollection<Video>();
            List<Task> tasks = new List<Task>();
            foreach (var videoPath in videoPaths)
            {
                Task task = Task.Factory.StartNew(() => concurrentVideos.Add(Video.Factory.Create(videoPath)));
                tasks.Add(task);
            }
            Task.WaitAll(tasks.ToArray());
            return concurrentVideos.ToArray();
        }
    }
}
