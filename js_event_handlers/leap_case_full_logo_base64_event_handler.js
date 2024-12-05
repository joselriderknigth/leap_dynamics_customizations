async function changeImage(executionContext){
    var formContext = executionContext.getFormContext();
    var image = formContext.getAttribute("leap_fullcompanylogo").getValue();
    var base64Image = formContext.getAttribute("leap_companylogo").getValue();
    if(image && !base64Image){
        console.log("DEBUG > image set and base64Image not set");
        var base64 = await toBase64(image["_file"]);
        formContext.getAttribute("leap_companylogo").setValue(
base64.replace("data:image/jpeg;base64,","").replace("data:image/png;base64,","").replace("data:image/jpg;base64,",""));
        // formContext.data.entity.save();
    } else if(!image) {
        console.log("DEBUG > image not set");
        formContext.getAttribute("leap_companylogo").setValue('');
        // formContext.data.entity.save();
    }
}

const toBase64 = file => new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.readAsDataURL(file);
    reader.onload = () => resolve(reader.result);
    reader.onerror = error => reject(error);
});