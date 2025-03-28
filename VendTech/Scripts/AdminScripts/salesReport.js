﻿$(document).ready(function () {

    $("input[type=button]#btnFilterVersion").live("click", function () {
        return Deposits.ManageDeposits($(this));
    });
    $("select#showRecords").on("change", function () {
        return Deposits.ShowRecords($(this));
    });
    $("#reportType").on("change", function () {
        var type = $("#reportType").val();
        var vendorId = $('#vendor').val();
        var pos = $('#pos').val();
        var meterNo = $('#meterNo').val();
        var transactionId = $('#tranId').val();
        var From = $('#FromDate').val();
        var To = $('#ToDate').val();
        window.location.href = "/Admin/Report/ManageReports?type=" + type + "&vendorId=" + vendorId + "&pos=" + pos + "&meter=" + meterNo + "&transactionId=" + transactionId + "&from=" + From + "&to=" + To;
    });
    $('.sorting').live("click", function () {
        return Deposits.SortDeposits($(this));
    });
    $('.rejectDepositBtn').live("click", function () {
        return Deposits.RejectDeposit($(this));
    });
    $('.addDepositBtn').live("click", function () {
        return Deposits.AddDeposit($(this));
    });
    $('.approveDepositBtn').live("click", function () {
        return Deposits.ApproveDeposit($(this));
    });
    $('.rejectReleaseDepositBtn').live("click", function () {
        return Deposits.RejectReleaseDeposit($(this));
    });
    $('.releaseDepositBtn').live("click", function () {
        return Deposits.ApproveReleaseDeposit($(this));
    });
    $("#btnFilterSearch").live("click", function () {
        return Deposits.SearchDeposits($(this));
    });

    $("#btnResetSearch").live("click", function () {
        $('#Search').val('');
        $('#searchField').val('');
        return Deposits.SearchDeposits($(this));
    });
});

var miniSalesReportHandler = {
    searchFilter: null,
    openMiniSalesReportModal: function (type) {
        $("#miniSalesReportModal").modal("show");
        $('#miniSalesReportModal').modal({
            backdrop: 'static',
            keyboard: false
        })
        this.fetchMiniSalesReport(this, type, true)

    },

    fetchMiniSalesReport: function (sender, type, isInitial) {
        document.getElementById("miniSalesListing").innerHTML = "";
        disableSubmit(true);
        var obj = new Object();
        obj.Search = $('#Search').val();
        obj.PageNo = paging.startIndex;
        obj.RecordsPerPage = paging.pageSize;
        obj.SortBy = $('#SortBy').val();
        obj.SortOrder = $('#SortOrder').val();
        obj.SearchField = $('#searchField').val();
        obj.PosId = $('#pos').val();
        obj.VendorId = $('#vendor').val();
        obj.ReportType = $("#reportType").val();
        obj.Meter = $('#meterNo').val();
        obj.TransactionId = $('#tranId').val();
        obj.Product = $('#productId').val();
        obj.Status = $('#status').val();

        if (isInitial === true) {
            obj.To = $('#ToDate').val();
            obj.From = $('#FromDate').val();
        } else {
            obj.To = $('#miniSaleRpToDate').val();
            obj.From = $('#miniSaleRpFromDate').val();
        }
        
        obj.miniSaleRpType = type;

        if (obj.From) {
            var val1 = obj.From.split("/");
            obj.From = val1[0] + "/" + val1[1] + "/" + val1[2];
        }
        if (obj.To) {
            var val2 = obj.To.split("/");
            obj.To = val2[0] + "/" + val2[1] + "/" + val2[2];
        }

        this.setTitle(type, obj)

        $("#miniSaleRpFromDate").kendoDatePicker({
            culture: "en-GB",
            value: new Date(),
            format: "dd/MM/yyyy"
        });
        $("#miniSaleRpToDate").kendoDatePicker({
            culture: "en-GB",
            value: new Date(),
            format: "dd/MM/yyyy"
        });
        
        $('#miniSaleRpFromDate').val(obj.From);
        $('#miniSaleRpToDate').val(obj.To);
        this.searchFilter = obj;
        $.ajax({
            url: baseUrl + '/Admin/Report/GetMiniSalesReport',
            data: $.postifyData(obj),
            type: "POST",
            success: function (data) {
                disableSubmit(false);
                const response = JSON.parse(data.result);
                if (response.length > 0) {
                    document.getElementById("miniSalesListing").innerHTML = "";
                    for (var i = 0; i < response.length; i++) {
                        const tr =
                            "<tr>" +
                                "<td style='text-align:center'>" + response[i].DateTime + "</td> " +
                                "<td style='text-align:center; font-weight:bold; color:deepskyblue;'>" + response[i].TAmount + "</td>" +
                            "</tr >";
                        var html = document.getElementById("miniSalesListing").innerHTML + tr;
                        document.getElementById("miniSalesListing").innerHTML = html;
                    }
                }
            },
            error: function () {
                disableSubmit(false);
            }
        });

    },

    search: function (sender) {
        disableSubmit(true);

        var from = $('#miniSaleRpFromDate').val();
        var to = $('#miniSaleRpToDate').val();
        this.searchFilter.From = from;
        this.searchFilter.To = to;
        $('#ToDate').val(to);
        $('#FromDate').val(from);
        this.setTitle(this.searchFilter.miniSaleRpType, this.searchFilter);
        document.getElementById("miniSalesListing").innerHTML = "";
        $.ajax({
            url: baseUrl + '/Admin/Report/GetMiniSalesReport',
            data: $.postifyData(this.searchFilter),
            type: "POST",
            success: function (data) {
                disableSubmit(false);
                const response = JSON.parse(data.result);
                if (response.length > 0) {
                    document.getElementById("miniSalesListing").innerHTML = "";
                    for (var i = 0; i < response.length; i++) {
                        const tr =
                            "<tr>" +
                            "<td style='text-align:center'>" + response[i].DateTime + "</td> " +
                            "<td style='text-align:center; font-weight:bold; color:deepskyblue;'>" + response[i].TAmount + "</td>" +
                            "</tr >";
                        var html = document.getElementById("miniSalesListing").innerHTML + tr;
                        document.getElementById("miniSalesListing").innerHTML = html;
                    }
                }
            },
            error: function () {
                disableSubmit(false);
            }
        });
    },

    setTitle: function (type, obj) {
        if (type === "daily") {
            $('#miniSalesReportTitle').html("DAILY SALES TOTAL " + obj.From + " - " + obj.To)
        } else if (type === "weekly") {
            $('#miniSalesReportTitle').html("WEEKLY SALES TOTAL " + obj.From + " - " + obj.To)
        } else
            $('#miniSalesReportTitle').html("MONTHLY SALES TOTAL " + obj.From + " - " + obj.To)
    }
}

