let usersCache = [];

async function loadUsers() {
    const res = await fetch('/api/users', { credentials: 'same-origin' });
    if (res.ok) usersCache = await res.json();
    return usersCache;
}

async function loadShifts() {
    const res = await fetch('/api/shifts', { credentials: 'same-origin' });
    if (!res.ok) throw new Error('Failed to load shifts');
    return res.json();
}

function initShiftsPage(tenantSlug) {
    document.getElementById('create-shift-form').addEventListener('submit', async e => {
        e.preventDefault();
        const body = {
            date: document.getElementById('shift-date').value,
            startTime: document.getElementById('shift-start').value,
            endTime: document.getElementById('shift-end').value,
            roleLabel: document.getElementById('shift-role').value,
            notes: document.getElementById('shift-notes').value || null
        };
        const res = await fetch('/api/shifts', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify(body)
        });
        if (res.ok) {
            e.target.reset();
            renderShiftsTable();
        } else {
            const err = await res.json();
            alert(err.message || 'Failed to create shift');
        }
    });

    renderShiftsTable();
}

async function renderShiftsTable() {
    const container = document.getElementById('shifts-table');
    try {
        const [shifts, users] = await Promise.all([loadShifts(), loadUsers()]);
        let html = '<table class="table table-striped"><thead><tr><th>Date</th><th>Time</th><th>Role</th><th>Assigned</th><th>Actions</th></tr></thead><tbody>';
        shifts.forEach(s => {
            const userOptions = users.map(u =>
                `<option value="${u.id}" ${u.id === s.assignedUserId ? 'selected' : ''}>${u.fullName}</option>`).join('');
            html += `<tr>
                <td>${s.date}</td>
                <td>${s.startTime}–${s.endTime}</td>
                <td>${s.roleLabel}</td>
                <td>
                    <select class="form-select form-select-sm assign-select" data-shift-id="${s.id}">
                        <option value="">— Unassigned —</option>${userOptions}
                    </select>
                </td>
                <td><button class="btn btn-sm btn-outline-danger delete-btn" data-id="${s.id}">Delete</button></td>
            </tr>`;
        });
        html += '</tbody></table>';
        container.innerHTML = html;

        container.querySelectorAll('.assign-select').forEach(sel => {
            sel.addEventListener('change', async () => {
                if (!sel.value) return;
                await fetch(`/api/shifts/${sel.dataset.shiftId}/assign`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    credentials: 'same-origin',
                    body: JSON.stringify({ userId: sel.value })
                });
            });
        });

        container.querySelectorAll('.delete-btn').forEach(btn => {
            btn.addEventListener('click', async () => {
                if (!confirm('Delete this shift?')) return;
                await fetch(`/api/shifts/${btn.dataset.id}`, { method: 'DELETE', credentials: 'same-origin' });
                renderShiftsTable();
            });
        });
    } catch {
        container.innerHTML = '<div class="alert alert-danger">Failed to load shifts.</div>';
    }
}
