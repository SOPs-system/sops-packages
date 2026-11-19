/**
 * @sops-system/models — Canonical TypeScript Domain Contracts
 * Fully aligned with JSON Schema v1 and C# Sops.Models contracts.
 */

// ==========================================
// 1. Common Types & Value Objects
// ==========================================

export interface Money {
  /** Canonical amount in currency minor units (e.g. 10050 piasters) */
  amountInMinorUnits: number;
  /** Legacy snake_case alias */
  amount_minor_units?: number;
  /** ISO 4217 currency code */
  currency: string;
  /** Exact decimal string representation (e.g. '100.50') protecting values > 2^53 - 1 */
  decimalString?: string;
}

export interface Quantity {
  value: number;
  unit: string;
}

export interface Address {
  street_line1: string;
  street_line2?: string | null;
  city: string;
  state_or_governorate: string;
  postal_code?: string | null;
  country_code: string;
}

export interface AuditMetadata {
  created_at_utc: string;
  created_by_user_id?: string | null;
  updated_at_utc?: string | null;
  updated_by_user_id?: string | null;
  version: number;
}

export interface PageRequest {
  page_number: number;
  page_size: number;
  search_term?: string | null;
  sort_by?: string | null;
  sort_descending?: boolean;
}

export interface PageResult<T> {
  page_number: number;
  page_size: number;
  total_count: number;
  total_pages: number;
  items: T[];
}

export interface ErrorContract {
  code: string;
  message: string;
  target?: string | null;
  details?: Record<string, string> | null;
}

export interface ResultEnvelope<T> {
  is_success: boolean;
  correlation_id: string;
  data?: T | null;
  error?: ErrorContract | null;
}

// ==========================================
// 2. Catalogue Domain
// ==========================================

export type DrugDosageForm =
  | 'Tablet'
  | 'Capsule'
  | 'Syrup'
  | 'Suspension'
  | 'Sachet'
  | 'Injection'
  | 'Drop'
  | 'Cream'
  | 'Ointment'
  | 'Gel'
  | 'Inhaler'
  | 'Patch'
  | 'Suppository'
  | 'Spray'
  | 'Lotion'
  | 'Other';

export type StorageType =
  | 'RoomTemperature'
  | 'Refrigerated'
  | 'Frozen'
  | 'ControlledSubstance';

export type DrugStatus =
  | 'Active'
  | 'Suspended'
  | 'Discontinued'
  | 'Recalled';

export interface DrugPackage {
  tier_name: string;
  conversion_factor_to_base: number;
  is_default_dispensing_unit: boolean;
}

export interface ActiveIngredient {
  name: string;
  strength: string;
}

export interface Drug {
  id: string;
  sku: string;
  barcode: string;
  name_en: string;
  name_ar: string;
  generic_name: string;
  category_id?: string | null;
  dosage_form: DrugDosageForm;
  official_price: Money;
  is_price_regulated: boolean;
  requires_prescription: boolean;
  storage_type: StorageType;
  packages: DrugPackage[];
  active_ingredients: ActiveIngredient[];
  status: DrugStatus;
  catalog_revision: number;
}

export interface DrugCategory {
  id: string;
  parent_id?: string | null;
  code: string;
  name_en: string;
  name_ar: string;
  description?: string | null;
}

export interface Distributor {
  id: string;
  name: string;
  tax_number?: string | null;
  phone: string;
  email?: string | null;
  address?: Address | null;
  is_active: boolean;
}

export interface DrugSearchResult {
  drug_id: string;
  sku: string;
  barcode: string;
  name_en: string;
  name_ar: string;
  dosage_form: string;
  official_price: Money;
  is_in_stock: boolean;
  available_base_units: number;
  shelf_location?: string | null;
}

// ==========================================
// 3. Local Inventory Domain
// ==========================================

export interface LocalDrug {
  id: string;
  global_drug_id: string;
  pharmacy_id: string;
  local_cost_price: Money;
  custom_selling_price?: Money | null;
  storage_location_id?: string | null;
  minimum_stock_threshold: number;
  maximum_stock_threshold: number;
  is_active_locally: boolean;
}

export interface StorageLocation {
  id: string;
  pharmacy_id: string;
  name: string;
  zone: string;
  is_temperature_controlled: boolean;
}

