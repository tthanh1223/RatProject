export class SystemLogger {
    constructor(elementId) {
        this.container = document.getElementById(elementId);
    }

    log(message) {
        const div = document.createElement('div');
        div.style.marginBottom = "5px";
        div.style.borderBottom = "1px dashed #333";
        div.innerText = message;
        this.container.prepend(div);
    }

    clear() {
        this.container.innerHTML = '';
    }
}