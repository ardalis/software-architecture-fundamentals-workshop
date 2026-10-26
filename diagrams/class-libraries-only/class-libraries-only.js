// "Class libraries only" e-commerce diagram: modules that reference each other
// freely end up with circular dependencies.
// Coordinates use a 2000 x 820 canvas. Elements sharing a step animate together.
// Every edge starts black; a callout's step turns the edges of its cycle red.
// A step with no caption keeps the previous caption on screen.

import { Diagram } from 'animated-diagrams/model';

const W = 293; // node width
const H = 119; // node height

// Node positions (top-left corners)
const col = { a: 57, b: 537, c: 1120, d: 1678 };
const row = { top: 233, bottom: 514 };

// Labels sit beside their lines, never on top of them (17px text, so half a label is about 4.7px per character).
// `beside` is the center x of a label just left (side -1) or right (side 1) of a vertical line at `x`;
// `above` is the center y of a label just over a horizontal line at `y`.
const longest = (label) => Math.max(...label.split('\n').map((l) => l.length));
const beside = (x, label, side = 1) => x + side * (10 + longest(label) * 4.7);
const above = (y, label) => y - 5 - label.split('\n').length * 11;

export const classLibrariesOnly = new Diagram({
  id: 'class-libraries-only',
  title: 'Class Libraries Only: Circular Dependencies',
  description:
    'Six e-commerce modules built as plain class libraries reference each other directly, ' +
    'forming two circular dependencies that both run through Customers, Shopping Cart and Orders.',
  width: 2000,
  height: 820,
  // In the open space between the Products/Shipping row and the bottom row of modules.
  watermark: { text: 'ModularMonoliths.com', x: 975, y: 440, fontSize: 44 },

  nodes: [
    { id: 'customers', label: 'Customers', x: col.a, y: row.bottom, step: 1,
      caption: 'Our model begins with the Customer.' },
    { id: 'cart', label: 'Shopping Cart', x: col.b, y: row.bottom, step: 2,
      caption: 'Customers each have a Shopping Cart.' },
    { id: 'products', label: 'Products', x: col.b, y: row.top, step: 4,
      caption: 'Cart items need information about Products.' },
    { id: 'orders', label: 'Orders', x: col.c, y: row.bottom, step: 6,
      caption: 'When checked out, a cart is used to create an Order.' },
    { id: 'inventory', label: 'Inventory', x: col.d, y: row.bottom, step: 9,
      caption: 'Before accepting an Order, we need to confirm its items are in stock.' },
    { id: 'shipping', label: 'Shipping', x: col.d, y: row.top, step: 11,
      caption: 'Next, Orders need to be shipped.' },
  ],

  edges: [
    {
      id: 'customers-cart',
      from: 'customers',
      to: 'cart',
      label: 'Has a',
      points: [{ x: col.a + W, y: 574 }, { x: col.b, y: 574 }],
      labelAt: { x: 443, y: above(574, 'Has a') },
      step: 3,
    },
    {
      id: 'cart-products',
      from: 'cart',
      to: 'products',
      label: 'Product Name, Base Price',
      points: [{ x: 683, y: row.bottom }, { x: 683, y: row.top + H }],
      // Left of the line, clear of the watermark.
      labelAt: { x: beside(683, 'Product Name, Base Price', -1), y: 433 },
      step: 5,
    },
    {
      id: 'cart-orders',
      from: 'cart',
      to: 'orders',
      label: 'Create on Checkout',
      points: [{ x: col.b + W, y: 574 }, { x: col.c, y: 574 }],
      labelAt: { x: 975, y: above(574, 'Create on Checkout') },
      step: 7,
    },
    {
      id: 'orders-products',
      from: 'orders',
      to: 'products',
      label: 'Verify Price',
      points: [{ x: 1266, y: row.bottom }, { x: 1266, y: 292 }, { x: col.b + W, y: 292 }],
      labelAt: { x: 1165, y: above(292, 'Verify Price') },
      caption: 'Cart items may be stale, so Orders must check the current Product prices.',
      step: 8,
    },
    {
      id: 'orders-inventory',
      from: 'orders',
      to: 'inventory',
      label: 'Check Inventory',
      points: [{ x: col.c + W, y: 574 }, { x: col.d, y: 574 }],
      labelAt: { x: 1545, y: above(574, 'Check Inventory') },
      step: 10,
    },
    {
      id: 'orders-shipping',
      from: 'orders',
      to: 'shipping',
      label: 'Calculate Shipping',
      points: [{ x: 1311, y: row.bottom }, { x: 1311, y: 334 }, { x: col.d, y: 334 }],
      labelAt: { x: 1494, y: above(334, 'Calculate Shipping') },
      caption: 'Orders ask Shipping to calculate the shipping cost...',
      step: 12,
    },
    {
      id: 'shipping-products',
      from: 'shipping',
      to: 'products',
      label: 'Get Dimensions, Weight to Calculate Shipping',
      points: [
        { x: col.d, y: 292 },
        { x: 1351, y: 292 },
        { x: 1351, y: 197 },
        { x: 683, y: 197 },
        { x: 683, y: row.top },
      ],
      labelAt: { x: 1050, y: above(197, 'Get Dimensions, Weight to Calculate Shipping') },
      caption: "...which depends on each Product's dimensions and weight.",
      step: 13,
    },
    {
      id: 'shipping-inventory',
      from: 'shipping',
      to: 'inventory',
      label: 'Deduct Inventory',
      points: [{ x: 1825, y: row.top + H }, { x: 1825, y: row.bottom }],
      labelAt: { x: beside(1825, 'Deduct Inventory', -1), y: 433 },
      caption: 'When an Order ships, Shipping deducts its items from Inventory.',
      step: 14,
    },
    {
      id: 'shipping-customers',
      from: 'shipping',
      to: 'customers',
      label: 'Get Customer Email, SMS to Send Tracking\nInfo',
      points: [
        { x: 1825, y: row.top },
        { x: 1825, y: 84 },
        { x: 203, y: 84 },
        { x: 203, y: row.bottom },
      ],
      labelAt: { x: 881, y: above(84, 'Get Customer Email, SMS to Send Tracking\nInfo') },
      caption: "To send tracking info, Shipping needs the Customer's email and phone number.",
      step: 15,
    },
    {
      id: 'orders-customers',
      from: 'orders',
      to: 'customers',
      label: 'Get Default Payment, Address Details',
      points: [
        { x: 1266, y: row.bottom + H },
        { x: 1266, y: 781 },
        { x: 203, y: 781 },
        { x: 203, y: row.bottom + H },
      ],
      // Left of the second callout's tail.
      labelAt: { x: 600, y: above(781, 'Get Default Payment, Address Details') },
      caption: "Orders also need the Customer's default payment method and address.",
      step: 17,
    },
  ],

  callouts: [
    {
      id: 'cycle-1',
      lines: ['CIRCULAR', 'DEPENDENCY #1!'],
      x: 177,
      y: 155,
      width: 176,
      height: 104,
      tail: { x: 215, y: 288 },
      // Customers -> Cart -> Orders -> Shipping -> Customers
      highlights: ['customers-cart', 'cart-orders', 'orders-shipping', 'shipping-customers'],
      caption: "But Customers have Carts, which create Orders, which use Shipping. That's a circular dependency!",
      step: 16,
    },
    {
      id: 'cycle-2',
      lines: ['CIRCULAR', 'DEPENDENCY #2!'],
      x: 866,
      y: 639,
      width: 186,
      height: 107,
      tail: { x: 906, y: 775 },
      // Customers -> Cart -> Orders -> Customers
      highlights: ['customers-cart', 'cart-orders', 'orders-customers'],
      caption: 'Customers, Shopping Cart, and Orders now form a second cycle.',
      step: 18,
    },
  ],

  captions: [
    {
      id: 'wrap-up',
      caption: 'With only class libraries, nothing stops modules from depending on each other in both directions.',
      step: 19,
    },
  ],
});

export default classLibrariesOnly;
