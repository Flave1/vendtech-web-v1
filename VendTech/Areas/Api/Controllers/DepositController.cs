using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Description;
using Twilio.TwiML.Fax;
using VendTech.Attributes;
using VendTech.BLL.Common;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;
using VendTech.BLL.PlatformApi;
using VendTech.DAL;
using VendTech.Framework.Api;


namespace VendTech.Areas.Api.Controllers
{
    public class DepositController : BaseAPIController
    {
        private readonly IUserManager _userManager;
        private readonly IDepositManager _depositManager;
        private readonly IPOSManager _posManager;
        private readonly IEmailTemplateManager _templateManager;
        private readonly IPaymentTypeManager _paymentTypeManager;
        private readonly EmailNotification emailNotification;
        private readonly MobileNotification mobileNotification;
        public DepositController(IUserManager userManager,
            IErrorLogManager errorLogManager,
            IDepositManager depositManager,
            IPOSManager pOSManager,
            IEmailTemplateManager emailTemplateManager,
            IPaymentTypeManager paymentTypeManager,
            MobileNotification mobileNotification,
            EmailNotification emailNotification)
            : base(errorLogManager)
        {
            _userManager = userManager;
            _depositManager = depositManager;
            _posManager = pOSManager;
            _templateManager = emailTemplateManager;
            _paymentTypeManager = paymentTypeManager;
            this.mobileNotification = mobileNotification;
            this.emailNotification = emailNotification;
        }

        [HttpPost]
        [ResponseType(typeof(ResponseBase))]
        public async Task<HttpResponseMessage> SaveDepositRequest(DepositModel model)
        {
            ActionOutput<PendingDeposit> pd = null;
            model.UserId = LOGGEDIN_USER.UserId;
            //model.TotalAmountWithPercentage = model.Amount;
            model.BankAccountId = 1;
            //model.ValueDate = DateTime.Now.Date.ToString("dd/MM/yyyy");

            if (model.ContinueDepoit == 0)
            {
                var pendingDeposits = _depositManager.ReturnPendingDepositsTotalAmount(model);
                if (pendingDeposits > 0)
                {
                    return new JsonContent(BLL.Common.Utilities.FormatAmount(pendingDeposits), Status.Success).ConvertToHttpResponseOK();
                }
            }

            pd = _depositManager.SaveDepositRequest(model);
            string mesg = pd.Message;
            if (pd.Object.User.AutoApprove.Value)
            {
                await _depositManager.ChangeDepositStatus(pd.Object.PendingDepositId, DepositPaymentStatusEnum.Released, true);

                var deposit = _depositManager.GetDeposit(pd.Object.PendingDepositId);
                emailNotification.SendEmailToUserOnDepositApproval(deposit);
                emailNotification.SendEmailToAdminOnDepositAutoApproval(deposit, 40249);
                await emailNotification.SendSmsToUserOnDepositApproval(deposit);

                await _depositManager.DeletePendingDeposits(deposit);

                mobileNotification.PushNotificationToMobile(deposit.ApprovedDepId);
                PushNotification.Instance
                   .IncludeAdminNotificationCount()
                   .IncludeUserBalanceOnTheWeb(deposit.UserId)
                   .IncludeAdminWidgetDeposits()
                   .IncludeAdminUnreleasedDeposits()
                   .Send();
            }
            else
            {
                mobileNotification.Notify(pd.Object.PendingDepositId, pd.Object.UserId, "Deposit Requested");
                emailNotification.SendEmailToAdminOnDepositRequest(pd.Object);
                PushNotification.Instance
                   .IncludeAdminNotificationCount()
                   .IncludeUserBalanceOnTheWeb(pd.Object.UserId)
                   .IncludeAdminWidgetDeposits()
                   .IncludeAdminUnreleasedDeposits()
                   .Send();

            }
            return new JsonContent(mesg, pd.Status == ActionStatus.Successfull ? Status.Success : Status.Failed).ConvertToHttpResponseOK();
        }


