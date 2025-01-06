const PROGRESS_MESSAGE = "Generating Usage Form(s)";
var isTriggered = false;

const RESULT_MESSAGES = {
    SUCCESS: "Usage Form(s) have been created.",
    FAILURE: "Encountered an error while creating usage forms. Check logs for details."
};

class RestockInfo {
    constructor(address, requiredDate, primaryContact) {
        this.address = address;
        this.requiredDate = requiredDate;
        this.primaryContact = primaryContact;
    }
}

function getUsageFormCount(caseId) {
    var fetchXML =  `<fetch mapping="logical" >
        <entity name="leap_usageform">
        <attribute name="leap_usageformid"/>
        <filter type="and">
            <condition attribute="leap_case" operator="eq" value="${caseId}" />
            <condition attribute="statecode" operator="eq" value="0" />
        </filter>
        </entity>
    </fetch>`;
    fetchXML = "?fetchXml=" + encodeURIComponent(fetchXML);

    return Xrm.WebApi.retrieveMultipleRecords("leap_usageform", fetchXML).then(result => {
        if(result == null) {
            throw Error("Invalid operation result when attempting to retrieve existing usage forms for case.");
        }
        return result.entities.length;
    }).catch(error => {
        throw Error(`Error retrieving usage forms for case with id ${caseId}: ${error.message}`);
    });
}

function getCaseProducts(caseId) {
    var fetchXML =  `<fetch mapping="logical" >
        <entity name="leap_caseproduct">
        <attribute name="leap_caseproductid"/>
        <attribute name="leap_supplier"/>
        <attribute name="leap_productid"/>
        <attribute name="leap_quantity"/>
        <attribute name="leap_io"/>
        <attribute name="leap_restock"/>
        <attribute name="leap_restockaddressstreet1"/>
        <attribute name="leap_restockaddressstreet2"/>
        <attribute name="leap_restockaddressstreet3"/>
        <attribute name="leap_restockaddresscity"/>
        <attribute name="leap_restockaddressstate"/>
        <attribute name="leap_restockaddresszipcode"/>
        <attribute name="leap_restockaddresscountry"/>
        <attribute name="leap_restockrequireddate" />
        <attribute name="leap_restockaddressprimarycontact" />
        <attribute name="leap_ioquantity" />
        <attribute name="leap_discountpercent" />
            <attribute name="leap_overrideprice" />
            <attribute name="leap_overridepricereason" />
        <attribute name="leap_unitprice" />
        <attribute name="leap_caseextendedprice" />
        <attribute name="leap_producttype" />
        <attribute name="leap_catalognumber" />
        <attribute name="leap_lotnumber" />
        <attribute name="leap_serialnumber" />
        <attribute name="leap_expirationdate" />
        <attribute name="leap_unitpriceoverride" />
        <attribute name="leap_newsuppliername" />
        <attribute name="leap_newproductname" />
        <attribute name="leap_newproductid" />
        <attribute name="leap_unitpriceoverridereason" />
        <filter type="and">
                <condition attribute="leap_caseid" operator="eq" value="${caseId}" />
        </filter>
        <order attribute="leap_supplier" />
        <order attribute="leap_newsuppliername" />
        </entity>
    </fetch>`;
    fetchXML = "?fetchXml=" + encodeURIComponent(fetchXML);

    return Xrm.WebApi.retrieveMultipleRecords("leap_caseproduct", fetchXML).then(result => {
        if(result == null) {
            throw Error("Invalid operation result when attempting to retrieve case products for case.");
        }
        return result.entities;
    }).catch(error => {
        throw Error(`Error retrieving case products for case with id ${caseId}: ${error.message}`);
    });
}

function getCaseInfo(caseId) {
    return Xrm.WebApi.retrieveRecord("leap_case", caseId, "?$select=leap_signature,leap_signaturecapturedate,leap_signeetitle,leap_signeefullname&$expand=leap_Hospital($select=accountid),owninguser($select=systemuserid),leap_Surgeon($select=contactid)")
        .then(result => {
            if (result == null) {
                throw Error("Invalid operation result when attempting to retrieve case information.");
            }
            return result;
        }).catch(error => {
            throw Error(`Error retrieving case with with id ${caseId}: ${error.message}`);
        });
}

