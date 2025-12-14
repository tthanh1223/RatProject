export class PowerManager {
    constructor(ws, logger) {
        this.ws = ws;
        this.logger = logger;
        
        // Lấy đúng ID mà bạn đã đặt trong view-shutdown mới
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
        // Hỏi lại cho chắc vì nút to quá dễ bấm nhầm
        const msg = action === 'shutdown' ? 'Tắt máy' : 'Khởi động lại';
        if (confirm(`⚠️ CẢNH BÁO: Bạn có chắc chắn muốn ${msg} máy nạn nhân không?`)) {
            this.sendCommand(action);
        }
    }

    sendCommand(action) {
        // Gửi lệnh JSON về Server
        // Server C# sẽ nhận: {"type": "power", "action": "shutdown"}
        this.ws.send(JSON.stringify({
            type: 'power',
            action: action
        }));
        
        this.logger.log(`🔌 Đã gửi lệnh: ${action.toUpperCase()}`);
    }
}