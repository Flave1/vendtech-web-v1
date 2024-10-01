using System.Threading.Tasks;
using VendTech.BLL.Models;

namespace VendTech.BLL.Interfaces
{
    public interface IVendtechExtensionSales
    {
        Task<ReceiptModel> RechargeFromVendtechExtension(RechargeMeterModel model);
    }
}
