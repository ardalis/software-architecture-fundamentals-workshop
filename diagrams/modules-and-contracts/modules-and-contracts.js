// "Modules and contracts" e-commerce diagram: each module exposes a contracts
// project, and modules only depend on each other's contracts.
// Coordinates use a 1660 x 660 canvas. Elements sharing a step animate together.

import { Diagram } from 'animated-diagrams/model';

const W = 240; // node width
const MODULE_H = 97;
const CONTRACT_H = 50;

// Column left edges and centers
const col = { a: 55, b: 445, c: 918, d: 1372 };
const mid = { a: 175, b: 565, c: 1038, d: 1492 };

// A module and its contracts project: the contracts sit above the module,
// and a dashed "References" edge runs from the module up to them.
// `caption` plays as the pair appears; `referencesCaption` (optional) as the edge draws.
function moduleWithContracts({ id, label, contractsLabel, x, cx, contractsY, moduleY, caption, referencesCaption, step }) {
  return {
    nodes: [
      { id, label, x, y: moduleY, width: W, height: MODULE_H, radius: 14, variant: 'module', caption, step },
      {
        id: `${id}-contracts`,
        label: contractsLabel ?? `${label} Contracts`,
        x,
        y: contractsY,
        width: W,
        height: CONTRACT_H,
        radius: 20,
        variant: 'contract',
        step,
      },
    ],
    edge: {
      id: `${id}-references`,
      from: id,
      to: `${id}-contracts`,
      label: 'References',
      variant: 'reference',
      dashed: true,
      points: [{ x: cx, y: moduleY }, { x: cx, y: contractsY + CONTRACT_H }],
      labelAt: { x: cx, y: (moduleY + contractsY + CONTRACT_H) / 2 },
      caption: referencesCaption,
      step: step + 1,
    },
  };
}

const modules = [
  moduleWithContracts({
    id: 'customers', label: 'Customers', x: col.a, cx: mid.a, contractsY: 338, moduleY: 437,
    caption: 'Again, we begin with Customers. This time the module comes with a separate Contracts project.',
    referencesCaption: 'Customers references its own Contracts, which hold the only types other modules may use.',
    step: 1,
  }),
  moduleWithContracts({
    id: 'cart', label: 'Shopping Cart', x: col.b, cx: mid.b, contractsY: 338, moduleY: 437,
    caption: 'Shopping Cart gets its own Contracts project, too.',
    step: 3,
  }),
  moduleWithContracts({
    id: 'products', label: 'Products', x: col.b, cx: mid.b, contractsY: 104, moduleY: 205,
    caption: 'Products follows the same pattern.',
    step: 7,
  }),
  moduleWithContracts({
    id: 'orders', label: 'Orders', x: col.c, cx: mid.c, contractsY: 338, moduleY: 437,
    caption: 'Orders gets a Contracts project as well.',
    step: 10,
  }),
  moduleWithContracts({
    id: 'inventory', label: 'Inventory', x: col.d, cx: mid.d, contractsY: 338, moduleY: 437,
    caption: 'Inventory exposes its own Contracts.',
    step: 16,
  }),
  moduleWithContracts({
    id: 'shipping', label: 'Shipping', x: col.d, cx: mid.d, contractsY: 94, moduleY: 193,
    caption: 'Finally, Shipping and its Contracts.',
    step: 19,
  }),
];

