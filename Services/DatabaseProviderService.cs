using NongTimeAI.Enums;

namespace NongTimeAI.Services;

public class DatabaseProviderService
{
    public DatabaseProvider Provider { get; }

    public DatabaseProviderService(DatabaseProvider provider)
    {
        Provider = provider;
    }
}