function getRestockInfo(caseProduct) {
    var address = caseProduct.leap_restockaddressstreet1 +
        (caseProduct.leap_restockaddressstreet2 ?? '') +
        (caseProduct.leap_restockaddressstreet3 ?? '') +
        ', ' +
        caseProduct.leap_restockaddresscity +
        ', ' +
        caseProduct.leap_restockaddressstate +
        ' ' +
        caseProduct.leap_restockaddresszipcode +
        (caseProduct.leap_restockaddresscountry != null ? (', ' + caseProduct.leap_restockaddresscountry) : '');
    return new RestockInfo(address, caseProduct.leap_restockrequireddate, caseProduct.leap_restockaddressprimarycontact);
}

function createUsageForm(caseProduct, caseInfo) {
    const restockInfo = caseProduct.leap_restock == 1 ? getRestockInfo(caseProduct) : null;
    const usageFormData = {
        "leap_Case@odata.bind": `/leap_cases(${caseInfo.leap_caseid})`,
        "leap_Hospital@odata.bind": `/accounts(${caseInfo.leap_Hospital.accountid})`,
        ...(caseProduct._leap_supplier_value != null && {"leap_Supplier@odata.bind": `/accounts(${caseProduct._leap_supplier_value})`}),
        "leap_Surgeon@odata.bind": `/contacts(${caseInfo.leap_Surgeon.contactid})`,
        "owninguser@odata.bind": `/systemusers(${caseInfo.owninguser.systemuserid})`,
        "leap_newsuppliername": caseProduct.leap_newsuppliername,
        "leap_signature": caseInfo.leap_signature,
        "leap_signaturecapturedate": caseInfo.leap_signaturecapturedate,
        "leap_signeetitle": caseProduct.leap_signeetitle,
        "leap_signeefullname": caseProduct.leap_signeefullname,
        ...(restockInfo && {"leap_shiptoaddress": restockInfo.address}),
        ...(restockInfo && {"leap_requireddate": restockInfo.requiredDate}),
        ...(restockInfo && {"leap_primarycontact": restockInfo.primaryContact}),
        "leap_instrumentcharge": 0.00,
        "leap_shippingdelivery": 0.00
    }

    return Xrm.WebApi.createRecord("leap_usageform", usageFormData)
        .then(result => {
            if (result == null) {
                throw new Error("Invalid operation result when attempting to create usage form");
            } else {
                console.log(`Created usage form with id: ${result.id}`);
                return result.id;
            }
        }).catch(error => {
            throw new Error(`Error creating usage form record for case product with id: ${caseProduct.leap_caseproductid}: ${error.message}`);
        });
}

function createUsageFormDetail(caseProduct, caseInfo, currentUsageForm, processAsIo) {
    const usageFormDetailData = {
        "leap_UsageForm@odata.bind": `/leap_usageforms(${currentUsageForm})`,
        "owninguser@odata.bind": `/systemusers(${caseInfo.owninguser.systemuserid})`,
        "leap_CaseProduct@odata.bind": `/leap_caseproducts(${caseProduct.leap_caseproductid})`,
        ...(caseProduct._leap_productid_value != null && {"leap_Product@odata.bind": `/products(${caseProduct._leap_productid_value})`}),
        ...(caseProduct._leap_supplier_value != null && {"leap_Supplier@odata.bind": `/accounts(${caseProduct._leap_supplier_value})`}),
        "leap_newproductname": caseProduct.leap_newproductname,
        "leap_newsuppliername": caseProduct.leap_newsuppliername,
        "leap_qty": processAsIo ? caseProduct.leap_ioquantity : (caseProduct.leap_io ? caseProduct.leap_quantity - caseProduct.leap_ioquantity : caseProduct.leap_quantity),
        "leap_io": processAsIo,
        "leap_unitpriceoverride": caseProduct.leap_unitpriceoverride,
        "leap_unitpriceoverridereason": caseProduct.leap_unitpriceoverridereason,
        "leap_restock": caseProduct.leap_restock,
        "leap_producttype": caseProduct.leap_producttype,
        "leap_catalognumber": caseProduct._leap_supplier_value != null ? caseProduct.leap_catalognumber : caseProduct.leap_newproductid,
        "leap_lotnumber": caseProduct.leap_lotnumber,
        "leap_serialnumber": caseProduct.leap_serialnumber,
        "leap_expirationdate": caseProduct.leap_expirationdate,
        "leap_unitprice": caseProduct.leap_unitprice,
        "leap_extendedprice": caseProduct.leap_extendedprice
    }

    return Xrm.WebApi.createRecord("leap_usageformdetail", usageFormDetailData)
        .then(result => {
            if (result == null) {
                throw new Error("Invalid operation result when attempting to create usage form detail");
            } else {
                console.log(`Created usage form detail with id: ${result.id}`);
                return result.id;
            }
        }).catch(error => {
            throw new Error(`Error creating usage form detail record for case product with id: ${caseProduct.leap_caseproductid}: ${error.message}`);
        });
}

