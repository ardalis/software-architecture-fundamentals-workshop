// Clean Architecture: the three projects of the Clean Architecture sample (Web,
// Core, Infrastructure). First the compile-time references, which all point at
// Core; then a request at runtime, which flows from Web through Core out to
// Infrastructure, against the direction of the reference that makes it work.
// Coordinates use a 1600 x 660 canvas. Elements sharing a step animate together.

import { Diagram } from 'animated-diagrams/model';

const W = 300; // node width
const H = 140; // node height

// Web and Infrastructure in a row at the top, Core centered below them.
const top = 100; // top of the Web and Infrastructure row
const coreTop = 440; // top of Core
const col = { web: 160, core: 650, infra: 1140 }; // left edges
const mid = { web: 310, core: 800, infra: 1290 }; // centers
const CORE_MID_Y = coreTop + H / 2;

// The center x of a label that sits just right of a vertical line at `x`, rather than on top of it.
// Labels are centered on their point, so this allows about half the label's width (24px bold text).
const beside = (x, label) => x + 14 + label.length * 7.5;

// An edge down from the bottom of Web or Infrastructure, then across into the side of Core,
// labeled beside its vertical part.
const intoCore = ({ id, from, label, variant, caption, step }) => {
  const x = mid[from];
  const side = from === 'web' ? col.core : col.core + W;
  return {
    id, from, to: 'core', label, variant, caption, step,
    points: [{ x, y: top + H }, { x, y: CORE_MID_Y }, { x: side, y: CORE_MID_Y }],
    labelAt: { x: beside(x, label), y: (top + H + CORE_MID_Y) / 2 },
  };
};

// The same route as intoCore, from Core back up to Infrastructure.
const outOfCore = (edge) => ({ ...edge, from: 'core', to: 'infra', points: [...edge.points].reverse() });

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
    { id: 'core', label: 'Core', x: col.core, y: coreTop, width: W, height: H, variant: 'domain', step: 1,
      caption: 'Core holds the domain model, its rules, and the interfaces it needs. It references no other project.' },
    { id: 'infra', label: 'Infrastructure', x: col.infra, y: top, width: W, height: H, variant: 'data', step: 2 },
    { id: 'web', label: 'Web', x: col.web, y: top, width: W, height: H, variant: 'layer', step: 3 },
  ],

  edges: [
    intoCore({
      id: 'infra-core', from: 'infra', label: 'References', step: 2,
      caption: "Infrastructure references Core, and implements Core's interfaces: data access, payments, email.",
    }),
    intoCore({
      id: 'web-core', from: 'web', label: 'References', step: 3,
      caption: "Web references Core to call its use cases.",
    }),
    {
      id: 'web-infra',
      from: 'web',
      to: 'infra',
      label: 'References (only to register implementations at startup)',
      dashed: true,
      // Over the top of the row, labeled above the line.
      points: [{ x: mid.web, y: top }, { x: mid.web, y: 60 }, { x: mid.infra, y: 60 }, { x: mid.infra, y: top }],
      labelAt: { x: 800, y: 32 },
      caption: 'Web also references Infrastructure, but only as the composition root that wires implementations into DI.',
      step: 4,
    },

    // Runtime: after the reset.
    intoCore({
      id: 'run-web-core', from: 'web', label: 'IPlaceOrderService', step: 7,
      caption: 'POST /orders: the Web endpoint calls IPlaceOrderService, an interface Core defines and implements.',
    }),
    outOfCore(intoCore({
      id: 'run-core-infra', from: 'infra', label: 'IOrderRepository', step: 8,
      caption: "PlaceOrderService saves the order through IOrderRepository, and the call lands in Infrastructure's EF Core repository.",
    })),
    {
      id: 'implements',
      from: 'infra',
      to: 'core',
      label: 'Implements IOrderRepository',
      dashed: true,
      // From the left of Infrastructure's bottom edge, across, and down into the top of Core, labeled above the line.
      points: [{ x: 1190, y: top + H }, { x: 1190, y: 340 }, { x: mid.core, y: 340 }, { x: mid.core, y: coreTop }],
      labelAt: { x: 995, y: 318 },
      caption: "The call goes out to Infrastructure, but the reference points back in. That's the Dependency Inversion Principle.",
      step: 9,
    },
  ],

  callouts: [
    {
      id: 'depends-on-nothing',
      lines: ['DEPENDS ON', 'NOTHING!'],
      // Above Core, in the space the runtime arrows use later. The reset clears it before they appear.
      x: 690,
      y: 270,
      width: 220,
      height: 110,
      tail: { x: 800, y: 435 },
      caption: 'Every arrow points at Core. Core never sees EF Core, SMTP, or ASP.NET Core.',
      step: 5,
    },
  ],

  resets: [
    { id: 'runtime', caption: 'Now follow one request at runtime.', step: 6 },
  ],
});

export default cleanArchitecture;
