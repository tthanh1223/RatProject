// Login Page Script
class LoginManager {
    constructor() {
        this.form = document.getElementById('loginForm');
        this.input = document.getElementById('serverAddress');
        this.btnConnect = document.getElementById('btnConnect');
        this.status = document.getElementById('status');
        
        this.setupEventListeners();
        this.input.focus();
    }

    setupEventListeners() {
        this.form.addEventListener('submit', (e) => this.handleConnect(e));
    }

    // Validate IP:PORT format
    validateAddress(address) {
        if (address.toLowerCase().startsWith("localhost:")) {
            const port = parseInt(address.split(':')[1]);
            // Kiểm tra port hợp lệ
            if (port >= 1 && port <= 65535) return address;
            return null;
        }
        const regex = /^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?):([0-9]{1,5})$/;
        const match = address.match(regex);
        if (!match) return null;
        
        const port = parseInt(match[1]);
        if (port < 1 || port > 65535) return null;
        
        return address;
    }

    // Show status message
    showStatus(message, type) {
        this.status.textContent = message;
        this.status.className = 'status ' + type;
        this.status.style.display = 'block';
    }

    // Test WebSocket connection
    async testConnection(address) {
        return new Promise((resolve, reject) => {
            const ws = new WebSocket(`ws://${address}/`);
            let isHandshakeReceived = false;
            
            const timeout = setTimeout(() => {
                if (!isHandshakeReceived) {
                    ws.close();
                    reject(new Error('Timeout: Server không phản hồi'));
                }
            }, 5000);

            ws.onopen = () => {
                this.showStatus('Đang chờ xác thực từ server...', 'info');
            };

            ws.onmessage = (event) => {
                try {
                    const data = JSON.parse(event.data);
                    
                    // Check handshake from server
                    if (data.type === 'handshake' && data.server_name === 'RAT_SERVER_V1.0') {
                        clearTimeout(timeout);
                        isHandshakeReceived = true;
                        ws.close();
                        resolve(true);
                    }
                } catch (e) {
                    // Invalid JSON or wrong format
                }
            };

            ws.onerror = () => {
                clearTimeout(timeout);
                ws.close();
                reject(new Error('Không thể kết nối tới server'));
            };

            ws.onclose = () => {
                clearTimeout(timeout);
                if (!isHandshakeReceived) {
                    reject(new Error('Server đóng kết nối không mong muốn'));
                }
            };
        });
    }

    // Handle form submit
    async handleConnect(e) {
        e.preventDefault();
        
        const address = this.input.value.trim();
        
        if (!this.validateAddress(address)) {
            this.showStatus('❌ Địa chỉ không hợp lệ! Format: IP:PORT', 'error');
            return;
        }

        this.btnConnect.disabled = true;
        this.btnConnect.textContent = '⏳ CONNECTING...';
        this.showStatus('Đang kết nối tới ' + address + '...', 'info');

        try {
            await this.testConnection(address);
            this.showStatus('✅ Kết nối thành công!', 'success');
            
            // Redirect to dashboard after 1 second
            setTimeout(() => {
                window.location.href = `/?server=${address.split(':')[0]}`;
            }, 1000);
            
        } catch (error) {
            this.showStatus('❌ ' + error.message, 'error');
            this.btnConnect.disabled = false;
            this.btnConnect.textContent = '▶ CONNECT TO SERVER';
        }
    }

}

// Initialize when DOM is ready
window.addEventListener('DOMContentLoaded', () => {
    new LoginManager();
});