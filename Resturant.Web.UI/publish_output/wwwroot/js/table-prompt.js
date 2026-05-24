var TablePrompt = (function () {
    const TABLE_STORAGE_KEY = 'tableNumber';
    const TABLE_EXPIRY_KEY = 'tableNumberExpiry';
    const LAST_ACTIVE_KEY = 'lastActiveTime';
    const EXPIRY_MINUTES = 480; // 8 Hours (session absolute expiry)
    const INACTIVITY_TIMEOUT_MS = 5 * 60 * 1000; // 5 Minutes inactivity timeout

    let tableNumberModal = null;
    let tableNumberResolve = null;

    // Check inactivity and clear if needed
    function checkInactivity() {
        const storedNumber = localStorage.getItem(TABLE_STORAGE_KEY);
        if (!storedNumber) return; // Nothing to clear

        const lastActive = localStorage.getItem(LAST_ACTIVE_KEY);
        const now = Date.now();

        if (lastActive && (now - parseInt(lastActive) > INACTIVITY_TIMEOUT_MS)) {
            console.log("Session timed out due to inactivity. Clearing table number.");
            clearTableData();
            // Optional: Reload page to show prompt immediately if on a page that requires it
            if (window.location.pathname.includes('/Menu') || window.location.pathname === '/') {
                window.location.reload();
            }
        }
    }

    // Update last active timestamp
    function updateActivity() {
        localStorage.setItem(LAST_ACTIVE_KEY, Date.now().toString());
    }

    // Check if table number is stored locally
    function getStoredTableNumber() {
        try {
            checkInactivity(); // Check before returning

            const storedNumber = localStorage.getItem(TABLE_STORAGE_KEY);
            const expiryTime = localStorage.getItem(TABLE_EXPIRY_KEY);

            if (storedNumber && expiryTime) {
                const now = new Date().getTime();
                if (now < parseInt(expiryTime)) {
                    return parseInt(storedNumber);
                }
            }
        } catch (e) { console.error(e); }
        return null;
    }

    // Store table number with expiry
    function storeTableNumber(number) {
        const now = new Date().getTime();
        const expiryTime = now + (EXPIRY_MINUTES * 60 * 1000);
        localStorage.setItem(TABLE_STORAGE_KEY, number.toString());
        localStorage.setItem(TABLE_EXPIRY_KEY, expiryTime.toString());
        localStorage.setItem(LAST_ACTIVE_KEY, now.toString()); // Init activity
        updateTableNumberUI(number);
    }

    // Prompt user for table number using modal
    async function promptForTableNumber(forceShow = false) {
        // If forceShow is true, we are changing the table, so we don't return early.
        // But if just checking (forceShow=false), getStoredTableNumber will check timeout.
        const storedNumber = getStoredTableNumber();
        let currentTableNumber = storedNumber;

        if (storedNumber && !forceShow) {
            // Trust local storage - do not re-verify with server to avoid re-prompting
            updateTableNumberUI(storedNumber);
            return Promise.resolve(storedNumber);
        }

        return new Promise((resolve) => {
            tableNumberResolve = resolve;
            const modalEl = document.getElementById('tableNumberModal');
            if (!modalEl) {
                console.error("Table Number Modal not found in DOM");
                resolve(null);
                return;
            }

            const bsRequest = window.bootstrap || bootstrap;
            tableNumberModal = new bsRequest.Modal(modalEl, { backdrop: 'static', keyboard: false });

            // Pre-fill with current table number if changing
            const input = document.getElementById('tableNumberInput');
            if (currentTableNumber && currentTableNumber > 0) {
                input.value = currentTableNumber;
            } else {
                input.value = '';
            }
            input.classList.remove('is-invalid');

            tableNumberModal.show();
            // Focus after modal shown
            modalEl.addEventListener('shown.bs.modal', function () {
                input.focus();
            }, { once: true });
        });
    }

    // Confirm table number from modal
    async function confirmTableNumber() {
        const input = document.getElementById('tableNumberInput');
        const errorDiv = document.getElementById('tableNumberError');
        const confirmBtn = document.getElementById('confirmTableBtn');
        const value = parseInt(input.value);

        if (!value || value <= 0) {
            input.classList.add('is-invalid');
            errorDiv.textContent = 'Please enter a valid table number.';
            return;
        }

        confirmBtn.disabled = true;
        confirmBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Checking...';

        try {
            const response = await fetch(`/Order/CheckTable?tableNumber=${value}`);
            // Handle non-200 responses if any, though CheckTable usually returns JSON even on false
            if (!response.ok) throw new Error("Network response was not ok");

            const result = await response.json();

            // RELAXED RULE: If we want to allow multiple orders, we might ignore result.available
            // But usually CheckTable returns available=false if occupied.
            // If the requirement is to allow sharing, we should check if result says "Occupied" but we proceed anyway?
            // For now, adhering to existing logic: if (!result.available) -> error.
            // If the user wants multiple orders, backend CheckTable should have been updated to return available=true even if occupied.
            // OR we bypass this check here?
            // Given the implementation plan only mentioned backend relaxation, let's assume backend CheckTable is what governs this.

            if (!result.available) {
                input.classList.add('is-invalid');
                errorDiv.textContent = result.message || 'Table not available';
                return;
            }

            const newTableNumber = value;
            storeTableNumber(newTableNumber);

            if (tableNumberModal) {
                tableNumberModal.hide();
            }

            if (tableNumberResolve) {
                tableNumberResolve(newTableNumber);
                tableNumberResolve = null;
            }
        } catch (error) {
            console.error(error);
            input.classList.add('is-invalid');
            errorDiv.textContent = 'Could not verify table. Please try again.';
        } finally {
            confirmBtn.disabled = false;
            confirmBtn.innerHTML = '<i class="bi bi-check-lg me-2"></i>Confirm Table';
        }
    }

    // Update table number display in UI
    function updateTableNumberUI(number) {
        // Safe check for element existence
        const tableDisplay = document.getElementById('current-table-number');
        if (tableDisplay) {
            if (number && number > 0) {
                tableDisplay.textContent = number;
                tableDisplay.className = 'badge bg-primary fs-6';
            } else {
                tableDisplay.textContent = 'Not Set';
                tableDisplay.className = 'badge bg-secondary fs-6';
            }
        }
    }

    // Clear table data
    function clearTableData() {
        localStorage.removeItem(TABLE_STORAGE_KEY);
        localStorage.removeItem(TABLE_EXPIRY_KEY);
        localStorage.removeItem(LAST_ACTIVE_KEY);
        updateTableNumberUI(null);
    }

    // Init function to be called on page load
    function init(initialTableNumber) {
        // Activity Listeners
        ['click', 'mousemove', 'keypress', 'scroll', 'touchstart'].forEach(event => {
            document.addEventListener(event, updateActivity, { passive: true });
        });

        // Initial check
        checkInactivity();

        // Periodic check (every 1 minute)
        setInterval(checkInactivity, 60 * 1000);

        // Event Listeners
        const confirmBtn = document.getElementById('confirmTableBtn');
        if (confirmBtn) confirmBtn.addEventListener('click', confirmTableNumber);

        const input = document.getElementById('tableNumberInput');
        if (input) {
            input.addEventListener('keypress', function (e) {
                if (e.key === 'Enter') confirmTableNumber();
            });
        }

        const changeBtn = document.getElementById('change-table-btn');
        if (changeBtn) changeBtn.addEventListener('click', () => promptForTableNumber(true));

        // Logic
        if (initialTableNumber) {
            storeTableNumber(initialTableNumber);
        } else {
            const stored = getStoredTableNumber();
            if (stored) updateTableNumberUI(stored);
        }
    }

    return {
        init: init,
        prompt: promptForTableNumber,
        get: getStoredTableNumber,
        clear: clearTableData
    };
})();