        [HttpPost]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage SaveDepositRequestTemp(DepositModel model)
        {
            model.UserId = LOGGEDIN_USER.UserId;
            //model.TotalAmountWithPercentage = model.Amount;
            model.BankAccountId = 1;
            //model.ValueDate = DateTime.Now.Date.ToString("dd/MM/yyyy");

            if (model.ContinueDepoit == 0)
            {
                var pendingDeposits = _depositManager.ReturnPendingDepositsTotalAmount(model);
                if (pendingDeposits > 0)
                {
                    return new JsonContent(BLL.Common.Utilities.FormatAmount(pendingDeposits), Status.Success).ConvertToHttpResponseOK();
                }
            }

            var result = _depositManager.SaveDepositRequest(model);



            var pos = _posManager.GetSinglePos(result.Object.POSId);
            if (pos != null)
            {
                var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.DepositRequestNotification);
                if(emailTemplate.Receivers.Count > 0)
                    foreach (var email in emailTemplate.Receivers)
                    {
                        if (emailTemplate != null)
                        {
                            var userAccount = _userManager.GetUserDetailByEmail(email);
                            if (emailTemplate.TemplateStatus)
                            {
                                string body = emailTemplate.TemplateContent;
                                body = body.Replace("%AdminUserName%", userAccount?.Name + " " + userAccount?.SurName);
                                body = body.Replace("%VendorName%", pos.User.Vendor);
                                body = body.Replace("%POSID%", pos.SerialNumber);
                                body = body.Replace("%REF%", result.Object.CheckNumberOrSlipId);
                                body = body.Replace("%Amount%", BLL.Common.Utilities.FormatAmount(result.Object.Amount));
                                body = body.Replace("%CurrencyCode%", BLL.Common.Utilities.GetCountry().CurrencyCode);
                                BLL.Common.Utilities.SendEmail(email, emailTemplate.EmailSubject, body);
                            }

                        }
                    }
            }

            return new JsonContent(result.Message, result.Status == ActionStatus.Successfull ? Status.Success : Status.Failed).ConvertToHttpResponseOK();
        }

        [HttpGet]
         [ResponseType(typeof(ResponseBase))]
         public HttpResponseMessage GetDeposits(int pageNo,int pageSize)
         {            
             var result = _depositManager.GetUserDepositList(pageNo,pageSize,LOGGEDIN_USER.UserId);
             return new JsonContent(result.TotalCount,result.Message, result.Status == ActionStatus.Successfull ? Status.Success : Status.Failed,result.List).ConvertToHttpResponseOK();
         }

         [HttpGet]
         [ResponseType(typeof(ResponseBase))]
         public HttpResponseMessage GetDepositDetail(long depositId)
         {
             var result = _depositManager.GetDepositDetail(depositId);
             return new JsonContent(result.Message, result.Status == ActionStatus.Successfull ? Status.Success : Status.Failed, result.Object).ConvertToHttpResponseOK();
         }
         [HttpGet]
         [ResponseType(typeof(ResponseBase))]
         public HttpResponseMessage GetDepositPdf(long depositId)
         {
             var result = new ActionOutput<DepositListingModel>();
             var domain = VendTech.BLL.Common.Utilities.DomainUrl;
             result = _depositManager.GetDepositDetail(depositId);
             if(result.Object==null || result.Object.DepositId<=0)
                 return new JsonContent("Error occured in generating pdf link.", Status.Success, new { path = "" }).ConvertToHttpResponseOK();
             var folderName = "/Content/DepositPdf/" + result.Object.Status;
             var folderPath = HttpContext.Current.Server.MapPath("~"+folderName);
             if (!Directory.Exists(folderPath))
                 Directory.CreateDirectory(folderPath);
             var name = "Deposit_" + depositId + ".pdf";
             var fileName = Path.Combine(folderPath, name);
             if (File.Exists(fileName))
             {
                 domain = domain + folderName+"/" + name;
                 return new JsonContent("Pdf link fetched successfully", Status.Success, new { path = domain }).ConvertToHttpResponseOK();
             }
             //Create a byte array that will eventually hold our final PDF

             Byte[] bytes;

             //Boilerplate iTextSharp setup here
             //Create a stream that we can write to, in this case a MemoryStream
             using (var ms = new MemoryStream())
             {

                 //Create an iTextSharp Document which is an abstraction of a PDF but **NOT** a PDF
                 using (var doc = new Document())
                 {

                     //Create a writer that's bound to our PDF abstraction and our stream
                     using (var writer = PdfWriter.GetInstance(doc, ms))
                     {

                         //Open the document for writing
                         doc.Open();

                         //Our sample HTML and CSS
                         var example_html = System.IO.File.ReadAllText(HttpContext.Current.Server.MapPath("~/Templates/DepositPDF.html"));

                         /**************************************************
                          * Example #1                                     *
                          *                                                *
                          * Use the built-in HTMLWorker to parse the HTML. *
                          * Only inline CSS is supported.                  *
                          * ************************************************/

                         //Create a new HTMLWorker bound to our document
                         example_html = example_html.Replace("%UserName%", result.Object.UserName);
                         example_html = example_html.Replace("%ChkNoOrSlipId%", result.Object.ChkNoOrSlipId);
                         example_html = example_html.Replace("%Amount%", result.Object.Amount.ToString());
                         example_html = example_html.Replace("%CreatedAt%", result.Object.CreatedAt);
                         example_html = example_html.Replace("%TransactionId%", result.Object.TransactionId);
                         example_html = example_html.Replace("%Status%", result.Object.Status);
                         example_html = example_html.Replace("%Comments%", result.Object.Comments);
                         example_html = example_html.Replace("%Type%", result.Object.Type);
                         using (var htmlWorker = new iTextSharp.text.html.simpleparser.HTMLWorker(doc))
                         {

                             //HTMLWorker doesn't read a string directly but instead needs a TextReader (which StringReader subclasses)
                             using (var sr = new StringReader(example_html))
                             {

                                 //Parse the HTML
                                 htmlWorker.Parse(sr);
                             }
                         }




                         doc.Close();
                     }
                 }
                 bytes = ms.ToArray();
             }
             System.IO.File.WriteAllBytes(fileName, bytes);
             domain = domain + folderName+"/" + name;
             return new JsonContent("Pdf link fetched successfully", Status.Success, new { path = domain }).ConvertToHttpResponseOK();
         }

