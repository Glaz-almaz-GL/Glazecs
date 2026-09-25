// Выделение строк таблицы мышью: щелчок с Ctrl/Shift и рамка выделения, как в Проводнике.
// Строка таблицы опознаётся по классу "fmms-row-id-<Id>", который ставит GridSelectionController.

const STYLE_HREF = "./_content/Glazecs.Modules.FMMS/css/gridSelection.css";
const DRAG_THRESHOLD_PX = 5;
const AUTOSCROLL_ZONE_PX = 40;
const AUTOSCROLL_STEP_PX = 20;
const IGNORED_TARGETS = "input, textarea, select, button, a, label, .mud-checkbox, .mud-button-root, .mud-resizer, .mud-menu";

function ensureStyles() {
    if (document.querySelector(`link[href="${STYLE_HREF}"]`)) {
        return;
    }

    const link = document.createElement("link");
    link.rel = "stylesheet";
    link.href = STYLE_HREF;
    document.head.appendChild(link);
}

function getRowId(row) {
    for (const cls of row.classList) {
        if (cls.startsWith("fmms-row-id-")) {
            return parseInt(cls.substring("fmms-row-id-".length), 10);
        }
    }

    return null;
}

function findRow(target, container) {
    const row = target.closest("tr.fmms-row");
    return row && container.contains(row) ? row : null;
}

function findScroller(element) {
    for (let node = element.parentElement; node; node = node.parentElement) {
        const overflowY = getComputedStyle(node).overflowY;
        if ((overflowY === "auto" || overflowY === "scroll") && node.scrollHeight > node.clientHeight) {
            return node;
        }
    }

    return document.scrollingElement || document.documentElement;
}

// Флажок выделения строки полем ввода не считается: с фокусом на нём Ctrl+A должен выделять строки
function isEditable(target) {
    return target.isContentEditable || target.matches("textarea, select, input:not([type=checkbox]):not([type=radio])");
}

class GridSelection {
    constructor(container, dotNetRef) {
        this.container = container;
        this.dotNetRef = dotNetRef;
        this.drag = null;
        this.suppressClick = false;

        this.onMouseDown = this.onMouseDown.bind(this);
        this.onMouseMove = this.onMouseMove.bind(this);
        this.onMouseUp = this.onMouseUp.bind(this);
        this.onClick = this.onClick.bind(this);
        this.onKeyDown = this.onKeyDown.bind(this);
        this.onScroll = this.onScroll.bind(this);

        container.addEventListener("mousedown", this.onMouseDown);
        // Фаза перехвата: щелчок после рамки не должен дойти до обработчика строки MudDataGrid
        container.addEventListener("click", this.onClick, true);
        container.addEventListener("keydown", this.onKeyDown);
    }

    // Координаты точки в системе содержимого прокручиваемого предка — рамка держится за строки при прокрутке
    toContent(clientX, clientY) {
        const scroller = this.drag.scroller;
        const isRoot = scroller === document.scrollingElement || scroller === document.documentElement;
        const rect = isRoot ? { left: 0, top: 0 } : scroller.getBoundingClientRect();
        return {
            x: clientX - rect.left + scroller.scrollLeft,
            y: clientY - rect.top + scroller.scrollTop
        };
    }

    toClient(x, y) {
        const scroller = this.drag.scroller;
        const isRoot = scroller === document.scrollingElement || scroller === document.documentElement;
        const rect = isRoot ? { left: 0, top: 0 } : scroller.getBoundingClientRect();
        return {
            x: x - scroller.scrollLeft + rect.left,
            y: y - scroller.scrollTop + rect.top
        };
    }

    onMouseDown(e) {
        if (e.button !== 0 || e.target.closest(IGNORED_TARGETS)) {
            return;
        }

        const row = findRow(e.target, this.container);
        if (!row) {
            return;
        }

        this.container.focus({ preventScroll: true });

        this.drag = {
            active: false,
            additive: e.ctrlKey || e.metaKey,
            startRow: row,
            startClientX: e.clientX,
            startClientY: e.clientY,
            lastClientX: e.clientX,
            lastClientY: e.clientY,
            scroller: findScroller(this.container),
            box: null,
            lastIds: "",
            frame: 0,
            autoScrollTimer: 0
        };
        this.drag.start = this.toContent(e.clientX, e.clientY);

        document.addEventListener("mousemove", this.onMouseMove);
        document.addEventListener("mouseup", this.onMouseUp);
    }

    onMouseMove(e) {
        const drag = this.drag;
        if (!drag) {
            return;
        }

        drag.lastClientX = e.clientX;
        drag.lastClientY = e.clientY;

        if (!drag.active) {
            const dx = Math.abs(e.clientX - drag.startClientX);
            const dy = Math.abs(e.clientY - drag.startClientY);
            if (dx < DRAG_THRESHOLD_PX && dy < DRAG_THRESHOLD_PX) {
                return;
            }

            this.beginArea();
        }

        e.preventDefault();
        this.updateAutoScroll();
        this.scheduleUpdate();
    }

    beginArea() {
        const drag = this.drag;
        drag.active = true;

        drag.box = document.createElement("div");
        drag.box.className = "fmms-selection-box";
        document.body.appendChild(drag.box);

        drag.scroller.addEventListener("scroll", this.onScroll, { passive: true });
        if (drag.scroller === document.scrollingElement || drag.scroller === document.documentElement) {
            window.addEventListener("scroll", this.onScroll, { passive: true });
        }

        this.dotNetRef.invokeMethodAsync("OnAreaStart", drag.additive);
    }

