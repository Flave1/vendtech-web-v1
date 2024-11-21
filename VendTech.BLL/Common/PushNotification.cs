//using FirebaseAdmin;
//using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Dynamic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Configuration;
using VendTech.BLL.Models;
using VendTech.DAL;

namespace VendTech.BLL.Common
{
    public class PushNotification
    {
        private static readonly Lazy<PushNotification> _instance = new Lazy<PushNotification>(() => new PushNotification());

        private static List<string> notification_urls { get; set; }
        private static long userId { get; set; }

        public PushNotification()
        {
            notification_urls = new List<string>();
            userId = 0;
        }

        public static PushNotification Instance => _instance.Value;


        public PushNotification IncludeUserBalanceOnTheWeb(long user)
        {
            var url = WebConfigurationManager.AppSettings["SignaRServer"] + "vendtech/balance";
            userId = user;
            notification_urls.Add(url);
            return this;
        }

        public PushNotification IncludeAdminWidgetSales()
        {
            var url = WebConfigurationManager.AppSettings["SignaRServer"] + "vendtech/updatewigdetsales";
            notification_urls.Add(url);
            return this;
        }

        public PushNotification IncludeAdminWidgetDeposits()
        {
            var url = WebConfigurationManager.AppSettings["SignaRServer"] + "vendtech/updatewigdetdeposit";
            notification_urls.Add(url);
            return this;
        }
        public PushNotification IncludeAdminNotificationCount()
        {
            var url = WebConfigurationManager.AppSettings["SignaRServer"] + "vendtech/admin_notification_count";
            notification_urls.Add(url);
            return this;
        }
        public PushNotification IncludeAdminUnreleasedDeposits()
        {
            var url = WebConfigurationManager.AppSettings["SignaRServer"] + "vendtech/updateunreleaseddeposit";
            notification_urls.Add(url);
            return this;
        }
        public void Send()
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {

                    var urls = notification_urls.Where(url => !string.IsNullOrEmpty(url));

                    var responses = new ConcurrentBag<string>();
                    var requestBody = new SignalRMessageBody { UserId = userId.ToString(), Message = "message" };
                    string jsonPayload = JsonConvert.SerializeObject(requestBody);


                    Parallel.ForEach(urls, (url) =>
                    {
                        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                        HttpResponseMessage response = client.PostAsync(url, content).Result;
                        string responseBody = response.Content.ReadAsStringAsync().Result;
                        responses.Add(responseBody);
                    });

                    foreach (var response in responses)
                    {
                        Console.WriteLine(response);
                    }
                }
                catch (Exception) { }
            }

        }

        public static void IncludeAndroidPush(PushNotificationModel model)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    var url = WebConfigurationManager.AppSettings["SignaRServer"] + "vendtech/push_to_mobile";

                    var responses = new ConcurrentBag<string>();
                    var requestBody = new MessageRequest { 
                        Body = model.Message, 
                        DeviceToken = model.DeviceToken,
                        Id = model.Id.ToString(),
                        Title = model.Title, 
                        NotificationType = ((int)model.NotificationType).ToString()
                    };
                    string jsonPayload = JsonConvert.SerializeObject(requestBody);

                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = client.PostAsync(url, content).Result;
                    string responseBody = response.Content.ReadAsStringAsync().Result;

                    SaveNotificationToDB(model, (int)NotificationStatusEnum.Success);
                }
                catch (Exception) { }
            }
        }


        public static string ASendNotificationTOMobile(PushNotificationModel model)
        {
            try
            {
                string GoogleAppID = Config.GetWebApiKey;
                var SENDER_ID = Config.GetSenderId;

                WebRequest tRequest;
                tRequest = WebRequest.Create(Config.FCMUrl);
                tRequest.Method = "post";
                tRequest.ContentType = "application/json";
                //tRequest.Headers.Add(string.Format("Authorization: key={0}", GoogleAppID));
                tRequest.Headers.Add("Authorization", $"Bearer {GoogleAppID}");
                tRequest.Headers.Add(string.Format("Sender: id={0}", SENDER_ID));


                if (model.DeviceType == (int)AppTypeEnum.IOS)
                {
                    var payload = new
                    {
                        to = model.DeviceToken,
                        priority = "high",
                        content_available = true,
                        notification = new
                        {
                            title = model.Title,
                            body = model.Message,
                            type = (int)model.NotificationType,
                            sound = "default",
                            id = model.Id

                        }
                    };
                    string postData = JsonConvert.SerializeObject(payload).ToString();

                    Byte[] byteArray = Encoding.UTF8.GetBytes(postData);
                    tRequest.ContentLength = byteArray.Length;

                    Stream dataStream = tRequest.GetRequestStream();
                    dataStream.Write(byteArray, 0, byteArray.Length);
                    dataStream.Close();

                    WebResponse tResponse = tRequest.GetResponse();

                    dataStream = tResponse.GetResponseStream();

                    StreamReader tReader = new StreamReader(dataStream);

                    String sResponseFromServer = tReader.ReadToEnd();

                    tReader.Close();
                    dataStream.Close();
                    tResponse.Close();
                    SaveNotificationToDB(model, (int)NotificationStatusEnum.Success);
                    return sResponseFromServer;
                }
                else
                {
                    var payload = new
                    {
                        to = model.DeviceToken,
                        priority = "high",
                        content_available = true,
                        data = new
                        {
                            title = model.Title,
                            body = model.Message,
                            id = model.Id,
                            type = (int)model.NotificationType,
                            balance = model.Balance
                        }
                    };



                    string postData = JsonConvert.SerializeObject(payload).ToString();

                    Byte[] byteArray = Encoding.UTF8.GetBytes(postData);
                    tRequest.ContentLength = byteArray.Length;

                    Stream dataStream = tRequest.GetRequestStream();
                    dataStream.Write(byteArray, 0, byteArray.Length);
                    dataStream.Close();

                    WebResponse tResponse = tRequest.GetResponse();

                    dataStream = tResponse.GetResponseStream();

                    StreamReader tReader = new StreamReader(dataStream);
                    String sResponseFromServer = tReader.ReadToEnd();

                    tReader.Close();
                    dataStream.Close();
                    tResponse.Close();
                    SaveNotificationToDB(model, (int)NotificationStatusEnum.Success);
                    return sResponseFromServer;
                }

            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse response && response.StatusCode == HttpStatusCode.MovedPermanently)
                {
                    string redirectUrl = response.Headers["Location"];
                    Console.WriteLine("Redirected to: " + redirectUrl);
                    // Update your request URL to redirectUrl if needed.
                }
                else
                {
                    throw;
                }
                SaveNotificationToDB(model, (int)NotificationStatusEnum.Failed);
                return ex.Message;
            }
            catch (Exception ex)
            {
                SaveNotificationToDB(model, (int)NotificationStatusEnum.Failed);
                return ex.Message;
            }
        }

        private static bool SaveNotificationToDB(PushNotificationModel model, int status)
        {
            var db = new VendtechEntities();
            var dbNotification = new Notification();
            dbNotification.SentOn = DateTime.UtcNow;
            dbNotification.UserId = model.UserId;
            dbNotification.Title = model.Title;
            dbNotification.Text = model.Message;
            dbNotification.Type = (int)model.NotificationType;
            dbNotification.Status = status;
            dbNotification.RowId = model.Id;
            db.Notifications.Add(dbNotification);
            db.SaveChanges();
            return true;
        }

    }
}
