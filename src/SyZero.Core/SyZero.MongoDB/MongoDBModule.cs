using Microsoft.Extensions.DependencyInjection;

namespace SyZero.MongoDB
{
    public static class MongoDBModule
    {
        public static IServiceCollection AddMongoDBModule(this IServiceCollection builder)
        {
            return builder.AddSyZeroMongoDB();
        }
    }
}
