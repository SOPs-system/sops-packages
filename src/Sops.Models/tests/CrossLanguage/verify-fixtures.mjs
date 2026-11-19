/**
 * Cross-language JSON fixture verification runner using Node.js standard library (no external npm dependencies required).
 */
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const fixturesDir = path.resolve(__dirname, '../../schemas/fixtures');

console.log('=== [Cross-Language Fixture Verification (Node.js)] ===\n');
console.log(`Loading fixtures from: ${fixturesDir}\n`);

let passedCount = 0;

function runTest(name, fn) {
  try {
    fn();
    console.log(`  ✔ PASS: ${name}`);
    passedCount++;
  } catch (err) {
    console.error(`  ✖ FAIL: ${name}`);
    console.error(err);
    process.exitCode = 1;
  }
}

// 1. Money Canonical
runTest('Money canonical wire format supports camelCase and snake_case', () => {
  const filePath = path.join(fixturesDir, 'money_canonical.json');
  const raw = fs.readFileSync(filePath, 'utf8');
  const parsed = JSON.parse(raw);

  assert.strictEqual(parsed.amountInMinorUnits, 15075);
  assert.strictEqual(parsed.amount_minor_units, 15075);
  assert.strictEqual(parsed.currency, 'EGP');
  assert.strictEqual(parsed.decimalString, '150.75');

  // Round-trip verification
  const roundTripped = JSON.parse(JSON.stringify(parsed));
  assert.deepStrictEqual(roundTripped, parsed);
});

// 2. Money Safe Integer Boundary (> 2^53 - 1)
runTest('Money decimalString protects values exceeding JavaScript safe integer limit', () => {
  const filePath = path.join(fixturesDir, 'money_large_integer.json');
  const raw = fs.readFileSync(filePath, 'utf8');
  
  const parsed = JSON.parse(raw);
  assert.strictEqual(parsed.currency, 'EGP');
  assert.strictEqual(parsed.decimalString, '90071992547409.95');

  // Verify BigInt parsing directly from raw text preserves exact 64-bit int
  const bigIntMatch = raw.match(/"amountInMinorUnits":\s*(\d+)/);
  assert.ok(bigIntMatch, 'Should find amountInMinorUnits in raw json');
  const bigIntVal = BigInt(bigIntMatch[1]);
  assert.strictEqual(bigIntVal, 9007199254740995n);
  assert.ok(bigIntVal > BigInt(Number.MAX_SAFE_INTEGER), 'Value must exceed 2^53 - 1');
});

// 3. Drug & Arabic Unicode
runTest('Drug fixture preserves Arabic Unicode and packaging conversion tiers', () => {
  const filePath = path.join(fixturesDir, 'drug_panadol.json');
  const raw = fs.readFileSync(filePath, 'utf8');
  const drug = JSON.parse(raw);

  assert.strictEqual(drug.sku, 'EDA-2024-001');
  assert.strictEqual(drug.barcode, '6221234567890');
  assert.strictEqual(drug.name_ar, '\u0628\u0646\u0627\u062f\u0648\u0644 \u0625\u0643\u0633\u062a\u0631\u0627 500 \u0645\u062c\u0645');
  assert.strictEqual(drug.dosage_form, 'Tablet');
  assert.strictEqual(drug.is_price_regulated, true);
  assert.strictEqual(drug.requires_prescription, false);

  // Packages check
  assert.strictEqual(Array.isArray(drug.packages), true);
  assert.strictEqual(drug.packages.length, 3);
  assert.strictEqual(drug.packages[0].tier_name, 'Box');
  assert.strictEqual(drug.packages[0].conversion_factor_to_base, 24);
  assert.strictEqual(drug.packages[0].is_default_dispensing_unit, true);

  // Assert conversion factors are strictly positive integers
  for (const pkg of drug.packages) {
    assert.ok(Number.isInteger(pkg.conversion_factor_to_base), 'Conversion factor must be integer');
    assert.ok(pkg.conversion_factor_to_base >= 1, 'Conversion factor must be >= 1');
    assert.strictEqual(pkg.barcode, undefined, 'DrugPackage value object must NOT contain barcode');
  }
});

// 4. Transaction Retail Anonymous Checkout
runTest('Transaction fixture supports anonymous checkout and money line totals', () => {
  const filePath = path.join(fixturesDir, 'transaction_retail.json');
  const raw = fs.readFileSync(filePath, 'utf8');
  const tx = JSON.parse(raw);

  assert.strictEqual(tx.invoice_number, 'INV-2026-0042');
  assert.strictEqual(tx.patient_id, null, 'Anonymous checkout has null patient_id');
  assert.strictEqual(tx.type, 'Sale');
  assert.strictEqual(tx.status, 'Completed');
  assert.strictEqual(tx.total_amount.amountInMinorUnits, 10000);
  assert.strictEqual(tx.total_amount.currency, 'EGP');

  assert.strictEqual(tx.items.length, 1);
  assert.strictEqual(tx.items[0].quantity.value, 2);
  assert.strictEqual(tx.items[0].quantity.unit, 'boxes');

  assert.strictEqual(tx.payments.length, 1);
  assert.strictEqual(tx.payments[0].method, 'Cash');
  assert.strictEqual(tx.payments[0].amount.amountInMinorUnits, 10000);
});

// 5. Patient Summary Privacy Projection
runTest('PatientSummary fixture protects sensitive clinical details', () => {
  const filePath = path.join(fixturesDir, 'patient_summary.json');
  const raw = fs.readFileSync(filePath, 'utf8');
  const summary = JSON.parse(raw);

  assert.strictEqual(summary.full_name, 'Fatma Mahmoud');
  assert.strictEqual(summary.has_chronic_conditions, true);
  assert.strictEqual(summary.has_allergies, false);

  const prohibitedKeys = ['national_id', 'national_id_hash', 'chronic_conditions', 'allergies', 'diagnoses'];
  for (const key of prohibitedKeys) {
    assert.strictEqual(summary[key], undefined, `PatientSummary must not contain sensitive key: ${key}`);
  }
});

// 6. Device Identity Privacy Projection
runTest('DeviceIdentitySummary fixture protects raw hardware signals', () => {
  const filePath = path.join(fixturesDir, 'device_identity.json');
  const raw = fs.readFileSync(filePath, 'utf8');
  const dev = JSON.parse(raw);

  assert.strictEqual(dev.device_code, 'D7KX-92MA-88P');
  assert.strictEqual(dev.platform, 'Windows');
  assert.strictEqual(dev.status, 'Active');

  const prohibitedSignals = ['cpu_id', 'motherboard_serial', 'mac_address', 'disk_serial'];
  for (const sig of prohibitedSignals) {
    assert.strictEqual(dev[sig], undefined, `DeviceIdentitySummary must not contain raw hardware signal: ${sig}`);
  }
});

console.log(`\nAll ${passedCount} cross-language fixture tests PASSED.`);
