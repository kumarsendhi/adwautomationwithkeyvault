using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using autoscalingADW.shared;
using System.Net;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;

namespace autoscalingADW
{
    public static class getadwinfo
    {
        [FunctionName("getadwinformation")]
        public static async Task<IActionResult> getadwinformation(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "getadwinformation")] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
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
                log.LogInformation(resourceId);
                
                //string startupPath = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
                //string dwuConfigFilePath = startupPath + "\\" + dwuConfigFile;
                //var dwuConfigManager = new DwuConfigManager(dwuConfigFilePath);

                // Create a DataWarehouseManagementClient
                var dwClient = await DwClientFactory.Create(resourceId.ToString(),context);

                log.LogInformation(dwClient.ToString());
                // Get database information
                var dbInfo = dwClient.GetDatabase();
                dynamic dbInfoObject = JsonConvert.DeserializeObject(dbInfo);
                var currentDwu = dbInfoObject.properties.currentServiceObjectiveName.ToString();
                //logEntity.DwuBefore = currentDwu;
                log.LogInformation($"Current DWU is {currentDwu}");

                
                return new OkObjectResult(dbInfoObject);

            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult($"Bad Operation {ex}");
            }

            
            
        }
    }
}
