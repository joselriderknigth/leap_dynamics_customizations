function blockSave(executionContext) {
    const formContext = executionContext.getFormContext();
    const adjustRetroactively = formContext.getAttribute("leap_adjustretroactively");

    console.log("adjust retroactively: " + adjustRetroactively.getValue());

    if(adjustRetroactively.getValue() === 1) {
        // prevent save, we'll save manually after some process
        executionContext.getEventArgs().preventDefault();
        console.log("prevent save");
    }
}