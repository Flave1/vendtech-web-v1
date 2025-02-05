using Quartz;
using Quartz.Impl;
using System;
using System.Security.Cryptography;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using VendTech.App_Start;
using VendTech.BLL.Common;
using VendTech.BLL.Jobs;
using VendTech.BLL.Models;

namespace VendTech
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            IScheduler scheduler = StdSchedulerFactory.GetDefaultScheduler();
            scheduler.Start();

            /// APLICATION USE TIME
            ITrigger appUseTimeTrigger = TriggerBuilder.Create().StartNow()
            .WithSimpleSchedule (s => s.WithIntervalInMinutes(1).RepeatForever()).Build();
            IJobDetail appUseTimeJob = JobBuilder.Create<ApplicationNotUsedSchedulerJob>().Build();
            scheduler.ScheduleJob(appUseTimeJob, appUseTimeTrigger);
            ///


            /// PENDING TRANSACTIONS
            //ITrigger pendingTransactionTrigger = TriggerBuilder.Create().StartNow()
            //.WithSimpleSchedule(s => s.WithIntervalInSeconds(30).RepeatForever()).Build();
            //IJobDetail pendingTransactionKob = JobBuilder.Create<PendingTransactionCheckJob>().Build();
            //scheduler.ScheduleJob(pendingTransactionKob, pendingTransactionTrigger);
            ///


            /// LOW BALANCE CHECK
            ITrigger balanceLowTrigger = TriggerBuilder.Create().StartNow()
            .WithSimpleSchedule(s => s.WithIntervalInHours(12).RepeatForever()).Build();
            IJobDetail balanceLowJob = JobBuilder.Create<BalanceLowSheduleJob>().Build();
            scheduler.ScheduleJob(balanceLowJob, balanceLowTrigger);
            ///

            /// UNCLEARED BALANCE
            ITrigger unclearedDepositsTrigger = TriggerBuilder.Create().StartNow()
            .WithSimpleSchedule(s => s.WithIntervalInHours(12).RepeatForever()).Build();
            IJobDetail unclearedDepositsJob = JobBuilder.Create<UnclearedDepositsSheduleJob>().Build();
            scheduler.ScheduleJob(unclearedDepositsJob, unclearedDepositsTrigger);
            ///

            /// COMMON
            //ITrigger commonJobsTrigger = TriggerBuilder.Create().StartNow()
            //.WithSimpleSchedule(s => s.WithIntervalInHours(24).RepeatForever()).Build();
            //IJobDetail commonJobs = JobBuilder.Create<CommonShedulesJob>().Build();
            //scheduler.ScheduleJob(commonJobs, commonJobsTrigger);
            ///

            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
           
        }
        

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            System.Globalization.CultureInfo newCulture =
                (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            newCulture.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
            newCulture.DateTimeFormat.DateSeparator = "/";
            System.Threading.Thread.CurrentThread.CurrentCulture = newCulture;
        }

        private void HandleSignOut(HttpContext httpContext)
        {
            HttpCookie auth_cookie = Request.Cookies[Cookies.AdminAuthorizationCookie];
            if (auth_cookie != null)
            {
                auth_cookie.Expires = DateTime.Now.AddDays(-30);
                Response.Cookies.Add(auth_cookie);
            }
            HttpCookie web_auth_cookie = Request.Cookies[Cookies.AuthorizationCookie];
            if (web_auth_cookie != null)
            {
                web_auth_cookie.Expires = DateTime.Now.AddDays(-30);
                Response.Cookies.Add(web_auth_cookie);
            }
            //httpContext.Response.Redirect("~/Home/Error?errorMessage="+ httpContext.AllErrors[0].Message+"");
            httpContext.Response.Redirect("~/Home/Index");
        }

        private void LogOperationCanceledException(OperationCanceledException ex)
        {
            string logMessage = $"handled Operation canceled: {ex.Message}\nOccurred at: {DateTime.UtcNow}\nStackTrace:";
            Utilities.LogExceptionToDatabase(ex, logMessage);
        }


        protected void Application_Error()
        {

            HttpContext httpContext = HttpContext.Current;
            if (httpContext != null)
            {
                try
                {


                    Exception error = Server.GetLastError();
                    // Check if the exception is an OperationCanceledException
                    if (error is OperationCanceledException canceledException)
                    {
                        LogOperationCanceledException(canceledException);
                        Server.ClearError(); // Clear the error to prevent further handling
                        return;
                    }



                    RequestContext requestContext = ((MvcHandler)httpContext.CurrentHandler).RequestContext;
                    /* When the request is ajax the system can automatically handle a mistake with a JSON response. 
                   Then overwrites the default response */

                   

                    if (requestContext.HttpContext.Request.IsAjaxRequest())
                    {
                        httpContext.Response.Clear();
                        string controllerName = requestContext.RouteData.GetRequiredString("controller");
                        IControllerFactory factory = ControllerBuilder.Current.GetControllerFactory();
                        IController controller = factory.CreateController(requestContext, controllerName);
                        ControllerContext controllerContext = new ControllerContext(requestContext, (ControllerBase)controller);

                        JsonResult jsonResult = new JsonResult
                        {
                            Data = new { success = false, serverError = "500" },
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                        jsonResult.ExecuteResult(controllerContext);
                        httpContext.Response.End();
                    }
                    else
                    {
                        HandleSignOut(httpContext);
                    }
                }
                catch (InvalidCastException)
                {
                    HandleSignOut(httpContext);
                }

                catch (CryptographicException)
                {
                    HandleSignOut(httpContext);
                }
            }
        }
    }
}
