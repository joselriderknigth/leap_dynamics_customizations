function addAccountTypeFilter(executionContext) {
    var formContext = executionContext.getFormContext();
    // var recordId = formContext.data.entity.getId();
    
    // if (!recordId) {
    var lookupControl = formContext.getControl("leap_commissionpayee");
    if (lookupControl) {
        lookupControl.addPreSearch(function () {
        var fetchXmlFilter = "<filter type='and'>" +
                "<condition attribute='statecode' operator='eq' value='0' />"+
                "<filter type='or'>" +
                    "<condition attribute='customertypecode' operator='eq' value='293360000' />" +
                    "<condition attribute='customertypecode' operator='eq' value='293360002' />" +
                "</filter>" +
            "</filter>";
        lookupControl.addCustomFilter(fetchXmlFilter, "account");
        });
    }
    // } 
}