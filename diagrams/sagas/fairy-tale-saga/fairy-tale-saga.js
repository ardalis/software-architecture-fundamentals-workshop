// Fairy Tale Saga: synchronous, eventually consistent, orchestrated.
// The orchestrator calls each service in turn; failures are retried, not rolled back.

import { orchestrated, sagaDiagram } from '../saga-kit.js';

export default sagaDiagram(orchestrated, {
  id: 'saga-fairy-tale',
  title: 'Fairy Tale Saga: Synchronous, Eventual, Orchestrated',
  description:
    'A Checkout Orchestrator calls Inventory, Payments and Ticketing synchronously, and each service ' +
    'commits its own work. When Ticketing fails, the orchestrator tells the fan the order is pending ' +
    'and retries until the tickets are issued, instead of rolling anything back.',

  cast: [
    { ids: ['fan'], step: 1, caption: 'A fan buys tickets. Seats must be held, the card charged, and tickets issued.' },
    {
      ids: ['orchestrator', 'inventory', 'payments', 'ticketing'],
      step: 2,
      caption: 'Once again, a Checkout Orchestrator coordinates the three services.',
    },
  ],

  happy: [
    { id: 'buy', from: 'fan', to: 'orchestrator', label: 'Buy Tickets', step: 3,
      caption: 'First, the happy path.' },
    { id: 'hold', from: 'orchestrator', to: 'inventory', label: 'Hold Seats', step: 4,
      caption: 'The orchestrator calls each service synchronously, one at a time...' },
    { id: 'held', from: 'inventory', to: 'orchestrator', label: 'Seats Held', lane: 1, step: 5,
      caption: '...and each service commits its own local transaction right away.' },
    { id: 'charge', from: 'orchestrator', to: 'payments', label: 'Charge Card', step: 6 },
    { id: 'charged', from: 'payments', to: 'orchestrator', label: 'Charged', lane: 1, step: 7 },
    { id: 'issue', from: 'orchestrator', to: 'ticketing', label: 'Issue Tickets', step: 8 },
    { id: 'issued', from: 'ticketing', to: 'orchestrator', label: 'Tickets Issued', lane: 1, step: 9 },
    { id: 'confirmed', from: 'orchestrator', to: 'fan', label: 'Tickets Confirmed', lane: 1, step: 10,
      caption: 'Every step succeeded, so the fan gets their tickets.' },
  ],

  reset: { caption: 'Now replay the same purchase, but this time Ticketing fails.' },

  messages: [
    { id: 'buy', from: 'fan', to: 'orchestrator', label: 'Buy Tickets', step: 3 },
    { id: 'hold', from: 'orchestrator', to: 'inventory', label: 'Hold Seats', step: 4 },
    { id: 'held', from: 'inventory', to: 'orchestrator', label: 'Seats Held', lane: 1, step: 5 },
    { id: 'charge', from: 'orchestrator', to: 'payments', label: 'Charge Card', step: 6 },
    { id: 'charged', from: 'payments', to: 'orchestrator', label: 'Charged', lane: 1, step: 7 },
    { id: 'issue', from: 'orchestrator', to: 'ticketing', label: 'Issue Tickets', step: 8 },
    { id: 'failed', from: 'ticketing', to: 'orchestrator', label: 'Failed', lane: 1, kind: 'fail', step: 9,
      caption: "Ticketing fails. This saga is eventually consistent, so the orchestrator doesn't undo the earlier work." },
    { id: 'pending', from: 'orchestrator', to: 'fan', label: 'Order Pending', lane: 1, step: 10,
      caption: 'The fan gets an honest answer: the order is in, and the tickets are on the way.' },
    { id: 'retry', from: 'orchestrator', to: 'ticketing', label: 'Retry Issue Tickets', lane: 2, step: 11,
      caption: 'The orchestrator owns the error, so it retries...' },
    { id: 'issued', from: 'ticketing', to: 'orchestrator', label: 'Tickets Issued', lane: 3, step: 12,
      caption: '...until Ticketing recovers and the system is consistent again.' },
  ],

  callout: {
    lines: ['HAPPILY', 'EVER AFTER'],
    step: 13,
    caption: 'Fairy Tale Saga: synchronous, eventually consistent, orchestrated. One owner handles the workflow and its errors.',
  },

  captions: [
    { id: 'wrap-up', step: 14,
      caption: 'It is a common choice: much less coupling than an Epic Saga, with the same familiar request/response style.' },
  ],
});
