const rolesAllowedCaseProductEdit = ['System Administrator'];
const caseStatusesWhereAllUsersEditAllowed = [1, 293360000, 293360007];
const caseStatusesWhereElevatedUsersEditAllowed = [293360007];

class CaseInfo {
    constructor(status, needsReset) {
        this.status = status;
        this.needsReset = needsReset;
    }
}

/*
* Utility
*/

function getCaseInfo(caseId) {
    return Xrm.WebApi.retrieveRecord("leap_case", caseId, "?$select=statuscode,leap_needsreset")
        .then(result => {
            if (result == null) {
                throw Error(`[Event Handler] Retrieved case with ID ${caseId} is null.`);
            }
            return new CaseInfo(result.statuscode, result.leap_needsreset);
        }).catch(error => {
            throw Error(`[Event Handler] Error retrieving case with ID ${caseId} - ${error.message}`);
        });
}

function userRoleAllowsElevatedEdit(caseInfo) {
    var userRoles = Xrm.Utility.getGlobalContext().userSettings.roles.getAll();
    var hasProperRole = false;
    for(const userRole of userRoles) {
        if(rolesAllowedCaseProductEdit.includes(userRole.name)) {
            hasProperRole = true;
            break;
        }
    }

    return hasProperRole && (caseStatusesWhereElevatedUsersEditAllowed.includes(caseInfo.status) && caseInfo.needsReset);
}

function caseStatusAllowsEdit(caseStatus) {
    console.log("[Event Handler] statuses that allow edit " + caseStatusesWhereAllUsersEditAllowed + " current status: " + caseStatus);
    return caseStatusesWhereAllUsersEditAllowed.includes(caseStatus);
}

function isQuickCreate(formContext) {
    let cpId = formContext.data?.entity?.getId();
    console.log("[Event Handler] case product id: " + cpId + "and type: " + typeof cpId);
    return cpId == null || cpId == undefined || cpId == "";
}

async function isEditAllowed(formContext) {
    if(isQuickCreate(formContext)) {
        console.log('[Event Handler] triggering from quick create.');
        return true;
    }

    var caseId = formContext.getAttribute("leap_caseid")?.getValue()[0]?.id;
    if (caseId == null || caseId == undefined) {
        console.log('[Event Handler] unable to find case id.');
        return false;
    } else {
        try {
            let caseInfo = await getCaseInfo(caseId);
            console.log("[Event Handler] Got case info: " + JSON.stringify(caseInfo));
            // TODO: for now we only care about the status but once we add the changes to N52 'amR' we can uncomment this
            // return caseStatusAllowsEdit(caseInfo.status) || userRoleAllowsElevatedEdit(caseInfo);
            return caseStatusAllowsEdit(caseInfo.status);
        } catch(error) {
            console.log(error.message);
            return false;
        } 
    }
}

/*
* PRODUCT
*/

function handleProductField(formContext, showField) {
    var productFieldCtrl = formContext.getControl("leap_productid");
    var productField = formContext.getAttribute("leap_productid");
    var unitPriceFieldCtrl = formContext.getControl("leap_unitprice");
    var unitPriceField = formContext.getAttribute("leap_unitprice");
    var unitPriceOverrideFieldCtrl = formContext.getControl("leap_unitpriceoverride");
    var unitPriceOverrideField = formContext.getAttribute("leap_unitpriceoverride");

    productFieldCtrl.setVisible(showField);
    unitPriceFieldCtrl.setVisible(showField);

    if(showField) { 
        productField.setRequiredLevel('required');
        unitPriceField.setValue(null);
        unitPriceField.setRequiredLevel('required');
        unitPriceOverrideFieldCtrl.setLabel('Unit Price Override');
        unitPriceOverrideField.setValue(null);
        unitPriceOverrideField.setRequiredLevel('none');
    } else {
        productField.setValue(null);
        productField.setRequiredLevel('none');
        unitPriceField.setValue(null);
        unitPriceField.setRequiredLevel('none');
        unitPriceOverrideFieldCtrl.setLabel('New Product Price');
        unitPriceOverrideField.setValue(null);
        unitPriceOverrideField.setRequiredLevel('required');
    }
}

function handleNewProductField(formContext, showField) {
    var newProductFieldCtrl = formContext.getControl("leap_newproductname");
    var newProductIdFieldCtrl = formContext.getControl("leap_newproductid");
    var newProductField = formContext.getAttribute("leap_newproductname");
    var newProductIdField = formContext.getAttribute("leap_newproductid");

    newProductFieldCtrl.setVisible(showField);
    newProductIdFieldCtrl.setVisible(showField);

    if(showField) {
        newProductField.setRequiredLevel('required');
        newProductIdField.setRequiredLevel('required');
    } else {
        newProductField.setValue(null);
        newProductIdField.setValue(null);
        newProductField.setRequiredLevel('none');
        newProductIdField.setRequiredLevel('none');
    }
}

/*
* SUPPLIER
*/

function handleSupplierField(formContext, showField) {
    var supplierFieldCtrl = formContext.getControl("leap_supplier");
    var supplierField = formContext.getAttribute("leap_supplier");
    var productType = formContext.getAttribute("leap_producttype");

    supplierFieldCtrl.setVisible(showField);

    if(showField) {
        supplierField.setRequiredLevel('required');
        productType.setValue(null);
    } else {
        supplierField.setValue(null);
        supplierField.setRequiredLevel('none');
        productType.setValue(null);
    }
}

