// Login Page Script
class LoginManager {
    constructor() {
        this.form = document.getElementById('loginForm');
        this.input = document.getElementById('serverAddress');
        this.btnConnect = document.getElementById('btnConnect');
        this.btnDiscover = document.getElementById('btnDiscover');
        this.status = document.getElementById('status');
        this.discoveredList = document.getElementById('discoveredServers');
        
        this.setupEventListeners();
        this.input.focus();
    }

    setupEventListeners() {
        this.form.addEventListener('submit', (e) => this.handleConnect(e));
        this.btnDiscover.addEventListener('click', () => this.handleDiscover());
    }

    // Validate IP:PORT format
    validateAddress(address) {
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

    // Auto discover servers
    async handleDiscover() {
        this.showStatus('🔍 Đang quét mạng LAN...', 'info');
        this.btnDiscover.disabled = true;
        this.btnDiscover.textContent = '⏳ SCANNING...';
        this.discoveredList.innerHTML = '';
        this.discoveredList.style.display = 'none';

        try {
            // Get local IP
            const localIP = await fetch('/api/get-local-ip').then(r => r.text());
            const subnet = localIP.substring(0, localIP.lastIndexOf('.'));
            
            const commonPorts = [8080, 8081, 9000];
            const found = [];

            // Scan 20 nearest IPs
            const currentHost = parseInt(localIP.split('.')[3]);
            const startIP = Math.max(1, currentHost - 10);
            const endIP = Math.min(254, currentHost + 10);

            for (let i = startIP; i <= endIP; i++) {
                for (const port of commonPorts) {
                    try {
                        const testAddr = `${subnet}.${i}:${port}`;
                        await this.testConnection(testAddr);
                        found.push(testAddr);
                        
                        // Display immediately when found
                        this.addServerItem(testAddr);
                    } catch (e) {
                        // Server not exists or not RAT server
                    }
                }
            }

            if (found.length === 0) {
                this.showStatus('❌ Không tìm thấy server nào trong mạng', 'error');
            } else {
                this.showStatus(`✅ Tìm thấy ${found.length} server(s)`, 'success');
            }
        } catch (error) {
            this.showStatus('❌ Lỗi khi quét mạng: ' + error.message, 'error');
        } finally {
            this.btnDiscover.disabled = false;
            this.btnDiscover.textContent = '🔍 AUTO DISCOVER';
        }
    }

    // Add server item to list
    addServerItem(address) {
        const item = document.createElement('div');
        item.className = 'server-item';
        item.textContent = `✓ ${address}`;
        item.onclick = () => {
            this.input.value = address;
            this.form.dispatchEvent(new Event('submit'));
        };
        this.discoveredList.appendChild(item);
        this.discoveredList.style.display = 'block';
    }
}

// Initialize when DOM is ready
window.addEventListener('DOMContentLoaded', () => {
    new LoginManager();
});