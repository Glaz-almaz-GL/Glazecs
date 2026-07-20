namespace Glazecs.Modules.ASR.Abstractions.Models
{
    public readonly record struct SpeechInitializeProgress(
         int Current,
         int Total,
         string Message = "")
    {
        public double Percent => Total > 0 ? (double)Current / Total * 100.0 : 0.0;
    }
}
