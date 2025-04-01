window.MS = window.MS || {}; // Attach namespace to window
window.MS.LocalFunctions = window.MS.LocalFunctions || {}; // Ensure sub-namespace exists

window.MS.LocalFunctions.HelperFunction = function showResultsDialog(resultText) {
    const alertStrings = { confirmButtonLabel: 'Ok', text: resultText, title: 'Operation Result'};
    const alertOptions = { height: 120, width: 260 };
    
    return Xrm.Navigation.openAlertDialog(alertStrings, alertOptions).then(function() {});
}

window.MS.LocalFunctions.TestFunction = async function testFunction(ruleId) {
    console.log(`Test processing for rule id: ${ruleId}`);

    Xrm.Utility.showProgressIndicator("progress 1");
    await new Promise(r => setTimeout(r, 2000));

    Xrm.Utility.showProgressIndicator("progress 2");
    await new Promise(r => setTimeout(r, 2000));

    Xrm.Utility.showProgressIndicator("progress 3");
    await new Promise(r => setTimeout(r, 2000));

    Xrm.Utility.closeProgressIndicator();
    MS.LocalFunctions.HelperFunction("Done test processing");
}