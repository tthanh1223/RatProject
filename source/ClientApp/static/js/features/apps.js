export class AppsManager {
    constructor(ws, logger) {
        this.ws = ws;
        this.logger = logger;
        this.tbody = document.getElementById('tbody-apps');
        
        this.setupListeners();
    }

    setupListeners() {
        document.getElementById('btn-refresh-apps').addEventListener('click', () => {
            this.refresh();
        });

        document.getElementById('btn-start-app').addEventListener('click', () => {
            this.startApp();
        });

        this.ws.on('apps', (data) => {
            this.render(data.data);
            this.logger.log(`> Loaded ${data.data.length} apps.`);
        });
    }

    refresh() {
        this.ws.send('listApps');
    }

    render(apps) {
        this.tbody.innerHTML = '';
        apps.forEach(app => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${app.pid}</td>
                <td style="color:#5e6ad2">${app.tieu_de}</td>
                <td>${app.ten}.exe</td>
                <td><button class="btn-kill" style="font-size:14px; padding:2px 10px; height:auto;" data-app="${app.ten}">X</button></td>
            `;
            
            row.querySelector('button').addEventListener('click', (e) => {
                this.stopApp(e.target.dataset.app);
            });
            
            this.tbody.appendChild(row);
        });
    }

    startApp() {
        const name = document.getElementById('appInput').value.trim();
        if (name) {
            this.ws.send(`startApp ${name}`);
        }
    }

    stopApp(name) {
        if (confirm(`Close app '${name}'?`)) {
            this.ws.send(`stopApp ${name}`);
        }
    }
}