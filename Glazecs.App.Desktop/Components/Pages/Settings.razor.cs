using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Glazecs.App.Desktop.Components.Pages
{
    public partial class Settings(ILogger<Settings>? logger = null) : ComponentBase
    {
        private readonly ILogger<Settings>? _logger = logger;
    }
}
