// Helper functions
export function parseQueryString() {
    const params = new URLSearchParams(window.location.search);
    return {
        serverIp: params.get('server') || 'localhost'
    };
}

export function preventSpaceKeyOnButtons() {
    document.addEventListener('keydown', function(e) {
        if (e.code === 'Space' && e.target.tagName === 'BUTTON') {
            e.preventDefault();
        }
    }, true);
}
