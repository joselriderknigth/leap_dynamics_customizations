const validStatuses = [1, 293360000, 293360007, 293360001]; // planned, pending, pending close, pending po
const pollingSubIntervalSeconds = 3;
let ufDocCount = 0;


function pollCountToRefreshSubgrd(executionContext) {
    //debugger;
    let formContext = executionContext.getFormContext();
    const intervalId = setInterval(pollFlagSubG, pollingSubIntervalSeconds * 1000, formContext);
    console.log(`Created fetch with interval id: ${intervalId}`)
}

/* function getxdocsUrl(varxcacheId) {
//     //debugger;
//     var fetchXML = `<fetch distinct="false" mapping="logical" returntotalrecordcount="true" page="1" count="10" no-lock="false">
//   <entity name="sharepointdocument">
//     <attribute name="documentid"/>
//     <attribute name="absoluteurl"/>
//     <attribute name="fullname"/>
//     <attribute name="relativelocation"/>
//     <attribute name="sharepointdocumentid"/>
//     <order attribute="relativelocation" descending="false"/>
//     <attribute name="modified"/>
//     <attribute name="sharepointmodifiedby"/>
//     <attribute name="sharepointcreatedon"/>
//     <link-entity name="leap_case" from="leap_caseid" to="regardingobjectid" alias="bb">
//       <filter type="and">
//         <condition attribute="leap_caseid" operator="eq" uitype="leap_case" value="${varxcacheId}" />
//       </filter>
//     </link-entity>
//   </entity>
// </fetch>`;
//     fetchXML = "?fetchXml=" + encodeURIComponent(fetchXML);

//     return Xrm.WebApi.retrieveMultipleRecords("sharepointdocument", fetchXML).then(result => {
//         if(result == null) {
//             throw Error("Invalid operation result when attempting to retrieve case products for case.");
//         }
//         return result.entities;
//     }).catch(error => {
//         throw Error(`Error retrieving case products for case with id ${varxcacheId}: ${error.message}`);
//     });
// }   */

function createArray(n) {
    return Array.from({ length: n });
}

async function pollFlagSubG(formContext) {
    //debugger;
    var cpSubgrid = formContext.getControl("Subgrid_new_2"); 
    if (!cpSubgrid) {
        console.log("Subgrid not found.");
        return;
    }
    var grid = cpSubgrid.getGrid();
    if (!grid) {
        console.log("Subgrid grid not ready yet.");
        return;
    }
    ufDocCount = grid.getRows().getLength();
     
    const status = formContext.getAttribute("statuscode").getValue();
    if(validStatuses.includes(status)) {
        const caseId = formContext && formContext.data && formContext.data.entity
            ? formContext.data.entity.getId()
            : null;
        if(caseId && caseId != "") {
            try {
                console.log("Polling uf doc count...");
                // const currentFlagValue = await getxdocsUrl(caseId);
                const currentFlagValue = createArray(ufDocCount > 10 ? 0 : ufDocCount + 1);
                console.log(`Current document count: ${currentFlagValue.length}. Previous document count: ${ufDocCount}`);
                if((currentFlagValue.length > 0) && (currentFlagValue.length != ufDocCount) ) {
                    cpSubgrid.refresh();
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