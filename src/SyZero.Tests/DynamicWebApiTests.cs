using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.Extensions.DependencyInjection;
using SyZero.Application.Attributes;
using SyZero.Application.Service;
using SyZero.Client;
using SyZero.DynamicWebApi;
using SyZero.DynamicWebApi.Attributes;
using Xunit;

namespace SyZero.Tests;

public class DynamicWebApiTests
{
    [Fact]
    public void AddDynamicWebApi_AllowsMissingConfiguration_AndAvoidsDuplicateFeatureProviders()
    {
        var previousConfiguration = AppConfig.Configuration;
        AppConfig.Configuration = null!;

        try
        {
            var services = new ServiceCollection();
            services.AddControllers();

            services.AddDynamicWebApi(configuration: null);
            services.AddDynamicWebApi(configuration: null);

            using var provider = services.BuildServiceProvider();
            var partManager = provider.GetRequiredService<ApplicationPartManager>();

            Assert.Single(partManager.FeatureProviders.OfType<DynamicWebApiControllerFeatureProvider>());
        }
        finally
        {
            AppConfig.Configuration = previousConfiguration;
        }
    }

    [Fact]
    public void ControllerFeatureProvider_HonorsDynamicApiAndExclusionAttributes()
    {
        DynamicWebApiControllerFeatureProvider.ClearCache();

        var provider = new TestControllerFeatureProvider();

        Assert.True(provider.Check(typeof(ExposedAppService)));
        Assert.False(provider.Check(typeof(MissingAttributeAppService)));
        Assert.False(provider.Check(typeof(NonWebApiAppService)));
        Assert.False(provider.Check(typeof(ExcludedDynamicApiAppService)));
        Assert.False(provider.Check(typeof(FallbackDynamicApiAppService)));
    }

    [Fact]
    public void Convention_ConfiguresGeneratedSelectors_HttpVerbs_AndBodyBinding()
    {
        DynamicWebApiConvention.ClearCache();

        var controller = CreateControllerModel(typeof(GeneratedAppService), nameof(GeneratedAppService));
        var createAction = CreateActionModel(typeof(GeneratedAppService).GetMethod(nameof(GeneratedAppService.CreateWidgetAsync))!);
        var getAction = CreateActionModel(typeof(GeneratedAppService).GetMethod(nameof(GeneratedAppService.GetWidgetAsync))!);
        var hiddenAction = CreateActionModel(typeof(GeneratedAppService).GetMethod(nameof(GeneratedAppService.HiddenAsync))!);

        controller.Actions.Add(createAction);
        controller.Actions.Add(getAction);
        controller.Actions.Add(hiddenAction);

        var application = new ApplicationModel();
        application.Controllers.Add(controller);

        var convention = new DynamicWebApiConvention(new DynamicWebApiOptions
        {
            DefaultApiPrefix = "api",
            DefaultAreaName = "fallback"
        });

        convention.Apply(application);

        Assert.Equal("inventory", controller.RouteValues["area"]);
        Assert.Equal("api/inventory/generated", controller.Selectors.Single().AttributeRouteModel!.Template);

        var createSelector = Assert.Single(createAction.Selectors);
        var createConstraint = Assert.Single(createSelector.ActionConstraints.OfType<HttpMethodActionConstraint>());
        Assert.Equal("createwidget", createSelector.AttributeRouteModel!.Template);
        Assert.Equal("POST", Assert.Single(createConstraint.HttpMethods));
        Assert.Equal("Body", createAction.Parameters.Single().BindingInfo!.BindingSource!.Id);

        var getSelector = Assert.Single(getAction.Selectors);
        var getConstraint = Assert.Single(getSelector.ActionConstraints.OfType<HttpMethodActionConstraint>());
        Assert.Equal("getwidget", getSelector.AttributeRouteModel!.Template);
        Assert.Equal("GET", Assert.Single(getConstraint.HttpMethods));
        Assert.Null(getAction.Parameters.Single().BindingInfo);

        Assert.False(hiddenAction.ApiExplorer.IsVisible ?? true);
    }

    [Fact]
    public void Convention_UsesDefaultArea_WhenDynamicApiAttributeIsMissing()
    {
        DynamicWebApiConvention.ClearCache();

        var controller = CreateControllerModel(typeof(AttributeLessDynamicAppService), "AttributeLessDynamicApp");
        controller.Actions.Add(CreateActionModel(typeof(AttributeLessDynamicAppService).GetMethod(nameof(AttributeLessDynamicAppService.PingAsync))!));

        var application = new ApplicationModel();
        application.Controllers.Add(controller);

        var convention = new DynamicWebApiConvention(new DynamicWebApiOptions
        {
            DefaultAreaName = "default-area",
            DefaultApiPrefix = "api"
        });

        var exception = Record.Exception(() => convention.Apply(application));

        Assert.Null(exception);
        Assert.Equal("default-area", controller.RouteValues["area"]);
        Assert.Equal("api/default-area/attributelessdynamicapp", controller.Selectors.Single().AttributeRouteModel!.Template);
    }

    private static ControllerModel CreateControllerModel(Type controllerType, string controllerName)
    {
        return new ControllerModel(controllerType.GetTypeInfo(), Array.Empty<object>())
        {
            ControllerName = controllerName
        };
    }

    private static ActionModel CreateActionModel(MethodInfo methodInfo)
    {
        var action = new ActionModel(methodInfo, Array.Empty<object>())
        {
            ActionName = methodInfo.Name
        };

        foreach (var parameter in methodInfo.GetParameters())
        {
            action.Parameters.Add(new ParameterModel(parameter, Array.Empty<object>()));
        }

        return action;
    }

    private sealed class TestControllerFeatureProvider : DynamicWebApiControllerFeatureProvider
    {
        public bool Check(Type type)
        {
            return IsController(type.GetTypeInfo());
        }
    }
}

[DynamicApi]
public interface IExposedAppService : IDynamicApi
{
}

public class ExposedAppService : IExposedAppService
{
}

public class MissingAttributeAppService : IDynamicApi
{
}

[DynamicApi]
public interface INonWebApiAppService : IDynamicApi
{
}

[NonWebApiService]
public class NonWebApiAppService : INonWebApiAppService
{
}

[DynamicApi]
public interface IExcludedDynamicApiAppService : IDynamicApi
{
}

[NonDynamicApi]
public class ExcludedDynamicApiAppService : IExcludedDynamicApiAppService
{
}

[DynamicApi]
public interface IFallbackDynamicApiAppService : IDynamicApi
{
}

public class FallbackDynamicApiAppService : IFallbackDynamicApiAppService, IFallback
{
}

[DynamicApi(Module = "inventory")]
public interface IGeneratedAppService : IDynamicApi
{
    Task<string> CreateWidgetAsync(WidgetInput input);

    Task<string> GetWidgetAsync(WidgetInput input);

    [NonWebApiMethod]
    Task<string> HiddenAsync(WidgetInput input);
}

public class GeneratedAppService : IGeneratedAppService
{
    public Task<string> CreateWidgetAsync(WidgetInput input) => Task.FromResult(input.Name);

    public Task<string> GetWidgetAsync(WidgetInput input) => Task.FromResult(input.Name);

    public Task<string> HiddenAsync(WidgetInput input) => Task.FromResult(input.Name);
}

public class AttributeLessDynamicAppService : IDynamicApi
{
    public Task<string> PingAsync(WidgetInput input) => Task.FromResult(input.Name);
}

public class WidgetInput
{
    public string Name { get; set; } = string.Empty;
}
