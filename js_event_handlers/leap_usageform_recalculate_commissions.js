const PROGRESS_MESSAGES = {
    DELETE: "Deleting Old Usage Form Details",
    CREATE: "Creating New Usage Form Details"
};

const RESULT_MESSAGES = {
    SUCCESS: "Commissions regenerated successfully.",
    MIXED: "Partial success regenerating commissions. Check logs for details.",
    FAILURE: "Failed to regenerate commissions. Check logs for details."
};

class UsageFormInfo {
    constructor(id, supplierId, supplierName, ownerId) {
        this.id = id;
        this.supplierId = supplierId;
        this.supplierName = supplierName;
        this.ownerId = ownerId;
    }
}

async function deleteUsageFormDetails(usageFormId) {
    var fetchXML = `<fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
        <entity name="leap_usageformdetail">
        <attribute name="leap_usageformdetailid" />
        <attribute name="leap_name" />
        <order attribute="leap_name" descending="false" />
        <filter type="and">
            <condition attribute="leap_usageform" operator="eq" value="${usageFormId}"/>
            <condition attribute="statecode" operator="eq" value="0" />
        </filter>
        </entity>
    </fetch>`;
    fetchXML = "?fetchXml=" + encodeURIComponent(fetchXML);

    try {
        const usageFormDetails = await Xrm.WebApi.retrieveMultipleRecords("leap_usageformdetail", fetchXML);
        if (usageFormDetails == null) {
            throw new Error("Invalid operation result when attempting to retrieve usage form details.");
        } else if(usageFormDetails.entities.length > 0) {
            const errors = [];
            for(const usageFormDetail of usageFormDetails.entities) {
                const usageFormDetailId = usageFormDetail.leap_usageformdetailid;
                try {
                    await Xrm.WebApi.deleteRecord("leap_usageformdetail", usageFormDetailId);
                    console.log("Deleted usage form detail with id: " + usageFormDetailId);
                } catch(error) {
                    errors.push({ id: usageFormDetailId, error: error.message });
                }
            }

            if (errors.length > 0) {
                console.table(errors);
                throw new Error("Some usage form details could not be deleted. Check logs for details.");
            }
        }
    } catch(error) {
        // console.error(`Error retrieving or deleting usage form details for usage form ID ${usageFormId}: ${error.message}`);
        throw error;
    }
}

function getUsageFormInfo(usageFormId) {
    return Xrm.WebApi.retrieveRecord("leap_usageform", usageFormId, "?$select=leap_newsuppliername&$expand=leap_Supplier($select=accountid),owninguser($select=systemuserid)")
        .then(result => {
            if (result == null) {
                throw Error("Invalid operation result when attempting to retrieve usage form information.");
            }
            return new UsageFormInfo(usageFormId, result.leap_Supplier.accountid, result.leap_newsuppliername, result.owninguser.systemuserid);
        }).catch(error => {
            throw Error(`Error retrieving usage form with with id ${usageFormId}: ${error.message}`);
        });
}

function processCaseProduct(usageFormInfo, caseProduct, processAsIo) {
    const usageFormDetailData = {
        "leap_UsageForm@odata.bind": `/leap_usageforms(${usageFormInfo.id})`,
        "owninguser@odata.bind": `/systemusers(${usageFormInfo.ownerId})`,
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
        "leap_catalognumber": caseProduct.leap_supplier != null ? caseProduct.leap_catalognumber : caseProduct.leap_newproductid,
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
                return result;
            }
        }).catch(error => {
            throw new Error(`Error creating usage form detail record for case product with id: ${caseProduct.leap_caseproductid}: ${error.message}`);
        });
}

