using Glazecs.Modules.FMMS.Abstractions.Models;

namespace Glazecs.Modules.FMMS.Abstractions.Interfaces
{
    public interface IDirectoryScannerService
    {
        IAsyncEnumerable<ScannedDirectory> ScanDirectoryAsync(
            string rootPath,
            DirectoryScanningSettings settings,
            IProgress<double> progress,
            CancellationToken cancellationToken = default);
    }
}
