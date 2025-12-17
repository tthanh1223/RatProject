export class WebSocketManager {
    constructor(url) {
        this.url = url;
        this.socket = null;
        this.listeners = new Map();
    }

    connect() {
        this.socket = new WebSocket(this.url);
        
        this.socket.onopen = () => {
            this.emit('connected');
        };

        this.socket.onmessage = (event) => {
            try {
                const data = JSON.parse(event.data);
                this.emit('message', data);
                
                // Emit specific message type events
                if (data.type) {
                    this.emit(data.type, data);
                }
            } catch (e) {
                this.emit('error', { message: 'Parse error: ' + event.data });
            }
        };

        this.socket.onclose = () => {
            this.emit('disconnected');
            setTimeout(() => this.connect(), 3000);
        };

        this.socket.onerror = (error) => {
            this.emit('error', error);
        };
    }

    send(command) {
        if (this.socket && this.socket.readyState === WebSocket.OPEN) {
            this.socket.send(command);
            return true;
        }
        return false;
    }

    on(eventType, callback) {
        if (!this.listeners.has(eventType)) {
            this.listeners.set(eventType, []);
        }
        this.listeners.get(eventType).push(callback);
    }

    emit(eventType, data) {
        const callbacks = this.listeners.get(eventType) || [];
        callbacks.forEach(cb => cb(data));
    }

    isConnected() {
        return this.socket && this.socket.readyState === WebSocket.OPEN;
    }
}