    onScroll() {
        this.scheduleUpdate();
    }

    scheduleUpdate() {
        const drag = this.drag;
        if (!drag || drag.frame) {
            return;
        }

        drag.frame = requestAnimationFrame(() => {
            if (this.drag === drag) {
                drag.frame = 0;
                this.updateArea();
            }
        });
    }

    updateArea() {
        const drag = this.drag;
        const current = this.toContent(drag.lastClientX, drag.lastClientY);
        const topLeft = this.toClient(Math.min(drag.start.x, current.x), Math.min(drag.start.y, current.y));
        const width = Math.abs(current.x - drag.start.x);
        const height = Math.abs(current.y - drag.start.y);

        drag.box.style.left = `${topLeft.x}px`;
        drag.box.style.top = `${topLeft.y}px`;
        drag.box.style.width = `${width}px`;
        drag.box.style.height = `${height}px`;

        // Строка выделяется целиком, если рамка задевает её по вертикали (как FullRowSelect в DataGridView)
        const boxTop = topLeft.y;
        const boxBottom = topLeft.y + height;
        const ids = [];
        for (const row of this.container.querySelectorAll("tr.fmms-row")) {
            const rect = row.getBoundingClientRect();
            if (rect.bottom >= boxTop && rect.top <= boxBottom) {
                const id = getRowId(row);
                if (id !== null) {
                    ids.push(id);
                }
            }
        }

        // Первой идёт строка, с которой начали, — она станет опорной для Shift
        if (current.y < drag.start.y) {
            ids.reverse();
        }

        const key = ids.join(",");
        if (key !== drag.lastIds) {
            drag.lastIds = key;
            this.dotNetRef.invokeMethodAsync("OnAreaChange", ids);
        }
    }

    updateAutoScroll() {
        const drag = this.drag;
        const scroller = drag.scroller;
        const isRoot = scroller === document.scrollingElement || scroller === document.documentElement;
        const top = isRoot ? 0 : scroller.getBoundingClientRect().top;
        const bottom = isRoot ? window.innerHeight : scroller.getBoundingClientRect().bottom;

        let step = 0;
        if (drag.lastClientY < top + AUTOSCROLL_ZONE_PX) {
            step = -AUTOSCROLL_STEP_PX;
        } else if (drag.lastClientY > bottom - AUTOSCROLL_ZONE_PX) {
            step = AUTOSCROLL_STEP_PX;
        }

        clearInterval(drag.autoScrollTimer);
        drag.autoScrollTimer = 0;
        if (step !== 0) {
            drag.autoScrollTimer = setInterval(() => {
                scroller.scrollTop += step;
            }, 30);
        }
    }

    onMouseUp() {
        const drag = this.drag;
        document.removeEventListener("mousemove", this.onMouseMove);
        document.removeEventListener("mouseup", this.onMouseUp);

        if (!drag) {
            return;
        }

        if (drag.active) {
            cancelAnimationFrame(drag.frame);
            drag.frame = 0;
            this.updateArea();
            this.suppressClick = true;
            setTimeout(() => { this.suppressClick = false; }, 0);
        }

        this.endDrag();
    }

    endDrag() {
        const drag = this.drag;
        if (!drag) {
            return;
        }

        cancelAnimationFrame(drag.frame);
        clearInterval(drag.autoScrollTimer);
        drag.scroller.removeEventListener("scroll", this.onScroll);
        window.removeEventListener("scroll", this.onScroll);
        drag.box?.remove();
        this.drag = null;
    }

    onClick(e) {
        if (this.suppressClick) {
            e.stopPropagation();
            this.suppressClick = false;
            return;
        }

        // Флажок в строке и кнопки обрабатывает сам MudDataGrid
        if (e.button !== 0 || e.target.closest(IGNORED_TARGETS)) {
            return;
        }

        const row = findRow(e.target, this.container);
        const id = row ? getRowId(row) : null;
        if (id === null) {
            return;
        }

        this.dotNetRef.invokeMethodAsync("OnRowClick", id, e.ctrlKey || e.metaKey, e.shiftKey);
    }

    onKeyDown(e) {
        if (isEditable(e.target)) {
            return;
        }

        const ctrl = e.ctrlKey || e.metaKey;
        const handled =
            (ctrl && (e.code === "KeyA" || e.code === "KeyC")) ||
            e.code === "ArrowUp" || e.code === "ArrowDown" ||
            e.code === "Home" || e.code === "End";

        // Иначе WebView выделит весь текст страницы или прокрутит её
        if (handled) {
            e.preventDefault();
        }
    }

    scrollRowIntoView(id) {
        const row = this.container.querySelector(`tr.fmms-row-id-${id}`);
        row?.scrollIntoView({ block: "nearest" });
    }

    dispose() {
        this.endDrag();
        this.container.removeEventListener("mousedown", this.onMouseDown);
        this.container.removeEventListener("click", this.onClick, true);
        this.container.removeEventListener("keydown", this.onKeyDown);
    }
}

export function attach(container, dotNetRef) {
    ensureStyles();
    return new GridSelection(container, dotNetRef);
}
