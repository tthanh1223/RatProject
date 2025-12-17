export class TabManager {
    constructor() {
        this.currentTab = 'apps';
        this.tabs = document.querySelectorAll('.tab');
        
        // 1. Cập nhật danh sách Toolbar (Bỏ shutdown vì đã xóa toolbar này)
        this.toolbars = {
            apps: document.getElementById('toolbar-apps'),
            processes: document.getElementById('toolbar-processes'),
            screen: document.getElementById('toolbar-screen'),
            keylog: document.getElementById('toolbar-keylog'),
            webcam: document.getElementById('toolbar-webcam'),
            files: document.getElementById('toolbar-files')
        };

        // 2. Cập nhật danh sách View (THÊM shutdown vào đây)
        this.views = {
            tables: document.getElementById('view-tables'),
            screen: document.getElementById('view-screen'),
            keylog: document.getElementById('view-keylog'),
            webcam: document.getElementById('view-webcam'),
            files: document.getElementById('view-files'),
            shutdown: document.getElementById('view-shutdown'), // <--- MỚI THÊM
            'table-apps': document.getElementById('table-apps'),
            'table-processes': document.getElementById('table-processes')
        };
        
        this.setupListeners();
    }

    setupListeners() {
        this.tabs.forEach(tab => {
            tab.addEventListener('click', (e) => {
                const tabName = e.target.dataset.tab;
                this.switchTab(tabName);
            });
        });
    }

    switchTab(tabName) {
        this.currentTab = tabName;
        
        // Update tab styles
        this.tabs.forEach(t => t.classList.remove('active'));
        const activeTab = document.querySelector(`[data-tab="${tabName}"]`);
        if (activeTab) activeTab.classList.add('active');

        // Hide all toolbars
        Object.values(this.toolbars).forEach(toolbar => {
            if (toolbar) toolbar.classList.add('hidden');
        });

        // Hide all views (Bao gồm cả view shutdown mới)
        this.views.tables.classList.add('hidden');
        this.views.screen.classList.add('hidden');
        this.views.keylog.classList.add('hidden');
        this.views.webcam.classList.add('hidden');
        this.views.files.classList.add('hidden');
        if (this.views.shutdown) this.views.shutdown.classList.add('hidden'); // <--- Ẩn view cũ

        // Show current toolbar (CHỈ HIỆN NẾU NÓ TỒN TẠI)
        // Tab Shutdown không có toolbar nên dòng này sẽ an toàn bỏ qua
        if (this.toolbars[tabName]) {
            this.toolbars[tabName].classList.remove('hidden');
        }

        // Show appropriate view
        if (tabName === 'apps') {
            this.views.tables.classList.remove('hidden');
            this.views['table-apps'].classList.remove('hidden');
            this.views['table-processes'].classList.add('hidden');
        } else if (tabName === 'processes') {
            this.views.tables.classList.remove('hidden');
            this.views['table-apps'].classList.add('hidden');
            this.views['table-processes'].classList.remove('hidden');
        } else if (tabName === 'screen') {
            this.views.screen.classList.remove('hidden');
        } else if (tabName === 'keylog') {
            this.views.keylog.classList.remove('hidden');
        } else if (tabName === 'webcam') {
            this.views.webcam.classList.remove('hidden');
        } else if (tabName === 'files') {
            this.views.files.classList.remove('hidden');
        } else if (tabName === 'shutdown') {
            // <--- LOGIC MỚI CHO TAB POWER
            if (this.views.shutdown) {
                this.views.shutdown.classList.remove('hidden');
            }
        }

        return tabName;
    }

    getCurrentTab() {
        return this.currentTab;
    }
}