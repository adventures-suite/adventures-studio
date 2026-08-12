const strictUtc = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/;

function fail(classification) { throw new Error(classification); }

function parseUtc(value) {
  if (typeof value !== 'string' || !strictUtc.test(value)) fail('invalid_authority_window');
  const milliseconds = Date.parse(value);
  if (!Number.isFinite(milliseconds) || new Date(milliseconds).toISOString().replace('.000Z', 'Z') !== value) {
    fail('invalid_authority_window');
  }
  return milliseconds;
}

export function validateAuthorityWindow(assignedAt, deadline, now = new Date(), requireActive = true) {
  const assigned = parseUtc(assignedAt);
  const expires = parseUtc(deadline);
  const current = now instanceof Date ? now.getTime() : parseUtc(now);
  if (!Number.isFinite(current) || expires <= assigned || expires - assigned > 30 * 60 * 1000) fail('invalid_authority_window');
  if (requireActive && (current < assigned || current >= expires)) fail('authority_window_expired');
  return { assignmentTimestampUtc: assignedAt, authorityDeadlineUtc: deadline };
}

if (process.argv[1]?.endsWith('foundation-authority-window.mjs')) {
  try {
    const [mode, assignedAt, deadline] = process.argv.slice(2);
    if (!['active', 'cleanup'].includes(mode)) fail('invalid_authority_window');
    process.stdout.write(`${JSON.stringify(validateAuthorityWindow(assignedAt, deadline, new Date(), mode === 'active'))}\n`);
  } catch (error) {
    process.stdout.write(`${JSON.stringify({ classification: error.message })}\n`);
    process.exitCode = 1;
  }
}
