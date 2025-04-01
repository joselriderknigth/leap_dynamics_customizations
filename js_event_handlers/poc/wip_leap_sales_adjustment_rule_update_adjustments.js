const RESULT_MESSAGES = {
    SUCCESS: "Sales Adjustments updated successfully.",
    FAILURE: "There were errors updating Sales Adjustments. Check console logs for details.",
};

function getSalesAdjustments(ruleId) {
    return Xrm.WebApi.retrieveMultipleRecords("leap_salesadjustment", ruleId)
        .then(result => {
            if (result == null) {
                throw Error("Refresh Button Errror - Invalid operation result when attempting to fetch case flag.");
            }
            return result.leap_hasproductpendingusageform;
        }).catch(error => {
            throw Error(`Refresh Button Errror - Error retrieving case with id ${caseId}: ${error.message}`);
        });
}

function showResultsDialog(successes, errors) {
    console.table(successes.concat(errors));
    const resultText = errors.length === 0 ? RESULT_MESSAGES.SUCCESS : RESULT_MESSAGES.FAILURE;

    const alertStrings = { confirmButtonLabel: 'Ok', text: resultText, title: 'Operation Result'};
    const alertOptions = { height: 120, width: 260 };
    
    return Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(function() {});
}

async function updateAdjustments(salesAdjustmentRuleId) {
    const successes = [];
    const errors = [];
    
    try {
        const salesAdjustments = await getSalesAdjustments(salesAdjustmentRuleId); // check if null and if .entities null
        const salesAdjustmentCount = salesAdjustments.entities.length;

        let processedCount = 0;
        Xrm.Utility.showProgressIndicator(`Updating Sales Adjustments - (${processedCount} / ${salesAdjustmentCount})`);
        for(let i = 0; i < salesAdjustments.entities.length; i++) {
            if(i % 5 === 0) Xrm.Utility.showProgressIndicator(`Updating Sales Adjustments - (${processedCount} / ${salesAdjustmentCount})`);
            
            try {
                const salesAdjustment = salesAdjustments.entities[i];
                await updateSalesAdjustment(salesAdjustment.leap_salesadjustmentid);

                successes.push({salesAdjustmentId: salesAdjustment.leap_salesadjustmentid, result: "Success"});
            } catch(caughtError) {
                errors.push({salesAdjustmentId: salesAdjustments.entities[i].leap_salesadjustmentid, result: caughtError.message});
            }
        }
    } catch(caughtError) {
        console.error("Error updating sales adjustments");
        errors.push({result: caughtError.message});
    } finally {
        showResultsDialog(successes, errors);
    }
}