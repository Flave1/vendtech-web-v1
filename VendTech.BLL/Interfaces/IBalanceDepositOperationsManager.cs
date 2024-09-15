using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VendTech.BLL.Models;
using VendTech.DAL;

namespace VendTech.BLL.Interfaces
{
    public interface IBalanceDepositOperationsManager
    {
        Task<Deposit> CreateDeposit(DepositDTOV2 depositDto, long currentUserId, bool processPercentage);
    }
}
