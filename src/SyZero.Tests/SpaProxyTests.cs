using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SyZero.AspNetCore.SpaProxy;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SyZero.Tests;

public class SpaProxyTests
{
    [Fact]
    public void ServerInfo_BuildRedirectUrl_PreservesBasePathAndQuery()
    {
        var serverInfo = new SpaProxyServerInfo
        {
            ServerUrl = "http://localhost:5173",
            RedirectUrl = "http://localhost:5173/app"
        };

        var redirectUrl = serverInfo.BuildRedirectUrl("/orders", new QueryString("?page=2"));

        Assert.Equal("http://localhost:5173/app/orders?page=2", redirectUrl);
    }

    [Fact]
    public void LaunchManager_ServerInfo_WhenConfigIsInvalid_ReturnsNull()
    {
        using var tempDirectory = TempDirectory.Create();
        File.WriteAllText(Path.Combine(tempDirectory.Path, "spa.proxy.json"), "{ invalid json");

        var manager = CreateLaunchManager(
            tempDirectory.Path,
            new ProbeStub(_ => false),
            new FakeProcessFactory(new FakeProcess()));

        Assert.Null(manager.ServerInfo);
    }

    [Fact]
    public async Task LaunchManager_EnsureServerStartedAsync_WaitsUntilServerBecomesReachable()
    {
        using var tempDirectory = TempDirectory.Create();
        WriteSpaProxyConfig(tempDirectory.Path, maxTimeoutInSeconds: 2);

        var probe = new ProbeSequenceStub(false, false, true);
        var process = new FakeProcess();
        var manager = CreateLaunchManager(tempDirectory.Path, probe, new FakeProcessFactory(process));

        await manager.EnsureServerStartedAsync(CancellationToken.None);

        Assert.Equal(1, process.StartCount);
        Assert.True(probe.CallCount >= 3);
    }

