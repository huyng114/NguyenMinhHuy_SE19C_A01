(() => {
    "use strict";
    const element = document.getElementById("newsModal");
    const modal = new bootstrap.Modal(element);
    const content = document.getElementById("newsModalContent");
    let opener, request;
    let saving = false;
    const editGuard = window.FUNews.trackUnsavedForm(content, () => saving);
    function focusField() {
        window.FUNews.focusForm(content);
    }
    function bindValidation() { $.validator.unobtrusive.parse(content); focusField(); }
    function redirect(response) {
        if (!response.redirected) return false;
        window.location.assign(response.url);
        return true;
    }
    function showError(target, message) { target.textContent = message; target.classList.remove("d-none"); }
    document.querySelectorAll("[data-news-form]").forEach(button => {
        button.addEventListener("click", async () => {
            opener = button;
            editGuard.reset();
            request?.abort();
            const currentRequest = new AbortController();
            request = currentRequest;
            content.innerHTML = '<div class="modal-header"><h2 class="modal-title h5" id="newsModalLabel">Article</h2><button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button></div><div class="modal-body" role="status">Loading...</div>';
            modal.show();
            try {
                const response = await fetch(button.dataset.newsForm, { signal: currentRequest.signal });
                if (redirect(response)) return;
                if (!response.ok) throw new Error("Unable to load this article. Close the dialog and refresh the list.");
                const html = await response.text();
                if (currentRequest.signal.aborted) return;
                content.innerHTML = html;
                bindValidation();
                editGuard.markClean();
            } catch (error) {
                if (error.name !== "AbortError") showError(content.querySelector(".modal-body"), error.message);
            }
        });
    });
    element.addEventListener("shown.bs.modal", focusField);
    element.addEventListener("hide.bs.modal", event => { if (!editGuard.confirmClose()) event.preventDefault(); else request?.abort(); });
    element.addEventListener("hidden.bs.modal", () => { editGuard.reset(); opener?.focus(); });
    content.addEventListener("submit", async event => {
        const form = event.target;
        if (form.id !== "newsForm") return;
        event.preventDefault();
        if (saving) return;
        if (!$(form).valid()) {
            window.FUNews.focusForm(form);
            return;
        }
        saving = true;
        const button = form.querySelector("button[type='submit']");
        const originalLabel = button.textContent;
        button.disabled = true;
        button.textContent = "Saving...";
        form.setAttribute("aria-busy", "true");
        try {
            const response = await fetch(form.action, { method: "POST", body: new FormData(form) });
            if (redirect(response)) return;
            if (response.status === 422) { content.innerHTML = await response.text(); bindValidation(); return; }
            if (!response.ok) throw new Error("Unable to save the article. Please refresh the page and try again.");
            if ((await response.json()).success) {
                editGuard.reset();
                window.FUNews.reloadAfterSuccess();
            }
        } catch (error) { showError(document.getElementById("newsFormError"), error.message); }
        finally { saving = false; button.disabled = false; button.textContent = originalLabel; form.removeAttribute("aria-busy"); }
    });

    const deleteModal = document.getElementById("deleteNewsModal");
    const deleteForm = document.getElementById("deleteNewsForm");
    const deleteError = document.getElementById("deleteNewsError");
    let deleting = false;
    deleteModal.addEventListener("show.bs.modal", event => {
        document.getElementById("deleteNewsId").value = event.relatedTarget.dataset.newsId;
        document.getElementById("deleteNewsName").textContent = event.relatedTarget.dataset.newsName;
        deleteError.classList.add("d-none");
    });
    deleteModal.addEventListener("hide.bs.modal", event => { if (deleting) event.preventDefault(); });
    deleteForm.addEventListener("submit", async event => {
        event.preventDefault();
        if (deleting) return;
        deleting = true;
        const button = deleteForm.querySelector("button[type='submit']");
        const originalLabel = button.textContent;
        button.disabled = true;
        button.textContent = "Deleting...";
        deleteForm.setAttribute("aria-busy", "true");
        try {
            const response = await fetch(deleteForm.action, { method: "POST", body: new FormData(deleteForm) });
            if (redirect(response)) return;
            if (!(response.headers.get("content-type") || "").includes("application/json"))
                throw new Error("Unable to delete the article. Please refresh the page and try again.");
            const result = await response.json();
            if (!response.ok) throw new Error(result.message || "Unable to delete the article.");
            if (result.success) {
                window.FUNews.reloadAfterSuccess();
            }
        } catch (error) { showError(deleteError, error.message); }
        finally { deleting = false; button.disabled = false; button.textContent = originalLabel; deleteForm.removeAttribute("aria-busy"); }
    });
})();
