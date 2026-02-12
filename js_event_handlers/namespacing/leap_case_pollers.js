window.Case = window.Case || {};
Case.InformationForm = Case.InformationForm || {};
Case.InformationForm.Pollers = Case.InformationForm.Pollers || {};

(function(ns) {
    const validStatuses = [1, 293360000, 293360007, 293360001,293360002,293360005]; // planned, pending, pending close, pending po, commission receivable, commission payable
    const pollingIntervalSeconds = 2;
    const pollingSubIntervalSeconds = 3;
    let prevPendingUFFlagValue = null;
    let prevNeedsAdminFlagValue = null;
    let prevCommsProcessedFlagValue = null;
    let prevCaseRegenFlagValue = null;
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

        console.log("Case button refreshed");
    }

    function fetchCaseFlags(caseId) {
        return Xrm.WebApi.retrieveRecord("leap_case", caseId, "?$select=leap_hasproductpendingusageform,leap_needsreset,leap_allusageformscomplete,leap_regeneratecase")
            .then(result => {
                if (result == null) {
                    throw Error("Refresh Button Errror - Invalid operation result when attempting to fetch case flags.");
                }
                return result;
            }).catch(error => {
                throw Error(`Refresh Button Errror - Error retrieving case with id ${caseId}: ${error.message}`);
            });
    }

    async function pollFlag(formContext, ufSubgrid, ufSubgridHO) {
       debugger;
        const status = formContext.getAttribute("statuscode").getValue();
        if(validStatuses.includes(status)) {
            const caseId = formContext && formContext.data && formContext.data.entity
                ? formContext.data.entity.getId()
                : null;
            if(caseId && caseId != "") {
                try {
                    const currentFlags = await fetchCaseFlags(caseId);
                    const hasRegenFlagFlipped = currentFlags.leap_hasproductpendingusageform !== prevPendingUFFlagValue;
                    const needsResetFlagFlipped = !currentFlags.leap_needsreset && (currentFlags.leap_needsreset!== prevNeedsAdminFlagValue);
                    const allCommsFlagFlipped = currentFlags.leap_allusageformscomplete !== prevCommsProcessedFlagValue;
                    const caseRegenFlagFlipped = currentFlags.leap_regeneratecase && (currentFlags.leap_regeneratecase !== prevCaseRegenFlagValue);

                    if(hasRegenFlagFlipped || needsResetFlagFlipped || allCommsFlagFlipped || caseRegenFlagFlipped) {
                        console.log(`Current flag values:
                            Needs Regen (${currentFlags.leap_hasproductpendingusageform}),
                            Needs Adming (${currentFlags.leap_needsreset}),
                            Commissions Processed (${currentFlags.leap_allusageformscomplete}),
                            Case Regenerate (${currentFlags.leap_regeneratecase}). 
                            Previous flag values:
                            Needs Regen (${prevPendingUFFlagValue}),
                            Needs Adming (${prevNeedsAdminFlagValue}),
                            Commissions Processed (${prevCommsProcessedFlagValue}),
                            Case Regenerate (${prevCaseRegenFlagValue})`);

                        const isMidProcessGlitch =
                            prevPendingUFFlagValue === true &&
                            currentFlags.leap_hasproductpendingusageform === true &&
                            prevCommsProcessedFlagValue === true &&
                            currentFlags.leap_allusageformscomplete === false &&
                            needsResetFlagFlipped === false;

                        if (!isMidProcessGlitch && (
                            hasRegenFlagFlipped ||
                            needsResetFlagFlipped ||
                            allCommsFlagFlipped ||
                            caseRegenFlagFlipped
                        )) {
                            refreshWebResource(formContext);
                        }

                        if (hasRegenFlagFlipped && !currentFlags.leap_hasproductpendingusageform) {
                            ufSubgrid.refresh();
                            ufSubgridHO.refresh();
                            console.log("Refreshed UF subgrid");
                        }

                        prevPendingUFFlagValue = currentFlags.leap_hasproductpendingusageform;
                        prevNeedsAdminFlagValue = currentFlags.leap_needsreset;
                        prevCommsProcessedFlagValue = currentFlags.leap_allusageformscomplete;
                        prevCaseRegenFlagValue = currentFlags.leap_regeneratecase;
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

    function getxdocsUrl(varxcacheId) {
        debugger;
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


    async function pollFlagSubG(formContext, cpSubgrid) {
        debugger;
        const status = formContext.getAttribute("statuscode").getValue();
        if(validStatuses.includes(status)) {
            const caseId = formContext && formContext.data && formContext.data.entity
                ? formContext.data.entity.getId()
                : null;
            if(caseId && caseId != "") {
                try {
                    const currentFlagValue = await getxdocsUrl(caseId);
                    if((currentFlagValue.length > 0) && (currentFlagValue.length != ufDocCount) ) {
                        console.log(`Current PDF count: ${currentFlagValue.length}. Previous PDF count: ${ufDocCount}`);
                        cpSubgrid.refresh();
                        console.log("UF Document subgrid refreshed");
                    }
                    ufDocCount = currentFlagValue.length;
                } catch(error) {
                    console.error(error.message);
                }
            } else {
                console.error("Refresh SubGrid Error - unable to get case ID from form.");
            }
        } else {
            console.log("Skipping fetch due to status");
        }
    }

    ns.pollFlagToRefreshButton = function (executionContext) {
        let formContext = executionContext.getFormContext();
        let ufSubgrid = formContext.getControl("usageForms");
        let ufSubgridHO = formContext.getControl("HospitalOwned");
        const intervalId = setInterval(pollFlag, pollingIntervalSeconds * 1000, formContext, ufSubgrid, ufSubgridHO);        
        console.log(`Flag poller has interval id: ${intervalId}`);
        console.log("Polling flag...");
    };

    ns.pollFlagToRefreshSubgrid = function (executionContext) {
        let formContext = executionContext.getFormContext();
        var cpSubgrid = formContext.getControl("Subgrid_new_2");
        if (cpSubgrid) {
            ufDocCount = cpSubgrid.getGrid().getRows().getLength();
            const intervalId = setInterval(pollFlagSubG, pollingSubIntervalSeconds * 1000, formContext, cpSubgrid);
            console.log(`UF Doc poller has interval id: ${intervalId}`);
        } else {
            console.error("Unable to get case product subgrid");
        }
    };

})(Case.InformationForm.Pollers);