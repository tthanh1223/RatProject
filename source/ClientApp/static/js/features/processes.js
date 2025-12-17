export class ProcessesManager {
    constructor(ws, logger) {
        this.ws = ws;
        this.logger = logger;
        this.tbody = document.getElementById('tbody-processes');
        
        this.setupListeners();
    }

    setupListeners() {
        document.getElementById('btn-refresh-processes').addEventListener('click', () => {
            this.refresh();
        });

        document.getElementById('btn-start-process').addEventListener('click', () => {
            this.startProcess();
        });

        this.ws.on('processes', (data) => {
            this.render(data.data);
            this.logger.log(`> Loaded ${data.data.length} processes.`);
        });
    }

    refresh() {
        this.ws.send('listProcesses');
    }

    render(processes) {
        this.tbody.innerHTML = '';
        processes.forEach(proc => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${proc.pid}</td>
                <td><b>${proc.ten}</b></td>
                <td class="mem-col">${proc.mem}</td>
                <td><button class="btn-kill" style="font-size:14px; padding:2px 10px; height:auto; background:#444;" data-pid="${proc.pid}">KILL</button></td>
            `;
            
            row.querySelector('button').addEventListener('click', (e) => {
                this.killProcess(e.target.dataset.pid);
            });
            
            this.tbody.appendChild(row);
        });
    }

    startProcess() {
        const name = document.getElementById('procInput').value.trim();
        if (name) {
            this.ws.send(`startApp ${name}`);
        } else {
            alert("Input name!");
        }
    }

    killProcess(pid) {
        if (confirm(`Kill PID ${pid}?`)) {
            this.ws.send(`killProcess ${pid}`);
        }
    }
}