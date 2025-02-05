using System.Threading.Tasks;
using System.Web.Mvc;
using VendTech.Attributes;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;
using VendTech.BLL.Jobs;
using Quartz;
using Quartz.Impl;

namespace VendTech.Areas.Admin.Controllers
{
    public class PlatformController : AdminBaseV2Controller
    {
        #region Variable Declaration
        private new readonly IPlatformManager _platformManager;
        private readonly IEmailTemplateManager _templateManager;
        private readonly IPlatformApiManager _platformApiManager;
        #endregion

        public PlatformController(
            IPlatformManager platformManager, 
            IErrorLogManager errorLogManager, 
            IEmailTemplateManager templateManager,
            IPlatformApiManager platformApiManager)
            : base(errorLogManager)
        {
            _platformManager = platformManager;
            _templateManager = templateManager;
            _platformApiManager = platformApiManager;
        }

        public ActionResult ManagePlatforms()
        {
            ViewBag.SelectedTab = SelectedAdminTab.Platforms;
            ViewBag.PlatformTypes = PlatformModel.GetPlatformTypes();
            return View("ManagePlatformsV2", _platformManager.GetPlatforms());
        }

        [AjaxOnly, HttpGet]
        public JsonResult GetApiConnectionsForPlatform(int platformId)
        {
            return Json(_platformApiManager.GetPlatformApiConnectionsForPlatform(platformId), JsonRequestBehavior.AllowGet);
        }

        [AjaxOnly, HttpPost]
        public JsonResult DeletePlatform(int id)
        {
            return JsonResult(_platformManager.DeletePlatform(id));
        }
        [AjaxOnly, HttpPost]
        public JsonResult EnablePlatform(int id)
        {
            var result = _platformManager.ChangePlatformStatus(id, true);
            if(result.Status == ActionStatus.Successfull)
            {
                NotifyAppUsersOnServiceEnable();
            }
            return JsonResult(result);
        }
        [AjaxOnly, HttpPost]
        public JsonResult DisablePlatform(int id)
        {
            return JsonResult(_platformManager.ChangePlatformStatus(id, false));
        }
        [AjaxOnly, HttpPost]
        public JsonResult SavePlatform(SavePlatformModel model)
        {
            return JsonResult(_platformManager.SavePlateform(model));
        }

        [AjaxOnly, HttpPost]
        public JsonResult EnableThisPlatform(EnableThisPlatform model)
        {
            var result = _platformManager.EnableThisPlateform(model);
            if(!model.DisablePlatform)
                NotifyAppUsersOnServiceEnable();
            return JsonResult(result);
        }

        private void NotifyAppUsersOnServiceEnable()
        {
            IScheduler scheduler =  StdSchedulerFactory.GetDefaultScheduler();
            scheduler.Start();

            IJobDetail job = JobBuilder.Create<NotifyAppUsersOnServiceEnabledSheduleJob>().Build();
            ITrigger trigger = TriggerBuilder.Create()
                .StartNow()
                .Build();

            scheduler.ScheduleJob(job, trigger);
        }
    }
}