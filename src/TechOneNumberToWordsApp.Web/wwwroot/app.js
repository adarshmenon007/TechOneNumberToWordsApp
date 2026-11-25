'use strict';

// Sends the amount to POST /api/convert and shows the answer (D5). No validation here: every rule lives on the server (D6).
// Everything from the server is shown as plain text with textContent, never as markup (D24).
(function () {
    const FALLBACK = 'Something went wrong. Please try again.';
    const CONVERTING = 'Converting…';
    const TIMEOUT_MS = 15000; // a request gets 15 seconds (D60)

    const form = document.getElementById('convert-form');
    const input = document.getElementById('amount');
    const button = document.getElementById('convert');
    const output = document.getElementById('output');

    let busy = false;

    function result(text) {
        return { kind: 'result', text: text };
    }

    function error(text) {
        return { kind: 'error', text: text };
    }

    // The value of a string field of the response body, or '' when it is missing or not a string.
    function field(body, name) {
        return body !== null && typeof body === 'object' && typeof body[name] === 'string' ? body[name] : '';
    }

    // Asks the server and says what to show. It never throws: a failure is an error to show (D24, F3, D60).
    async function convert(amount) {
        let response;
        try {
            response = await fetch('/api/convert', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ amount: amount }),
                signal: AbortSignal.timeout(TIMEOUT_MS),
            });
        } catch {
            // No answer: offline, the server is down, or the 15 seconds ran out (D60).
            return error(FALLBACK);
        }

        // Read the body as JSON whatever its content type; a proxy's error page, for example, is not JSON.
        let body = null;
        try {
            body = await response.json();
        } catch {
            body = null;
        }

        const words = field(body, 'words');
        if (response.ok && words !== '') {
            return result(words);
        }

        if (response.status === 500) {
            // The server's own text is generic; this one carries the id that finds the log entry (F3, C2).
            const traceId = field(body, 'traceId');
            return error(traceId === '' ? FALLBACK : FALLBACK + ' Reference: ' + traceId);
        }

        // The amount errors and the 413 carry a message for the person typing (D24, C1).
        const detail = field(body, 'detail');
        return error(detail === '' ? FALLBACK : detail);
    }

    function show(outcome) {
        output.className = outcome.kind;
        output.replaceChildren();

        if (outcome.kind === 'error') {
            // The label is its own element, so an error is not shown by colour alone.
            const label = document.createElement('span');
            label.className = 'error-label';
            label.textContent = 'Error:';
            const message = document.createElement('span');
            message.textContent = outcome.text;
            output.append(label, ' ', message);
        } else {
            output.textContent = outcome.text;
        }
    }

    form.addEventListener('submit', async function (event) {
        event.preventDefault();
        if (busy) {
            return;
        }

        // While the request runs: no second submit, no editing (so the answer always matches what is in the field, E6),
        // and the old answer is replaced. The field is read-only rather than disabled so it keeps focus.
        busy = true;
        button.disabled = true;
        input.readOnly = true;
        show({ kind: 'progress', text: CONVERTING });

        let outcome;
        try {
            outcome = await convert(input.value);
        } finally {
            busy = false;
            button.disabled = false;
            input.readOnly = false;
            // A disabled button drops focus; put it back so the next Tab carries on from Convert (E6). Not when focus
            // is somewhere else on purpose, and only after the button is enabled again, because a disabled one cannot take focus.
            if (document.activeElement === null || document.activeElement === document.body) {
                button.focus();
            }
        }

        show(outcome);
    });
})();
