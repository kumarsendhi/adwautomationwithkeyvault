using autoscalingADW.shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace autoscalingADW
{
    public static class scaleADW
    {
        [FunctionName("scaleADW")]
        public static async Task<IActionResult> scale(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "scaleADW")] HttpRequestMessage req,
            ILogger log, ExecutionContext context)
        {
            dynamic informationString = null;
            dynamic scalingResponseObject = null;
            dynamic UserActivityObject = null;
            string forceScaling = GetValueFromUrl(req);
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
                dynamic CurrentServiceObject = JsonConvert.DeserializeObject(req.Content.ReadAsStringAsync().Result);
                string currentDwu = CurrentServiceObject.currentServiceObjectiveName.ToString();

                // Create a DataWarehouseManagementClient
                var dwClient = await DwClientFactory.Create(resourceId.ToString(), context);
                // Get database information
                var dbInfo = dwClient.GetDatabase();
                dynamic dbInfoObject = JsonConvert.DeserializeObject(dbInfo);
                var currentStatus = dbInfoObject.properties.status.ToString();
                var skusize = dbInfoObject.sku.name.ToString();
                //logEntity.DwuBefore = currentDwu;
                log.LogInformation($"Current Status is {currentStatus}");

                //if (currentStatus != "Paused")
                //{
                //    return req.CreateResponse(HttpStatusCode.BadRequest, "Bad Operation");
                //}

                if (skusize != currentDwu)
                {

                    if (currentStatus == "Online")
                    {
                        if (forceScaling == "No")
                        {
                            UserActivityObject = GetUserActivity(log, dwClient);

                            if (UserActivityObject.properties.activeQueriesCount == 0)
                            {
                               scalingResponseObject = ScaleDataWarehouse(dwClient, currentDwu, dwLocation);
                            }
                            else
                            {
                                informationString = currentStatus == "Online" ? JsonConvert.DeserializeObject("{\"operation\":\"" + currentStatus + "\",\"activeQueriesCount\":\"" + UserActivityObject.properties.activeQueriesCount + "}") : JsonConvert.DeserializeObject("{\"operation\":\"" + currentStatus + "\"}");
                            }
                        }
                        else
                        {
                            scalingResponseObject = ScaleDataWarehouse(dwClient, currentDwu, dwLocation);
                        }
                       

                    }
                    else if (currentStatus == "Scaling")
                    {
                        informationString = JsonConvert.DeserializeObject("{\"operation\":\"Scaling\"}");

                    }
                    else
                    {
                        informationString = currentStatus == "Online" ? JsonConvert.DeserializeObject("{\"operation\":\"" + currentStatus + "\",\"activeQueriesCount\":\"" + UserActivityObject.properties.activeQueriesCount + "}") : JsonConvert.DeserializeObject("{\"operation\":\"" + currentStatus + "\"}");
                    }
                }
                else
                {
                    informationString = currentStatus == "Online" ? JsonConvert.DeserializeObject("{\"operation\":\"" + currentStatus + "\",\"comment\":\"Same Sku Size\"}") : JsonConvert.DeserializeObject("{\"operation\":\"" + currentStatus + "\"}");
                }

                // return new OkObjectResult(scalingResponseObject);




                return scalingResponseObject != null
                ? (ActionResult)new OkObjectResult(scalingResponseObject)
                : (ActionResult)new OkObjectResult(informationString);


            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult($"Bad Operation {ex}");
            }


            //return name != null
            //    ? (ActionResult)new OkObjectResult($"Hello, {name}")
            //    : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
        }

        private static string GetValueFromUrl(HttpRequestMessage req)
        {
            string forceScaling;
            if (req.RequestUri.Query != "")
            {
                forceScaling = req.RequestUri.ParseQueryString().GetValues("forcescale").GetValue(0).ToString();
            }
            else
            {
                forceScaling = "No";
            }
           
            return forceScaling;
        }

        private static dynamic GetUserActivity(ILogger log, DwManagementClient dwClient)
        {
            dynamic UserActivityObject;
            HttpResponseMessage resUA = dwClient.UserActivities();

            UserActivityObject = JsonConvert.DeserializeObject(resUA.Content.ReadAsStringAsync().Result);

            log.LogInformation($"Current Active Queries Count , {UserActivityObject.properties.activeQueriesCount}");
            return UserActivityObject;
        }

        private static Object ScaleDataWarehouse(dynamic dwClient,string currentDwu, string dwLocation)
        {
            HttpResponseMessage res = dwClient.ScaleWarehouse(currentDwu, dwLocation);

            Object scalingResponseObject = JsonConvert.DeserializeObject(res.Content.ReadAsStringAsync().Result);
            if (res.StatusCode != HttpStatusCode.Accepted)
            {
                return new NotFoundObjectResult("Internal Server Error");
            }
            return scalingResponseObject;
        }
    }
}
