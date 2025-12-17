export class StatusManager {
    constructor() {
        this.dot = document.getElementById('status-dot');
        this.text = document.getElementById('status-text');
    }

    setOnline() {
        this.dot.classList.add('online');
        this.text.innerText = "CONNECTED";
        this.text.style.color = "#00e436";
    }

    setOffline() {
        this.dot.classList.remove('online');
        this.text.innerText = "DISCONNECTED";
        this.text.style.color = "#ff2a6d";
    }
}