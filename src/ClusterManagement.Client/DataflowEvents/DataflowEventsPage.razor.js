// Arrow keys move between the rows of the dataflow events table and open or close its groups, as a treegrid promises.
// A heading row is reached through its button, an event row through the row itself; keys pressed inside an input of a
// row keep their usual meaning.
const rowSelector = ':scope > tr.group, :scope > tr.event';

const targetOf = row => row?.classList.contains('group') ? row.querySelector('button.group-toggle') : row;

function parentOf(rows, index) {
    const level = Number(rows[index].getAttribute('aria-level'));
    for (let i = index - 1; i >= 0; i--) {
        if (rows[i].classList.contains('group') && Number(rows[i].getAttribute('aria-level')) === level - 1)
            return rows[i];
    }

    return null;
}

// Returns the row to move to, 'toggle' to open or close the group, null to stay, or undefined for a key it leaves alone.
function moveFor(rows, index, key) {
    const row = rows[index];
    const isGroup = row.classList.contains('group');
    const isExpanded = row.getAttribute('aria-expanded') === 'true';
    switch (key) {
        case 'ArrowDown': return rows[index + 1] ?? null;
        case 'ArrowUp': return rows[index - 1] ?? null;
        case 'ArrowRight': return isGroup ? (isExpanded ? rows[index + 1] ?? null : 'toggle') : undefined;
        case 'ArrowLeft': return isGroup && isExpanded ? 'toggle' : parentOf(rows, index);
        default: return undefined;
    }
}

function onKeyDown(event) {
    const row = event.target.closest('tr.group, tr.event');
    if (!row || targetOf(row) !== event.target)
        return;

    const rows = [...row.parentElement.querySelectorAll(rowSelector)];
    const move = moveFor(rows, rows.indexOf(row), event.key);
    if (move === undefined)
        return;

    event.preventDefault();
    if (move === 'toggle')
        event.target.click();
    else
        targetOf(move)?.focus();
}

export function attach(table) {
    table.addEventListener('keydown', onKeyDown);
}

// Leaving the page removes the table before the page is disposed; its listener went with it.
export function detach(table) {
    table?.removeEventListener('keydown', onKeyDown);
}
