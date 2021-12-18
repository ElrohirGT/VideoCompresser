using System;

namespace VideoCompresser
{
    public class StopWatch
    {
        private DateTime _startTime;
        private DateTime _endTime;

        public void StartRecording() => _startTime = DateTime.Now;

        public void StopRecording() => _endTime = DateTime.Now;

        public TimeSpan RecordedTime => _endTime - _startTime;
    }
}