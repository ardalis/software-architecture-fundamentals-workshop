// Layered (N-tier) architecture: Presentation, Business Logic, and Data Access
// stacked over the database, each calling only the layer below it. Then relaxed
// layering skips a layer, and the architecture sinkhole is called out.
// Coordinates use a 1600 x 700 canvas. Elements sharing a step animate together.

import { Diagram } from 'animated-diagrams/model';

const X = 500; // left edge of every layer
const W = 600; // layer width
const H = 110; // layer height
const MID = X + W / 2;

// Top of each layer
const row = { ui: 30, business: 200, data: 370, db: 540 };

// The center x of a label that sits just right of a vertical line at `x`, rather than on top of it.
// Labels are centered on their point, so this allows about half the label's width (24px bold text).
const beside = (x, label) => x + 14 + label.length * 7.5;

// An edge from the bottom of one layer to the top of the next.
const calls = (id, from, to, step, label = 'Calls') => ({
  id,
  from,
  to,
  label,
  points: [{ x: MID, y: row[from] + H }, { x: MID, y: row[to] }],
  labelAt: { x: beside(MID, label), y: (row[from] + H + row[to]) / 2 },
  step,
});

export const layered = new Diagram({
  id: 'layered-architecture',
  title: 'Layered (N-tier) Architecture',
  description:
    'Presentation calls Business Logic, which calls Data Access, which talks to the database. ' +
    'Relaxed layering lets Presentation skip to Data Access, and layers that only pass requests along form a sinkhole.',
  width: 1600,
  height: 700,
  stylesheet: new URL('../monolithic-styles.css', import.meta.url).href,
  watermark: { x: 250, y: 670, fontSize: 36 },

  nodes: [
    { id: 'ui', label: 'Presentation', x: X, y: row.ui, width: W, height: H, variant: 'layer', step: 1,
      caption: 'Presentation: the pages, controllers, and API endpoints.' },
    { id: 'business', label: 'Business Logic', x: X, y: row.business, width: W, height: H, variant: 'domain', step: 2,
      caption: 'Business Logic holds the rules, and Presentation calls down into it.' },
    { id: 'data', label: 'Data Access', x: X, y: row.data, width: W, height: H, variant: 'layer', step: 3,
      caption: 'Business Logic calls Data Access to load and save.' },
    { id: 'db', label: 'Database', x: X, y: row.db, width: W, height: H, variant: 'data', step: 4,
      caption: 'Data Access talks to the database.' },
  ],

  edges: [
    calls('ui-business', 'ui', 'business', 2),
    calls('business-data', 'business', 'data', 3),
    calls('data-db', 'data', 'db', 4, 'SQL'),
    {
      id: 'ui-data',
      from: 'ui',
      to: 'data',
      label: 'Skips a layer',
      dashed: true,
      points: [
        { x: X + W, y: row.ui + H / 2 },
        { x: 1300, y: row.ui + H / 2 },
        { x: 1300, y: row.data + H / 2 },
        { x: X + W, y: row.data + H / 2 },
      ],
      labelAt: { x: beside(1300, 'Skips a layer'), y: (row.ui + row.data + H) / 2 },
      caption: 'Relaxed layering lets a layer skip the one below it. Simpler for reads, but now Presentation depends on Data Access too.',
      step: 6,
    },
  ],

  callouts: [
    {
      id: 'sinkhole',
      lines: ['SINKHOLE?'],
      x: 220,
      y: 90,
      width: 230,
      height: 80,
      tail: { x: 505, y: 235 },
      highlights: ['ui-business', 'business-data'],
      caption: "When most requests pass through a layer that does no real work, that's the architecture sinkhole.",
      step: 7,
    },
  ],

  captions: [
    {
      id: 'top-down',
      caption: 'Every dependency points down, ending at the database. Changing one feature usually means touching every layer.',
      step: 5,
    },
    {
      id: 'wrap-up',
      caption: 'Layers separate technical concerns, but the business rules end up depending on data access.',
      step: 8,
    },
  ],
});

export default layered;
