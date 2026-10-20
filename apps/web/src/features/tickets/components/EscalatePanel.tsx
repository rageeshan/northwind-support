import { useState } from 'react';
import { toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { EscalateTicketPayload, TicketDetail, TicketPriority } from '../../../types/api';

const nextPriority: Partial<Record<TicketPriority, TicketPriority>> = {
  Low: 'Medium',
  Medium: 'High',
  High: 'Critical',
};

interface EscalatePanelProps {
  ticket: TicketDetail;
  onUpdated: (ticket: TicketDetail) => void;
}

/**
 * Shows the current priority and – when escalation is still possible – a form to escalate
 * the ticket one level with a mandatory reason.
 */
export function EscalatePanel({ ticket, onUpdated }: EscalatePanelProps) {
  const [reason, setReason] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string>();
  const [success, setSuccess] = useState<string>();

  const canEscalate =
    ticket.priority !== 'Critical' &&
    ticket.status !== 'Resolved' &&
    ticket.status !== 'Closed';

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(undefined);
    setSuccess(undefined);

    if (reason.trim().length < 5) {
      setError('Reason must be at least 5 characters.');
      return;
    }

    setIsSubmitting(true);
    try {
      const payload: EscalateTicketPayload = {
        reason: reason.trim(),
      };
      const updated = await ticketsApi.escalate(ticket.id, payload);
      setSuccess(`Escalated to ${updated.priority}.`);
      setReason('');
      onUpdated(updated);
    } catch (caught) {
      setError(toErrorMessage(caught, 'Could not escalate the ticket.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const target = nextPriority[ticket.priority];

  return (
    <div className="card escalate-panel">
      <h2>Escalation</h2>

      {!canEscalate ? (
        <p className="escalate-panel__blocked">
          {ticket.priority === 'Critical'
            ? 'This ticket is already Critical and cannot be escalated further.'
            : `This ticket is ${ticket.status} and cannot be escalated.`}
        </p>
      ) : (
        <>
          <p className="escalate-panel__hint">
            Escalating will raise priority from <strong>{ticket.priority}</strong> to{' '}
            <strong>{target}</strong> and re-assign the SLA window from now.
          </p>

          <form className="escalate-panel__form" onSubmit={handleSubmit} noValidate>
            <div className="field">
              <label className="field__label" htmlFor="escalate-reason">
                Reason <span className="field__required">*</span>
              </label>
              <textarea
                id="escalate-reason"
                rows={3}
                placeholder="Why is this ticket being escalated?"
                value={reason}
                maxLength={500}
                onChange={(e) => setReason(e.target.value)}
              />
            </div>

            {error && (
              <p className="field__error" role="alert">
                {error}
              </p>
            )}
            {success && (
              <p className="escalate-panel__success" role="status">
                ✓ {success}
              </p>
            )}

            <div className="button-row">
              <button
                type="submit"
                className="button button--danger"
                disabled={isSubmitting}
              >
                {isSubmitting ? 'Escalating…' : `Escalate to ${target ?? ''}`}
              </button>
            </div>
          </form>
        </>
      )}
    </div>
  );
}
