namespace SyZero.AspNetCore.SpaProxy
{
    internal interface ISpaProxyProcessFactory
    {
        ISpaProxyProcess Create(SpaProxyServerInfo serverInfo);
    }
}
