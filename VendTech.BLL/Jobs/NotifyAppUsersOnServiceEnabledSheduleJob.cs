using Newtonsoft.Json;
using Quartz;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Web.Configuration;
using System.Web.Mvc;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;

namespace VendTech.BLL.Jobs
{
    public class NotifyAppUsersOnServiceEnabledSheduleJob : IJob
    {
     
        public async void Execute(IJobExecutionContext context)
        {
            var _errorManager = DependencyResolver.Current.GetService<IErrorLogManager>();
            var usermanager = DependencyResolver.Current.GetService<IUserManager>();


            try
            {
                var userTokens = await usermanager.GetActiveUsersDeviceTokens(pageNo: 1, pageSize: 1);
                var url = WebConfigurationManager.AppSettings["VendtechExtentionServer"] + "vendtech/push_to_multiple_mobile";

                var responses = new ConcurrentBag<string>();
                List<MessageRequest> requests = new List<MessageRequest>();
               
                using (HttpClient client = new HttpClient())
                {
                    for (int i = 0; i < userTokens.Count; i++)
                    {
                        var requestBody = new MessageRequest
                        {
                            Body = "VENDTECH has enabled EDSA Service",
                            DeviceToken = userTokens[i],
                            Id = userTokens[i],
                            Title = "EDSA SERVICE ENABLED",
                            NotificationType = "vend_service_restored"
                        };
                        requests.Add(requestBody);
                    }

                    var payload = new
                    {
                        MessageRequests = requests,
                    };

                    string jsonPayload = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = client.PostAsync(url, content).Result;
                    string responseBody = response.Content.ReadAsStringAsync().Result;
                }
                
            }
            catch (Exception ex)
            {
                _errorManager.LogExceptionToDatabase(new Exception("Error ocurred trying to push notifications to app users on service restoration", ex));
            }
        }
    }
}
