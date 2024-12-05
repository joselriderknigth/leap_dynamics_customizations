async function checkMixedUsageForm(formContext) {
    var viewSelector = formContext.getControl("usageFormDetail").getViewSelector();
    var usageFormId = formContext.data.entity.getId();
    var fetchXML = `<fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
        <entity name="leap_caseproduct">
            <attribute name="leap_caseproductid" />
            <attribute name="leap_name" />
            <attribute name="leap_existingproduct" />
            <order attribute="leap_name" descending="false" />
            <filter type="and">
                <condition attribute="statecode" operator="eq" value="0" />
                <condition attribute="leap_existingproduct" operator="eq" value="0" />
            </filter>
            <link-entity name="leap_usageformdetail" from="leap_caseproduct" to="leap_caseproductid" link-type="inner" alias="aa">
                <filter type="and">
                    <condition attribute="leap_usageform" operator="eq" value="${usageFormId}"/>
                </filter>
            </link-entity>
        </entity>
    </fetch>`;
    fetchXML = "?fetchXml=" + encodeURIComponent(fetchXML);

    await Xrm.WebApi.retrieveMultipleRecords("leap_caseproduct", fetchXML).then(
        function success(result) {
            if (result == null) {
                console.log("Error retrieving case products for usage form: " + usageFormId);
            } else {
                if(result.entities.length > 0) {
                    var MixedView = {
                        entityType: 1039,
                        id: "8444d9cf-6ca3-ef11-8a69-002248226325",
                        name: "Active Usage Form Details With New And Existing Products (Associated)"
                    };
            
                    viewSelector.setCurrentView(MixedView);
                }
            }
        },
        function (error) {
            console.log("Error retrieving case products for usage form: " + usageFormId + ' - ' + error);
        }
    );
}

function fnChangeViewOfSubgrid(executionContext) {
    var formContext = executionContext.getFormContext();
    var supplierField = formContext.getAttribute("leap_supplier").getValue();
    var newSupplierField = formContext.getAttribute("leap_newsuppliername").getValue();
    var viewSelector = formContext.getControl("usageFormDetail").getViewSelector();
    var defaultView = viewSelector.getCurrentView();
    
    if(supplierField == null && (newSupplierField != null && newSupplierField != '')) {
        var NewView = {
            entityType: 1039,
            id: "a7f7dbf3-81a3-ef11-8a69-7c1e525ae22e",
            name: "Active Usage Form Details With New Supplier (Associated)"
        };

        viewSelector.setCurrentView(NewView);
    } else if(supplierField != null) {
        checkMixedUsageForm(formContext);
    } else {
        viewSelector.setCurrentView(defaultView);
    }
}