const validStatuses = [1, 293360000, 293360007, 293360001]; // planned, pending, pending close, pending po
const pollingIntervalSeconds = 2;
const pollingSubIntervalSeconds = 3;
let prevFlagValue = null;
let ufDocCount = 0;

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


function pollFlagToRefreshSubgird(executionContext) {
    //debugger;
    let formContext = executionContext.getFormContext();
    const intervalId = setInterval(pollFlagSubG, pollingSubIntervalSeconds * 1000, formContext);
    console.log(`Created fetch with interval id: ${intervalId}`)
}

function getxdocsUrl(varxcacheId) {
    //debugger;
    var fetchXML = `<fetch distinct="false" mapping="logical" returntotalrecordcount="true" page="1" count="10" no-lock="false">
  <entity name="sharepointdocument">
    <attribute name="documentid"/>
    <attribute name="absoluteurl"/>
    <attribute name="fullname"/>
    <attribute name="relativelocation"/>
    <attribute name="sharepointdocumentid"/>
    <order attribute="relativelocation" descending="false"/>
    <attribute name="modified"/>
    <attribute name="sharepointmodifiedby"/>
    <attribute name="sharepointcreatedon"/>
    <link-entity name="leap_case" from="leap_caseid" to="regardingobjectid" alias="bb">
      <filter type="and">
        <condition attribute="leap_caseid" operator="eq" uitype="leap_case" value="${varxcacheId}" />
      </filter>
    </link-entity>
  </entity>
</fetch>`;
    fetchXML = "?fetchXml=" + encodeURIComponent(fetchXML);

    return Xrm.WebApi.retrieveMultipleRecords("sharepointdocument", fetchXML).then(result => {
        if(result == null) {
            throw Error("Invalid operation result when attempting to retrieve case products for case.");
        }
        return result.entities;
    }).catch(error => {
        throw Error(`Error retrieving case products for case with id ${varxcacheId}: ${error.message}`);
    });
}


async function pollFlagSubG(formContext) {
    //debugger;
    var cpSubgrid = formContext.getControl("Subgrid_new_2"); 
    if (cpSubgrid) {
      ufDocCount = cpSubgrid.getGrid().getRows().getLength();
    }
    var cpViewSelector;
    var tabControl = Xrm.Page.ui.tabs.get("tab_3");
    if (tabControl) {
        cpViewSelector = cpSubgrid.getViewSelector();
    }  
     
    const status = formContext.getAttribute("statuscode").getValue();
    if(validStatuses.includes(status)) {
        const caseId = formContext.data?.entity?.getId();
        if(caseId && caseId != "") {
            try {
                console.log("Polling flag...");
                const currentFlagValue = await getxdocsUrl(caseId);
                console.log(`Current flag value: ${currentFlagValue.length}. Previous flag value: ${ufDocCount}`);
                if((currentFlagValue.length > 0) && (currentFlagValue.length != ufDocCount) ) {

                    cpSubgrid.refresh();
                    /*var mixedUFView = {
                        entityType: 9501,
                        id: "0016F9F3-41CC-4276-9D11-04308D15858D",
                        name: "Document Associated Grid"
                    };            
                    cpViewSelector.setCurrentView(mixedUFView);*/
                }
            } catch(error) {
                console.error(error.message);
            }
        } else {
            console.error("Refresh SubGrid Errror - unable to get case ID from form.");
        }
    } else {
        console.log("Skipping fetch due to status");
    }
}