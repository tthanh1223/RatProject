import { WebSocketManager } from './core/websocket.js';
import { SystemLogger } from './core/logger.js';
import { TabManager } from './ui/tabs.js';
import { ModalManager } from './ui/modal.js';
import { StatusManager } from './ui/status.js';
import { AppsManager } from './features/apps.js';
import { ProcessesManager } from './features/processes.js';
import { ScreenCaptureManager } from './features/screen.js';
import { KeyloggerManager } from './features/keylogger.js';
import { WebcamManager } from './features/webcam.js';
import { PowerManager } from './features/power.js';
import { FileManagerManager } from './features/filemanager.js';
import { parseQueryString, preventSpaceKeyOnButtons } from './utils/helpers.js';

class Application {
    constructor() {
        // ✅ LẤY SERVER IP TỪ URL PARAMS
        const urlParams = new URLSearchParams(window.location.search);
        let serverIP = urlParams.get('server');
        
        // ✅ NẾU KHÔNG CÓ SERVER PARAM → MẶC ĐỊNH DÙNG LOCALHOST (để test)
        if (!serverIP) {
            console.warn('⚠️ Không có server param, dùng localhost để test');
            serverIP = 'localhost';
            
            // ✅ TÙY CHỌN: Có thể redirect về login nếu muốn bắt buộc người dùng nhập
            // Uncomment dòng dưới nếu muốn bắt buộc phải có ?server= param
            // window.location.href = '/';
            // return;
        }
        
        // ✅ Kiểm tra format IP (bỏ qua kiểm tra nếu là localhost)
        if (serverIP !== 'localhost' && !this.isValidIP(serverIP)) {
            alert('❌ Invalid server IP format!\n\nVí dụ đúng: 192.168.1.100\n\nĐang redirect về login page...');
            window.location.href = '/';
            return;
        }

        // ✅ Construct WebSocket URL
        const wsUrl = `ws://${serverIP}:8080/`;
        console.log(`🔗 Connecting to: ${wsUrl}`);
        
        // Initialize core services
        this.ws = new WebSocketManager(wsUrl);
        this.logger = new SystemLogger('chat-content');
        
        // Initialize UI managers
        this.tabs = new TabManager();
        this.modal = new ModalManager();
        this.status = new StatusManager();
        
        // Initialize feature managers
        this.apps = new AppsManager(this.ws, this.logger);
        this.processes = new ProcessesManager(this.ws, this.logger);
        this.screen = new ScreenCaptureManager(this.ws, this.logger, this.modal);
        this.keylogger = new KeyloggerManager(this.ws, this.logger);
        this.webcam = new WebcamManager(this.ws, this.logger);
        this.power = new PowerManager(this.ws, this.logger);
        this.filemanager = new FileManagerManager(this.ws, this.logger);
        
        this.setupGlobalListeners();
        this.ws.connect();
    }

    // ✅ VALIDATE IP ADDRESS
    isValidIP(ip) {
        const regex = /^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$/;
        return regex.test(ip);
    }

    setupGlobalListeners() {
        // WebSocket connection events
        this.ws.on('connected', () => {
            this.status.setOnline();
            this.logger.log('✅ Connected to server');
            this.refreshCurrentTab();
        });

        this.ws.on('disconnected', () => {
            this.status.setOffline();
            this.logger.log('❌ Disconnected from server');
            
            // ✅ TỰ ĐỘNG REDIRECT VỀ LOGIN SAU 5 GIÂY (chỉ khi không phải localhost)
            const urlParams = new URLSearchParams(window.location.search);
            const serverIP = urlParams.get('server');
            
            if (serverIP && serverIP !== 'localhost') {
                setTimeout(() => {
                    if (confirm('❌ Connection lost. Return to login page?')) {
                        window.location.href = '/';
                    }
                }, 5000);
            }
        });

        // Handle handshake message
        this.ws.on('handshake', (data) => {
            this.logger.log(`🤝 Server: ${data.server_name}`);
            if (data.version) {
                this.logger.log(`📦 Version: ${data.version}`);
            }
        });

        // Handle general status messages
        this.ws.on('message', (data) => {
            if (data.trang_thai) {
                this.logger.log(`[${data.trang_thai}] ${data.thong_bao}`);
                if (data.trang_thai === 'thanh_cong') {
                    this.refreshCurrentTab();
                }
            }
        });

        // Chat functionality
        const chatInput = document.getElementById('chatInput');
        const sendBtn = document.getElementById('btn-send-chat');
        
        if (sendBtn) {
            sendBtn.addEventListener('click', () => this.sendChat());
        }
        
        if (chatInput) {
            chatInput.addEventListener('keydown', (e) => {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    this.sendChat();
                }
            });
        }

        // Prevent space key on buttons
        preventSpaceKeyOnButtons();
    }

    refreshCurrentTab() {
        if (!this.ws.isConnected()) return;
        
        const currentTab = this.tabs.getCurrentTab();
        
        if (currentTab === 'apps') {
            this.apps.refresh();
        } else if (currentTab === 'processes') {
            this.processes.refresh();
        } else if (currentTab === 'files') {
            this.filemanager.refresh();
        }
    }

    sendChat() {
        const input = document.getElementById('chatInput');
        if (!input) return;
        
        const val = input.value.trim();
        
        if (val && this.ws.isConnected()) {
            this.ws.send(val);
            this.logger.log("💬 ME: " + val);
            input.value = '';
        }
    }
}

// Initialize app when DOM is ready
window.addEventListener('DOMContentLoaded', () => {
    try {
        window.app = new Application();
    } catch (error) {
        console.error('❌ Failed to initialize application:', error);
        alert('Failed to start application. Check console for details.');
    }
});