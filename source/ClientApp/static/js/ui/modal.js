export class ModalManager {
    constructor() {
        this.modal = document.getElementById('imgModal');
        this.modalImg = document.getElementById('img01');
        this.closeBtn = document.getElementById('closeModalBtn');
        
        this.setupListeners();
    }

    setupListeners() {
        this.closeBtn.addEventListener('click', () => this.close());
        
        window.addEventListener('click', (event) => {
            if (event.target === this.modal) {
                this.close();
            }
        });
    }

    open(imageSrc) {
        if (imageSrc && imageSrc.startsWith("data:image")) {
            this.modal.style.display = "block";
            this.modalImg.src = imageSrc;
        }
    }

    close() {
        this.modal.style.display = "none";
    }
}