async function createUsageFormDetails(usageFormInfo) {
    try {
        const supplierCondition = usageFormInfo.supplierId != null ?
            `<condition attribute="leap_supplier" operator="eq" value="${usageFormInfo.supplierId}"/>` :
            `<condition attribute="leap_newsuppliername" operator="eq" value="${usageFormInfo.supplierName}"/>`;
        var fetchXML = `<fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="true">
            <entity name="leap_caseproduct">
            <attribute name="leap_caseproductid" />
            <attribute name="leap_caseproductid"/>
            <attribute name="leap_supplier"/>
            <attribute name="leap_productid"/>
            <attribute name="leap_quantity"/>
            <attribute name="leap_io"/>
            <attribute name="leap_restock"/>
            <attribute name="leap_ioquantity" />
            <attribute name="leap_discountpercent" />
            <attribute name="leap_overrideprice" />
            <attribute name="leap_overridepricereason" />
            <attribute name="leap_unitprice" />
            <attribute name="leap_extendedprice" />
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
            <order attribute="leap_name" descending="false" />
            <filter type="and">
                <condition attribute="statecode" operator="eq" value="0" />
                ${supplierCondition}
            </filter>
            <link-entity name="leap_case" from="leap_caseid" to="leap_caseid" link-type="inner" alias="aa">
                <link-entity name="leap_usageform" from="leap_case" to="leap_caseid" link-type="inner" alias="ab">
                <filter type="and">
                    <condition attribute="leap_usageformid" operator="eq" value="${usageFormInfo.id}"/>
                </filter>
                </link-entity>
            </link-entity>
            </entity>
        </fetch>`;
        fetchXML = "?fetchXml=" + encodeURIComponent(fetchXML);

        const caseProducts = await Xrm.WebApi.retrieveMultipleRecords("leap_caseproduct", fetchXML);
        if(caseProducts == null) {
            throw new Error("Invalid operation result when attempting to retrieve case products.");
        } else if(caseProducts.entities.length == 0) {
            throw new Error("Found 0 valid case products to generate usage form details.");
        } else {
            const errors = [];
            for(const caseProduct of caseProducts.entities) {
                try {
                    if(!caseProduct.leap_io) {
                        await processCaseProduct(usageFormInfo, caseProduct, false);
                    } else {
                        if(caseProduct.leap_quantity > caseProduct.leap_ioquantity) {
                            await processCaseProduct(usageFormInfo, caseProduct, false); // non-io subset
                        }
                        await processCaseProduct(usageFormInfo, caseProduct, true);
                    }
                } catch(error) {
                    errors.push({ caseProductId: caseProduct.leap_caseproductid, error: error.message })
                }
            }

            if(errors.length > 0) {
                console.table(errors);
                throw new Error("Some usage form details could not be created. Check logs for details.");
            }
        }
    } catch (error) {
        // console.error(`Error retrieving case products or creating usage form details for usage form ID ${usageFormId}: ${error.message}`);
        throw error;
    }
}

function showResultsDialog(resultText) {
    const alertStrings = { confirmButtonLabel: 'Ok', text: resultText, title: 'Operation Result'};
    const alertOptions = { height: 120, width: 260 };
    
    return Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(function() {});
}


/*  recreating usage form details will recalculate commissions */
async function recreateUsageFormDetails(selectedControlSelectedItemIds) {
    console.log(`Triggered event handler - Parameter: ${selectedControlSelectedItemIds}`);
    const successes = [];
    const errors = [];
    for(const usageFormId of selectedControlSelectedItemIds) {
        try {
            Xrm.Utility.showProgressIndicator(PROGRESS_MESSAGES.DELETE);
            await deleteUsageFormDetails(usageFormId)
            const usageFormInfo = await getUsageFormInfo(usageFormId);
            Xrm.Utility.showProgressIndicator(PROGRESS_MESSAGES.CREATE);
            await createUsageFormDetails(usageFormInfo)

            successes.push({usageFormId: usageFormId, result: "Success"});
        } catch(caughtError) {
            errors.push({usageFormId: usageFormId, result: caughtError.message});
        }
    }

    Xrm.Utility.closeProgressIndicator();
    console.table(successes.concat(errors));

    if(successes.length == selectedControlSelectedItemIds.length) {
        showResultsDialog(RESULT_MESSAGES.SUCCESS);
    } else if(errors.length == selectedControlSelectedItemIds.length) {
        showResultsDialog(RESULT_MESSAGES.FAILURE);
    } else {
        showResultsDialog(RESULT_MESSAGES.MIXED);
    }
}
