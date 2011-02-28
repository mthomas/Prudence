namespace Prudence.Configuration
{
    public class ForwarderConfiguration
    {
        /// <summary>
        ///   Tracks what segements of what log files have been forwarded
        /// </summary>
        public string ForwardLogPath { get; set; }

        /// <summary>
        ///   The destination where log files should be forwarded
        /// </summary>
        public string TargetPath { get; set; }

        /// <summary>
        /// </summary>
        public string[] PathsToWatch { get; set; }

        public int PollPeriodMiliseconds { get; set; }
    }
}