var Deposits = {
    SortDeposits: function (sender) {
        if ($(sender).hasClass("sorting_asc")) {
            $('.sorting').removeClass("sorting_asc");
            $('.sorting').removeClass("sorting_desc")
            $(sender).addClass("sorting_desc");
            $('#SortBy').val($(sender).attr('data-sortby'));
            $('#SortOrder').val('Desc');
            paging.startIndex = 1;
            paging.currentPage = 0;
            Paging();
        } else {
            $('.sorting').removeClass("sorting_asc");
            $('.sorting').removeClass("sorting_desc")
            $(sender).addClass("sorting_asc");
            $('#SortBy').val($(sender).attr('data-sortby'));
            $('#SortOrder').val('Asc');
            paging.startIndex = 1;
            paging.currentPage = 0;
            Paging();
        }
    },

    ManageDeposits: function (totalCount) {
        var totalRecords = 0;
        totalRecords = parseInt(totalCount);
        //alert(totalRecords);
        PageNumbering(totalRecords);
    },

    SearchDeposits: function (sender) {
        paging.startIndex = 1;
        Paging(sender);

    },

    ShowRecords: function (sender) {

        paging.startIndex = 1;
        paging.pageSize = parseInt($(sender).find('option:selected').val());
        Paging(sender);

    },
    ApproveDeposit: function (sender) {
        $.ConfirmBox("", "Are you sure to approve this deposit?", null, true, "Yes", true, null, function () {
            $.ajaxExt({
                url: baseUrl + '/Admin/Deposit/ApproveDeposit',
                type: 'POST',
                validate: false,
                showErrorMessage: true,
                messageControl: $('div.messageAlert'),
                showThrobber: true,
                button: $(sender),
                throbberPosition: { my: "left center", at: "right center", of: $(sender) },
                data: { depositId: $(sender).attr("data-depositid") },
                success: function (results, message) {
                    $.ShowMessage($('div.messageAlert'), message, MessageType.Success);
                    Paging();
                }
            });
        });
    },
    RejectDeposit: function (sender) {
        $.ConfirmBox("", "Are you sure to reject this deposit?", null, true, "Yes", true, null, function () {
            $.ajaxExt({
                url: baseUrl + '/Admin/Deposit/RejectDeposit',
                type: 'POST',
                validate: false,
                showErrorMessage: true,
                messageControl: $('div.messageAlert'),
                showThrobber: true,
                button: $(sender),
                throbberPosition: { my: "left center", at: "right center", of: $(sender) },
                data: { depositId: $(sender).attr("data-depositid") },
                success: function (results, message) {
                    $.ShowMessage($('div.messageAlert'), message, MessageType.Success);
                    Paging();
                }
            });
        });
    },
    RejectReleaseDeposit: function (sender) {
        $.ConfirmBox("", "Are you sure to reject release of this deposit?", null, true, "Yes", true, null, function () {
            $.ajaxExt({
                url: baseUrl + '/Admin/Deposit/RejectReleaseDeposit',
                type: 'POST',
                validate: false,
                showErrorMessage: true,
                messageControl: $('div.messageAlert'),
                showThrobber: true,
                button: $(sender),
                throbberPosition: { my: "left center", at: "right center", of: $(sender) },
                data: { depositId: $(sender).attr("data-depositid") },
                success: function (results, message) {
                    $.ShowMessage($('div.messageAlert'), message, MessageType.Success);
                    Paging();
                }
            });
        });
    },
    ApproveReleaseDeposit: function (sender) {
        $.ConfirmBox("", "Are you sure to release this deposit?", null, true, "Yes", true, null, function () {
            $.ajaxExt({
                url: baseUrl + '/Admin/Deposit/ApproveReleaseDeposit',
                type: 'POST',
                validate: false,
                showErrorMessage: true,
                messageControl: $('div.messageAlert'),
                showThrobber: true,
                button: $(sender),
                throbberPosition: { my: "left center", at: "right center", of: $(sender) },
                data: { depositId: $(sender).attr("data-depositid") },
                success: function (results, message) {
                    $.ShowMessage($('div.messageAlert'), message, MessageType.Success);
                    Paging();
                }
            });
        });
    },
    AddDeposit: function (sender) {
        if (!$("#amount").val() || $("#amount").val() == "0") {
            $.ShowMessage($('div.messageAlert'), "Amount is required and amount must be greater then 0.", MessageType.Error);
            return;
        }
      else  if (!$("#ChkOrSlipNo").val()) {
            $.ShowMessage($('div.messageAlert'), "Cheque or Slip Id is required.", MessageType.Error);
            return;
        }
        else {
            $.ajaxExt({
                url: baseUrl + '/Admin/Deposit/AddDeposit',
                type: 'POST',
                validate: true,
                showErrorMessage: true,
                messageControl: $('div.messageAlert'),
                formToValidate: $(sender).parents("form:first"),
                formToPost: $(sender).parents("form:first"),
                isAjaxForm: true,
                showThrobber: true,
                button: $(sender),
                throbberPosition: { my: "left center", at: "right center", of: $(sender) },
                success: function (results, message) {
                    $.ShowMessage($('div.messageAlert'), message, MessageType.Success);
                    setTimeout(function () {
                        window.location.reload();
                    }, 1500);

                }
            });
        }

    },
};

