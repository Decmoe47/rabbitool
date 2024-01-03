using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit.DependencyInjection.AspNetCoreTesting;

namespace Rabbitool.Test;

public class BaseTest
{
    public class Startup
    {
        public IHostBuilder CreateHostBuilder()
        {
            return MinimalApiHostBuilderFactory.GetHostBuilder<Program>()
                .ConfigureHostConfiguration(builder =>
                    builder.AddInMemoryCollection([KeyValuePair.Create(HostDefaults.EnvironmentKey, "Testing")]));
        }
    }
}