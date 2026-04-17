using Consul;
using Microsoft.AspNetCore.Http;
using Ocelot.Logging;
using Ocelot.Provider.Consul;
using Ocelot.Provider.Consul.Interfaces;

namespace SyZero.ApiGateway
{
    public class SyZeroConsulServiceBuilder : DefaultConsulServiceBuilder
    {
        public SyZeroConsulServiceBuilder(
            IHttpContextAccessor contextAccessor,
            IConsulClientFactory clientFactory,
            IOcelotLoggerFactory loggerFactory)
            : base(contextAccessor, clientFactory, loggerFactory)
        {
        }

        protected override string GetDownstreamHost(ServiceEntry entry, Node node)
        {
            return entry.Service.Address;
        }
    }
}
