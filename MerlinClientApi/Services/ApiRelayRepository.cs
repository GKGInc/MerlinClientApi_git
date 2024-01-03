using Memex.Merlin.Client;
using MerlinClientApi.Classes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace MerlinClientApi.Services
{

    public class ApiRelayRepository
    {
        private readonly ILogger<MerlinClient> _logger;
        private readonly IOptions<MerlinClientOptions> _merlinClientOptions;
        private readonly IServiceProvider _serviceProvider;
        private static MerlinClientApiSDK merlinClientApiSDK;


        public ApiRelayRepository()
        {
            ////_logger.LogInformation("Starting [MerlinClienApiSDK]...");

            var httpClientFactory = _serviceProvider.GetService<IHttpClientFactory>();
            var client = new MerlinClient(httpClientFactory, _merlinClientOptions, _logger);
            merlinClientApiSDK = new MerlinClientApiSDK(client);
        }

        //public async Task TestMerlinClienApiSDK()
        //{
        //    _logger.LogInformation("Starting [TestMerlinClienApiSDK]...");

        //    var httpClientFactory = _serviceProvider.GetService<IHttpClientFactory>();

        //    var client = new MerlinClient(httpClientFactory, _merlinClientOptions, _logger);

        //    var sdkTester = new MerlinClientApiSDKTester(client);
        //    await sdkTester.ApiTestRoutine();

        //    //Console.ReadLine();

        //    await Task.CompletedTask;
        //}

    }
}