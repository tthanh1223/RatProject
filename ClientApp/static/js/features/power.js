export class PowerManager {
    constructor(ws, logger) {
        this.ws = ws;
        this.logger = logger;
        
        this.setupListeners();
    }

    setupListeners() {
        document.getElementById('btn-shutdown').addEventListener('click', () => {
            this.shutdown();
        });

        document.getElementById('btn-restart').addEventListener('click', () => {
            this.restart();
        });
    }

    shutdown() {
        if (confirm('ARE YOU SURE TO SHUTDOWN REMOTE PC?')) {
            this.ws.send('shutdown');
            this.logger.log('Shutdown sent.');
        }
    }

    restart() {
        if (confirm('ARE YOU SURE TO RESTART REMOTE PC?')) {
            this.ws.send('restart');
            this.logger.log('Restart sent.');
        }
    }
}