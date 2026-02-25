const PROGRESS_MESSAGES = {
    DELETE: "Deactivating Old UFDs",
    CREATE: "Creating New UFDs"
};

const PROGRESSRSC_MESSAGES = {
    DELETE: "Deactivating Old Sales Rep Commissions",
    CREATE: "Creating New Sales Rep Commissions"
};

const RESULT_MESSAGES = {
    SUCCESS: "UFDs regenerated successfully.",
    MIXED: "Partial success regenerating UFDs. Check console logs for details.",
    FAILURE: "Failed to regenerate UFDs. Check console logs for details."
};

const RESULTRSC_MESSAGES = {
    SUCCESS: "Sales Rep Commissions regenerated successfully.",
    MIXED: "Partial success regenerating Sales Rep Commissions. Check console logs for details.",
    FAILURE: "Failed to regenerate Sales Rep Commissions. Check console logs for details."
};

class UsageFormInfo {
    constructor(id, supplierId, supplierName, ownerId, poNumber, howned, caseDate) {
        this.id = id;
        this.supplierId = supplierId;
        this.supplierName = supplierName;
        this.ownerId = ownerId;
        this.poNumber = poNumber;
        this.howned = howned;
        this.caseDate = caseDate;
    }
}

const deactivateData = {
    "statecode": 1,
    "statuscode": 2
}

async function deactivateOldSalesAdjustments(usageFormDetailId) {
    var fetchXML = `<fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
        <entity name="leap_salesadjustment">
        <attribute name="leap_salesadjustmentid" />
        <attribute name="leap_usageformdetail" />
        <filter type="and">
            <condition attribute="leap_usageformdetail" operator="eq" value="${usageFormDetailId}"/>
            <condition attribute="statecode" operator="eq" value="0" />
        </filter>
        </entity>
    </fetch>`;
    fetchXML = "?fetchXml=" + encodeURIComponent(fetchXML);

    const successes = [];
    const errors = [];

    try {
        const salesAdjustments = await Xrm.WebApi.retrieveMultipleRecords("leap_salesadjustment", fetchXML);
        if (salesAdjustments == null) {
            throw new Error("Invalid operation result when attempting to fetch sales adjustments.");
        } else if(salesAdjustments.entities?.length > 0) {
            const promises = salesAdjustments.entities.map(async (salesAdjustment) => {
                try {
                    const result = await Xrm.WebApi.updateRecord("leap_salesadjustment", salesAdjustment.leap_salesadjustmentid, deactivateData);
                    if (result == null) {
                        throw Error(`Invalid operation result when attempting to deactivate sales adjustment with id ${salesAdjustment.leap_salesadjustmentid}.`);
                    } else {
                        successes.push(salesAdjustment.leap_salesadjustmentid);
                    }
                } catch(error) {
                    console.error(`Error updating sales adjustment with id ${salesAdjustment.leap_salesadjustmentid} to deactivate`);
                    errors.push(salesAdjustment.leap_salesadjustmentid);
                }
            });

            await Promise.all(promises); // process deactivation in parallel

            const result = {type: "Sales Adjustments", successes: successes, errors: errors};
            console.log(result);
            return result;
        }
    } catch {
        throw new Error(`Error deactivating sales adjustments for usage form detail ${usageFormDetailId}: ${error.message}`);
    }
}

async function deactivateOldSupplierCommissions(usageFormDetailId) {
    var fetchXML = `<fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
        <entity name="leap_suppliercommission">
        <attribute name="leap_suppliercommissionid" />
        <attribute name="leap_usageformdetail" />
        <filter type="and">
            <condition attribute="leap_usageformdetail" operator="eq" value="${usageFormDetailId}"/>
            <condition attribute="statecode" operator="eq" value="0" />
        </filter>
        </entity>
    </fetch>`;
    fetchXML = "?fetchXml=" + encodeURIComponent(fetchXML);

    const successes = [];
    const errors = [];

    try {
        const supplierCommissions = await Xrm.WebApi.retrieveMultipleRecords("leap_suppliercommission", fetchXML);
        if (supplierCommissions == null) {
            throw new Error("Invalid operation result when attempting to fetch supplier commissions.");
        } else if(supplierCommissions.entities?.length > 0) {
            const promises = supplierCommissions.entities.map(async (supplierCommission) => {
                try {
                    const result = await Xrm.WebApi.updateRecord("leap_suppliercommission", supplierCommission.leap_suppliercommissionid, deactivateData);
                    if (result == null) {
                        throw Error(`Invalid operation result when attempting to deactivate supplier commission with id ${supplierCommission.leap_suppliercommissionid}.`);
                    } else {
                        successes.push(supplierCommission.leap_suppliercommissionid);
                    }
                } catch(error) {
                    console.error(`Error updating supplier commission with id ${supplierCommission.leap_suppliercommissionid} to deactivate`);
                    errors.push(supplierCommission.leap_suppliercommissionid);
                }
            });

            await Promise.all(promises); // process deactivation in parallel

            const result = {type: "Supplier Commissions", successes: successes, errors: errors};
            console.log(result);
            return result;
        }
    } catch {
        throw new Error(`Error deactivating supplier commissions for usage form detail ${usageFormDetailId}: ${error.message}`);
    }
}