export interface StockLot {
  id: string;
  local_drug_id: string;
  global_drug_id: string;
  batch_number: string;
  expiry_date: string;
  received_quantity: Quantity;
  remaining_quantity: Quantity;
  reserved_quantity: Quantity;
  purchase_price: Money;
  distributor_id?: string | null;
  received_date_utc: string;
}

export type StockMovementType =
  | 'Receipt'
  | 'Sale'
  | 'CustomerReturn'
  | 'SupplierReturn'
  | 'AdjustmentPositive'
  | 'AdjustmentNegative'
  | 'TransferOut'
  | 'TransferIn'
  | 'ExpiredWriteOff';

export interface StockMovement {
  movement_id: string;
  lot_id: string;
  drug_id: string;
  movement_type: StockMovementType;
  quantity_delta: Quantity;
  reference_document_id?: string | null;
  user_id?: string | null;
  timestamp_utc: string;
  reason?: string | null;
}

export interface StockAvailability {
  drug_id: string;
  total_physical_quantity: Quantity;
  total_reserved_quantity: Quantity;
  available_quantity: Quantity;
  earliest_expiry_date?: string | null;
  lot_count: number;
}

// ==========================================
// 4. Patients Domain
// ==========================================

export type Gender = 'Male' | 'Female' | 'Unspecified';

export interface Patient {
  id: string;
  national_id_hash?: string | null;
  full_name: string;
  phone_number: string;
  date_of_birth?: string | null;
  gender: Gender;
  blood_group?: string | null;
  chronic_conditions: string[];
  allergies: string[];
  created_at_utc: string;
}

export interface PatientSummary {
  patient_id: string;
  full_name: string;
  phone_number: string;
  has_chronic_conditions: boolean;
  has_allergies: boolean;
}

export interface Medication {
  medication_id: string;
  patient_id: string;
  drug_id: string;
  drug_name: string;
  dosage_instructions: string;
  prescribing_doctor?: string | null;
  start_date: string;
  end_date?: string | null;
  is_active: boolean;
}

export interface MedicationHistoryEntry {
  dispense_id: string;
  patient_id: string;
  drug_id: string;
  drug_name: string;
  quantity_dispensed: Quantity;
  pharmacy_id: string;
  dispensed_at_utc: string;
}

export type ConsentScope =
  | 'ViewMedicationHistory'
  | 'RecordDispense'
  | 'FullClinicalProfile';

export interface PatientConsent {
  consent_id: string;
  patient_id: string;
  authorized_chain_id?: string | null;
  authorized_pharmacy_id?: string | null;
  granted_scope: ConsentScope;
  granted_at_utc: string;
  expires_at_utc: string;
  is_revoked: boolean;
}

// ==========================================
// 5. Point-of-Sale Transactions Domain
// ==========================================

export type TransactionType = 'Sale' | 'Return' | 'Exchange' | 'Void';

export type TransactionStatus = 'Draft' | 'Completed' | 'Voided' | 'Suspended';

export type PaymentMethod =
  | 'Cash'
  | 'CreditCard'
  | 'DebitCard'
  | 'Voucher'
  | 'InsuranceClaim'
  | 'Split'
  | 'LoyaltyPoints';

export type PaymentStatus = 'Pending' | 'Completed' | 'Failed' | 'Refunded';

export interface Payment {
  payment_id: string;
  method: PaymentMethod;
  amount: Money;
  tendered_amount?: Money | null;
  change_returned?: Money | null;
  status: PaymentStatus;
  card_reference?: string | null;
}

export interface TransactionItem {
  item_id: string;
  drug_id: string;
  drug_name: string;
  package_tier: string;
  quantity: Quantity;
  unit_price: Money;
  discount_amount: Money;
  tax_rate: number;
  total_line_amount: Money;
  lot_id?: string | null;
}

export interface Transaction {
  id: string;
  invoice_number: string;
  pharmacy_id: string;
  terminal_id: string;
  cashier_user_id: string;
  shift_id: string;
  patient_id?: string | null;
  type: TransactionType;
  status: TransactionStatus;
  subtotal: Money;
  discount_amount: Money;
  tax_amount: Money;
  total_amount: Money;
  items: TransactionItem[];
  payments: Payment[];
  created_at_utc: string;
  completed_at_utc?: string | null;
}

