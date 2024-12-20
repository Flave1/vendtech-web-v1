using System.Linq;
using VendTech.BLL.Managers;
using VendTech.BLL.Models;
using VendTech.DAL;

namespace VendTech.BLL.Common
{
    public class MobileNotification: BaseManager
    {
        private readonly VendtechEntities _context;

        public MobileNotification(VendtechEntities context)
        {
            _context = context;
        }

        public void PushNotificationToMobile(long dbDepositId)
        {
            try
            {
                Deposit dbDeposit = _context.Deposits.FirstOrDefault(d => d.DepositId == dbDepositId);
                
                if(dbDeposit != null)
                {
                    var deviceTokens = _context.Users.FirstOrDefault(d => d.UserId == dbDeposit.UserId).TokensManagers.Where(p => p.DeviceToken != null && p.DeviceToken != string.Empty).Select(p => new { p.AppType, p.DeviceToken }).ToList().Distinct();
                    var obj = new PushNotificationModel();
                    obj.UserId = dbDeposit.UserId;
                    obj.Id = dbDeposit.DepositId;
                    obj.Balance = dbDeposit.POS.Balance.Value;
                    var notyAmount = Utilities.FormatAmount(dbDeposit.Amount);
                    if (dbDeposit.Status == (int)DepositPaymentStatusEnum.Rejected || dbDeposit.Status == (int)DepositPaymentStatusEnum.RejectedByAccountant)
                    {
                        obj.Title = "Deposit request rejected";
                        obj.Message = "Your deposit request has been rejected of NLe " + notyAmount;
                    }
                    else if (dbDeposit.Status == (int)DepositPaymentStatusEnum.Released)
                    {
                        obj.Title = "Wallet updated successfully";
                        obj.Message = "Your wallet has been updated with NLe " + notyAmount;
                    }
                    else if (dbDeposit.Status == (int)DepositPaymentStatusEnum.ApprovedByAccountant)
                    {
                        obj.Title = "Deposit request in progress";
                        obj.Message = "Your deposit request has been in processed of NLe " + notyAmount;
                    }
                    obj.NotificationType = NotificationTypeEnum.DepositStatusChange;
                    foreach (var item in deviceTokens)
                    {
                        obj.DeviceToken = item.DeviceToken;
                        obj.DeviceType = item.AppType.Value;
                        PushNotification.PushNotificationToMobile(obj);
                    }
                }
              
            }
            catch (System.Exception)
            {
                return;
            }
        }

        public void Notify(long targetId, long userId, string message)
        {
            var deviceTokens = _context.Users.FirstOrDefault(d => d.UserId == userId).TokensManagers.Where(p => p.DeviceToken != null && p.DeviceToken != string.Empty).Select(p => new { p.AppType, p.DeviceToken }).ToList().Distinct();
            var obj = new PushNotificationModel();
            obj.UserId = userId;
            obj.Id = targetId;
            obj.Balance = 0;

            obj.Title = "VENDTECHSL";
            obj.Message = message;

            obj.NotificationType = NotificationTypeEnum.DepositStatusChange;
            foreach (var item in deviceTokens)
            {
                obj.DeviceToken = item.DeviceToken;
                obj.DeviceType = item.AppType.Value;
                PushNotification.PushNotificationToMobile(obj);
            }
        }
    }
}
