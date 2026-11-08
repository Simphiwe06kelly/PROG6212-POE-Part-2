// RaceDay site script: image preview for file inputs, and auto-dismissing success messages.
document.addEventListener("DOMContentLoaded", function () {
    // Show a preview of the chosen image next to the file input (data-preview="#imageId")
    document.querySelectorAll("input[type=file][data-preview]").forEach(function (input) {
        input.addEventListener("change", function () {
            var target = document.querySelector(input.getAttribute("data-preview"));
            var file = input.files && input.files[0];
            if (!target || !file || !file.type.startsWith("image/")) { return; }

            target.src = URL.createObjectURL(file);
            target.classList.remove("d-none");
            var placeholder = target.parentElement.querySelector(".rd-banner-placeholder");
            if (placeholder) { placeholder.classList.add("d-none"); }
        });
    });

    // Success messages fade away after a few seconds (errors stay until closed)
    document.querySelectorAll(".alert-success").forEach(function (alertBox) {
        setTimeout(function () {
            if (window.bootstrap) { bootstrap.Alert.getOrCreateInstance(alertBox).close(); }
        }, 6000);
    });
});
