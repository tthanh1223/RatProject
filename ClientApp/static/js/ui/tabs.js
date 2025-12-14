export class TabManager {
    constructor() {
        this.currentTab = 'apps';
        this.tabs = document.querySelectorAll('.tab');
        this.toolbars = {
            apps: document.getElementById('toolbar-apps'),
            processes: document.getElementById('toolbar-processes'),
            screen: document.getElementById('toolbar-screen'),
            keylog: document.getElementById('toolbar-keylog'),
            shutdown: document.getElementById('toolbar-shutdown'),
            webcam: document.getElementById('toolbar-webcam'),
            files: document.getElementById('toolbar-files')
        };
        this.views = {
            tables: document.getElementById('view-tables'),
            screen: document.getElementById('view-screen'),
            keylog: document.getElementById('view-keylog'),
            webcam: document.getElementById('view-webcam'),
            files: document.getElementById('view-files'),
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
        document.querySelector(`[data-tab="${tabName}"]`).classList.add('active');

        // Hide all toolbars
        Object.values(this.toolbars).forEach(toolbar => {
            toolbar.classList.add('hidden');
        });

        // Hide all views
        this.views.tables.classList.add('hidden');
        this.views.screen.classList.add('hidden');
        this.views.keylog.classList.add('hidden');
        this.views.webcam.classList.add('hidden');
        this.views.files.classList.add('hidden');

        // Show current toolbar
        this.toolbars[tabName].classList.remove('hidden');

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
        }

        return tabName;
    }

    getCurrentTab() {
        return this.currentTab;
    }
}