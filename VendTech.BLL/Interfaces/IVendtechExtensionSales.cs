using System.Threading.Tasks;
using VendTech.BLL.Models;

namespace VendTech.BLL.Interfaces
{
    public interface IVendtechExtensionSales
    {
        Task<ReceiptModel> RechargeFromVendtechExtension(RechargeMeterModel model);
        Task<ReceiptModel> GetStatusFromVendtechExtension(string trxId, bool billVendor = false);
    }
}
