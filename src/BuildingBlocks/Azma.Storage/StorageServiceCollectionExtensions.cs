using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace Azma.Storage;

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddAzmaStorage(
        this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(StorageOptions.SectionName);
        services.Configure<StorageOptions>(section);
        var opt = section.Get<StorageOptions>() ?? new StorageOptions();

        services.AddSingleton<IMinioClient>(_ =>
        {
            var b = new MinioClient()
                .WithEndpoint(opt.Endpoint)
                .WithCredentials(opt.AccessKey, opt.SecretKey);
            if (opt.UseSsl) b = b.WithSSL();
            if (!string.IsNullOrWhiteSpace(opt.Region)) b = b.WithRegion(opt.Region);
            return b.Build();
        });

        services.AddSingleton<IObjectStorage, MinioObjectStorage>();
        return services;
    }
}
