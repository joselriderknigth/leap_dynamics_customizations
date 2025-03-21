const validStatuses = [1, 293360000, 293360007, 293360001]; // planned, pending, pending close, pending po
const pollingIntervalSeconds = 3;
let prevFlagValue = null;

function refreshWebResource(formContext) {
    const webResourceCtrl = formContext.getControl("WebResource_north52_quick_button_close_case");
    const webResourceSource = webResourceCtrl.getSrc();
    webResourceCtrl.setSrc(null);
    webResourceCtrl.setVisible(false);
    setTimeout(function () {
        webResourceCtrl.setSrc(webResourceSource);
        webResourceCtrl.setVisible(true);
    }, 500);

    console.log("Refresh Button refreshed");
}

function fetchCaseFlag(caseId) {
    return Xrm.WebApi.retrieveRecord("leap_case", caseId, "?$select=leap_hasproductpendingusageform")
        .then(result => {
            if (result == null) {
                throw Error("Refresh Button Errror - Invalid operation result when attempting to fetch case flag.");
            }
            return result.leap_hasproductpendingusageform;
        }).catch(error => {
            throw Error(`Refresh Button Errror - Error retrieving case with id ${caseId}: ${error.message}`);
        });
}

async function pollFlag(formContext) {
    const status = formContext.getAttribute("statuscode").getValue();
    if(validStatuses.includes(status)) {
        const caseId = formContext.data?.entity?.getId();
        if(caseId && caseId != "") {
            try {
                console.log("Polling flag...");
                const currentFlagValue = await fetchCaseFlag(caseId);
                console.log(`Current flag value: ${currentFlagValue}. Previous flag value: ${prevFlagValue}`);
                if(currentFlagValue != prevFlagValue) {
                    if(currentFlagValue == true) {
                        refreshWebResource(formContext);
                    }
                    prevFlagValue = currentFlagValue;
                }
            } catch(error) {
                console.error(error.message);
            }
        } else {
            console.error("Refresh Button Errror - unable to get case ID from form.");
        }
    } else {
        console.log("Skipping fetch due to status");
    }
}

function pollFlagToRefreshButton(executionContext) {
    let formContext = executionContext.getFormContext();
    const intervalId = setInterval(pollFlag, pollingIntervalSeconds * 1000, formContext);
    console.log(`Created fetch with interval id: ${intervalId}`)
    // clearInterval(intervalId);
}