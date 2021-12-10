using System;

namespace VideoCompresser
{
    public class StopWatch
    {
        DateTime _startTime;
        DateTime _endTime;

        public void StartRecording() => _startTime = DateTime.Now;
        public void StopRecording() => _endTime = DateTime.Now;
        public TimeSpan RecordedTime => _endTime - _startTime;
    }
}