export type ShiftStatus = 'Open' | 'Closed' | 'Reconciled';

export interface Shift {
  id: string;
  shift_number: number;
  pharmacy_id: string;
  terminal_id: string;
  cashier_user_id: string;
  opening_cash: Money;
  closing_cash?: Money | null;
  expected_cash?: Money | null;
  cash_difference?: Money | null;
  status: ShiftStatus;
  opened_at_utc: string;
  closed_at_utc?: string | null;
}

export interface Settlement {
  settlement_id: string;
  pharmacy_id: string;
  business_date: string;
  total_sales: Money;
  total_returns: Money;
  net_sales: Money;
  cash_total: Money;
  card_total: Money;
  settled_by_user_id?: string | null;
  settled_at_utc: string;
}

// ==========================================
// 6. SaaS Billing Domain
// ==========================================

export type BillingPaymentStatus =
  | 'Pending'
  | 'Authorized'
  | 'Captured'
  | 'Failed'
  | 'Refunded';

export interface BillingPayment {
  billing_payment_id: string;
  tenant_id: string;
  invoice_number: string;
  amount: Money;
  status: BillingPaymentStatus;
  gateway_provider: string;
  gateway_transaction_reference?: string | null;
  created_at_utc: string;
  paid_at_utc?: string | null;
}

export interface SubscriptionPlan {
  plan_id: string;
  name: string;
  price_per_month: Money;
  max_branches: number;
  max_seats_per_branch: number;
  has_analytics: boolean;
  has_whatsapp_notifications: boolean;
}

export interface InvoiceSummary {
  invoice_id: string;
  invoice_number: string;
  tenant_id: string;
  total_due: Money;
  issue_date: string;
  due_date: string;
  is_settled: boolean;
}

// ==========================================
// 7. Tenancy & Chain Logistics Domain
// ==========================================

export type SubscriptionTier = 'Free' | 'Professional' | 'Enterprise' | 'Custom';

export type TenantProvisionStatus =
  | 'Pending'
  | 'Provisioning'
  | 'Active'
  | 'Suspended'
  | 'Decommissioned';

export interface Chain {
  id: string;
  name: string;
  tax_number: string;
  commercial_registration?: string | null;
  subscription_tier: SubscriptionTier;
  provision_status: TenantProvisionStatus;
  created_at_utc: string;
}

export interface Pharmacy {
  id: string;
  chain_id: string;
  name: string;
  license_number: string;
  supervising_pharmacist?: string | null;
  is_active: boolean;
}

export interface Branch {
  id: string;
  chain_id: string;
  global_pharmacy_id?: string | null;
  branch_code: string;
  name: string;
  is_warehouse: boolean;
  address: Address;
  phone: string;
  manager_name?: string | null;
  is_active: boolean;
}

export type TransferStatus =
  | 'Draft'
  | 'Requested'
  | 'Approved'
  | 'Rejected'
  | 'Dispatched'
  | 'InTransit'
  | 'Received'
  | 'Completed'
  | 'Cancelled';

export interface TransferLine {
  line_id: string;
  drug_id: string;
  drug_name: string;
  requested_quantity: Quantity;
  approved_quantity?: Quantity | null;
  dispatched_quantity?: Quantity | null;
  received_quantity?: Quantity | null;
  batch_number?: string | null;
  expiry_date?: string | null;
}

export interface TransferRequest {
  id: string;
  transfer_number: string;
  tenant_id: string;
  source_branch_id: string;
  destination_branch_id: string;
  status: TransferStatus;
  lines: TransferLine[];
  requested_by_user_id?: string | null;
  approved_by_user_id?: string | null;
  dispatched_at_utc?: string | null;
  received_at_utc?: string | null;
  notes?: string | null;
  created_at_utc: string;
}

export interface BranchStockSnapshot {
  branch_id: string;
  drug_id: string;
  drug_sku: string;
  quantity_in_base_units: number;
  base_unit_name: string;
  snapshot_revision: number;
  snapshot_timestamp_utc: string;
  last_synchronized_utc: string;
  is_stale: boolean;
}

