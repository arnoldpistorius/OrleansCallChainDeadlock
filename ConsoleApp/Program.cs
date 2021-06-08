using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Concurrency;
using Orleans.Hosting;
using Orleans.Streams;

namespace ConsoleApp
{
    class Program
    {
        static Task Main(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseOrleans(siloBuilder =>
                {
                    siloBuilder
                        .UseLocalhostClustering()
                        .ConfigureApplicationParts(x => x.AddApplicationPart(Assembly.GetExecutingAssembly()).WithCodeGeneration());
                })
                .ConfigureWebHostDefaults(configure: config => config.UseStartup<Startup>())
                .RunConsoleAsync();
    }

    class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHostedService<Service>();
        }

        public void Configure(IApplicationBuilder app)
        {

        }

        class Service : IHostedService
        {
            readonly IGrainFactory _grainFactory;

            public Service(IGrainFactory grainFactory)
            {
                _grainFactory = grainFactory;
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                var x = _grainFactory.GetGrain<IA>(0);
                await x.Publish();

            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }

    public interface IA : IGrainWithIntegerKey
    {
        Task Publish();
        Task<Poco> GetDetails();
        Task Operation(IA obj);
    }

    public interface IB : IGrainWithIntegerKey
    {
        Task OnPoco(Poco poco, StreamSequenceToken token);
    }

    public class A : Grain, IA
    {
        public async Task Publish()
        {
            var poco = new Poco {Id = (int) this.GetPrimaryKeyLong()};
            await GrainFactory.GetGrain<IB>(0).OnPoco(poco, null);
        }

        public Task<Poco> GetDetails() => Task.FromResult(new Poco {Id = (int) this.GetPrimaryKeyLong()});

        public async Task Operation(IA obj)
        {
            // Will deadlock
            Console.WriteLine($"{nameof(A)}(ID={this.GetPrimaryKeyLong()}): Before GetDetails");
            var details = await obj.GetDetails();
            Console.WriteLine($"{nameof(A)}(ID={this.GetPrimaryKeyLong()}): After GetDetails");
        }
    }

    public class B : Grain, IB
    {
        public async Task OnPoco(Poco poco, StreamSequenceToken token)
        {
            var y = GrainFactory.GetGrain<IA>(42);
            var x = GrainFactory.GetGrain<IA>(poco.Id);

            // Will not deadlock
            Console.WriteLine($"{nameof(B)}(ID={this.GetPrimaryKeyLong()}): Before GetDetails");
            var details = await x.GetDetails();
            Console.WriteLine($"{nameof(B)}(ID={this.GetPrimaryKeyLong()}): After GetDetails");
            await y.Operation(x);
        }
    }

    [Immutable, Serializable]
    public class Poco
    {
        public int Id { get; set; }
    }
}