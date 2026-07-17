using Glazecs.Modules.ASR.Abstractions.Interfaces;

namespace Glazecs.Modules.ASR.Abstractions.Models
{
    public record SpeechRecognitionToken(string? Text, TimeSpan Start, TimeSpan End, float Confidence) : ISpeechRecognitionToken;
}
