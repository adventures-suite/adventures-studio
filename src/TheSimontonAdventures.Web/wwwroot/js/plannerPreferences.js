export function readPageSize(key) {
    let value;
    try {
        value = window.localStorage.getItem(key);
    } catch {
        return null;
    }

    if (value === null) {
        return null;
    }

    const parsed = Number.parseInt(value, 10);
    return Number.isInteger(parsed) ? parsed : null;
}

export function writePageSize(key, value) {
    try {
        window.localStorage.setItem(key, String(value));
    } catch {
        // The preference is optional; planning remains fully usable without storage.
    }
}

const destinationDragBindings = new WeakMap();

export function enableDestinationDragDrop(rail, dotNetReference) {
    if (!rail || destinationDragBindings.has(rail)) {
        return;
    }

    const onDragStart = event => {
        const source = event.target.closest('[data-planner-destination-footstep="true"]');
        if (!source || !rail.contains(source) || !event.dataTransfer) {
            return;
        }

        event.dataTransfer.effectAllowed = 'copy';
        event.dataTransfer.setData('application/x-adventures-suite-footstep', source.dataset.plannerFootstepId);
        event.dataTransfer.setData('text/plain', 'AdventuresSuite destination FootStep');
    };

    const onDragOver = event => {
        const target = event.target.closest('[data-planner-destination-drop="true"]');
        if (!target || !event.dataTransfer) {
            return;
        }

        event.preventDefault();
        event.dataTransfer.dropEffect = 'copy';
    };

    const onDrop = event => {
        const target = event.target.closest('[data-planner-destination-drop="true"]');
        if (!target || !event.dataTransfer) {
            return;
        }

        const footStepId = event.dataTransfer.getData('application/x-adventures-suite-footstep');
        if (!footStepId) {
            return;
        }

        event.preventDefault();
        void dotNetReference.invokeMethodAsync('HandleDestinationFootStepDropAsync', footStepId);
    };

    let pointerDrag = null;
    const onPointerDown = event => {
        if (event.button !== 0 || event.target.closest('button, a, input, select, textarea, summary')) {
            return;
        }

        const source = event.target.closest('[data-planner-destination-footstep="true"]');
        if (!source || !rail.contains(source)) {
            return;
        }

        event.preventDefault();
        pointerDrag = {
            id: source.dataset.plannerFootstepId,
            startX: event.clientX,
            startY: event.clientY,
            active: false
        };
        void dotNetReference.invokeMethodAsync('HandleDestinationFootStepDragStartedAsync', pointerDrag.id);
    };

    const onPointerMove = event => {
        if (!pointerDrag) {
            return;
        }

        if (!pointerDrag.active
            && Math.hypot(event.clientX - pointerDrag.startX, event.clientY - pointerDrag.startY) >= 6) {
            pointerDrag.active = true;
        }
        if (!pointerDrag.active) {
            return;
        }

        event.preventDefault();
        document.querySelectorAll('[data-planner-destination-drop="true"]')
            .forEach(target => target.classList.toggle(
                'planner-board__route--pointer-over', target.contains(document.elementFromPoint(event.clientX, event.clientY))));
    };

    const finishPointerDrag = event => {
        if (!pointerDrag) {
            return;
        }

        const target = document.elementFromPoint(event.clientX, event.clientY)
            ?.closest('[data-planner-destination-drop="true"]');
        const completed = pointerDrag.active && target;
        const footStepId = pointerDrag.id;
        pointerDrag = null;
        document.querySelectorAll('[data-planner-destination-drop="true"]')
            .forEach(candidate => candidate.classList.remove('planner-board__route--pointer-over'));
        if (completed) {
            void dotNetReference.invokeMethodAsync('HandleDestinationFootStepDropAsync', footStepId);
        }
        void dotNetReference.invokeMethodAsync('HandleDestinationFootStepDragEndedAsync');
    };

    document.addEventListener('dragstart', onDragStart, true);
    document.addEventListener('dragover', onDragOver, true);
    document.addEventListener('drop', onDrop, true);
    rail.addEventListener('pointerdown', onPointerDown, true);
    document.addEventListener('pointermove', onPointerMove, { capture: true, passive: false });
    document.addEventListener('pointerup', finishPointerDrag, true);
    document.addEventListener('pointercancel', finishPointerDrag, true);
    destinationDragBindings.set(rail, {
        onDragStart, onDragOver, onDrop, onPointerDown, onPointerMove, finishPointerDrag
    });
}

export function disableDestinationDragDrop(rail) {
    const binding = rail ? destinationDragBindings.get(rail) : null;
    if (!binding) {
        return;
    }

    document.removeEventListener('dragstart', binding.onDragStart, true);
    document.removeEventListener('dragover', binding.onDragOver, true);
    document.removeEventListener('drop', binding.onDrop, true);
    rail.removeEventListener('pointerdown', binding.onPointerDown, true);
    document.removeEventListener('pointermove', binding.onPointerMove, true);
    document.removeEventListener('pointerup', binding.finishPointerDrag, true);
    document.removeEventListener('pointercancel', binding.finishPointerDrag, true);
    destinationDragBindings.delete(rail);
}
