// Phone Tag Saga: synchronous, atomic, choreographed.
// Each service calls the next; a failure travels back down the chain as compensations.

import { choreographed, sagaDiagram } from '../saga-kit.js';

export default sagaDiagram(choreographed, {
  id: 'saga-phone-tag',
  title: 'Phone Tag Saga: Synchronous, Atomic, Choreographed',
  description:
    'Checkout calls Inventory, which calls Payments, which calls Ticketing, all synchronously. ' +
    'When Ticketing fails, the failure travels back along the chain: Payments refunds the card, ' +
    'Inventory releases the seats, and Checkout tells the fan the purchase failed.',

  cast: [
    { ids: ['fan'], step: 1, caption: 'A fan buys tickets. Seats must be held, the card charged, and tickets issued.' },
    {
      ids: ['checkout', 'inventory', 'payments', 'ticketing'],
      step: 2,
      caption: 'There is no orchestrator this time. Each service knows the next step and calls it directly.',
    },
  ],

  happy: [
    { id: 'buy', from: 'fan', to: 'checkout', label: 'Buy Tickets', step: 3,
      caption: 'First, the happy path. The fan makes a synchronous call to Checkout, the first service in the chain.' },
    { id: 'hold', from: 'checkout', to: 'inventory', label: 'Hold Seats', step: 4,
      caption: 'Checkout calls Inventory...' },
    { id: 'charge', from: 'inventory', to: 'payments', label: 'Charge Card', step: 5,
      caption: '...which calls Payments...' },
    { id: 'issue', from: 'payments', to: 'ticketing', label: 'Issue Tickets', step: 6,
      caption: '...which calls Ticketing. Every service waits on the next.' },
    { id: 'issued', from: 'ticketing', to: 'payments', label: 'Done', step: 7,
      caption: 'The replies travel back up the chain...' },
    { id: 'charged', from: 'payments', to: 'inventory', label: 'Done', step: 8 },
    { id: 'held', from: 'inventory', to: 'checkout', label: 'Done', step: 9 },
    { id: 'confirmed', from: 'checkout', to: 'fan', label: 'Tickets Confirmed', lane: 1, step: 10,
      caption: '...and the fan gets their tickets.' },
  ],

  reset: { caption: 'Now replay the same purchase, but this time Ticketing fails.' },

  messages: [
    { id: 'buy', from: 'fan', to: 'checkout', label: 'Buy Tickets', step: 3 },
    { id: 'hold', from: 'checkout', to: 'inventory', label: 'Hold Seats', step: 4 },
    { id: 'charge', from: 'inventory', to: 'payments', label: 'Charge Card', step: 5 },
    { id: 'issue', from: 'payments', to: 'ticketing', label: 'Issue Tickets', step: 6 },
    { id: 'failed', from: 'ticketing', to: 'payments', label: 'Failed', kind: 'fail', step: 7,
      caption: 'Ticketing fails. The saga is atomic, so everything must be undone.' },
    { id: 'refunded', from: 'payments', to: 'inventory', label: 'Refunded, Failed', kind: 'compensate', step: 8,
      caption: 'With no orchestrator, the failure travels back along the chain. Payments refunds the card...' },
    { id: 'released', from: 'inventory', to: 'checkout', label: 'Released, Failed', kind: 'compensate', step: 9,
      caption: '...Inventory releases the seats...' },
    { id: 'purchase-failed', from: 'checkout', to: 'fan', label: 'Purchase Failed', lane: 1, kind: 'fail', step: 10,
      caption: '...and Checkout finally tells the fan.' },
  ],

  callout: {
    lines: ['PHONE', 'TAG!'],
    step: 11,
    caption: 'Phone Tag Saga: synchronous, atomic, choreographed. There is no orchestrator to bottleneck, but every service carries workflow and undo logic.',
  },

  captions: [
    { id: 'wrap-up', step: 12,
      caption: 'This works for short, linear workflows. It gets hard to change as the workflow grows.' },
  ],
});
