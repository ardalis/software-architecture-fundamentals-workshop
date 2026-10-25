// Fantasy Fiction Saga: asynchronous, atomic, orchestrated.
// The orchestrator sends messages without waiting, yet must still undo everything on failure.

import { orchestrated, sagaDiagram } from '../saga-kit.js';

export default sagaDiagram(orchestrated, {
  id: 'saga-fantasy-fiction',
  title: 'Fantasy Fiction Saga: Asynchronous, Atomic, Orchestrated',
  description:
    'A Checkout Orchestrator sends asynchronous messages to Inventory, Payments and Ticketing at once. ' +
    'Replies arrive out of order. When Ticketing fails, the orchestrator must refund the card and, ' +
    "once Inventory's late reply arrives, release the seats, while the fan waits for the outcome.",

  cast: [
    { ids: ['fan'], step: 1, caption: 'A fan buys tickets. Seats must be held, the card charged, and tickets issued.' },
    {
      ids: ['orchestrator', 'inventory', 'payments', 'ticketing'],
      step: 2,
      caption: 'A Checkout Orchestrator coordinates the three services.',
    },
  ],

  happy: [
    { id: 'buy', from: 'fan', to: 'orchestrator', label: 'Buy Tickets', step: 3,
      caption: 'First, the happy path.' },
    { id: 'hold', from: 'orchestrator', to: 'inventory', label: 'Hold Seats', async: true, step: 4,
      caption: 'The orchestrator sends asynchronous messages and does not wait for replies.' },
    { id: 'charge', from: 'orchestrator', to: 'payments', label: 'Charge Card', async: true, step: 4 },
    { id: 'issue', from: 'orchestrator', to: 'ticketing', label: 'Issue Tickets', async: true, step: 4 },
    { id: 'held', from: 'inventory', to: 'orchestrator', label: 'Seats Held', lane: 1, async: true, step: 5,
      caption: 'Replies come back as each service finishes.' },
    { id: 'charged', from: 'payments', to: 'orchestrator', label: 'Charged', lane: 1, async: true, step: 5 },
    { id: 'issued', from: 'ticketing', to: 'orchestrator', label: 'Tickets Issued', lane: 1, async: true, step: 5 },
    { id: 'confirmed', from: 'orchestrator', to: 'fan', label: 'Tickets Confirmed', lane: 1, step: 6,
      caption: 'Once every reply is in, the waiting fan gets their tickets.' },
  ],

  reset: { caption: 'Now replay the same purchase, but this time Ticketing fails.' },

  messages: [
    { id: 'buy', from: 'fan', to: 'orchestrator', label: 'Buy Tickets', step: 3 },
    { id: 'hold', from: 'orchestrator', to: 'inventory', label: 'Hold Seats', async: true, step: 4 },
    { id: 'charge', from: 'orchestrator', to: 'payments', label: 'Charge Card', async: true, step: 4 },
    { id: 'issue', from: 'orchestrator', to: 'ticketing', label: 'Issue Tickets', async: true, step: 4 },
    { id: 'charged', from: 'payments', to: 'orchestrator', label: 'Charged', lane: 1, async: true, step: 5,
      caption: 'Replies arrive in whatever order the services finish.' },
    { id: 'failed', from: 'ticketing', to: 'orchestrator', label: 'Failed', lane: 1, async: true, kind: 'fail', step: 6,
      caption: "Ticketing fails, and Inventory hasn't even answered yet." },
    { id: 'refund', from: 'orchestrator', to: 'payments', label: 'Refund Card', lane: 2, async: true, kind: 'compensate', step: 7,
      caption: 'The saga is atomic, so the orchestrator starts compensating...' },
    { id: 'held', from: 'inventory', to: 'orchestrator', label: 'Seats Held', lane: 1, async: true, step: 8,
      caption: "...while still tracking requests in flight. Inventory's late reply needs undoing, too." },
    { id: 'release', from: 'orchestrator', to: 'inventory', label: 'Release Seats', lane: 2, async: true, kind: 'compensate', step: 9 },
    { id: 'purchase-failed', from: 'orchestrator', to: 'fan', label: 'Purchase Failed', lane: 1, kind: 'fail', step: 10,
      caption: 'The fan waited the whole time: in an atomic saga, there is no answer until every reply is in.' },
  ],

  callout: {
    lines: ['PURE', 'FANTASY!'],
    step: 11,
    caption: 'Fantasy Fiction Saga: asynchronous, atomic, orchestrated. Atomicity forces the orchestrator to track every pending request and race.',
  },

  captions: [
    { id: 'wrap-up', step: 12,
      caption: 'It is usually a sign to pick a different trade-off: drop atomicity (Parallel Saga) or drop async (Epic Saga).' },
  ],
});
