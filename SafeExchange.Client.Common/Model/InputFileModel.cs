/// <summary>
/// InputFileModel
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System.IO;

    public abstract class InputFileModel
    {
        public abstract string ContentType { get; }

        public abstract string Name { get; }

        public abstract long Size { get; }

        public abstract Stream OpenReadStream(long maxAllowedSize = 512000, System.Threading.CancellationToken cancellationToken = default);
    }
}
