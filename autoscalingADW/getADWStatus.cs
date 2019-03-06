using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using autoscalingADW.shared;
using Microsoft.Extensions.Configuration;
using System.Threading;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;

namespace autoscalingADW
{
    public static class getADWStatus
    {
        
        [FunctionName("getADWStatus")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get",  Route = "getadwstatus")] HttpRequest req,
            ILogger log, Microsoft.Azure.WebJobs.ExecutionContext context)
        {
            string currentStatus;
            dynamic dbInfoObject;
        log.LogInformation("autoscaleup of adw initiated");

            var config = new ConfigurationBuilder()
            .SetBasePath(context.FunctionAppDirectory)
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

            var azureServiceTokenProvider = new AzureServiceTokenProvider();

            var kvClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
            var kvURL = config["KeyVaultURL"];

            try
            {
                var dwLocation = config["SqlDwLocation"];
                //var tableName = config["DwScaleLogsTable"];
                var dwuConfigFile = config["DwuConfigFile"];
                var resourceIdSecret = config["ResourceId"];
                var resourceId = (await kvClient.GetSecretAsync(kvURL, resourceIdSecret)).Value;
                //string startupPath = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
                //string dwuConfigFilePath = startupPath + "\\" + dwuConfigFile;
                //var dwuConfigManager = new DwuConfigManager(dwuConfigFilePath);

                //var taskResult = await Task.Factory.StartNew<int>(() =>
                //{
                //    Thread.Sleep(300000);
                //    return 0;
                //});

                // Create a DataWarehouseManagementClient
                var dwClient = await DwClientFactory.Create(resourceId.ToString(), context);
                do
                {
                    
                    // Get database information
                    var dbInfo = dwClient.GetDatabase();

                     dbInfoObject = JsonConvert.DeserializeObject(dbInfo);
                     currentStatus = dbInfoObject.properties.status.ToString();
                    //logEntity.DwuBefore = currentDwu;
                    log.LogInformation($"Current DWU is {currentStatus}");
                } while (currentStatus != "Online");
                


                return new OkObjectResult(dbInfoObject);

            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult($"Bad Operation {ex}");
            }
        }
    }
}
