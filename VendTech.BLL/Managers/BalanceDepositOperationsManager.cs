using System.Threading.Tasks;
using System;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;
using VendTech.DAL;
using System.Data.Entity;
using VendTech.DAL.DomainBuilders;
using VendTech.BLL.Common;

namespace VendTech.BLL.Managers
{
    public class BalanceDepositOperationsManager : IBalanceDepositOperationsManager
    {
        private readonly VendtechEntities _context;

        public BalanceDepositOperationsManager(VendtechEntities context)
        {
            _context = context;
        }

        public async Task<Deposit> CreateDeposit(DepositDTOV2 depositDto, bool giveAgencyAdminCommission)
        {
            try
            {
                var deposit = await CreateNewDeposit(depositDto, giveAgencyAdminCommission);
                await GenerateDepositLog(deposit, depositDto.Approver);
                return deposit;
            }
            catch (Exception e)
            {
                Utilities.LogExceptionToDatabase(new Exception($"CreateDeposit.Error"), $"Exception: {e?.ToString()}");
                throw new ArgumentException(e.Message);
            }
        }

        private async Task<Deposit> CreateNewDeposit(DepositDTOV2 newDepositDto, bool giveAgencyAdminCommission)
        {
            var deposit = GenerateDeposit(newDepositDto);
            string firstDepositTransactionId = string.IsNullOrEmpty(newDepositDto.FirstDepositTransactionId) ? deposit.TransactionId : newDepositDto.FirstDepositTransactionId;
            deposit.InitiatingTransactionId = firstDepositTransactionId;
            POS vendorPos = await _context.POS.FirstOrDefaultAsync(d => d.VendorId == deposit.UserId);
            await CalculateBalance(deposit, vendorPos);
            _context.Deposits.Add(deposit);
            await _context.SaveChangesAsync();

            //GENERATE COMMISSION ROW FOR A VENDOR DEPOSIT
            if (newDepositDto.Amount > 0 && vendorPos.Commission.Percentage > 0)
            {
                DepositDTOV2 commisionDepositDto = newDepositDto;
                commisionDepositDto.Amount = (deposit.Amount * vendorPos.Commission.Percentage / 100);
                commisionDepositDto.FirstDepositTransactionId = firstDepositTransactionId;
                await GenerateCommission(commisionDepositDto, vendorPos);
            }

            //GENERATE COMMISSION IF IS AGENCY ADMIN
            if (giveAgencyAdminCommission)
            {
                long vendorId = newDepositDto.UserId;
                long agencyId = deposit.POS?.User.AgentId.Value ?? 0;
                Agency agency = await _context.Agencies.FirstOrDefaultAsync(d => d.AgencyId == agencyId);
                long vendorAdminId = agency?.Representative ?? 0;
                if (newDepositDto.Amount > 0 && agencyId != Utilities.VENDTECH && agencyId != 0)
                {
                    if (vendorId != vendorAdminId)
                    {
                        POS adminPos = await _context.POS.FirstOrDefaultAsync(d => d.VendorId == vendorAdminId);
                        if (adminPos != null)
                        {
                            DepositDTOV2 commisionDepositDto = newDepositDto;
                            commisionDepositDto.Amount = (deposit.Amount  * adminPos.Commission.Percentage / 100);
                            commisionDepositDto.UserId = vendorAdminId;
                            commisionDepositDto.POSId = adminPos.POSId;
                            commisionDepositDto.FirstDepositTransactionId = firstDepositTransactionId;
                            await GenerateCommission(commisionDepositDto, adminPos);
                        }
                    }
                }
            }
            return deposit;
        }

        private async Task GenerateCommission(DepositDTOV2 depositDto, POS pos)
        {

            if(await isAdmin(pos.VendorId.Value))
                depositDto.PaymentType = (int)DepositPaymentTypeEnum.AgencyCommision;
            else
                depositDto.PaymentType = (int)DepositPaymentTypeEnum.VendorCommision;

            var deposit = GenerateDeposit(depositDto);
            await CalculateBalance(deposit, pos);
            _context.Deposits.Add(deposit);
            await _context.SaveChangesAsync();
            await GenerateDepositLog(deposit, depositDto.Approver);
            return;
        }

        private async Task<Deposit> CalculateBalance(Deposit deposit, POS pos)
        {
            if (pos != null)
            {
                deposit.BalanceBefore = pos.Balance;
                pos.Balance = pos.Balance == null ?  deposit.Amount : pos.Balance.Value + deposit.Amount;
                deposit.NewBalance = pos.Balance;
                return await Task.Run(() => deposit);
            }
            return deposit;
        }

        private Deposit GenerateDeposit(DepositDTOV2 depositDto)
        {
            return new DepositBuilder()
                        .WithInitiatingTransactionId(depositDto.FirstDepositTransactionId)
                        .WithCheckNumberOrSlipId(depositDto.CheckNumberOrSlipId)
                        .WithTransactionId(Utilities.NewDepositTransactionId())
                        .WithStatus((int)DepositPaymentStatusEnum.Released)
                        .WithNextReminderDate(depositDto.NextReminderDate)
                        .WithValueDateStamp(depositDto.ValueDateStamp)
                        .WithChequeBankName(depositDto.ChequeBankName)
                        .WithBankAccountId(depositDto.BankAccountId)
                        .WithNameOnCheque(depositDto.NameOnCheque)
                        .WithPaymentType(depositDto.PaymentType)
                        .WithValueDate(depositDto.ValueDate)
                        .WithUpdatedAt(depositDto.UpdatedAt)
                        .WithComments(depositDto.Comments)
                        .WithIsAudit(depositDto.IsAudit)
                        .WithAmount(depositDto.Amount)
                        .WithUserId(depositDto.UserId)
                        .WithPOSId(depositDto.POSId)
                        .WithPercentageAmount(0)
                        .WithAgencyCommission(0)
                        .WithIsDeleted(false)
                        .Build();
        }
        
        private async Task GenerateDepositLog(Deposit deposit, long currentUserId)
        {
            //Creating Log entry in deposit logs table
            var dbDepositLog = new DepositLog();
            dbDepositLog.UserId = currentUserId;
            dbDepositLog.DepositId = deposit.DepositId;
            dbDepositLog.PreviousStatus = (int)DepositPaymentStatusEnum.Released;
            dbDepositLog.NewStatus = (int)DepositPaymentStatusEnum.Released;
            dbDepositLog.CreatedAt = DateTime.UtcNow;
            _context.DepositLogs.Add(dbDepositLog);
            await _context.SaveChangesAsync();
        }
        private async Task<bool> isAdmin(long userId) => await _context.Agencies.AnyAsync(d => d.Representative.Value == userId);
    }
}