async function deactivateOldSalesRepCommissions(usageFormDetailId) {
    var fetchXML = `<fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
        <entity name="leap_salesrepcommission">
        <attribute name="leap_salesrepcommissionid" />
        <attribute name="leap_usageformdetailid" />
        <filter type="and">
            <condition attribute="leap_usageformdetailid" operator="eq" value="${usageFormDetailId}"/>
            <condition attribute="statecode" operator="eq" value="0" />
        </filter>
        </entity>
    </fetch>`;
    fetchXML = "?fetchXml=" + encodeURIComponent(fetchXML);

    const successes = [];
    const errors = [];

    try {
        const salesRepCommissions = await Xrm.WebApi.retrieveMultipleRecords("leap_salesrepcommission", fetchXML);
        if (salesRepCommissions == null) {
            throw new Error("Invalid operation result when attempting to fetch sales rep commissions.");
        } else if(salesRepCommissions.entities?.length > 0) {
            const promises = salesRepCommissions.entities.map(async (salesRepCommission) => {
                try {
                    const result = await Xrm.WebApi.updateRecord("leap_salesrepcommission", salesRepCommission.leap_salesrepcommissionid, deactivateData);
                    if (result == null) {
                        throw Error(`Invalid operation result when attempting to deactivate sales rep commission with id ${salesRepCommission.leap_salesrepcommissionid}.`);
                    } else {
                        successes.push(salesRepCommission.leap_salesrepcommissionid);
                    }
                } catch(error) {
                    console.error(`Error updating sales rep commission with id ${salesRepCommission.leap_salesrepcommissionid} to deactivate`);
                    errors.push(salesRepCommission.leap_salesrepcommissionid);
                }
            });

            await Promise.all(promises); // process deactivation in parallel

            const result = {type: "Sales Rep Commissions", successes: successes, errors: errors};
            console.log(result);
            return result;
        }
    } catch {
        throw new Error(`Error deactivating sales rep commissions for usage form detail ${usageFormDetailId}: ${error.message}`);
    }
}

async function deactivateOldUsageFormLogs(usageFormId) {
    var fetchXML = `<fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
        <entity name="leap_log">
            <attribute name="leap_logid" />
            <attribute name="leap_name" />
            <attribute name="createdon" />
            <order attribute="leap_name" descending="false" />
            <filter type="and">
            <condition attribute="statecode" operator="eq" value="0" />
            <condition attribute="leap_usageform" operator="eq" value="${usageFormId}" />
            </filter>
        </entity>
        </fetch>`;
    fetchXML = "?fetchXml=" + encodeURIComponent(fetchXML);

    const successes = [];
    const errors = [];

    try {
        const logs = await Xrm.WebApi.retrieveMultipleRecords("leap_log", fetchXML);
        if (logs == null) {
            throw new Error("Invalid operation result when attempting to fetch usage form Logs.");
        } else if(logs.entities?.length > 0) {
            console.log("Deactivating logs for usage form with id: " + usageFormId);

            const promises = logs.entities.map(async (log) => {
                try {
                    const result = await Xrm.WebApi.updateRecord("leap_log", log.leap_logid, deactivateData);
                    if (result == null) {
                        throw Error(`Invalid operation result when attempting to deactivate log with id ${log.leap_logid}.`);
                    } else {
                        successes.push(log.leap_logid);
                    }
                } catch(error) {
                    console.error(`Error updating log with id ${log.leap_logid} to deactivate`);
                    errors.push(log.leap_logid);
                }
            });

            await Promise.all(promises); // wait until all logs are deactivated (in parallel)

            const result = {type: "Log deactivation results", successes: successes, errors: errors};
            console.log(result);
            return result;
        }
    } catch(error) {
        throw new Error(`Error deactivating logs for usage form ${usageFormId}: ${error.message}`);
    }
}

