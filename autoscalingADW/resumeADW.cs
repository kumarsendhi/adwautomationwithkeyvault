using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using autoscalingADW.shared;
using System.Net.Http;
using System.Net;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;

namespace autoscalingADW
{
    public static class resumeADW
    {
        
        [FunctionName("resumeADW")]
        public static async Task<IActionResult> resume(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "resumeADW")] HttpRequestMessage req,
            ILogger log, ExecutionContext context)
        {
            
        log.LogInformation("resume the paused ADW");
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
                // var dwuConfigManager = new DwuConfigManager(dwuConfigFilePath);

                // Create a DataWarehouseManagementClient
                var dwClient = await DwClientFactory.Create(resourceId.ToString(), context);
                // Get database information
                var dbInfo = dwClient.GetDatabase();
                dynamic dbInfoObject = JsonConvert.DeserializeObject(dbInfo);
                var currentStatus = dbInfoObject.properties.status.ToString();
                //logEntity.DwuBefore = currentDwu;
                log.LogInformation($"Current Status is {currentStatus}");
                 
                if(currentStatus != "Paused")
                {
                    return new BadRequestObjectResult("Bad Operation");
                }

                HttpResponseMessage res = dwClient.Resume();
                dynamic resumeActivityObject = JsonConvert.DeserializeObject(res.Content.ReadAsStringAsync().Result);
                if (res.StatusCode != HttpStatusCode.Accepted)
                {
                    return new NotFoundObjectResult("Internal Server Error");
                }
                return new OkObjectResult(resumeActivityObject);


            }
            catch(Exception ex)
            {
                return new BadRequestObjectResult($"Bad Operation {ex}");
            }

        }
    }
}
