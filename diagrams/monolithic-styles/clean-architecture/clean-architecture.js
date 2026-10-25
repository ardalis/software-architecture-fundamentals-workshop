// Clean Architecture: the three projects of the Clean Architecture sample (Web,
// Core, Infrastructure). First the compile-time references, which all point at
// Core; then a request at runtime, which flows from Web through Core out to
// Infrastructure, against the direction of the reference that makes it work.
// Coordinates use a 1600 x 660 canvas. Elements sharing a step animate together.

import { Diagram } from 'animated-diagrams/model';

const W = 300; // node width
const H = 140; // node height
const Y = 300; // top of every node
const MID_Y = Y + H / 2;

// Node left edges and centers
const col = { web: 60, core: 650, infra: 1240 };
const mid = { web: 210, core: 800, infra: 1390 };

// A straight, horizontal edge between two adjacent nodes.
const across = ({ id, from, to, label, x1, x2, variant, caption, step }) => ({
  id, from, to, label, variant, caption, step,
  points: [{ x: x1, y: MID_Y }, { x: x2, y: MID_Y }],
  labelAt: { x: (x1 + x2) / 2, y: MID_Y },
});

export const cleanArchitecture = new Diagram({
  id: 'clean-architecture',
  title: 'Clean Architecture: Dependencies Point Inward',
  description:
    'Web and Infrastructure both reference Core, and Core references nothing. At runtime a request flows from Web ' +
    'into Core and out to Infrastructure through an interface that Core defines and Infrastructure implements.',
  width: 1600,
  height: 660,
  stylesheet: new URL('../monolithic-styles.css', import.meta.url).href,
  watermark: { x: 260, y: 620, fontSize: 32 },

  nodes: [
    { id: 'core', label: 'Core', x: col.core, y: Y, width: W, height: H, variant: 'domain', step: 1,
      caption: 'Core holds the domain model, its rules, and the interfaces it needs. It references no other project.' },
    { id: 'infra', label: 'Infrastructure', x: col.infra, y: Y, width: W, height: H, variant: 'data', step: 2 },
    { id: 'web', label: 'Web', x: col.web, y: Y, width: W, height: H, variant: 'layer', step: 3 },
  ],

  edges: [
    across({
      id: 'infra-core', from: 'infra', to: 'core', label: 'References',
      x1: col.infra, x2: col.core + W, step: 2,
      caption: "Infrastructure references Core, and implements Core's interfaces: data access, payments, email.",
    }),
    across({
      id: 'web-core', from: 'web', to: 'core', label: 'References',
      x1: col.web + W, x2: col.core, step: 3,
      caption: "Web references Core to call its use cases.",
    }),
    {
      id: 'web-infra',
      from: 'web',
      to: 'infra',
      label: 'References (only to register implementations at startup)',
      dashed: true,
      points: [{ x: mid.web, y: Y }, { x: mid.web, y: 60 }, { x: mid.infra, y: 60 }, { x: mid.infra, y: Y }],
      labelAt: { x: 800, y: 60 },
      caption: 'Web also references Infrastructure, but only as the composition root that wires implementations into DI.',
      step: 4,
    },

    // Runtime: after the reset.
    across({
      id: 'run-web-core', from: 'web', to: 'core', label: 'PlaceOrderService',
      x1: col.web + W, x2: col.core, step: 7,
      caption: 'POST /orders: the Web endpoint calls PlaceOrderService in Core.',
    }),
    across({
      id: 'run-core-infra', from: 'core', to: 'infra', label: 'IOrderRepository',
      x1: col.core + W, x2: col.infra, step: 8,
      caption: "Core saves the order through IOrderRepository, and the call lands in Infrastructure's EF Core repository.",
    }),
    {
      id: 'implements',
      from: 'infra',
      to: 'core',
      label: 'Implements IOrderRepository',
      dashed: true,
      points: [{ x: mid.infra, y: Y + H }, { x: mid.infra, y: 540 }, { x: mid.core, y: 540 }, { x: mid.core, y: Y + H }],
      labelAt: { x: 1095, y: 540 },
      caption: "The call goes out to Infrastructure, but the reference points back in. That's the Dependency Inversion Principle.",
      step: 9,
    },
  ],

  callouts: [
    {
      id: 'depends-on-nothing',
      lines: ['DEPENDS ON', 'NOTHING!'],
      x: 1000,
      y: 130,
      width: 220,
      height: 110,
      tail: { x: 940, y: 295 },
      caption: 'Every arrow points at Core. Core never sees EF Core, SMTP, or ASP.NET Core.',
      step: 5,
    },
  ],

  resets: [
    { id: 'runtime', caption: 'Now follow one request at runtime.', step: 6 },
  ],
});

export default cleanArchitecture;