async function deactivateUsageFormDetails(usageFormId) {
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
                    await deactivateOldSalesAdjustments(usageFormDetailId);
                    await deactivateOldSupplierCommissions(usageFormDetailId);
                    await deactivateOldSalesRepCommissions(usageFormDetailId);
                    await Xrm.WebApi.updateRecord("leap_usageformdetail", usageFormDetailId, deactivateData)
                        .then(result => {
                            if(result == null) {
                                throw new Error(`Invalid operation result when attempting to deactivate usage form detail with id ${usageFormDetailId}.`);
                            } else {
                                console.log("Deactivated usage form detail with id: " + usageFormDetailId);
                            }
                        });
                    
                } catch(error) {
                    errors.push({ id: usageFormDetailId, error: error.message });
                }
            }

            if (errors.length > 0) {
                console.table(errors);
                throw new Error("Some usage form details could not be deactivated. Check logs for details.");
            }
        }
    } catch(error) {
        // console.error(`Error retrieving or deleting usage form details for usage form ID ${usageFormId}: ${error.message}`);
        throw error;
    }
}

function getUsageFormInfo(usageFormId) {
    return Xrm.WebApi.retrieveRecord("leap_usageform", usageFormId, "?$select=leap_newsuppliername,leap_purchaseordernumber,leap_hospitalowned,leap_surgerydate&$expand=leap_Supplier($select=accountid),owninguser($select=systemuserid)")
        .then(result => {
            if (result == null) {
                throw Error("Invalid operation result when attempting to retrieve usage form information.");
            }
            return new UsageFormInfo(result.leap_usageformid, result.leap_Supplier.accountid, result.leap_newsuppliername, result.owninguser.systemuserid, result.leap_purchaseordernumber, result.leap_hospitalowned, result.leap_surgerydate);
        }).catch(error => {
            throw Error(`Error retrieving usage form with with id ${usageFormId}: ${error.message}`);
        });
}

