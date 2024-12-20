using iTextSharp.xmp.impl;
using System;
using System.Transactions;

namespace VendTech.BLL.Models
{

    public class VtechElectricitySaleRequest
    {
        public decimal Amount { get; set; }
        public string MeterNumber { get; set; }
        public string TransactionId { get; set; }
    }
    public class VtechElectricitySaleStatus
    {
        public string TransactionId { get; set; }
    }
    public class VtechExtensionResponse
    {
        public string Status { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; }
        public object Detailed { get; set; }
        public VendtechExtSalesResult Result { get; set; }
    }


    public class VendtechExtSalesResult
    {
        public string Status { get; set; }
        public SuccessResponse SuccessResponse { get; set; }
        public FailedResponse FailedResponse { get; set; }

    }

    public class SuccessResponse
    {
        public string TransactionId { get; set; }
        public DateTime RequestDate { get; set; }
        public decimal Amount { get; set; }
        public string MeterNumber { get; set; }
        public int TransactionStatus { get; set; }
        public string VendtechTransactionId { get; set; }
        public string WalleBalance { get; set; }
        public Voucher Voucher { get; set; } = new Voucher();

    }
    public class Voucher
    {
        public string MeterToken1 { get; set; }
        public string MeterToken2 { get; set; }
        public string MeterToken3 { get; set; }
        public string AccountNumber { get; set; }
        public string Customer { get; set; }
        public string ReceiptNumber { get; set; }
        public string ServiceCharge { get; set; }
        public string Tariff { get; set; }
        public string TaxCharge { get; set; }
        public string CostOfUnits { get; set; }
        public string Units { get; set; }
        public string DebitRecovery { get; set; }
        public string CustomerAddress { get; set; }
        public int StatusRequestCount { get; set; }
        public string VoucherSerialNumber { get; set; }
        public string VendStatusDescription { get; set; }

    }
    public class FailedResponse
    {
        public string ErrorMessage { get; set; }
        public string ErrorDetail { get; set; }
     
     
    }
}
