using System;

namespace VideoCompresser
{
    public struct Video
    {
        public static VideoFactory Factory = new VideoFactory();

        public Video(string path, long numberOfFrames, double durationInSeconds, double frameRate)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            NumberOfFrames = numberOfFrames;
            DurationInSeconds = durationInSeconds;
            FrameRate = frameRate;
        }

        public string Path { get; }
        public long NumberOfFrames { get; }
        public double DurationInSeconds { get; }
        public double FrameRate { get; }
        public override string ToString() => $"{System.IO.Path.GetFileName(Path)}: {NumberOfFrames}";
    }

    public class VideoFactory
    {
        public Video Create(string videoPath)
        {
            try
            {
                double durationInSeconds = GetVideoDuration(videoPath).TotalSeconds;
                double frameRate = GetFrameRate(videoPath);
                long numberOfFrames = (long)Math.Round(durationInSeconds * frameRate, 0);

                return new Video(videoPath, numberOfFrames, durationInSeconds, frameRate);
            }
            catch (Exception) { return new Video(); }
        }
        static TimeSpan GetVideoDuration(string videoPath)
        {
            ConsoleCommand command = new ConsoleCommand()
            {
                Command = Program.FFPROBE_PATH,
                Args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 -sexagesimal \"{videoPath}\""
            };
            string result = Program.ExecuteCommandSync(command);
            return TimeSpan.Parse(result);
        }
        static double GetFrameRate(string videoPath)
        {
            ConsoleCommand command = new ConsoleCommand()
            {
                Command = Program.FFPROBE_PATH,
                Args = $"-v error -select_streams v:0 -show_entries stream=avg_frame_rate -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\""
            };
            string result = Program.ExecuteCommandSync(command);
            return ConvertFractionToDecimal(result.Trim());
        }
        static double ConvertFractionToDecimal(string v)
        {
            string[] numbers = v.Split('/');
            int number1 = int.Parse(numbers[0]);
            int number2 = int.Parse(numbers[1]);
            return number1 / (number2 == 0 ? 1 : number2);
        }
    }
}