function processCaseProduct(usageFormInfo, caseProduct, processAsIo) {
    const usageFormDetailData = {
        "leap_UsageForm@odata.bind": `/leap_usageforms(${usageFormInfo.id})`,
        "owninguser@odata.bind": `/systemusers(${usageFormInfo.ownerId})`,
        "ownerid@odata.bind": `/systemusers(${usageFormInfo.ownerId})`,
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
        "leap_hospitalowned": caseProduct.leap_hospitalowned,        
        "leap_producttype": caseProduct.leap_producttype,
        ...(processAsIo && {"leap_discountpercent": caseProduct.leap_discountpercent}),
        ...(processAsIo && {"leap_overrideprice": caseProduct.leap_overrideprice}),
        ...(processAsIo && {"leap_overridepricereason": caseProduct.leap_overridepricereason}),
        "leap_catalognumber": caseProduct._leap_productid_value != null ? caseProduct.leap_catalognumber : caseProduct.leap_newproductid,
        "leap_lotnumber": caseProduct.leap_lotnumber,
        "leap_serialnumber": caseProduct.leap_serialnumber,
        "leap_expirationdate": caseProduct.leap_expirationdate,
        "leap_unitprice": caseProduct.leap_unitprice,
        "leap_extendedprice": caseProduct.leap_caseextendedprice, //  caseProduct.leap_extendedprice is deprecated so use this one
        "leap_casedate": usageFormInfo.caseDate,
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
         debugger;
        const supplierCondition = usageFormInfo.supplierId != null ?
            `<condition attribute="leap_supplier" operator="eq" value="${usageFormInfo.supplierId}"/>` :
            `<condition attribute="leap_newsuppliername" operator="eq" value="${usageFormInfo.supplierName}"/>`;
        const hoCondition = usageFormInfo.howned == true ?
             `<condition attribute="leap_hospitalowned" operator="eq" value="${usageFormInfo.howned}"/>` :
             `<filter type="or">
                 <condition attribute="leap_hospitalowned" operator="eq" value="0" />
                 <condition attribute="leap_hospitalowned" operator="null" />
              </filter>`;
        var fetchXML = `<fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="true">
            <entity name="leap_caseproduct">
            <attribute name="leap_caseproductid" />
            <attribute name="leap_supplier"/>
            <attribute name="leap_productid"/>
            <attribute name="leap_quantity"/>
            <attribute name="leap_io"/>
            <attribute name="leap_restock"/>
            <attribute name="leap_hospitalowned"/>            
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
            <order attribute="leap_name" descending="false" />
            <filter type="and">
                <condition attribute="statecode" operator="eq" value="0" />
                ${supplierCondition}
                 ${hoCondition}
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


/* Shown with list of usage forms */
/*  recreating usage form details will recalculate adjustments and commissions */
async function recreateUsageFormDetails(selectedControlSelectedItemIds) {
    const successes = [];
    const errors = [];
    for(const usageFormId of selectedControlSelectedItemIds) {
        try {
            Xrm.Utility.showProgressIndicator(PROGRESS_MESSAGES.DELETE);
            await deactivateUsageFormDetails(usageFormId)
            const usageFormInfo = await getUsageFormInfo(usageFormId);
            Xrm.Utility.showProgressIndicator(PROGRESS_MESSAGES.CREATE);
            await createUsageFormDetails(usageFormInfo)

          const usageFormUpda = await updateFieldWebApi(usageFormId);

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



/* Shown on usage form form */
/*  recreating usage form details will recalculate adjustments and commissions */
async function recreateUsageFormDetailsSingle(usageFormId) {
    let success = true;
    const errors = [];
    try {
        Xrm.Utility.showProgressIndicator(PROGRESS_MESSAGES.DELETE);
        await deactivateUsageFormDetails(usageFormId)
        const usageFormInfo = await getUsageFormInfo(usageFormId);
        Xrm.Utility.showProgressIndicator(PROGRESS_MESSAGES.CREATE);
        await createUsageFormDetails(usageFormInfo)
    } catch (caughtError) {
        success = false;
    }

    Xrm.Utility.closeProgressIndicator();
    if (success) {
        showResultsDialog(RESULT_MESSAGES.SUCCESS);
    } else {
        showResultsDialog(RESULT_MESSAGES.FAILURE);
    }
}


/*---------- ticket JIRA 972 ---------------*/
async function recreateSalesRepCommissions(selectedControlSelectedItemIds) {
    const successes = [];
    const errors = [];
    for (const usageFormId of selectedControlSelectedItemIds) {
        try {

            Xrm.Utility.showProgressIndicator(PROGRESSRSC_MESSAGES.DELETE);
            await deactivateSalesRepCommissions(usageFormId)
            await deactivateOldUsageFormLogs(usageFormId);
            const usageFormInfo = await getUsageFormInfo(usageFormId);

            Xrm.Utility.showProgressIndicator(PROGRESSRSC_MESSAGES.CREATE);
            await createSalesRepCommissions(usageFormInfo)

            successes.push({ usageFormId: usageFormId, result: "Success" });
        } catch (caughtError) {
            errors.push({ usageFormId: usageFormId, result: caughtError.message });
        }
    }

    Xrm.Utility.closeProgressIndicator();
    console.table(successes.concat(errors));

    if (successes.length == selectedControlSelectedItemIds.length) {
        showResultsDialog(RESULTRSC_MESSAGES.SUCCESS);
    } else if (errors.length == selectedControlSelectedItemIds.length) {
        showResultsDialog(RESULTRSC_MESSAGES.FAILURE);
    } else {
        showResultsDialog(RESULTRSC_MESSAGES.MIXED);
    }

}

/*---------- ticket JIRA 908 ---------------*/
/*  recreating will recalculate seles rep commissions */
async function recreateSalesRepCommissionSingle(usageFormId) {
    let success = true;
    try {
        Xrm.Utility.showProgressIndicator(PROGRESSRSC_MESSAGES.DELETE);
        await deactivateSalesRepCommissions(usageFormId)
        await deactivateOldUsageFormLogs(usageFormId);
        const usageFormInfo = await getUsageFormInfo(usageFormId);

        Xrm.Utility.showProgressIndicator(PROGRESSRSC_MESSAGES.CREATE);
        await createSalesRepCommissions(usageFormInfo)

    } catch (caughtError) {
        success = false;
    }

    Xrm.Utility.closeProgressIndicator();
    if (success) {
        showResultsDialog(RESULTRSC_MESSAGES.SUCCESS);
    } else {
        showResultsDialog(RESULTRSC_MESSAGES.FAILURE);
    }
}

async function deactivateSalesRepCommissions(usageFormId) {
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
    const errors = [];
    try {
        const usageFormDetails = await Xrm.WebApi.retrieveMultipleRecords("leap_usageformdetail", fetchXML);
        if (usageFormDetails == null) {
            throw new Error("Invalid operation result when attempting to retrieve usage form details.");
        } else if (usageFormDetails.entities.length > 0) {
           
            for (const usageFormDetail of usageFormDetails.entities) {
                const usageFormDetailId = usageFormDetail.leap_usageformdetailid;
                try {
                    await deactivateOldSalesRepCommissions(usageFormDetailId);
                    await updateUsageFormDetailProcessedFlag(usageFormDetailId, false);
                } catch (error) {
                    errors.push({ id: usageFormDetailId, error: error.message });
                }
            }

            if (errors.length > 0) {
                console.table(errors);
                throw new Error("Some sales rep commissions could not be deactivated. Check logs for details.");
            }
        }
    } catch (error) {
        // console.error(`Error retrieving or deleting usage form details for usage form ID ${usageFormId}: ${error.message}`);
        throw error;
    }
}

function hasValue(val) {
    return val !== null && val !== undefined;
}

async function createSalesRepCommissions(usageFormInfo) {
    const errors = [];
    let fetchXML = `<fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
        <entity name="leap_usageformdetail">
        <attribute name="leap_usageformdetailid"/>
        <attribute name="leap_name"/>
        <attribute name="createdon"/>
        <attribute name="leap_product"/>
        <attribute name="leap_supplier"/>
            <attribute name="leap_io"/>
        <attribute name="leap_overrideprice"/>
        <attribute name="leap_unitpriceoverride"/>
        <attribute name="leap_unitprice"/>
        <attribute name="leap_usageform"/>
        <attribute name="leap_totalafteradjustments"/>
        <filter type="and">
            <condition attribute="leap_usageform" operator="eq" value="${usageFormInfo.id}"/>
            <condition attribute="statecode" operator="eq" value="0" />
        </filter>
        <order attribute="leap_name" descending="false"/>
        <!-- Product -->
        <link-entity name="product" from="productid" to="leap_product" link-type="inner" alias="aa">
            <attribute name="leap_manufacturer"/>
            <attribute name="producttypecode" />
            <attribute name="parentproductid"/>
        </link-entity>
        <!-- Usage Form -->
        <link-entity name="leap_usageform" from="leap_usageformid" to="leap_usageform" link-type="inner" alias="ab">
            <attribute name="leap_purchaseordernumber"/>
            <attribute name="leap_surgeon"/>
            <attribute name="leap_hospital"/>
            <!-- Case -->
            <link-entity name="leap_case" from="leap_caseid" to="leap_case" link-type="inner" alias="ac">
            <attribute name="leap_casedate"/>
            </link-entity>
        </link-entity>
            <!-- Supplier Commission -->
        <link-entity
            name="leap_suppliercommission"
            from="leap_usageformdetail"
            to="leap_usageformdetailid"
            link-type="outer"
            alias="sc">
            <attribute name="leap_commissionratepercent"/>
            <filter type="and">
            <condition attribute="statecode" operator="eq" value="0"/>
            </filter>
        </link-entity>    
        </entity>
        </fetch>`;

    fetchXML = "?fetchXml=" + encodeURIComponent(fetchXML);
    
    const flowUrl = await getSalesRepFlowUrl();
    if (!flowUrl) {
        throw new Error("Sales Rep Commission Flow URL is empty");
    }

    const usagefdetails = await Xrm.WebApi.retrieveMultipleRecords("leap_usageformdetail", fetchXML);
    if (usagefdetails == null) {
        throw new Error("Invalid operation result when attempting to retrieve UFDs.");
    }
    if (usagefdetails.entities.length == 0) {
        throw new Error("Found 0 valid UFDs to generate sales rep commssions.");
    } 

    // process each UFD in parallel
    const promises = usagefdetails.entities.map(async (record) => {
        try {
            const comissionrate_real = record["aa.producttypecode"] == 2 ? record["sc.leap_commissionratepercent"] : 0.0;

            let realUnitPrice = record["leap_unitprice"];
            if ((record["leap_io"] === 1 && hasValue(record["leap_overrideprice"])) ||
                hasValue(record["leap_unitpriceoverride"])
            ) {
                realUnitPrice = hasValue(record["leap_overrideprice"])
                    ? record["leap_overrideprice"]
                    : record["leap_unitpriceoverride"];
            }

            const payload = {
                    criteria: {
                        product: record["_leap_product_value"],
                        productFamily: record["aa.parentproductid"],
                        supplier: record["_leap_supplier_value"],
                        manufacturer: record["aa.leap_manufacturer"],
                        hospital: record["ab.leap_hospital"],
                        physician: record["ab.leap_surgeon"],
                        usageDate: record["ac.leap_casedate"]
                    },
                    unitPrice: realUnitPrice,
                    totalAfterAdjustments: record["leap_totalafteradjustments"],
                    supplierCommissionRate: comissionrate_real,
                    poNumber: record["ab.leap_purchaseordernumber"],
                    reference: {
                        entityName: "leap_usageformdetail",
                        entityId: record["leap_usageformdetailid"]
                    }
                };

                const response = await fetch(flowUrl, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json"
                    },
                    body: JSON.stringify(payload)
                });
                if (!response.ok) {
                    throw new Error("HTTP error calling Sales Rep Commission Flow: " + response.status);
                }

                await updateUsageFormDetailProcessedFlag(record["leap_usageformdetailid"], true);
        } catch (error) {
            console.error("Workflow call failed:", error);
            errors.push({ usageFormDetailId: record["leap_usageformdetailid"], error: error.message });
        }


    });

    await Promise.all(promises); // await all calls to finish before proceeding

    if (errors.length > 0) {
        console.table(errors);
        throw new Error("Some Sales Rep Commissions could not be created. Check logs for details.");
    }
}

let cachedFlowUrl = null;

async function getSalesRepFlowUrl() {
    if (!cachedFlowUrl) {
        cachedFlowUrl = await getEnvironmentVariableValue("leap_salesrepcommissionurl");
    }
    return cachedFlowUrl;
}

function getEnvironmentVariableValue(schemaName) {
    const fetchXml = `
<fetch top="1">
  <entity name="environmentvariabledefinition">
    <attribute name="schemaname" />
    <link-entity name="environmentvariablevalue"
                 from="environmentvariabledefinitionid"
                 to="environmentvariabledefinitionid"
                 link-type="outer"
                 alias="val">
      <attribute name="value" />
    </link-entity>
    <filter>
      <condition attribute="schemaname" operator="eq" value="${schemaName}" />
    </filter>
  </entity>
</fetch>`;

    return Xrm.WebApi.retrieveMultipleRecords(
        "environmentvariabledefinition",
        "?fetchXml=" + encodeURIComponent(fetchXml)
    ).then(result => {
        if (!result.entities.length) {
            throw new Error(`Environment variable ${schemaName} not found`);
        }

        const record = result.entities[0];

        // If no current value exists, fallback to defaultvalue
        return record["val.value"] ?? record.defaultvalue;
    });
}


/* -------------------------------------------- */
/* ticket JIRA 870 */
function updateFieldWebApi(usageFormId) {
    const entityName = "leap_usageform"; // Replace 'account' with your entity name

    try {
        // Define the payload
        const data = {};
        data["leap_bcpullstatus"] = 293360000;    
        // Send the Web API request
        Xrm.WebApi.updateRecord(entityName, usageFormId, data).then(
            function success(result2) {
                console.log(`Record updated successfully BCPullStatus. Record ID: ${result2.id}`);                
            },
            function error(error) {
                console.error("Error updating the record: BCPullStatus", error.message);
            }
        );
    } catch (caughtError) {
        success = false;
    }
}

async function updateUsageFormDetailProcessedFlag(usageFormDetailId, processed) {
    const data = {
        "leap_commissionsprocessed": processed
    };

    try {
        const result = await Xrm.WebApi.updateRecord("leap_usageformdetail", usageFormDetailId, data);
        if (result == null) {
            throw new Error(`Invalid operation result when attempting to update processed flag for usage form detail with id ${usageFormDetailId}.`);
        }
        return result;
    } catch (error) {
        throw new Error(`Error updating processed flag for usage form detail ${usageFormDetailId}: ${error.message}`);
    }
}