function Paging(sender) {
    
    var obj = new Object();
    obj.Search = $('#Search').val();
    obj.PageNo = paging.startIndex;
    obj.RecordsPerPage = paging.pageSize;
    obj.SortBy = $('#SortBy').val();
    obj.SortOrder = $('#SortOrder').val();
    obj.SearchField = $('#searchField').val();

    obj.PosId = $('#pos').val();
    obj.VendorId = $('#vendor').val();
    obj.From = $('#FromDate').val();
    obj.Status = $('#status').val();

    obj.To = $('#ToDate').val();

    obj.ReportType = $("#reportType").val();
    obj.Meter = $('#meterNo').val();
    obj.TransactionId = $('#tranId').val();

    obj.Product = $('#productId').val();
    if (obj.From) {
        $("#fromSpan").text($("#FromDate").val())
    }
    else
        $("#fromSpan").text("_");

    $("#btnFilterSearch").val('DATA LOADING........');
    $("#btnFilterSearch").prop('disabled', true);

    if (obj.To)
    {
        $("#toSpan").text($("#ToDate").val())
    }
    else
        $("#toSpan").text("_");
    var printDt = new Date();



   
    const date = new Date();
    const formattedDate = date.toLocaleDateString('en-GB', {
        day: '2-digit', month: '2-digit', year: 'numeric'
    }).replace(/ /g, '-') + " " + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }).replace("AM", "").replace("PM", "");


    $("#printedDate").text(formattedDate);
    $("#PrintedDateServer").val(formattedDate);
    $.ajaxExt({
        type: "POST",
        validate: false,
        parentControl: $(sender).parents("form:first"),
        data: $.postifyData(obj),
        messageControl: null,
        showThrobber: false,
        throbberPosition: { my: "left center", at: "right center", of: sender, offset: "5 0" },
        url: baseUrl + '/Admin/Report/GetSalesReportsPagingList',
        success: async function (results, message)
        {
            const jsoneValue = JSON.parse(results);
            console.log('jsoneValue', jsoneValue)

            await initTable(jsoneValue.List);

            $("#btnFilterSearch").val('SEARCH');
            $("#btnFilterSearch").prop('disabled', false); 
            //PageNumbering(results[1]);
        },
        error: function (e) {
            $("#btnFilterSearch").val('SEARCH');
            $("#btnFilterSearch").prop('disabled', false); 
        }
    });
}

