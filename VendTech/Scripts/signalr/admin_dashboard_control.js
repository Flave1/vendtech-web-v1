"use strict";

import { connection } from './admin_connection.js';

connection.on("UpdateWigdetSales", function (message) {
    refreshBalances();
    refreshVendStatus();
    refreshSalesStatus();
});

connection.on("UpdateWigdetDeposits", function (message) {
    refreshDeposits();
    refreshUnreleasedDeposits();
});

connection.on("UpdateAdminUnreleasedDeposits", function (message) {
    refreshUnreleasedDeposits();
});



$(document).ready(function () {
    refreshBalances();
    refreshVendStatus();
    refreshSalesStatus();
    refreshUnreleasedDeposits();
});



function refreshVendStatus() {
    $.ajax({
        url: '/Admin/Home/GetVendorBalanceSheetReports',
        success: function (data) {
            console.log('GetVendorBalanceSheetReports', data)
            $('#bsListing').html(data);
        }
    })
}
function refreshUnreleasedDeposits() {
    $.ajax({
        url: '/Admin/Home/GetUnreleasedDeposits',
        success: function (data) {
            $('#unreleasedDepositListing').html(data);
        }
    })
}
function refreshSalesStatus() {
    const salesRefresh = document.getElementById('salesRefresh');
    if (salesRefresh) {
        salesRefresh.style.display = 'block';
    }
    $.ajax({
        url: '/Admin/Home/GetSalesHistory',
        success: function (data) {
            console.log('GetSalesHistory', data)
            $('#saleListing').html(data);
            if (salesRefresh) {
                salesRefresh.style.display = 'none';
            }

        }
    })
}
function refreshBalances() {
    $.ajax({
        url: '/Admin/Home/UpdateBalances',
        success: function (data) {
            console.log('UpdateBalances', data)
            $("#rtsBalance").text(data.result.LastDealerBalance);
            $("#transDate").text(data.result.RequestDate);
            $("#salesBalance").text(data.result.TotalSales);
            $("#walletBalance").text(data.result.WalletBalance);
        }
    })
}
function refreshDeposits() {
    $.ajax({
        url: '/Admin/Home/UpdateDepositToday',
        success: function (data) {
            $("#depositBalanceToday").text(data.result.DepositBalanceToday);
        }
    })
}


