export class FileManagerManager {
    constructor(ws, logger) {
        this.ws = ws;
        this.logger = logger;
        this.currentPath = 'C:\\';
        this.downloadingFiles = new Map(); // track ongoing downloads
        this.fileChunks = new Map(); // store chunks by path
        
        this.setupEventListeners();
    }

    setupEventListeners() {
        const btnOpenPath = document.getElementById('btn-open-path');
        const btnRefresh = document.getElementById('btn-refresh-files');
        const pathInput = document.getElementById('pathInput');

        if (btnOpenPath) {
            btnOpenPath.addEventListener('click', () => this.openPath());
        }

        if (btnRefresh) {
            btnRefresh.addEventListener('click', () => this.refreshCurrentPath());
        }

        if (pathInput) {
            pathInput.addEventListener('keydown', (e) => {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    this.openPath();
                }
            });
        }

        // Listen for file list messages from server
        this.ws.on('file_list', (data) => {
            this.onFileListReceived(data);
        });

        // Listen for file download chunks
        this.ws.on('file_start', (data) => {
            this.onFileDownloadStart(data);
        });

        this.ws.on('file_chunk', (data) => {
            this.onFileChunkReceived(data);
        });

        this.ws.on('file_end', (data) => {
            this.onFileDownloadEnd(data);
        });
    }

    openPath() {
        const pathInput = document.getElementById('pathInput');
        const path = pathInput.value.trim();

        if (!path) {
            this.logger.log('[FILES] Path is empty');
            return;
        }

        this.currentPath = path;
        this.listDirectory(path);
    }

    refreshCurrentPath() {
        this.listDirectory(this.currentPath);
    }

    listDirectory(path) {
        this.logger.log(`[FILES] Listing: ${path}`);
        this.ws.send(`list_dir ${path}`);
    }

    onFileListReceived(data) {
        const { path, items } = data;
        this.currentPath = path;
        this.logger.log(`[FILES] Received ${items.length} items from ${path}`);

        // Update display path
        const pathDisplay = document.getElementById('current-path-display');
        if (pathDisplay) {
            pathDisplay.textContent = path;
        }

        // Render file list
        this.renderFileList(items, path);
    }

    renderFileList(items, currentPath) {
        const container = document.getElementById('file-list-container');
        if (!container) return;

        container.innerHTML = '';

        // Add back button if not root
        if (currentPath !== 'C:\\' && currentPath !== 'C:' && !currentPath.match(/^[A-Z]:\\$/i)) {
            const backDiv = document.createElement('div');
            backDiv.className = 'file-item file-folder';
            backDiv.innerHTML = '📁 <span class="file-name">..</span>';
            backDiv.addEventListener('click', () => this.goBack(currentPath));
            container.appendChild(backDiv);
        }

        // Add folders first, then files
        const folders = items.filter(item => item.isDirectory).sort((a, b) => a.name.localeCompare(b.name));
        const files = items.filter(item => !item.isDirectory).sort((a, b) => a.name.localeCompare(b.name));

        [...folders, ...files].forEach(item => {
            const itemDiv = document.createElement('div');
            const isDir = item.isDirectory;
            itemDiv.className = `file-item ${isDir ? 'file-folder' : 'file-file'}`;

            const icon = isDir ? '📁' : '📄';
            const sizeStr = isDir ? '' : ` (${this.formatSize(item.size)})`;
            const name = item.name;

            itemDiv.innerHTML = `<span class="file-name">${icon} ${name}</span><span class="file-size">${sizeStr}</span>`;
            
            if (isDir) {
                itemDiv.addEventListener('click', () => this.listDirectory(item.fullPath));
            } else {
                itemDiv.addEventListener('click', () => this.downloadFile(item.fullPath, item.name));
            }

            container.appendChild(itemDiv);
        });
    }

    goBack(currentPath) {
        // Navigate to parent directory
        const parts = currentPath.split('\\').filter(p => p);
        if (parts.length > 1) {
            parts.pop();
            const parentPath = parts.join('\\') + '\\';
            this.listDirectory(parentPath);
        }
    }

    downloadFile(filePath, fileName) {
        this.logger.log(`[FILES] Downloading: ${fileName}`);
        this.downloadingFiles.set(filePath, {
            fileName: fileName,
            chunks: [],
            size: 0
        });
        this.ws.send(`download_file ${filePath}`);
    }

    onFileDownloadStart(data) {
        const { path, size, contentType } = data;
        this.logger.log(`[FILES] Download start: ${path} (${this.formatSize(size)})`);
        
        if (this.downloadingFiles.has(path)) {
            this.downloadingFiles.get(path).size = size;
            this.downloadingFiles.get(path).contentType = contentType;
        }
    }

    onFileChunkReceived(data) {
        const { path, index, data: b64Data } = data;

        if (!this.downloadingFiles.has(path)) {
            this.logger.log(`[FILES] Received chunk for unknown file: ${path}`);
            return;
        }

        const fileInfo = this.downloadingFiles.get(path);
        if (!fileInfo.chunks[index]) {
            fileInfo.chunks[index] = b64Data;
        }

        const received = Object.keys(fileInfo.chunks).length;
        this.logger.log(`[FILES] Chunk ${index + 1} received (total: ${received})`);
    }

    onFileDownloadEnd(data) {
        const { path } = data;

        if (!this.downloadingFiles.has(path)) {
            this.logger.log(`[FILES] Download end for unknown file: ${path}`);
            return;
        }

        const fileInfo = this.downloadingFiles.get(path);
        const chunks = fileInfo.chunks.filter(c => c); // remove undefined

        this.logger.log(`[FILES] Download complete: ${fileInfo.fileName} (${chunks.length} chunks)`);

        // Reconstruct file from base64 chunks
        this.downloadFileFromChunks(fileInfo.fileName, chunks);

        // Cleanup
        this.downloadingFiles.delete(path);
    }

    downloadFileFromChunks(fileName, chunks) {
        try {
            // Concatenate all base64 chunks
            const concatenated = chunks.join('');
            
            // Convert base64 to binary
            const binaryString = atob(concatenated);
            const bytes = new Uint8Array(binaryString.length);
            for (let i = 0; i < binaryString.length; i++) {
                bytes[i] = binaryString.charCodeAt(i);
            }

            // Create blob and download
            const blob = new Blob([bytes], { type: 'application/octet-stream' });
            const url = URL.createObjectURL(blob);
            
            const a = document.createElement('a');
            a.href = url;
            a.download = fileName;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);

            this.logger.log(`[FILES] File downloaded: ${fileName}`);
        } catch (error) {
            this.logger.log(`[FILES] Download error: ${error.message}`);
        }
    }

    formatSize(bytes) {
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
    }

    refresh() {
        this.refreshCurrentPath();
    }
}
