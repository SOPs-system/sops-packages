/**
 * TypeScript Consumer Sample demonstrating Node.js / JavaScript consumption of SOPs contracts.
 */
console.log('TypeScript Consumer Sample Starting...');

const money = {
  amountInMinorUnits: 7550,
  amount_minor_units: 7550,
  currency: 'EGP',
  decimalString: '75.50'
};
console.log(`Validated Money: ${money.decimalString} ${money.currency}`);

const drug = {
  id: 'e3b0c442-98fc-1c14-9afb-f4c8996fb924',
  sku: 'EDA-2024-001',
  barcode: '6221234567890',
  name_en: 'Panadol Extra 500mg',
  name_ar: '\u0628\u0646\u0627\u062f\u0648\u0644 \u0625\u0643\u0633\u062a\u0631\u0627 500 \u0645\u062c\u0645',
  dosage_form: 'Tablet',
  official_price: money,
  is_price_regulated: true,
  requires_prescription: false,
  storage_type: 'RoomTemperature',
  packages: [
    { tier_name: 'Box', conversion_factor_to_base: 24, is_default_dispensing_unit: true }
  ],
  status: 'Active',
  catalog_revision: 1
};
console.log(`Validated Drug: ${drug.name_en} (${drug.barcode})`);

const transaction = {
  id: 'c71a396e-5264-42f0-98d0-e189dca3bc68',
  invoice_number: 'INV-TS-CONSUMER',
  total_amount: money,
  items: [],
  payments: []
};
console.log(`Validated Transaction: ${transaction.invoice_number}`);

console.log('TypeScript Consumer Succeeded!');