export const modulesAndContracts = new Diagram({
  id: 'modules-and-contracts',
  stylesheet: new URL('./styles.css', import.meta.url).href,
  title: 'Modules and Contracts',
  description:
    'Six e-commerce modules each expose a contracts project. Modules reference their own contracts ' +
    "and depend only on other modules' contracts, never on each other directly.",
  width: 1660,
  height: 660,
  // In the open space between the Product Name and Verify Price lines.
  watermark: { text: 'ModularMonoliths.com', x: 955, y: 220, fontSize: 32 },
  arrow: { length: 11, halfWidth: 5.5 },

  nodes: modules.flatMap((m) => m.nodes),

  edges: [
    ...modules.map((m) => m.edge),
    {
      id: 'customers-cart',
      from: 'customers',
      to: 'cart-contracts',
      label: 'Has a',
      caption: "Customers have a Shopping Cart, but they depend only on the Cart's Contracts.",
      points: [{ x: 295, y: 488 }, { x: 370, y: 488 }, { x: 370, y: 364 }, { x: col.b, y: 364 }],
      labelAt: { x: 370, y: 431 },
      step: 5,
    },
    {
      id: 'cart-customers',
      from: 'cart',
      to: 'customers-contracts',
      label: 'Get Customer Name',
      caption: "The Cart looks up the Customer's name through Customers Contracts.",
      // Routes under and around Customers, inside the "Get Default Payment" line, so nothing crosses.
      points: [
        { x: col.b, y: 486 },
        { x: 416, y: 486 },
        { x: 416, y: 575 },
        { x: 41, y: 575 },
        { x: 41, y: 372 },
        { x: col.a, y: 372 },
      ],
      labelAt: { x: 230, y: 575 },
      step: 6,
    },
    {
      id: 'cart-products',
      from: 'cart',
      to: 'products-contracts',
      label: 'Product Name,\nBase Price',
      caption: 'Cart items get Product names and base prices through Products Contracts.',
      points: [{ x: 684, y: 486 }, { x: 742, y: 486 }, { x: 742, y: 144 }, { x: 684, y: 144 }],
      labelAt: { x: 742, y: 400 },
      step: 9,
    },
    {
      id: 'cart-orders',
      from: 'cart',
      to: 'orders-contracts',
      label: 'Create on Checkout',
      caption: 'On checkout, the Cart creates an Order through Orders Contracts.',
      points: [
        { x: mid.b, y: 534 },
        { x: mid.b, y: 563 },
        { x: 823, y: 563 },
        { x: 823, y: 310 },
        { x: mid.c, y: 310 },
        { x: mid.c, y: 338 },
      ],
      labelAt: { x: 823, y: 463 },
      step: 12,
    },
    {
      id: 'orders-products',
      from: 'orders',
      to: 'products-contracts',
      label: 'Verify Price',
      caption: 'Orders verify current prices using Products Contracts.',
      points: [{ x: 1157, y: 446 }, { x: 1171, y: 446 }, { x: 1171, y: 114 }, { x: 684, y: 114 }],
      labelAt: { x: 1105, y: 114 },
      step: 13,
    },
    {
      id: 'orders-customers',
      from: 'orders',
      to: 'customers-contracts',
      label: 'Get Default Payment, Address Details',
      caption: "Orders get the Customer's default payment and address through Customers Contracts.",
      points: [{ x: mid.c, y: 534 }, { x: mid.c, y: 638 }, { x: 27, y: 638 }, { x: 27, y: 356 }, { x: col.a, y: 356 }],
      labelAt: { x: 437, y: 638 },
      step: 14,
    },
    {
      id: 'orders-inventory',
      from: 'orders',
      to: 'inventory-contracts',
      label: 'Check Inventory',
      caption: 'Orders check stock through Inventory Contracts.',
      points: [{ x: 1157, y: 486 }, { x: 1265, y: 486 }, { x: 1265, y: 364 }, { x: col.d, y: 364 }],
      labelAt: { x: 1265, y: 429 },
      step: 18,
    },
    {
      id: 'orders-shipping',
      from: 'orders',
      to: 'shipping-contracts',
      label: 'Calculate Shipping',
      caption: 'Orders ask Shipping Contracts to calculate the shipping cost.',
      points: [{ x: 1157, y: 455 }, { x: 1186, y: 455 }, { x: 1186, y: 120 }, { x: col.d, y: 120 }],
      labelAt: { x: 1279, y: 120 },
      step: 21,
    },
    {
      id: 'shipping-inventory',
      from: 'shipping',
      to: 'inventory-contracts',
      label: 'Deduct Inventory',
      caption: 'Shipping deducts items from Inventory through Inventory Contracts.',
      points: [{ x: mid.d, y: 290 }, { x: mid.d, y: 338 }],
      labelAt: { x: mid.d, y: 310 },
      step: 22,
    },
    {
      id: 'shipping-products',
      from: 'shipping',
      to: 'products-contracts',
      label: 'Get Dimensions, Weight to Calculate Shipping',
      caption: "Shipping gets each Product's dimensions and weight through Products Contracts.",
      points: [{ x: 1611, y: 218 }, { x: 1628, y: 218 }, { x: 1628, y: 74 }, { x: mid.b, y: 74 }, { x: mid.b, y: 104 }],
      labelAt: { x: 1167, y: 74 },
      step: 23,
    },
    {
      id: 'shipping-customers',
      from: 'shipping',
      to: 'customers-contracts',
      label: 'Get Customer Email, SMS to Send Tracking\nInfo',
      caption: "Shipping gets the Customer's contact details through Customers Contracts to send tracking info.",
      points: [{ x: 1611, y: 241 }, { x: 1640, y: 241 }, { x: 1640, y: 34 }, { x: mid.a, y: 34 }, { x: mid.a, y: 338 }],
      labelAt: { x: 878, y: 34 },
      step: 24,
    },
  ],

  captions: [
    {
      id: 'wrap-up',
      caption: "Modules depend on each other's Contracts instead of each other, so the cycles from before can't form.",
      step: 25,
    },    
    {
      id: 'wrap-up-2',
      caption: "Note the arrow directions. Internal module projects have arrows pointing out to the Contracts they depend on. Contract projects only have arrows pointing in.",
      step: 26,
    },
  ],
});

export default modulesAndContracts;