function handleNewSupplierField(formContext, showField) {
    var newSupplierFieldCtrl = formContext.getControl("leap_newsuppliername");
    var newSupplierField = formContext.getAttribute("leap_newsuppliername");

    newSupplierFieldCtrl.setVisible(showField);

    if(showField) {
        newSupplierField.setRequiredLevel('required');
    } else {
        newSupplierField.setValue(null);
        newSupplierField.setRequiredLevel('none');
    }
}

/*
* HANDLERS
*/

function addNewProductToggleHandler(executionContext) {
    var formContext = executionContext.getFormContext();
    var newProductToggle = formContext.getAttribute("leap_addanewproduct");
    var existingProduct = formContext.getAttribute("leap_existingproduct");
    var existingSupplierToggleCtrl = formContext.getControl("leap_existingsupplier");
    var existingSupplierToggle = formContext.getAttribute("leap_existingsupplier");

    isEditAllowed(formContext)
        .then(function (isAllowed) {
            if (isAllowed == null) {
                console.log('[Event Handler] unable to determine if edit is allowed.');
            } else if(isAllowed) {
                console.log('[Event Handler] edit is allowed.');
                if(newProductToggle.getValue() == 0) { // No
                    console.log('[Event Handler] not a new product.');
                    existingProduct.setValue(true);
                    handleProductField(formContext, true);
                    handleNewProductField(formContext, false);
            
                    existingSupplierToggle.setValue(true);
                    handleSupplierField(formContext, true);
                    handleNewSupplierField(formContext, false);
                    // existingSupplierToggleCtrl.setVisible(false); // Hide toggle for now
                } else {
                    console.log('[Event Handler] new product.');
                    existingProduct.setValue(false);
                    handleProductField(formContext, false);
                    handleNewProductField(formContext, true);
                    // existingSupplierToggleCtrl.setVisible(true); // Hide toggle for now
                }
            } else {
                console.log('[Event Handler] edit is not allowed.');
            }
        }).catch(function (error) {
            console.log(`[Event Handler] error trying to determine if edit is allowed. - ${error.message}`);
        });
}

function existingProductToggleHandler(executionContext) {
    var formContext = executionContext.getFormContext();
    var existingProduct = formContext.getAttribute("leap_existingproduct");

    isEditAllowed(formContext)
        .then(function (isAllowed) {
            if (isAllowed == null) {
                console.log('[Event Handler] unable to determine if edit is allowed.');
            } else if(isAllowed) {
                if (existingProduct.getValue() == 0) { // No
                    handleProductField(formContext, false);
                    handleNewProductField(formContext, true);
                } else { // Yes
                    handleProductField(formContext, true);
                    handleNewProductField(formContext, false);
                }
            }
        }).catch(function (error) {
            console.log(`[Event Handler] error trying to determine if edit is allowed. - ${error.message}`);
        });
}

function existingSupplierToggleHandler(executionContext) {
    var formContext = executionContext.getFormContext();
    var existingsupplier = formContext.getAttribute("leap_existingsupplier");
    var existingProductCtrl = formContext.getControl("leap_existingproduct");
    var existingProduct = formContext.getAttribute("leap_existingproduct");

    isEditAllowed(formContext)
        .then(function (isAllowed) {
            if (isAllowed == null) {
                console.log('[Event Handler] unable to determine if edit is allowed.');
            } else if(isAllowed) {
                if (existingsupplier.getValue() == 0) { // No
                    handleProductField(formContext, false);
                    handleNewProductField(formContext, true);
            
                    existingProduct.setValue(false);
                    existingProductCtrl.setDisabled(true);
                    
                    handleSupplierField(formContext, false);
                    handleNewSupplierField(formContext, true);
                } else { // Yes
                    existingProductCtrl.setDisabled(false);
            
                    handleSupplierField(formContext, true);
                    handleNewSupplierField(formContext, false);
                }
            }
        }).catch(function (error) {
            console.log(`[Event Handler] error trying to determine if edit is allowed. - ${error.message}`);
        });
}

function showProductDetailsToggleHandler(executionContext) {
    var formContext = executionContext.getFormContext();
    var showProductDetails = formContext.getAttribute("leap_showproductdetails");

    var lotFieldCtrl = formContext.getControl("leap_lotnumber");
    var serialNoFieldCtrl = formContext.getControl("leap_serialnumber");
    var expirationFieldCtrl = formContext.getControl("leap_expirationdate");
    var stickersFieldCtrl = formContext.getControl("leap_stickers");

    if (showProductDetails.getValue() == 0) { // No
        lotFieldCtrl.setVisible(false);
        serialNoFieldCtrl.setVisible(false);
        expirationFieldCtrl.setVisible(false);
        stickersFieldCtrl.setVisible(false);
    } else { // Yes
        lotFieldCtrl.setVisible(true);
        serialNoFieldCtrl.setVisible(true);
        expirationFieldCtrl.setVisible(true);
        stickersFieldCtrl.setVisible(true);
    }
}