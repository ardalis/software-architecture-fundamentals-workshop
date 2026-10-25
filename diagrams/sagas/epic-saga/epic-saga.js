// Epic Saga: synchronous, atomic, orchestrated.
// The orchestrator calls each service in turn and, when one fails, undoes the rest.

import { orchestrated, sagaDiagram } from '../saga-kit.js';

export default sagaDiagram(orchestrated, {
  id: 'saga-epic',
  title: 'Epic Saga: Synchronous, Atomic, Orchestrated',
  description:
    'A Checkout Orchestrator calls Inventory, Payments and Ticketing synchronously, one at a time. ' +
    'When Ticketing fails, it sends compensating requests to refund the card and release the seats, ' +
    'and only then tells the waiting fan that the purchase failed.',

  cast: [
    { ids: ['fan'], step: 1, caption: 'A fan buys tickets. Seats must be held, the card charged, and tickets issued.' },
    {
      ids: ['orchestrator', 'inventory', 'payments', 'ticketing'],
      step: 2,
      caption: 'Three services own that work. A Checkout Orchestrator coordinates them.',
    },
  ],

  happy: [
    { id: 'buy', from: 'fan', to: 'orchestrator', label: 'Buy Tickets', step: 3,
      caption: "First, the happy path. The fan's request is synchronous: they wait for the final answer." },
    { id: 'hold', from: 'orchestrator', to: 'inventory', label: 'Hold Seats', step: 4,
      caption: 'The orchestrator calls each service synchronously, one at a time.' },
    { id: 'held', from: 'inventory', to: 'orchestrator', label: 'Seats Held', lane: 1, step: 5 },
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
      caption: 'Ticketing fails. This saga is atomic, so all the work done so far must be undone.' },
    { id: 'refund', from: 'orchestrator', to: 'payments', label: 'Refund Card', lane: 2, kind: 'compensate', step: 10,
      caption: 'The orchestrator sends compensating requests, newest first: refund the card...' },
    { id: 'release', from: 'orchestrator', to: 'inventory', label: 'Release Seats', lane: 2, kind: 'compensate', step: 11,
      caption: '...then release the seats.' },
    { id: 'purchase-failed', from: 'orchestrator', to: 'fan', label: 'Purchase Failed', lane: 1, kind: 'fail', step: 12,
      caption: 'Only now does the fan get an answer: nothing happened.' },
  ],

  callout: {
    lines: ['ALL OR', 'NOTHING!'],
    step: 13,
    caption: 'Epic Saga: synchronous, atomic, orchestrated. It acts like a monolith\'s database transaction.',
  },

  captions: [
    { id: 'wrap-up', step: 14,
      caption: 'It is easy to understand, but it is tightly coupled: every service must be up, and the fan waits for all of them.' },
  ],
});