function getMonthName(number)
{
   
    var month = new Array();
    month[0] = "JAN";
    month[1] = "FEB";
    month[2] = "MAR";
    month[3] = "APR";
    month[4] = "MAY";
    month[5] = "JUN";
    month[6] = "JUL";
    month[7] = "AUG";
    month[8] = "SEP";
    month[9] = "OCT";
    month[10] = "NOV";
    month[11] = "DEC";
  //  return month[number];
    return number+1;
}


function disableSubmit(disabled = false) {
    if (disabled) {
        $("#miniSalesRPSearch").css({ backgroundColor: '#56bb96' });
        $("#miniSalesRPSearch").val('PROCESSING....');
        $("#miniSalesRPSearch").prop('disabled', true);
    } else {
        $("#miniSalesRPSearch").css({ backgroundColor: '#f1cf09' });
        $("#miniSalesRPSearch").val('Submit');
        $("#miniSalesRPSearch").prop('disabled', false);
    }


}

async function initTable(response) {
    const tableBody = document.getElementById("tableBody");

    tableBody.innerHTML = '';
    debugger
    for (var i = 0; i < response.length; i++) {
        const tr = document.createElement("tr");
        const falseValue = false;
        const token = response[i].RechargePin
            ? `<a href="javascript:void(0);" data-token="${response[i].RechargePin}" onclick="fetchSaleInformation('${response[i].RechargePin}', '${response[i].PlatformId}', true)" id="${response[i].RechargePin}">${response[i].RechargePin}</a>`
            : `<a href="javascript:void(0);" style="color: #ff6a00;" data-token="${response[i].TransactionId}" onclick="fetchVoucherStatusDialog('${response[i].TransactionId}', false, '${response[i].Amount}')" id="${response[i].TransactionId}">Request status for ${response[i].TransactionId}</a>`;

        tr.classList.add('odd', 'gradeX')

        let statusLabel = '';
        if (response[i].Paymentstatus === 'PENDING') {
            statusLabel = '<span style="color: orange; font-weight: bold;">PENDING</span>';
        } else if (response[i].Paymentstatus === 'DEDUCTED') {
            statusLabel = '<span style="color: green; font-weight: bold;">DEDUCTED</span>';
        } else if (response[i].Paymentstatus === 'REFUNDED') {
            statusLabel = '<span style="color: purple; font-weight: bold;">REFUNDED</span>';
        }else if (response[i].Paymentstatus === 'FAILED') {
            statusLabel = '<span style="color: purple; font-weight: bold;">FAILED</span>';
        }
        else {
            statusLabel = `<span style="color: gray; font-weight: bold;">UNKNOWN</span>`;
        }

        tr.innerHTML = `
            <td style="text-align:right;"> ${response[i].CreatedAtDate}</td>
            <td style="text-align:left;"> ${response[i].ProductShortName}</td>
            <td style="text-align:right;"> ${response[i].TransactionId}</td>
            <td style="text-align:right;"> ${response[i].MeterNumber}</td>
            <td> ${response[i].VendorName}</td>
            <td style="text-align:right;">  ${response[i].POSId}</td>

             <td style="text-align:center;">
                <strong>
                    <a href="javascript:void(0);" data-title="sd" data-token="${response[i].TransactionId}" onclick="GetRequestANDResponse('{{{  ${response[i].TransactionId}   }}}')" id="${response[i].RechargePin}">
                        {{{  ${response[i].TransactionId}   }}}
                    </a>
                </strong>
            </td>
             <td style="text-align:center;">
                <strong>
                   ${token}
                </strong>
             </td>
             <td style="text-align:left;"> ${statusLabel} </td>
            <td style="text-align:right;"> <strong> ${response[i].Amount}</strong></td>
        `;
        tableBody.appendChild(tr);
    }
}