export interface BranchFinanceDaily {
  branch_id: string;
  business_date: string;
  gross_sales: Money;
  returns: Money;
  net_sales: Money;
  transaction_count: number;
}

// ==========================================
// 8. Identity & Authorization Domain
// ==========================================

export interface UserSummary {
  user_id: string;
  username: string;
  full_name: string;
  email: string;
  phone?: string | null;
  is_active: boolean;
}

export interface StaffSummary {
  user_id: string;
  full_name: string;
  role: string;
  branch_id: string;
  is_pharmacist: boolean;
  license_number?: string | null;
}

export interface SessionSummary {
  session_id: string;
  user_id: string;
  username: string;
  terminal_id: string;
  branch_id: string;
  logged_in_at_utc: string;
  last_activity_utc: string;
}

export interface RoleDefinition {
  role_id: string;
  name: string;
  description?: string | null;
  permissions: string[];
}

export interface PermissionDefinition {
  code: string;
  name: string;
  module: string;
  description?: string | null;
}

export interface AuthorizationDecision {
  is_authorized: boolean;
  permission_code: string;
  denial_reason?: string | null;
  evaluated_at_utc: string;
}

export interface OfflineAuthorizationSnapshot {
  snapshot_id: string;
  branch_id: string;
  issued_at_utc: string;
  expires_at_utc: string;
  user_permissions: Record<string, string[]>;
}

// ==========================================
// 9. Device & Licensing Domain
// ==========================================

export type DevicePlatform =
  | 'Windows'
  | 'Linux'
  | 'OSX'
  | 'Android'
  | 'iOS'
  | 'Unknown';

export type DeviceStatus =
  | 'PendingActivation'
  | 'Active'
  | 'Suspended'
  | 'Revoked'
  | 'Decommissioned';

export interface DeviceInfo {
  machine_name: string;
  operating_system: string;
  platform: DevicePlatform;
  processor_count: number;
  total_memory_mb?: number | null;
}

export interface DeviceIdentitySummary {
  device_id: string;
  device_code: string;
  chain_id: string;
  pharmacy_id: string;
  platform: DevicePlatform;
  device_name: string;
  status: DeviceStatus;
  registered_at_utc: string;
  last_heartbeat_utc?: string | null;
}

export type LicenseStatus =
  | 'Valid'
  | 'GracePeriod'
  | 'Expired'
  | 'Suspended'
  | 'Revoked'
  | 'InvalidSignature'
  | 'DeviceMismatch';

export interface LicenseClaims {
  license_id: string;
  tenant_id: string;
  tier: string;
  issued_at_utc: string;
  expires_at_utc: string;
  max_branches: number;
  max_seats_per_branch: number;
  enabled_features: string[];
}

export interface ActivationResult {
  is_successful: boolean;
  status: string;
  certificate_raw?: string | null;
  failure_reason?: string | null;
}

// ==========================================
// 10. Synchronization Domain
// ==========================================

export type MutationKind = 'Created' | 'Updated' | 'Deleted' | 'ActionExecuted';

export interface SyncEventEnvelope {
  event_id: string;
  aggregate_type: string;
  aggregate_id: string;
  event_type: string;
  mutation_kind: MutationKind;
  occurred_at_utc: string;
  tenant_id: string;
  branch_id: string;
  payload_json: string;
  schema_version: string;
}

export interface SyncBatch {
  batch_id: string;
  branch_id: string;
  client_sequence_start: number;
  client_sequence_end: number;
  events: SyncEventEnvelope[];
  sent_at_utc: string;
}

export interface RecordChange {
  table_name: string;
  record_id: string;
  change_type: 'Insert' | 'Update' | 'Delete';
  data_json?: string | null;
  changed_at_utc: string;
}

export type ChangeRequestStatus = 'Pending' | 'Approved' | 'Rejected' | 'Expired';

export interface ChangeDecision {
  decision: 'Approved' | 'Rejected';
  decided_by_user_id: string;
  decided_at_utc: string;
  notes?: string | null;
}

export interface ChangeRequest {
  request_id: string;
  tenant_id: string;
  branch_id: string;
  requested_by_user_id: string;
  change_type: string;
  reason: string;
  status: ChangeRequestStatus;
  decision?: ChangeDecision | null;
  created_at_utc: string;
}
