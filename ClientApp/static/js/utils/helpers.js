// Helper functions
export function preventSpaceKeyOnButtons() {
    document.addEventListener('keydown', function(e) {
        if (e.code === 'Space' && e.target.tagName === 'BUTTON') {
            e.preventDefault();
        }
    }, true);
}