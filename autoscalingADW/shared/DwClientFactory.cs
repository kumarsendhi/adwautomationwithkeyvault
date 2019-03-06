using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

using Newtonsoft.Json;
using System.Net.Http;
using System.Globalization;
using Microsoft.Azure;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;

namespace autoscalingADW.shared
{
    public class DwClientFactory
    {
           public static string ActiveDirectoryEndpoint { get; set; } = "https://login.windows.net/";
        public static string ResourceManagerEndpoint { get; set; } = "https://management.azure.com/";
        public static string WindowsManagementUri { get; set; } = "https://management.core.windows.net/";

        //Leave the following Ids and keys unassigned so that they won't be checked in Git. They are assigned in Azure portal.
        public static string SubscriptionId { get; set; } 
        public static string TenantId { get; set; } 
        public static string ClientId { get; set; } 
        public static string ClientKey { get; set; } 

        public static async Task<DwManagementClient> Create(string resourceId, ExecutionContext context)
        {
            
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
                var SubscriptionIdSecret = config["SubscriptionId"];
                 SubscriptionId = (await kvClient.GetSecretAsync(kvURL, SubscriptionIdSecret)).Value;
                
                var TenantIdSecret = config["TenantId"];
                TenantId = (await kvClient.GetSecretAsync(kvURL, TenantIdSecret)).Value;
                ClientId = config["ClientId"];
                var ClientKeySecret = config["ClientKey"];
                var ClientKey = (await kvClient.GetSecretAsync(kvURL, ClientKeySecret)).Value;

                var httpClient = new HttpClient();
                var authenticationContext = new AuthenticationContext(ActiveDirectoryEndpoint + TenantId);


                var credential = new ClientCredential(clientId: ClientId, clientSecret: ClientKey);
                var result = authenticationContext.AcquireTokenAsync(resource: WindowsManagementUri, clientCredential: credential).Result;

                if (result == null) throw new InvalidOperationException("Failed to obtain the token!");

                var token = result.AccessToken;

                var aadTokenCredentials = new TokenCloudCredentials(SubscriptionId, token);

                var client = new DwManagementClient(aadTokenCredentials, resourceId);
                return client;
            }
            catch(Exception ex)
            {
                throw new InvalidOperationException("Failed to obtain the token!");
            }
        }
    }
}
