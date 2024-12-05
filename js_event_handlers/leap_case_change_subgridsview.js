var formContext = null;
var caseProductCount = 0;
var usageFormCount = 0;
var cpSubgrid = null;
var ufSubgrid = null;

function caseProductOnLoadHandler() {
    if(formContext == null || cpSubgrid == null) {
        console.log('undefined form context or cp subgrid control');
        return 0;
    }

    if(cpSubgrid.getGrid().getTotalRecordCount() != caseProductCount) {
        var cpViewSelector = cpSubgrid.getViewSelector();
        var caseId = formContext.data.entity.getId();
        var defaultCPView = {
            entityType: 1039,
            id: "bd64a7ca-6f56-4242-bc99-69499919b9ab",
            name: "Active Case Products"
        };
        var cpFetchXML = `<fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
            <entity name="leap_caseproduct">
                <attribute name="leap_caseproductid" />
                <attribute name="leap_existingproduct" />
                <filter type="and">
                    <condition attribute="statecode" operator="eq" value="0" />
                    <condition attribute="leap_existingproduct" operator="eq" value="0" />
                    <condition attribute="leap_caseid" operator="eq" value="${caseId}"/>
                </filter>
            </entity>
        </fetch>`;
        cpFetchXML = "?fetchXml=" + encodeURIComponent(cpFetchXML);

        Xrm.WebApi.retrieveMultipleRecords("leap_caseproduct", cpFetchXML).then(
            function success(result) {
                if (result == null) {
                    console.log("Error retrieving case products for cse: " + caseId);
                } else {
                    if(result.entities.length > 0) {
                        var mixedCPView = {
                            entityType: 1039,
                            id: "f0459098-dda5-ef11-8a69-002248226325",
                            name: "Active Case Products With New And Existing Products"
                        };
                
                        cpViewSelector.setCurrentView(mixedCPView);
                    } else {
                        cpViewSelector.setCurrentView(defaultCPView);
                    }
                }
                caseProductCount = cpSubgrid.getGrid().getTotalRecordCount();
            },
            function (error) {
                console.log("Error retrieving case products for case: " + caseId + ' - ' + error);
                caseProductCount = cpSubgrid.getGrid().getTotalRecordCount();
            }
        );
    }
}

function usageFormOnLoadHandler() {
    if(formContext == null || ufSubgrid == null) {
        console.log('undefined form context or uf subgrid control');
        return 0;
    }

    if(cpSubgrid.getGrid().getTotalRecordCount() != usageFormCount) {
        var ufViewSelector = ufSubgrid.getViewSelector();
        var caseId = formContext.data.entity.getId();
        var defaultUFView = {
            entityType: 1039,
            id: "c2f09413-d0b8-434c-97fb-dd5820c508d1",
            name: "Usage Forms"
        };
        var ufFetchXML = `<fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
            <entity name="leap_caseproduct">
                <attribute name="leap_caseproductid" />
                <attribute name="leap_name" />
                <attribute name="leap_existingsupplier" />
                <order attribute="leap_name" descending="false" />
                <filter type="and">
                    <condition attribute="statecode" operator="eq" value="0" />
                    <condition attribute="leap_existingsupplier" operator="eq" value="0" />
                    <condition attribute="leap_caseid" operator="eq" value="${caseId}"/>
                </filter>
            </entity>
        </fetch>`;
        ufFetchXML = "?fetchXml=" + encodeURIComponent(ufFetchXML);

        Xrm.WebApi.retrieveMultipleRecords("leap_caseproduct", ufFetchXML).then(
            function success(result) {
                if (result == null) {
                    console.log("Error retrieving case products for case: " + caseId);
                } else {
                    console.log('new supplier case products: ', result.entities.length);
                    if(result.entities.length > 0) {
                        var mixedUFView = {
                            entityType: 1039,
                            id: "ddc4c3f4-dea5-ef11-8a69-002248226325",
                            name: "Usage Forms With New And Existing Suppliers"
                        };
                
                        ufViewSelector.setCurrentView(mixedUFView);
                    } else {
                        ufViewSelector.setCurrentView(defaultUFView);
                    }
                }
                usageFormCount = cpSubgrid.getGrid().getTotalRecordCount();
            },
            function (error) {
                console.log("Error retrieving case products for case: " + caseId + ' - ' + error);
                usageFormCount = cpSubgrid.getGrid().getTotalRecordCount();
            }
        );
    }
}

function registerSubgridsOnLoadHandlers(executionContext) {
    formContext = executionContext.getFormContext();
    cpSubgrid = formContext.getControl("CaseProducts");
    ufSubgrid = formContext.getControl("usageForms");

    caseProductCount = cpSubgrid.getGrid().getTotalRecordCount();
    usageFormCount = cpSubgrid.getGrid().getTotalRecordCount();

    cpSubgrid.addOnLoad(caseProductOnLoadHandler);
    ufSubgrid.addOnLoad(usageFormOnLoadHandler);
}