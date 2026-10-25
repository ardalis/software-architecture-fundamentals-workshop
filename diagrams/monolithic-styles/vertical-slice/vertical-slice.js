// Vertical Slice Architecture: the features of the Vertical Slice sample, each a
// self-contained slice over a small shared base. Adding a feature (ticket
// transfer, the exercise) adds a slice instead of editing every layer.
// Coordinates use a 1600 x 660 canvas. Elements sharing a step animate together.

import { Diagram } from 'animated-diagrams/model';

const W = 260; // slice width
const SLICE = { y: 40, height: 340 };
const SHARED = { x: 70, y: 500, width: 1460, height: 100 };
const PARTS = 'Request\nEndpoint\nHandler\nResponse';
// A blank line between a slice's name and its parts. An empty line would collapse, so it holds a no-break space.
const BLANK_LINE = String.fromCharCode(0xa0);

// Slice left edges, left to right
const xs = [70, 370, 670, 970, 1270];

const slice = (id, name, i, step, caption, variant = 'domain') => ({
  id,
  label: [name, BLANK_LINE, PARTS].join('\n'),
  x: xs[i],
  y: SLICE.y,
  width: W,
  height: SLICE.height,
  variant,
  step,
  caption,
});

// An unlabeled "uses" edge from the bottom of a slice down to the shared base.
const uses = (id, i, step) => ({
  id: `${id}-shared`,
  from: id,
  to: 'shared',
  points: [{ x: xs[i] + W / 2, y: SLICE.y + SLICE.height }, { x: xs[i] + W / 2, y: SHARED.y }],
  step,
});

const slices = [
  ['place-order', 'Place Order'],
  ['refund-order', 'Refund Order'],
  ['get-order', 'Get Order'],
  ['list-events', 'List Events'],
];

export const verticalSlice = new Diagram({
  id: 'vertical-slice-architecture',
  title: 'Vertical Slice Architecture: One Slice per Feature',
  description:
    'Place Order, Refund Order, Get Order, and List Events are separate slices, each with its own request, endpoint, ' +
    'handler, and response, over a small shared base. Adding Transfer Ticket adds a new slice.',
  width: 1600,
  height: 660,
  stylesheet: new URL('../monolithic-styles.css', import.meta.url).href,
  watermark: { x: 800, y: 638, fontSize: 30 },

  nodes: [
    slice('place-order', 'Place Order', 0, 1,
      'Place Order is one slice: its request, endpoint, handler, and response live together in Features/PlaceOrder.'),
    slice('refund-order', 'Refund Order', 1, 2,
      "Refund Order is another slice. It doesn't call Place Order or reuse its handler."),
    slice('get-order', 'Get Order', 2, 3, 'Every request the API handles gets its own slice.'),
    slice('list-events', 'List Events', 3, 3),
    {
      id: 'shared',
      label: 'Shared: AppDbContext and entities, payments, email',
      ...SHARED,
      variant: 'data',
      step: 4,
      caption: 'Slices share only what is truly common: the data model and infrastructure.',
    },
    slice('transfer', 'Transfer Ticket', 4, 5,
      'A new feature is a new slice. Ticket transfer adds a folder; it does not edit every layer.', 'new'),
  ],

  edges: [
    ...slices.map(([id], i) => uses(id, i, 4)),
    uses('transfer', 4, 5),
  ],

  captions: [
    {
      id: 'wrap-up',
      caption: 'When slices start duplicating rules, refactor: push the shared logic down into the domain model.',
      step: 6,
    },
  ],
});

export default verticalSlice;
