using Glazecs.Modules.ASR.Abstractions.Interfaces;

namespace Glazecs.Modules.ASR.Abstractions.Models
{
    public record SpeechRecognitionResult(string Text, TimeSpan Start, TimeSpan End, bool IsFinal, float Confidence, IReadOnlyList<ISpeechRecognitionToken> Tokens) : ISpeechRecognitionResult;
}