async function generateUsageForms(executionContext) {
    if(!isTriggered) {
        isTriggered = true;
        const formContext = executionContext.getFormContext();
        const signatureCaptureDate = formContext.getAttribute("leap_signaturecapturedate");
        const statusCode = formContext.getAttribute("statuscode");

        if(signatureCaptureDate?.getValue() != null && (statusCode?.getValue() == 293360000 || statusCode.getValue() == 1)) {
            const caseId = formContext.data.entity.getId();
            if (caseId == null) {
                console.error("Error generating usage forms - Unable to get case ID to fetch case products.");
                showResultsDialog(RESULT_MESSAGES.FAILURE);
                return;
            }

            Xrm.Utility.showProgressIndicator(PROGRESS_MESSAGE);
            try {
                const usageFormCount = await getUsageFormCount(caseId);
                if(usageFormCount > 0) {
                    console.warn(`Aborted usage form generation -  case already has ${usageFormCount} usage forms.`);
                    return;
                }

                const caseInfo = await getCaseInfo(caseId);
                const caseProducts = await getCaseProducts(caseId);
                let prevUsageForm = null;
                let prevSupplier = null;
                for(const caseProduct of caseProducts) {
                    let currentSupplier = caseProduct._leap_supplier_value;
                    let currentNewSupplier = caseProduct.leap_newsuppliername;
                    if((currentSupplier != null && currentSupplier != prevSupplier) || (currentNewSupplier != null && currentNewSupplier != prevSupplier)) {
                        prevSupplier = currentSupplier ?? currentNewSupplier;
                        prevUsageForm = await createUsageForm(caseProduct, caseInfo);
                    }

                    if(!caseProduct.leap_io) {
                        await createUsageFormDetail(caseProduct, caseInfo, prevUsageForm, false);
                    } else {
                        if(caseProduct.leap_quantity > caseProduct.leap_ioquantity) {
                            await createUsageFormDetail(caseProduct, caseInfo, prevUsageForm, false);
                        }
                        await createUsageFormDetail(caseProduct, caseInfo, prevUsageForm, true);
                    }
                }

                console.log("Successfully processed all case products.");
                
                await formContext.data.refresh(false);
                
                const webResource = formContext.getControl("WebResource_north52_quick_button_close_case");
                if (webResource == null) {
                    throw Error("Could not find WebResource_north52_quick_button_close_case web resource");
                }

                var webResourceSrc = webResource.getSrc();
                webResource.setSrc("about:blank");
                // webResource.setSrc(webResourceSrc);
                setTimeout(function(){
                    webResource.setSrc(webResourceSrc);
                }, 1000);

                await formContext.data.refresh(false);

                formContext.ui.clearFormNotification('res');
                formContext.ui.setFormNotification(RESULT_MESSAGES.SUCCESS, 'INFO', 'res');
            } catch(error) {
                console.error(`Error generating usage forms - ${error.message}`);
                formContext.ui.clearFormNotification('res');
                formContext.ui.setFormNotification(RESULT_MESSAGES.FAILURE, 'ERROR', 'res');
                executionContext.getEventArgs().preventDefault();
            } finally {
                Xrm.Utility.closeProgressIndicator();
            }
        }
    }
}