namespace Glazecs.Modules.TTS.Abstractions.Interfaces
{
    public interface ITTSService<in TOptions>
    {
        string? TranscribeAsync(string filePath, TOptions options, CancellationToken cancellationToken = default);
        string? TranscribeAsync(Stream stream, TOptions options, CancellationToken cancellationToken = default);
        IEnumerable<string?> TranscribeAsync(IEnumerable<string> filePaths, TOptions options, CancellationToken cancellationToken = default);
        IEnumerable<string?> TranscribeAsync(IEnumerable<Func<Stream>> streamFactories, TOptions options, CancellationToken cancellationToken = default);
    }
}
