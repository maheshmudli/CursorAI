function initSwapsPage(tenantSlug, isAdmin) {
    if (!isAdmin) {
        loadAssignmentOptions();
        document.getElementById('swap-form').addEventListener('submit', async e => {
            e.preventDefault();
            const body = {
                requestingAssignmentId: parseInt(document.getElementById('requesting-assignment').value),
                targetAssignmentId: parseInt(document.getElementById('target-assignment').value)
            };
            const res = await fetch('/api/swaps', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'same-origin',
                body: JSON.stringify(body)
            });
            if (res.ok) {
                renderSwapsTable(isAdmin);
                loadAssignmentOptions();
            } else {
                const err = await res.json();
                alert(err.message || 'Failed to create swap request');
            }
        });
    }

    renderSwapsTable(isAdmin);
}

async function loadAssignmentOptions() {
    const res = await fetch('/api/shifts', { credentials: 'same-origin' });
    if (!res.ok) return;
    const shifts = await res.json();
    const mine = shifts.filter(s => s.assignmentId);
    const others = shifts.filter(s => s.assignmentId);

    const reqSelect = document.getElementById('requesting-assignment');
    const tgtSelect = document.getElementById('target-assignment');

    // Current user's shifts loaded via dashboard filter - use all assigned for selection
    reqSelect.innerHTML = mine.map(s =>
        `<option value="${s.assignmentId}">${s.roleLabel} — ${s.date} (${s.assignedUserName})</option>`).join('');
    tgtSelect.innerHTML = others.map(s =>
        `<option value="${s.assignmentId}">${s.roleLabel} — ${s.date} (${s.assignedUserName})</option>`).join('');
}

async function renderSwapsTable(isAdmin) {
    const container = document.getElementById('swaps-table');
    const res = await fetch('/api/swaps', { credentials: 'same-origin' });
    if (!res.ok) {
        container.innerHTML = '<div class="alert alert-danger">Failed to load swaps.</div>';
        return;
    }
    const swaps = await res.json();
    let html = '<table class="table table-striped"><thead><tr><th>From</th><th>To</th><th>Shifts</th><th>Status</th><th>Actions</th></tr></thead><tbody>';
    swaps.forEach(s => {
        html += `<tr>
            <td>${s.requestingUserName}</td>
            <td>${s.targetUserName}</td>
            <td>${s.requestingShiftLabel} ↔ ${s.targetShiftLabel}</td>
            <td>${s.status}</td>
            <td>`;
        if (s.status === 'Pending') {
            html += `<button class="btn btn-sm btn-success me-1 approve-btn" data-id="${s.id}">Approve</button>
                     <button class="btn btn-sm btn-danger reject-btn" data-id="${s.id}">Reject</button>`;
        }
        html += '</td></tr>';
    });
    html += '</tbody></table>';
    container.innerHTML = html;

    container.querySelectorAll('.approve-btn').forEach(btn => {
        btn.addEventListener('click', async () => {
            await fetch(`/api/swaps/${btn.dataset.id}/approve`, { method: 'POST', credentials: 'same-origin' });
            renderSwapsTable(isAdmin);
        });
    });
    container.querySelectorAll('.reject-btn').forEach(btn => {
        btn.addEventListener('click', async () => {
            await fetch(`/api/swaps/${btn.dataset.id}/reject`, { method: 'POST', credentials: 'same-origin' });
            renderSwapsTable(isAdmin);
        });
    });
}