        [HttpGet]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage GetPaymentTypes()
        {
            var result = _paymentTypeManager.GetPaymentTypeSelectList();
            return new JsonContent("Payment types fetched successfully.", Status.Success, result).ConvertToHttpResponseOK();
        }

        [HttpPost, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage SendDepositEmail(SendViaEmail model)
        {

            try
            {
                if (!BLL.Common.Utilities.IsEmailValid(model.Email))
                {
                    return new JsonContent("Email you provided is invalid", Status.Failed, model).ConvertToHttpResponseOK();
                }

                var td = _depositManager.GetSingleTransaction(long.Parse(model.TransactionId));
                if (td == null)
                    return new JsonContent("Not found", Status.Failed, model).ConvertToHttpResponseOK();


                var vendor = _userManager.GetUserDetailsByUserId(td.UserId);
                var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.DepositReceiptTemplate);
                if (emailTemplate.TemplateStatus)
                {
                    var body = emailTemplate.TemplateContent;
                    body = body.Replace("%ValueDate%", td.ValueDate);
                    body = body.Replace("%CreatedAt%", td.CreatedAt.ToString("dd/MM/yyyy"));
                    body = body.Replace("%VendorName%", td.User.Vendor);
                    body = body.Replace("%PosNumber%", td.POS.SerialNumber);
                    body = body.Replace("%TransactionId%", td.TransactionId);
                    body = body.Replace("%Type%", td.PaymentType1.Name);
                    body = body.Replace("%VendorName%", td.NameOnCheque);
                    body = body.Replace("%Amount%", BLL.Common.Utilities.FormatAmount(td.Amount));
                    body = body.Replace("%IssuingBank%", td.ChequeBankName);
                    body = body.Replace("%ChkNoOrSlipId%", td.CheckNumberOrSlipId);
                    body = body.Replace("%Bank%", td.ChequeBankName);
                    body = body.Replace("%PercentageAmount%", BLL.Common.Utilities.FormatAmount(td.PercentageAmount));
                    if (td.PercentageAmount != td.Amount)
                    {
                        body = body.Replace("%Commission%", BLL.Common.Utilities.FormatAmount(td.PercentageAmount - td.Amount));
                    }
                    body = body.Replace("%date%", td.CreatedAt.ToString("dd/MM/yyyy"));

                    string img1 = "<img src=\"https://vendtechsl.com/Content/images/ventech.png\" style=\"width:90px\" />";
                    string img2 = "<img src=\"https://vendtechsl.com/Images/ProfileImages/invoice.png\" style=\"width:200px\" />";
                    string modifiedContent = "";
                    if (td.PercentageAmount == td.Amount)
                        modifiedContent = BLL.Common.Utilities.RemoveTableRow(body, 11);
                    else
                        modifiedContent = body;


                    modifiedContent = modifiedContent.Replace("%img1%", img1);
                    modifiedContent = modifiedContent.Replace("%img2%", img2);
                    modifiedContent = modifiedContent.Replace("<br>", " ");

                    var file = BLL.Common.Utilities.CreatePdf(modifiedContent, td.TransactionId + "_invoice.pdf");
                    var subject = $"VENDTECH INVOICE - INV-{td.TransactionId} for {vendor.Vendor}";
                    var content = BLL.Common.Utilities.SendDepositViaEmailContent(vendor.Vendor, td.TransactionId);


                    BLL.Common.Utilities.SendPDFEmail(model.Email, subject, content, file.FirstOrDefault().Value, "VENDTECH_INVOICE-INV-" + td.TransactionId + ".pdf");

                }

                return new JsonContent("Email successfully sent", Status.Success).ConvertToHttpResponseOK();
            }
            catch (Exception)
            {

                throw;
            }
        }
    }
}