    [Fact]
    public async Task LaunchManager_IsServerRunningAsync_PropagatesCancellation()
    {
        using var tempDirectory = TempDirectory.Create();
        WriteSpaProxyConfig(tempDirectory.Path);

        var manager = CreateLaunchManager(
            tempDirectory.Path,
            new ProbeStub(cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return false;
            }),
            new FakeProcessFactory(new FakeProcess()));

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.IsServerRunningAsync(cancellationTokenSource.Token));
    }

    [Fact]
    public async Task Middleware_WhenServerIsStarting_ReturnsEncodedLaunchPage()
    {
        using var tempDirectory = TempDirectory.Create();
        WriteSpaProxyConfig(
            tempDirectory.Path,
            launchCommand: "npm run dev -- --host \"0.0.0.0\" <unsafe>",
            serverUrl: "http://localhost:5173/?q=<unsafe>",
            maxTimeoutInSeconds: 0);

        var manager = CreateLaunchManager(
            tempDirectory.Path,
            new ProbeSequenceStub(false, false, false),
            new FakeProcessFactory(new FakeProcess()));

        var middleware = new SpaProxyMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/";
        context.Request.Headers.Accept = "text/html";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(
            context,
            CreateEnvironment(tempDirectory.Path),
            manager,
            CreateLogger<SpaProxyMiddleware>());

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("2", context.Response.Headers.RetryAfter.ToString());
        Assert.Contains("npm run dev -- --host &quot;0.0.0.0&quot; &lt;unsafe&gt;", body);
        Assert.Contains("http://localhost:5173/?q=&lt;unsafe&gt;", body);
    }

    [Fact]
    public async Task BuildTargets_WriteValidJson_WhenLaunchCommandContainsQuotes()
    {
        using var tempDirectory = TempDirectory.Create();
        Directory.CreateDirectory(Path.Combine(tempDirectory.Path, "client"));

        var srcDirectory = GetRepositorySrcDirectory();
        var propsPath = Path.Combine(srcDirectory, "SyZero.Core", "SyZero.AspNetCore.SpaProxy", "buildTransitive", "SyZero.AspNetCore.SpaProxy.props");
        var targetsPath = Path.Combine(srcDirectory, "SyZero.Core", "SyZero.AspNetCore.SpaProxy", "buildTransitive", "SyZero.AspNetCore.SpaProxy.targets");
        var projectPath = Path.Combine(tempDirectory.Path, "SpaProxyTargetTests.csproj");

        File.WriteAllText(projectPath, $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="{{propsPath}}" />
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <SpaRoot>../client/</SpaRoot>
    <SpaProxyServerUrl>http://localhost:5173</SpaProxyServerUrl>
    <SpaProxyRedirectUrl>http://localhost:4173/app</SpaProxyRedirectUrl>
    <SpaProxyLaunchCommand>npm run dev -- --host &quot;0.0.0.0&quot;</SpaProxyLaunchCommand>
  </PropertyGroup>
  <Import Project="{{targetsPath}}" />
</Project>
""");

        await RunProcessAsync(
            "dotnet",
            $"msbuild \"{projectPath}\" /t:SyZeroWriteSpaConfigurationToDisk /nologo /verbosity:minimal");

        var configPath = Directory.GetFiles(tempDirectory.Path, "spa.proxy.json", SearchOption.AllDirectories).Single();
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
        var server = document.RootElement.GetProperty("SpaProxyServer");

        Assert.Equal("npm run dev -- --host \"0.0.0.0\"", server.GetProperty("LaunchCommand").GetString());
        Assert.Equal("http://localhost:4173/app", server.GetProperty("RedirectUrl").GetString());
        Assert.Equal(
            NormalizeDirectoryPath(Path.GetFullPath(Path.Combine(tempDirectory.Path, "../client"))),
            NormalizeDirectoryPath(server.GetProperty("WorkingDirectory").GetString()));
    }

    private static SpaProxyLaunchManager CreateLaunchManager(
        string contentRootPath,
        ISpaProxyServerProbe probe,
        ISpaProxyProcessFactory processFactory)
    {
        return new SpaProxyLaunchManager(
            CreateEnvironment(contentRootPath),
            CreateLogger<SpaProxyLaunchManager>(),
            probe,
            processFactory);
    }

    private static TestWebHostEnvironment CreateEnvironment(string contentRootPath)
    {
        return new TestWebHostEnvironment
        {
            ApplicationName = "SyZero.Tests",
            ContentRootPath = contentRootPath,
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath),
            EnvironmentName = Environments.Development,
            WebRootPath = contentRootPath,
            WebRootFileProvider = new PhysicalFileProvider(contentRootPath)
        };
    }

    private static ILogger<T> CreateLogger<T>()
    {
        return LoggerFactory.Create(_ => { }).CreateLogger<T>();
    }

    private static string GetRepositorySrcDirectory([CallerFilePath] string sourceFilePath = "")
    {
        var directory = new FileInfo(sourceFilePath).Directory;
        while (directory != null)
        {
            var srcDirectory = Path.Combine(directory.FullName, "src");
            if (File.Exists(Path.Combine(directory.FullName, "SyZero.slnx")) && Directory.Exists(srcDirectory))
            {
                return srcDirectory;
            }

            if (File.Exists(Path.Combine(srcDirectory, "SyZero.sln")))
            {
                return srcDirectory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository src directory.");
    }

    private static async Task RunProcessAsync(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            return;
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;
        throw new Xunit.Sdk.XunitException(
            $"Process '{fileName} {arguments}' failed with exit code {process.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{standardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{standardError}");
    }

    private static string NormalizeDirectoryPath(string? path)
    {
        return (path ?? string.Empty).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
    }

    private static void WriteSpaProxyConfig(
        string contentRootPath,
        string serverUrl = "http://localhost:5173",
        string redirectUrl = "http://localhost:5173",
        string launchCommand = "npm run dev",
        int maxTimeoutInSeconds = 120,
        bool keepRunning = false)
    {
        File.WriteAllText(Path.Combine(contentRootPath, "spa.proxy.json"), $$"""
{
  "SpaProxyServer": {
    "ServerUrl": "{{serverUrl}}",
    "RedirectUrl": "{{redirectUrl}}",
    "LaunchCommand": "{{launchCommand.Replace("\\", "\\\\").Replace("\"", "\\\"")}}",
    "WorkingDirectory": "{{contentRootPath.Replace("\\", "\\\\")}}",
    "MaxTimeoutInSeconds": {{maxTimeoutInSeconds}},
    "KeepRunning": {{keepRunning.ToString().ToLowerInvariant()}}
  }
}
""");
    }

    private sealed class ProbeStub : ISpaProxyServerProbe
    {
        private readonly Func<CancellationToken, bool> _resultFactory;

        public ProbeStub(Func<CancellationToken, bool> resultFactory)
        {
            _resultFactory = resultFactory;
        }

        public int CallCount { get; private set; }

        public Task<bool> CanReachServerAsync(string serverUrl, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_resultFactory(cancellationToken));
        }
    }

    private sealed class ProbeSequenceStub : ISpaProxyServerProbe
    {
        private readonly Queue<bool> _results;
        private bool _lastResult;

        public ProbeSequenceStub(params bool[] results)
        {
            _results = new Queue<bool>(results);
            _lastResult = results.LastOrDefault();
        }

        public int CallCount { get; private set; }

        public Task<bool> CanReachServerAsync(string serverUrl, CancellationToken cancellationToken)
        {
            CallCount++;
            if (_results.Count > 0)
            {
                _lastResult = _results.Dequeue();
            }

            return Task.FromResult(_lastResult);
        }
    }

    private sealed class FakeProcessFactory : ISpaProxyProcessFactory
    {
        private readonly FakeProcess _process;

        public FakeProcessFactory(FakeProcess process)
        {
            _process = process;
        }

        public ISpaProxyProcess Create(SpaProxyServerInfo serverInfo)
        {
            return _process;
        }
    }

    private sealed class FakeProcess : ISpaProxyProcess
    {
        public event EventHandler? Exited;

        public int ExitCode { get; private set; }

        public bool HasExited { get; private set; }

        public int Id { get; } = 1234;

        public int StartCount { get; private set; }

        public void Kill(bool entireProcessTree)
        {
            Exit(0);
        }

        public void Start()
        {
            StartCount++;
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }

        private void Exit(int exitCode)
        {
            ExitCode = exitCode;
            HasExited = true;
            Exited?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = null!;

        public string WebRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = string.Empty;

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "syzero-spaproxy-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
