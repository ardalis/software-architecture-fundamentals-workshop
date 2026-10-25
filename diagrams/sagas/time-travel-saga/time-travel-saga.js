// Time Travel Saga: synchronous, eventually consistent, choreographed.
// Each service commits its work and calls the next; failures are retried by the caller.

import { choreographed, sagaDiagram } from '../saga-kit.js';

export default sagaDiagram(choreographed, {
  id: 'saga-time-travel',
  title: 'Time Travel Saga: Synchronous, Eventual, Choreographed',
  description:
    'Checkout calls Inventory, which calls Payments, which calls Ticketing, all synchronously, and each ' +
    'service commits its own work. When Ticketing fails, nothing is rolled back: Payments retries, ' +
    'and the confirmations travel back up the chain to the fan.',

  cast: [
    { ids: ['fan'], step: 1, caption: 'A fan buys tickets. Seats must be held, the card charged, and tickets issued.' },
    {
      ids: ['checkout', 'inventory', 'payments', 'ticketing'],
      step: 2,
      caption: 'There is no orchestrator. Each service knows the next step.',
    },
  ],

  happy: [
    { id: 'buy', from: 'fan', to: 'checkout', label: 'Buy Tickets', step: 3,
      caption: 'First, the happy path.' },
    { id: 'hold', from: 'checkout', to: 'inventory', label: 'Hold Seats', step: 4,
      caption: 'Each service commits its own work, then synchronously hands off to the next, like pipes and filters.' },
    { id: 'charge', from: 'inventory', to: 'payments', label: 'Charge Card', step: 5 },
    { id: 'issue', from: 'payments', to: 'ticketing', label: 'Issue Tickets', step: 6 },
    { id: 'issued', from: 'ticketing', to: 'payments', label: 'Done', step: 7,
      caption: 'The replies travel back up the chain to the fan.' },
    { id: 'paid', from: 'payments', to: 'inventory', label: 'Done', step: 7 },
    { id: 'seated', from: 'inventory', to: 'checkout', label: 'Done', step: 7 },
    { id: 'confirmed', from: 'checkout', to: 'fan', label: 'Tickets Confirmed', lane: 1, step: 7 },
  ],

  reset: { caption: 'Now replay the same purchase, but this time Ticketing fails.' },

  messages: [
    { id: 'buy', from: 'fan', to: 'checkout', label: 'Buy Tickets', step: 3 },
    { id: 'hold', from: 'checkout', to: 'inventory', label: 'Hold Seats', step: 4 },
    { id: 'charge', from: 'inventory', to: 'payments', label: 'Charge Card', step: 5 },
    { id: 'issue', from: 'payments', to: 'ticketing', label: 'Issue Tickets', step: 6 },
    { id: 'failed', from: 'ticketing', to: 'payments', label: 'Failed', kind: 'fail', step: 7,
      caption: 'Ticketing fails. The seats are held and the card is charged; those transactions have already committed.' },
    { id: 'retry', from: 'payments', to: 'ticketing', label: 'Retry Issue Tickets', lane: 1, step: 8,
      caption: 'Nothing is rolled back. Payments owns this hand-off, so it retries.' },
    { id: 'issued', from: 'ticketing', to: 'payments', label: 'Tickets Issued', lane: 1, step: 9,
      caption: 'Ticketing recovers, and the data catches up.' },
    { id: 'paid', from: 'payments', to: 'inventory', label: 'Done', step: 10,
      caption: 'Now the replies travel back up the chain to the fan.' },
    { id: 'seated', from: 'inventory', to: 'checkout', label: 'Done', step: 10 },
    { id: 'confirmed', from: 'checkout', to: 'fan', label: 'Tickets Confirmed', lane: 1, step: 10 },
  ],

  callout: {
    lines: ['NO ROLLBACK,', 'JUST RETRY'],
    step: 11,
    caption: 'Time Travel Saga: synchronous, eventually consistent, choreographed. Simple chains get high throughput.',
  },

  captions: [
    { id: 'wrap-up', step: 12,
      caption: 'But each service handles its own errors, so complex workflows quickly become hard to follow.' },
  ],
});
