using System.Threading;
using System.Threading.Tasks;

namespace StartRide.App.Services;

public interface IClipboardService
{
	Task<bool> CopyTextAsync(string text, CancellationToken cancellationToken = default(CancellationToken));

	Task<string?> GetTextAsync(CancellationToken cancellationToken = default(CancellationToken));
}
