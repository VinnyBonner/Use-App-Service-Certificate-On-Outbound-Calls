using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Authentication;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Security.Cryptography.X509Certificates;

namespace UseCertificateRepro
{
    public static class Function1
    {
        // PreRequirements
        // Add Certificate to Azure App Services - https://learn.microsoft.com/en-us/azure/app-service/configure-ssl-certificate?tabs=apex%2Cportal

        // Resources 
        // https://github.com/projectkudu/kudu/wiki/Best-X509Certificate2-Practices#overview
        // https://paulstovell.com/x509certificate2/
        // https://learn.microsoft.com/en-us/azure/app-service/configure-ssl-certificate-in-code#load-certificate-in-windows-apps

        // Create static HttpClientHandler and set ClientCertificateOptions to manual and SSL to use Tls1.2
        private static readonly HttpClientHandler httpClientHandler = new HttpClientHandler()
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            SslProtocols = SslProtocols.Tls12
        };

        //Create static httpClient using the HttpClientHandler
        private static readonly HttpClient httpClient = new HttpClient(httpClientHandler);

        [FunctionName("Function1")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            
            try
            {
                HttpResponseMessage response = GetResponseUsingCert(log);
                
                string result = await response.Content.ReadAsStringAsync();

                log.LogInformation(result);

            }
            catch (Exception ex)
            {
                //Log and throw the error
                log.LogError(ex.ToString());
                throw;
            }
            
            return new OkObjectResult("Success!");
        }

        
        private static HttpResponseMessage GetResponseUsingCert(ILogger log)
        {
            // Set the thumbprint of the Certificate to use from the Certificate Store
            string certThumbprint = "<REPLACE_WITH_CERTIFICATE_THUMBPRINT>";
            bool validOnly = false;
            HttpResponseMessage response;
            
            try
            {
                // Get the certificate store by name (My in our case, which contains personal certificates)
                // and in the specific StoreLocation (In our care the current user)
                using (X509Store certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                {
                    // Opens the certStore in ReadOnly mode.
                    certStore.Open(OpenFlags.ReadOnly);
                    
                    // Gets the CertCollaction on all the valid certificates matching the certThumbprint.
                    X509Certificate2Collection certCollection = certStore.Certificates.Find(
                                                X509FindType.FindByThumbprint,
                                                certThumbprint,
                                                validOnly);
                    
                    // Get the first cert with the thumbprint
                    X509Certificate2 cert = certCollection.OfType<X509Certificate2>().FirstOrDefault();

                    // Check the cert is not null
                    if (cert is null)
                        throw new Exception($"Certificate with thumbprint {certThumbprint} was not found");


                    // Check the Certificate was loaded
                    log.LogInformation(cert.Thumbprint);

                    // Make outbound request with Cert and return the response for processing
                    response = MakeRequest(cert);

                    // Dispose of the Cert.
                    cert.Dispose();
                }
            }
            catch (Exception)
            {
                //throw the error
                throw;
            }
           
            // return response
            return response;
        }

        private static HttpResponseMessage MakeRequest(X509Certificate2 cert)
        {
            HttpResponseMessage response = null;

            try
            {
                // Add the cert to the httpClientHandler 
                httpClientHandler.ClientCertificates.Add(cert);

                // Make a GetRequest to bing.com then wait for and return the resulting HttpResponseMessage
                response = httpClient.GetAsync("https://bing.com").GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // throw the error
                throw;
            }

            return response;
        }
    }
}
