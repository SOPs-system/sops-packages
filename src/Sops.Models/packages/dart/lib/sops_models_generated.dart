// Deterministically generated Dart models
// Generator: v0.1.0 | Schema: v1.0.0
library sops_models_generated;

/// Evaluation outcome determining if an action is permitted.
class AuthorizationDecision {
  final bool isAuthorized;
  final String permissionCode;
  final String? denialReason;
  final String evaluatedAtUtc;

  const AuthorizationDecision({
    required this.isAuthorized,
    required this.permissionCode,
    this.denialReason,
    required this.evaluatedAtUtc,
  });

  factory AuthorizationDecision.fromJson(Map<String, dynamic> json) => AuthorizationDecision(
    isAuthorized: json['is_authorized'] as bool,
    permissionCode: json['permission_code'] as String,
    denialReason: json['denial_reason'] as String?,
    evaluatedAtUtc: json['evaluated_at_utc'] as String,
  );

  Map<String, dynamic> toJson() => {
    'is_authorized': isAuthorized,
    'permission_code': permissionCode,
    'denial_reason': denialReason,
    'evaluated_at_utc': evaluatedAtUtc,
  };
}

/// Cryptographically signed snapshot of staff roles and permissions for offline policy enforcement.
class OfflineAuthorizationSnapshot {
  final String snapshotId;
  final String branchId;
  final String issuedAtUtc;
  final String expiresAtUtc;
  final dynamic userPermissions;

  const OfflineAuthorizationSnapshot({
    required this.snapshotId,
    required this.branchId,
    required this.issuedAtUtc,
    required this.expiresAtUtc,
    required this.userPermissions,
  });

  factory OfflineAuthorizationSnapshot.fromJson(Map<String, dynamic> json) => OfflineAuthorizationSnapshot(
    snapshotId: json['snapshot_id'] as String,
    branchId: json['branch_id'] as String,
    issuedAtUtc: json['issued_at_utc'] as String,
    expiresAtUtc: json['expires_at_utc'] as String,
    userPermissions: json['user_permissions'] as dynamic,
  );

  Map<String, dynamic> toJson() => {
    'snapshot_id': snapshotId,
    'branch_id': branchId,
    'issued_at_utc': issuedAtUtc,
    'expires_at_utc': expiresAtUtc,
    'user_permissions': userPermissions,
  };
}

/// Discrete granular permission in the pharmacy system.
class PermissionDefinition {
  final String code;
  final String name;
  final String module;
  final String? description;

  const PermissionDefinition({
    required this.code,
    required this.name,
    required this.module,
    this.description,
  });

  factory PermissionDefinition.fromJson(Map<String, dynamic> json) => PermissionDefinition(
    code: json['code'] as String,
    name: json['name'] as String,
    module: json['module'] as String,
    description: json['description'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'code': code,
    'name': name,
    'module': module,
    'description': description,
  };
}

/// Organizational role grouping a set of permissions.
class RoleDefinition {
  final String roleId;
  final String name;
  final String? description;
  final List<string> permissions;

  const RoleDefinition({
    required this.roleId,
    required this.name,
    this.description,
    required this.permissions,
  });

  factory RoleDefinition.fromJson(Map<String, dynamic> json) => RoleDefinition(
    roleId: json['role_id'] as String,
    name: json['name'] as String,
    description: json['description'] as String?,
    permissions: json['permissions'] as List<string>,
  );

  Map<String, dynamic> toJson() => {
    'role_id': roleId,
    'name': name,
    'description': description,
    'permissions': permissions,
  };
}

/// SaaS software subscription payment (license renewal, branch seat fee, Paymob gateway charge). Strictly separate from POS retail Payment.
class BillingPayment {
  final String billingPaymentId;
  final String tenantId;
  final String invoiceNumber;
  final Money amount;
  final String status;
  final String gatewayProvider;
  final String? gatewayTransactionReference;
  final String createdAtUtc;
  final String? paidAtUtc;

  const BillingPayment({
    required this.billingPaymentId,
    required this.tenantId,
    required this.invoiceNumber,
    required this.amount,
    required this.status,
    required this.gatewayProvider,
    this.gatewayTransactionReference,
    required this.createdAtUtc,
    this.paidAtUtc,
  });

  factory BillingPayment.fromJson(Map<String, dynamic> json) => BillingPayment(
    billingPaymentId: json['billing_payment_id'] as String,
    tenantId: json['tenant_id'] as String,
    invoiceNumber: json['invoice_number'] as String,
    amount: json['amount'] as Money,
    status: json['status'] as String,
    gatewayProvider: json['gateway_provider'] as String,
    gatewayTransactionReference: json['gateway_transaction_reference'] as String?,
    createdAtUtc: json['created_at_utc'] as String,
    paidAtUtc: json['paid_at_utc'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'billing_payment_id': billingPaymentId,
    'tenant_id': tenantId,
    'invoice_number': invoiceNumber,
    'amount': amount,
    'status': status,
    'gateway_provider': gatewayProvider,
    'gateway_transaction_reference': gatewayTransactionReference,
    'created_at_utc': createdAtUtc,
    'paid_at_utc': paidAtUtc,
  };
}

/// Subscription invoice record.
class InvoiceSummary {
  final String invoiceId;
  final String invoiceNumber;
  final String tenantId;
  final Money totalDue;
  final String issueDate;
  final String dueDate;
  final bool isSettled;

  const InvoiceSummary({
    required this.invoiceId,
    required this.invoiceNumber,
    required this.tenantId,
    required this.totalDue,
    required this.issueDate,
    required this.dueDate,
    required this.isSettled,
  });

  factory InvoiceSummary.fromJson(Map<String, dynamic> json) => InvoiceSummary(
    invoiceId: json['invoice_id'] as String,
    invoiceNumber: json['invoice_number'] as String,
    tenantId: json['tenant_id'] as String,
    totalDue: json['total_due'] as Money,
    issueDate: json['issue_date'] as String,
    dueDate: json['due_date'] as String,
    isSettled: json['is_settled'] as bool,
  );

  Map<String, dynamic> toJson() => {
    'invoice_id': invoiceId,
    'invoice_number': invoiceNumber,
    'tenant_id': tenantId,
    'total_due': totalDue,
    'issue_date': issueDate,
    'due_date': dueDate,
    'is_settled': isSettled,
  };
}

/// SaaS software tier and pricing package for pharmacy tenants.
class SubscriptionPlan {
  final String planId;
  final String name;
  final Money pricePerMonth;
  final int maxBranches;
  final int maxSeatsPerBranch;
  final bool? hasAnalytics;
  final bool? hasWhatsappNotifications;

  const SubscriptionPlan({
    required this.planId,
    required this.name,
    required this.pricePerMonth,
    required this.maxBranches,
    required this.maxSeatsPerBranch,
    this.hasAnalytics,
    this.hasWhatsappNotifications,
  });

  factory SubscriptionPlan.fromJson(Map<String, dynamic> json) => SubscriptionPlan(
    planId: json['plan_id'] as String,
    name: json['name'] as String,
    pricePerMonth: json['price_per_month'] as Money,
    maxBranches: json['max_branches'] as int,
    maxSeatsPerBranch: json['max_seats_per_branch'] as int,
    hasAnalytics: json['has_analytics'] as bool?,
    hasWhatsappNotifications: json['has_whatsapp_notifications'] as bool?,
  );

  Map<String, dynamic> toJson() => {
    'plan_id': planId,
    'name': name,
    'price_per_month': pricePerMonth,
    'max_branches': maxBranches,
    'max_seats_per_branch': maxSeatsPerBranch,
    'has_analytics': hasAnalytics,
    'has_whatsapp_notifications': hasWhatsappNotifications,
  };
}

/// Pharmaceutical wholesaler or manufacturing supplier.
class Distributor {
  final String id;
  final String name;
  final String? taxNumber;
  final String phone;
  final String? email;
  final Address? address;
  final bool? isActive;

  const Distributor({
    required this.id,
    required this.name,
    this.taxNumber,
    required this.phone,
    this.email,
    this.address,
    this.isActive,
  });

  factory Distributor.fromJson(Map<String, dynamic> json) => Distributor(
    id: json['id'] as String,
    name: json['name'] as String,
    taxNumber: json['tax_number'] as String?,
    phone: json['phone'] as String,
    email: json['email'] as String?,
    address: json['address'] as Address?,
    isActive: json['is_active'] as bool?,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'name': name,
    'tax_number': taxNumber,
    'phone': phone,
    'email': email,
    'address': address,
    'is_active': isActive,
  };
}

/// Master drug catalogue contract with embedded packages and pricing.
class Drug {
  final String id;
  final String sku;
  final String barcode;
  final String nameEn;
  final String nameAr;
  final String genericName;
  final String? categoryId;
  final String dosageForm;
  final Money officialPrice;
  final bool isPriceRegulated;
  final bool requiresPrescription;
  final String storageType;
  final List<DrugPackage> packages;
  final List<object>? activeIngredients;
  final String status;
  final int catalogRevision;

  const Drug({
    required this.id,
    required this.sku,
    required this.barcode,
    required this.nameEn,
    required this.nameAr,
    required this.genericName,
    this.categoryId,
    required this.dosageForm,
    required this.officialPrice,
    required this.isPriceRegulated,
    required this.requiresPrescription,
    required this.storageType,
    required this.packages,
    this.activeIngredients,
    required this.status,
    required this.catalogRevision,
  });

  factory Drug.fromJson(Map<String, dynamic> json) => Drug(
    id: json['id'] as String,
    sku: json['sku'] as String,
    barcode: json['barcode'] as String,
    nameEn: json['name_en'] as String,
    nameAr: json['name_ar'] as String,
    genericName: json['generic_name'] as String,
    categoryId: json['category_id'] as String?,
    dosageForm: json['dosage_form'] as String,
    officialPrice: json['official_price'] as Money,
    isPriceRegulated: json['is_price_regulated'] as bool,
    requiresPrescription: json['requires_prescription'] as bool,
    storageType: json['storage_type'] as String,
    packages: json['packages'] as List<DrugPackage>,
    activeIngredients: json['active_ingredients'] as List<object>?,
    status: json['status'] as String,
    catalogRevision: json['catalog_revision'] as int,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'sku': sku,
    'barcode': barcode,
    'name_en': nameEn,
    'name_ar': nameAr,
    'generic_name': genericName,
    'category_id': categoryId,
    'dosage_form': dosageForm,
    'official_price': officialPrice,
    'is_price_regulated': isPriceRegulated,
    'requires_prescription': requiresPrescription,
    'storage_type': storageType,
    'packages': packages,
    'active_ingredients': activeIngredients,
    'status': status,
    'catalog_revision': catalogRevision,
  };
}

/// Therapeutic or commercial categorization of pharmaceutical products.
class DrugCategory {
  final String id;
  final String? parentId;
  final String code;
  final String nameEn;
  final String nameAr;
  final String? description;

  const DrugCategory({
    required this.id,
    this.parentId,
    required this.code,
    required this.nameEn,
    required this.nameAr,
    this.description,
  });

  factory DrugCategory.fromJson(Map<String, dynamic> json) => DrugCategory(
    id: json['id'] as String,
    parentId: json['parent_id'] as String?,
    code: json['code'] as String,
    nameEn: json['name_en'] as String,
    nameAr: json['name_ar'] as String,
    description: json['description'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'parent_id': parentId,
    'code': code,
    'name_en': nameEn,
    'name_ar': nameAr,
    'description': description,
  };
}

/// Value object embedded inside Drug defining package packaging levels and integer conversion factors.
class DrugPackage {
  final String tierName;
  final int conversionFactorToBase;
  final bool? isDefaultDispensingUnit;

  const DrugPackage({
    required this.tierName,
    required this.conversionFactorToBase,
    this.isDefaultDispensingUnit,
  });

  factory DrugPackage.fromJson(Map<String, dynamic> json) => DrugPackage(
    tierName: json['tier_name'] as String,
    conversionFactorToBase: json['conversion_factor_to_base'] as int,
    isDefaultDispensingUnit: json['is_default_dispensing_unit'] as bool?,
  );

  Map<String, dynamic> toJson() => {
    'tier_name': tierName,
    'conversion_factor_to_base': conversionFactorToBase,
    'is_default_dispensing_unit': isDefaultDispensingUnit,
  };
}

/// Optimized lightweight search projection for rapid POS drug lookup.
class DrugSearchResult {
  final String drugId;
  final String sku;
  final String barcode;
  final String nameEn;
  final String nameAr;
  final String dosageForm;
  final Money officialPrice;
  final bool isInStock;
  final double? availableBaseUnits;
  final String? shelfLocation;

  const DrugSearchResult({
    required this.drugId,
    required this.sku,
    required this.barcode,
    required this.nameEn,
    required this.nameAr,
    required this.dosageForm,
    required this.officialPrice,
    required this.isInStock,
    this.availableBaseUnits,
    this.shelfLocation,
  });

  factory DrugSearchResult.fromJson(Map<String, dynamic> json) => DrugSearchResult(
    drugId: json['drug_id'] as String,
    sku: json['sku'] as String,
    barcode: json['barcode'] as String,
    nameEn: json['name_en'] as String,
    nameAr: json['name_ar'] as String,
    dosageForm: json['dosage_form'] as String,
    officialPrice: json['official_price'] as Money,
    isInStock: json['is_in_stock'] as bool,
    availableBaseUnits: json['available_base_units'] as double?,
    shelfLocation: json['shelf_location'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'drug_id': drugId,
    'sku': sku,
    'barcode': barcode,
    'name_en': nameEn,
    'name_ar': nameAr,
    'dosage_form': dosageForm,
    'official_price': officialPrice,
    'is_in_stock': isInStock,
    'available_base_units': availableBaseUnits,
    'shelf_location': shelfLocation,
  };
}

/// Daily aggregated financial summary reported by a branch to chain management.
class BranchFinanceDaily {
  final String branchId;
  final String businessDate;
  final Money grossSales;
  final Money returns;
  final Money netSales;
  final int transactionCount;

  const BranchFinanceDaily({
    required this.branchId,
    required this.businessDate,
    required this.grossSales,
    required this.returns,
    required this.netSales,
    required this.transactionCount,
  });

  factory BranchFinanceDaily.fromJson(Map<String, dynamic> json) => BranchFinanceDaily(
    branchId: json['branch_id'] as String,
    businessDate: json['business_date'] as String,
    grossSales: json['gross_sales'] as Money,
    returns: json['returns'] as Money,
    netSales: json['net_sales'] as Money,
    transactionCount: json['transaction_count'] as int,
  );

  Map<String, dynamic> toJson() => {
    'branch_id': branchId,
    'business_date': businessDate,
    'gross_sales': grossSales,
    'returns': returns,
    'net_sales': netSales,
    'transaction_count': transactionCount,
  };
}

/// Projected branch inventory level snapshot reported to chain management. Non-authoritative.
class BranchStockSnapshot {
  final String branchId;
  final String drugId;
  final String drugSku;
  final int quantityInBaseUnits;
  final String baseUnitName;
  final int snapshotRevision;
  final String snapshotTimestampUtc;
  final String lastSynchronizedUtc;
  final bool? isStale;

  const BranchStockSnapshot({
    required this.branchId,
    required this.drugId,
    required this.drugSku,
    required this.quantityInBaseUnits,
    required this.baseUnitName,
    required this.snapshotRevision,
    required this.snapshotTimestampUtc,
    required this.lastSynchronizedUtc,
    this.isStale,
  });

  factory BranchStockSnapshot.fromJson(Map<String, dynamic> json) => BranchStockSnapshot(
    branchId: json['branch_id'] as String,
    drugId: json['drug_id'] as String,
    drugSku: json['drug_sku'] as String,
    quantityInBaseUnits: json['quantity_in_base_units'] as int,
    baseUnitName: json['base_unit_name'] as String,
    snapshotRevision: json['snapshot_revision'] as int,
    snapshotTimestampUtc: json['snapshot_timestamp_utc'] as String,
    lastSynchronizedUtc: json['last_synchronized_utc'] as String,
    isStale: json['is_stale'] as bool?,
  );

  Map<String, dynamic> toJson() => {
    'branch_id': branchId,
    'drug_id': drugId,
    'drug_sku': drugSku,
    'quantity_in_base_units': quantityInBaseUnits,
    'base_unit_name': baseUnitName,
    'snapshot_revision': snapshotRevision,
    'snapshot_timestamp_utc': snapshotTimestampUtc,
    'last_synchronized_utc': lastSynchronizedUtc,
    'is_stale': isStale,
  };
}

/// Item line in an inter-branch inventory transfer.
class TransferLine {
  final String lineId;
  final String drugId;
  final String drugName;
  final Quantity requestedQuantity;
  final Quantity? approvedQuantity;
  final Quantity? dispatchedQuantity;
  final Quantity? receivedQuantity;
  final String? batchNumber;
  final String? expiryDate;

  const TransferLine({
    required this.lineId,
    required this.drugId,
    required this.drugName,
    required this.requestedQuantity,
    this.approvedQuantity,
    this.dispatchedQuantity,
    this.receivedQuantity,
    this.batchNumber,
    this.expiryDate,
  });

  factory TransferLine.fromJson(Map<String, dynamic> json) => TransferLine(
    lineId: json['line_id'] as String,
    drugId: json['drug_id'] as String,
    drugName: json['drug_name'] as String,
    requestedQuantity: json['requested_quantity'] as Quantity,
    approvedQuantity: json['approved_quantity'] as Quantity?,
    dispatchedQuantity: json['dispatched_quantity'] as Quantity?,
    receivedQuantity: json['received_quantity'] as Quantity?,
    batchNumber: json['batch_number'] as String?,
    expiryDate: json['expiry_date'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'line_id': lineId,
    'drug_id': drugId,
    'drug_name': drugName,
    'requested_quantity': requestedQuantity,
    'approved_quantity': approvedQuantity,
    'dispatched_quantity': dispatchedQuantity,
    'received_quantity': receivedQuantity,
    'batch_number': batchNumber,
    'expiry_date': expiryDate,
  };
}

/// Multi-stage inter-branch inventory transfer workflow.
class TransferRequest {
  final String id;
  final String transferNumber;
  final String tenantId;
  final String sourceBranchId;
  final String destinationBranchId;
  final String status;
  final List<TransferLine> lines;
  final String? requestedByUserId;
  final String? approvedByUserId;
  final String? dispatchedAtUtc;
  final String? receivedAtUtc;
  final String? notes;
  final String createdAtUtc;

  const TransferRequest({
    required this.id,
    required this.transferNumber,
    required this.tenantId,
    required this.sourceBranchId,
    required this.destinationBranchId,
    required this.status,
    required this.lines,
    this.requestedByUserId,
    this.approvedByUserId,
    this.dispatchedAtUtc,
    this.receivedAtUtc,
    this.notes,
    required this.createdAtUtc,
  });

  factory TransferRequest.fromJson(Map<String, dynamic> json) => TransferRequest(
    id: json['id'] as String,
    transferNumber: json['transfer_number'] as String,
    tenantId: json['tenant_id'] as String,
    sourceBranchId: json['source_branch_id'] as String,
    destinationBranchId: json['destination_branch_id'] as String,
    status: json['status'] as String,
    lines: json['lines'] as List<TransferLine>,
    requestedByUserId: json['requested_by_user_id'] as String?,
    approvedByUserId: json['approved_by_user_id'] as String?,
    dispatchedAtUtc: json['dispatched_at_utc'] as String?,
    receivedAtUtc: json['received_at_utc'] as String?,
    notes: json['notes'] as String?,
    createdAtUtc: json['created_at_utc'] as String,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'transfer_number': transferNumber,
    'tenant_id': tenantId,
    'source_branch_id': sourceBranchId,
    'destination_branch_id': destinationBranchId,
    'status': status,
    'lines': lines,
    'requested_by_user_id': requestedByUserId,
    'approved_by_user_id': approvedByUserId,
    'dispatched_at_utc': dispatchedAtUtc,
    'received_at_utc': receivedAtUtc,
    'notes': notes,
    'created_at_utc': createdAtUtc,
  };
}

/// Physical postal address.
class Address {
  final String streetLine1;
  final String? streetLine2;
  final String city;
  final String stateOrGovernorate;
  final String? postalCode;
  final String countryCode;

  const Address({
    required this.streetLine1,
    this.streetLine2,
    required this.city,
    required this.stateOrGovernorate,
    this.postalCode,
    required this.countryCode,
  });

  factory Address.fromJson(Map<String, dynamic> json) => Address(
    streetLine1: json['street_line1'] as String,
    streetLine2: json['street_line2'] as String?,
    city: json['city'] as String,
    stateOrGovernorate: json['state_or_governorate'] as String,
    postalCode: json['postal_code'] as String?,
    countryCode: json['country_code'] as String,
  );

  Map<String, dynamic> toJson() => {
    'street_line1': streetLine1,
    'street_line2': streetLine2,
    'city': city,
    'state_or_governorate': stateOrGovernorate,
    'postal_code': postalCode,
    'country_code': countryCode,
  };
}

/// Standard tracking and concurrency metadata for persistent records.
class AuditMetadata {
  final String createdAtUtc;
  final String? createdByUserId;
  final String? updatedAtUtc;
  final String? updatedByUserId;
  final int version;

  const AuditMetadata({
    required this.createdAtUtc,
    this.createdByUserId,
    this.updatedAtUtc,
    this.updatedByUserId,
    required this.version,
  });

  factory AuditMetadata.fromJson(Map<String, dynamic> json) => AuditMetadata(
    createdAtUtc: json['created_at_utc'] as String,
    createdByUserId: json['created_by_user_id'] as String?,
    updatedAtUtc: json['updated_at_utc'] as String?,
    updatedByUserId: json['updated_by_user_id'] as String?,
    version: json['version'] as int,
  );

  Map<String, dynamic> toJson() => {
    'created_at_utc': createdAtUtc,
    'created_by_user_id': createdByUserId,
    'updated_at_utc': updatedAtUtc,
    'updated_by_user_id': updatedByUserId,
    'version': version,
  };
}

/// Standardized machine-readable error payload.
class ErrorContract {
  final String code;
  final String message;
  final String? target;
  final dynamic? details;

  const ErrorContract({
    required this.code,
    required this.message,
    this.target,
    this.details,
  });

  factory ErrorContract.fromJson(Map<String, dynamic> json) => ErrorContract(
    code: json['code'] as String,
    message: json['message'] as String,
    target: json['target'] as String?,
    details: json['details'] as dynamic?,
  );

  Map<String, dynamic> toJson() => {
    'code': code,
    'message': message,
    'target': target,
    'details': details,
  };
}

/// Represents an immutable monetary amount in integer minor units with explicit ISO 4217 currency. Supports both canonical camelCase ('amountInMinorUnits') and legacy snake_case ('amount_minor_units'), plus 'decimalString' for lossless serialization beyond JavaScript's safe integer boundary.
class Money {
  final int? amountInMinorUnits;
  final int? amountMinorUnits;
  final String currency;
  final String? decimalString;

  const Money({
    this.amountInMinorUnits,
    this.amountMinorUnits,
    required this.currency,
    this.decimalString,
  });

  factory Money.fromJson(Map<String, dynamic> json) => Money(
    amountInMinorUnits: json['amountInMinorUnits'] as int?,
    amountMinorUnits: json['amount_minor_units'] as int?,
    currency: json['currency'] as String,
    decimalString: json['decimalString'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'amountInMinorUnits': amountInMinorUnits,
    'amount_minor_units': amountMinorUnits,
    'currency': currency,
    'decimalString': decimalString,
  };
}

/// Pagination parameters for list queries.
class PageRequest {
  final int pageNumber;
  final int pageSize;
  final String? searchTerm;
  final String? sortBy;
  final bool? sortDescending;

  const PageRequest({
    required this.pageNumber,
    required this.pageSize,
    this.searchTerm,
    this.sortBy,
    this.sortDescending,
  });

  factory PageRequest.fromJson(Map<String, dynamic> json) => PageRequest(
    pageNumber: json['page_number'] as int,
    pageSize: json['page_size'] as int,
    searchTerm: json['search_term'] as String?,
    sortBy: json['sort_by'] as String?,
    sortDescending: json['sort_descending'] as bool?,
  );

  Map<String, dynamic> toJson() => {
    'page_number': pageNumber,
    'page_size': pageSize,
    'search_term': searchTerm,
    'sort_by': sortBy,
    'sort_descending': sortDescending,
  };
}

/// Paginated envelope container for query results.
class PageResult {
  final int pageNumber;
  final int pageSize;
  final int totalCount;
  final int totalPages;
  final List<String> items;

  const PageResult({
    required this.pageNumber,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
    required this.items,
  });

  factory PageResult.fromJson(Map<String, dynamic> json) => PageResult(
    pageNumber: json['page_number'] as int,
    pageSize: json['page_size'] as int,
    totalCount: json['total_count'] as int,
    totalPages: json['total_pages'] as int,
    items: json['items'] as List<String>,
  );

  Map<String, dynamic> toJson() => {
    'page_number': pageNumber,
    'page_size': pageSize,
    'total_count': totalCount,
    'total_pages': totalPages,
    'items': items,
  };
}

/// Represents a physical inventory or dosage quantity with a unit of measure.
class Quantity {
  final double value;
  final String unit;

  const Quantity({
    required this.value,
    required this.unit,
  });

  factory Quantity.fromJson(Map<String, dynamic> json) => Quantity(
    value: json['value'] as double,
    unit: json['unit'] as String,
  );

  Map<String, dynamic> toJson() => {
    'value': value,
    'unit': unit,
  };
}

/// Universal outcome wrapper for API and communication endpoints.
class ResultEnvelope {
  final bool isSuccess;
  final String correlationId;
  final dynamic? data;
  final ErrorContract? error;

  const ResultEnvelope({
    required this.isSuccess,
    required this.correlationId,
    this.data,
    this.error,
  });

  factory ResultEnvelope.fromJson(Map<String, dynamic> json) => ResultEnvelope(
    isSuccess: json['is_success'] as bool,
    correlationId: json['correlation_id'] as String,
    data: json['data'] as dynamic?,
    error: json['error'] as ErrorContract?,
  );

  Map<String, dynamic> toJson() => {
    'is_success': isSuccess,
    'correlation_id': correlationId,
    'data': data,
    'error': error,
  };
}

/// Safe, non-sensitive device registration summary for administrative and licensing APIs.
class DeviceIdentitySummary {
  final String deviceId;
  final String deviceCode;
  final String chainId;
  final String pharmacyId;
  final String platform;
  final String deviceName;
  final DeviceStatus status;
  final String registeredAtUtc;
  final String? lastHeartbeatUtc;

  const DeviceIdentitySummary({
    required this.deviceId,
    required this.deviceCode,
    required this.chainId,
    required this.pharmacyId,
    required this.platform,
    required this.deviceName,
    required this.status,
    required this.registeredAtUtc,
    this.lastHeartbeatUtc,
  });

  factory DeviceIdentitySummary.fromJson(Map<String, dynamic> json) => DeviceIdentitySummary(
    deviceId: json['device_id'] as String,
    deviceCode: json['device_code'] as String,
    chainId: json['chain_id'] as String,
    pharmacyId: json['pharmacy_id'] as String,
    platform: json['platform'] as String,
    deviceName: json['device_name'] as String,
    status: json['status'] as DeviceStatus,
    registeredAtUtc: json['registered_at_utc'] as String,
    lastHeartbeatUtc: json['last_heartbeat_utc'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'device_id': deviceId,
    'device_code': deviceCode,
    'chain_id': chainId,
    'pharmacy_id': pharmacyId,
    'platform': platform,
    'device_name': deviceName,
    'status': status,
    'registered_at_utc': registeredAtUtc,
    'last_heartbeat_utc': lastHeartbeatUtc,
  };
}

/// High-level workstation environment and hardware metrics.
class DeviceInfo {
  final String machineName;
  final String operatingSystem;
  final String platform;
  final int processorCount;
  final int? totalMemoryMb;

  const DeviceInfo({
    required this.machineName,
    required this.operatingSystem,
    required this.platform,
    required this.processorCount,
    this.totalMemoryMb,
  });

  factory DeviceInfo.fromJson(Map<String, dynamic> json) => DeviceInfo(
    machineName: json['machine_name'] as String,
    operatingSystem: json['operating_system'] as String,
    platform: json['platform'] as String,
    processorCount: json['processor_count'] as int,
    totalMemoryMb: json['total_memory_mb'] as int?,
  );

  Map<String, dynamic> toJson() => {
    'machine_name': machineName,
    'operating_system': operatingSystem,
    'platform': platform,
    'processor_count': processorCount,
    'total_memory_mb': totalMemoryMb,
  };
}

/// Lifecycle registration status of an authorized hardware workstation.
enum DeviceStatus {
  PendingActivation,
  Active,
  Suspended,
  Revoked,
  Decommissioned
}

/// Active workstation login session without revealing tokens or session keys.
class SessionSummary {
  final String sessionId;
  final String userId;
  final String username;
  final String terminalId;
  final String branchId;
  final String loggedInAtUtc;
  final String? lastActivityUtc;

  const SessionSummary({
    required this.sessionId,
    required this.userId,
    required this.username,
    required this.terminalId,
    required this.branchId,
    required this.loggedInAtUtc,
    this.lastActivityUtc,
  });

  factory SessionSummary.fromJson(Map<String, dynamic> json) => SessionSummary(
    sessionId: json['session_id'] as String,
    userId: json['user_id'] as String,
    username: json['username'] as String,
    terminalId: json['terminal_id'] as String,
    branchId: json['branch_id'] as String,
    loggedInAtUtc: json['logged_in_at_utc'] as String,
    lastActivityUtc: json['last_activity_utc'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'session_id': sessionId,
    'user_id': userId,
    'username': username,
    'terminal_id': terminalId,
    'branch_id': branchId,
    'logged_in_at_utc': loggedInAtUtc,
    'last_activity_utc': lastActivityUtc,
  };
}

/// Pharmacy staff profile including professional role and assigned branch.
class StaffSummary {
  final String userId;
  final String fullName;
  final String role;
  final String branchId;
  final bool isPharmacist;
  final String? licenseNumber;

  const StaffSummary({
    required this.userId,
    required this.fullName,
    required this.role,
    required this.branchId,
    required this.isPharmacist,
    this.licenseNumber,
  });

  factory StaffSummary.fromJson(Map<String, dynamic> json) => StaffSummary(
    userId: json['user_id'] as String,
    fullName: json['full_name'] as String,
    role: json['role'] as String,
    branchId: json['branch_id'] as String,
    isPharmacist: json['is_pharmacist'] as bool,
    licenseNumber: json['license_number'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'user_id': userId,
    'full_name': fullName,
    'role': role,
    'branch_id': branchId,
    'is_pharmacist': isPharmacist,
    'license_number': licenseNumber,
  };
}

/// Safe user projection without sensitive credentials, password hashes, or secrets.
class UserSummary {
  final String userId;
  final String username;
  final String fullName;
  final String email;
  final String? phone;
  final bool isActive;

  const UserSummary({
    required this.userId,
    required this.username,
    required this.fullName,
    required this.email,
    this.phone,
    required this.isActive,
  });

  factory UserSummary.fromJson(Map<String, dynamic> json) => UserSummary(
    userId: json['user_id'] as String,
    username: json['username'] as String,
    fullName: json['full_name'] as String,
    email: json['email'] as String,
    phone: json['phone'] as String?,
    isActive: json['is_active'] as bool,
  );

  Map<String, dynamic> toJson() => {
    'user_id': userId,
    'username': username,
    'full_name': fullName,
    'email': email,
    'phone': phone,
    'is_active': isActive,
  };
}

/// Branch-specific operational profile, local purchasing cost, and inventory limits for a catalogue drug.
class LocalDrug {
  final String id;
  final String globalDrugId;
  final String pharmacyId;
  final Money localCostPrice;
  final Money? customSellingPrice;
  final String? storageLocationId;
  final int minimumStockThreshold;
  final int maximumStockThreshold;
  final bool isActiveLocally;

  const LocalDrug({
    required this.id,
    required this.globalDrugId,
    required this.pharmacyId,
    required this.localCostPrice,
    this.customSellingPrice,
    this.storageLocationId,
    required this.minimumStockThreshold,
    required this.maximumStockThreshold,
    required this.isActiveLocally,
  });

  factory LocalDrug.fromJson(Map<String, dynamic> json) => LocalDrug(
    id: json['id'] as String,
    globalDrugId: json['global_drug_id'] as String,
    pharmacyId: json['pharmacy_id'] as String,
    localCostPrice: json['local_cost_price'] as Money,
    customSellingPrice: json['custom_selling_price'] as Money?,
    storageLocationId: json['storage_location_id'] as String?,
    minimumStockThreshold: json['minimum_stock_threshold'] as int,
    maximumStockThreshold: json['maximum_stock_threshold'] as int,
    isActiveLocally: json['is_active_locally'] as bool,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'global_drug_id': globalDrugId,
    'pharmacy_id': pharmacyId,
    'local_cost_price': localCostPrice,
    'custom_selling_price': customSellingPrice,
    'storage_location_id': storageLocationId,
    'minimum_stock_threshold': minimumStockThreshold,
    'maximum_stock_threshold': maximumStockThreshold,
    'is_active_locally': isActiveLocally,
  };
}

/// Calculated inventory availability projection for checkout and dispensing.
class StockAvailability {
  final String drugId;
  final Quantity totalPhysicalQuantity;
  final Quantity totalReservedQuantity;
  final Quantity availableQuantity;
  final String? earliestExpiryDate;
  final int? lotCount;

  const StockAvailability({
    required this.drugId,
    required this.totalPhysicalQuantity,
    required this.totalReservedQuantity,
    required this.availableQuantity,
    this.earliestExpiryDate,
    this.lotCount,
  });

  factory StockAvailability.fromJson(Map<String, dynamic> json) => StockAvailability(
    drugId: json['drug_id'] as String,
    totalPhysicalQuantity: json['total_physical_quantity'] as Quantity,
    totalReservedQuantity: json['total_reserved_quantity'] as Quantity,
    availableQuantity: json['available_quantity'] as Quantity,
    earliestExpiryDate: json['earliest_expiry_date'] as String?,
    lotCount: json['lot_count'] as int?,
  );

  Map<String, dynamic> toJson() => {
    'drug_id': drugId,
    'total_physical_quantity': totalPhysicalQuantity,
    'total_reserved_quantity': totalReservedQuantity,
    'available_quantity': availableQuantity,
    'earliest_expiry_date': earliestExpiryDate,
    'lot_count': lotCount,
  };
}

/// Locally authoritative inventory lot tracking batch number, physical expiry, and base-unit count.
class StockLot {
  final String id;
  final String localDrugId;
  final String globalDrugId;
  final String batchNumber;
  final String expiryDate;
  final Quantity receivedQuantity;
  final Quantity remainingQuantity;
  final Quantity reservedQuantity;
  final Money purchasePrice;
  final String? distributorId;
  final String receivedDateUtc;

  const StockLot({
    required this.id,
    required this.localDrugId,
    required this.globalDrugId,
    required this.batchNumber,
    required this.expiryDate,
    required this.receivedQuantity,
    required this.remainingQuantity,
    required this.reservedQuantity,
    required this.purchasePrice,
    this.distributorId,
    required this.receivedDateUtc,
  });

  factory StockLot.fromJson(Map<String, dynamic> json) => StockLot(
    id: json['id'] as String,
    localDrugId: json['local_drug_id'] as String,
    globalDrugId: json['global_drug_id'] as String,
    batchNumber: json['batch_number'] as String,
    expiryDate: json['expiry_date'] as String,
    receivedQuantity: json['received_quantity'] as Quantity,
    remainingQuantity: json['remaining_quantity'] as Quantity,
    reservedQuantity: json['reserved_quantity'] as Quantity,
    purchasePrice: json['purchase_price'] as Money,
    distributorId: json['distributor_id'] as String?,
    receivedDateUtc: json['received_date_utc'] as String,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'local_drug_id': localDrugId,
    'global_drug_id': globalDrugId,
    'batch_number': batchNumber,
    'expiry_date': expiryDate,
    'received_quantity': receivedQuantity,
    'remaining_quantity': remainingQuantity,
    'reserved_quantity': reservedQuantity,
    'purchase_price': purchasePrice,
    'distributor_id': distributorId,
    'received_date_utc': receivedDateUtc,
  };
}

/// Ledger entry recording physical changes in inventory (sales, receipts, adjustments, returns, write-offs).
class StockMovement {
  final String movementId;
  final String lotId;
  final String drugId;
  final String movementType;
  final Quantity quantityDelta;
  final String? referenceDocumentId;
  final String? userId;
  final String timestampUtc;
  final String? reason;

  const StockMovement({
    required this.movementId,
    required this.lotId,
    required this.drugId,
    required this.movementType,
    required this.quantityDelta,
    this.referenceDocumentId,
    this.userId,
    required this.timestampUtc,
    this.reason,
  });

  factory StockMovement.fromJson(Map<String, dynamic> json) => StockMovement(
    movementId: json['movement_id'] as String,
    lotId: json['lot_id'] as String,
    drugId: json['drug_id'] as String,
    movementType: json['movement_type'] as String,
    quantityDelta: json['quantity_delta'] as Quantity,
    referenceDocumentId: json['reference_document_id'] as String?,
    userId: json['user_id'] as String?,
    timestampUtc: json['timestamp_utc'] as String,
    reason: json['reason'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'movement_id': movementId,
    'lot_id': lotId,
    'drug_id': drugId,
    'movement_type': movementType,
    'quantity_delta': quantityDelta,
    'reference_document_id': referenceDocumentId,
    'user_id': userId,
    'timestamp_utc': timestampUtc,
    'reason': reason,
  };
}

/// Physical pharmacy location (aisle, shelf, drawer, refrigerator).
class StorageLocation {
  final String id;
  final String pharmacyId;
  final String name;
  final String zone;
  final bool? isTemperatureControlled;

  const StorageLocation({
    required this.id,
    required this.pharmacyId,
    required this.name,
    required this.zone,
    this.isTemperatureControlled,
  });

  factory StorageLocation.fromJson(Map<String, dynamic> json) => StorageLocation(
    id: json['id'] as String,
    pharmacyId: json['pharmacy_id'] as String,
    name: json['name'] as String,
    zone: json['zone'] as String,
    isTemperatureControlled: json['is_temperature_controlled'] as bool?,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'pharmacy_id': pharmacyId,
    'name': name,
    'zone': zone,
    'is_temperature_controlled': isTemperatureControlled,
  };
}

/// Outcome of device activation attempt.
class ActivationResult {
  final bool isSuccessful;
  final String status;
  final String? certificateRaw;
  final String? failureReason;

  const ActivationResult({
    required this.isSuccessful,
    required this.status,
    this.certificateRaw,
    this.failureReason,
  });

  factory ActivationResult.fromJson(Map<String, dynamic> json) => ActivationResult(
    isSuccessful: json['is_successful'] as bool,
    status: json['status'] as String,
    certificateRaw: json['certificate_raw'] as String?,
    failureReason: json['failure_reason'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'is_successful': isSuccessful,
    'status': status,
    'certificate_raw': certificateRaw,
    'failure_reason': failureReason,
  };
}

/// Cryptographically signed entitlements and feature claims encoded in license certificates.
class LicenseClaims {
  final String licenseId;
  final String tenantId;
  final String tier;
  final String issuedAtUtc;
  final String expiresAtUtc;
  final int maxBranches;
  final int maxSeatsPerBranch;
  final List<string> enabledFeatures;

  const LicenseClaims({
    required this.licenseId,
    required this.tenantId,
    required this.tier,
    required this.issuedAtUtc,
    required this.expiresAtUtc,
    required this.maxBranches,
    required this.maxSeatsPerBranch,
    required this.enabledFeatures,
  });

  factory LicenseClaims.fromJson(Map<String, dynamic> json) => LicenseClaims(
    licenseId: json['license_id'] as String,
    tenantId: json['tenant_id'] as String,
    tier: json['tier'] as String,
    issuedAtUtc: json['issued_at_utc'] as String,
    expiresAtUtc: json['expires_at_utc'] as String,
    maxBranches: json['max_branches'] as int,
    maxSeatsPerBranch: json['max_seats_per_branch'] as int,
    enabledFeatures: json['enabled_features'] as List<string>,
  );

  Map<String, dynamic> toJson() => {
    'license_id': licenseId,
    'tenant_id': tenantId,
    'tier': tier,
    'issued_at_utc': issuedAtUtc,
    'expires_at_utc': expiresAtUtc,
    'max_branches': maxBranches,
    'max_seats_per_branch': maxSeatsPerBranch,
    'enabled_features': enabledFeatures,
  };
}

/// Validation status of a branch or workstation license.
enum LicenseStatus {
  Valid,
  GracePeriod,
  Expired,
  Suspended,
  Revoked,
  InvalidSignature,
  DeviceMismatch
}

/// Patient's active or recurring prescription medication regimen.
class Medication {
  final String medicationId;
  final String patientId;
  final String drugId;
  final String drugName;
  final String dosageInstructions;
  final String? prescribingDoctor;
  final String startDate;
  final String? endDate;
  final bool? isActive;

  const Medication({
    required this.medicationId,
    required this.patientId,
    required this.drugId,
    required this.drugName,
    required this.dosageInstructions,
    this.prescribingDoctor,
    required this.startDate,
    this.endDate,
    this.isActive,
  });

  factory Medication.fromJson(Map<String, dynamic> json) => Medication(
    medicationId: json['medication_id'] as String,
    patientId: json['patient_id'] as String,
    drugId: json['drug_id'] as String,
    drugName: json['drug_name'] as String,
    dosageInstructions: json['dosage_instructions'] as String,
    prescribingDoctor: json['prescribing_doctor'] as String?,
    startDate: json['start_date'] as String,
    endDate: json['end_date'] as String?,
    isActive: json['is_active'] as bool?,
  );

  Map<String, dynamic> toJson() => {
    'medication_id': medicationId,
    'patient_id': patientId,
    'drug_id': drugId,
    'drug_name': drugName,
    'dosage_instructions': dosageInstructions,
    'prescribing_doctor': prescribingDoctor,
    'start_date': startDate,
    'end_date': endDate,
    'is_active': isActive,
  };
}

/// Historical dispensing record for a patient.
class MedicationHistoryEntry {
  final String dispenseId;
  final String patientId;
  final String drugId;
  final String drugName;
  final Quantity quantityDispensed;
  final String? pharmacyId;
  final String dispensedAtUtc;

  const MedicationHistoryEntry({
    required this.dispenseId,
    required this.patientId,
    required this.drugId,
    required this.drugName,
    required this.quantityDispensed,
    this.pharmacyId,
    required this.dispensedAtUtc,
  });

  factory MedicationHistoryEntry.fromJson(Map<String, dynamic> json) => MedicationHistoryEntry(
    dispenseId: json['dispense_id'] as String,
    patientId: json['patient_id'] as String,
    drugId: json['drug_id'] as String,
    drugName: json['drug_name'] as String,
    quantityDispensed: json['quantity_dispensed'] as Quantity,
    pharmacyId: json['pharmacy_id'] as String?,
    dispensedAtUtc: json['dispensed_at_utc'] as String,
  );

  Map<String, dynamic> toJson() => {
    'dispense_id': dispenseId,
    'patient_id': patientId,
    'drug_id': drugId,
    'drug_name': drugName,
    'quantity_dispensed': quantityDispensed,
    'pharmacy_id': pharmacyId,
    'dispensed_at_utc': dispensedAtUtc,
  };
}

/// Authoritative global patient profile with protected medical and allergy alerts.
class Patient {
  final String id;
  final String? nationalIdHash;
  final String fullName;
  final String phoneNumber;
  final String? dateOfBirth;
  final String gender;
  final String? bloodGroup;
  final List<string>? chronicConditions;
  final List<string>? allergies;
  final String createdAtUtc;

  const Patient({
    required this.id,
    this.nationalIdHash,
    required this.fullName,
    required this.phoneNumber,
    this.dateOfBirth,
    required this.gender,
    this.bloodGroup,
    this.chronicConditions,
    this.allergies,
    required this.createdAtUtc,
  });

  factory Patient.fromJson(Map<String, dynamic> json) => Patient(
    id: json['id'] as String,
    nationalIdHash: json['national_id_hash'] as String?,
    fullName: json['full_name'] as String,
    phoneNumber: json['phone_number'] as String,
    dateOfBirth: json['date_of_birth'] as String?,
    gender: json['gender'] as String,
    bloodGroup: json['blood_group'] as String?,
    chronicConditions: json['chronic_conditions'] as List<string>?,
    allergies: json['allergies'] as List<string>?,
    createdAtUtc: json['created_at_utc'] as String,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'national_id_hash': nationalIdHash,
    'full_name': fullName,
    'phone_number': phoneNumber,
    'date_of_birth': dateOfBirth,
    'gender': gender,
    'blood_group': bloodGroup,
    'chronic_conditions': chronicConditions,
    'allergies': allergies,
    'created_at_utc': createdAtUtc,
  };
}

/// Explicit authorization granted by a patient for sharing or viewing clinical records.
class PatientConsent {
  final String consentId;
  final String patientId;
  final String? authorizedChainId;
  final String? authorizedPharmacyId;
  final String grantedScope;
  final String grantedAtUtc;
  final String expiresAtUtc;
  final bool? isRevoked;

  const PatientConsent({
    required this.consentId,
    required this.patientId,
    this.authorizedChainId,
    this.authorizedPharmacyId,
    required this.grantedScope,
    required this.grantedAtUtc,
    required this.expiresAtUtc,
    this.isRevoked,
  });

  factory PatientConsent.fromJson(Map<String, dynamic> json) => PatientConsent(
    consentId: json['consent_id'] as String,
    patientId: json['patient_id'] as String,
    authorizedChainId: json['authorized_chain_id'] as String?,
    authorizedPharmacyId: json['authorized_pharmacy_id'] as String?,
    grantedScope: json['granted_scope'] as String,
    grantedAtUtc: json['granted_at_utc'] as String,
    expiresAtUtc: json['expires_at_utc'] as String,
    isRevoked: json['is_revoked'] as bool?,
  );

  Map<String, dynamic> toJson() => {
    'consent_id': consentId,
    'patient_id': patientId,
    'authorized_chain_id': authorizedChainId,
    'authorized_pharmacy_id': authorizedPharmacyId,
    'granted_scope': grantedScope,
    'granted_at_utc': grantedAtUtc,
    'expires_at_utc': expiresAtUtc,
    'is_revoked': isRevoked,
  };
}

/// Safe, non-sensitive patient summary for POS customer selection without leaking private clinical data.
class PatientSummary {
  final String patientId;
  final String fullName;
  final String phoneNumber;
  final bool? hasChronicConditions;
  final bool? hasAllergies;

  const PatientSummary({
    required this.patientId,
    required this.fullName,
    required this.phoneNumber,
    this.hasChronicConditions,
    this.hasAllergies,
  });

  factory PatientSummary.fromJson(Map<String, dynamic> json) => PatientSummary(
    patientId: json['patient_id'] as String,
    fullName: json['full_name'] as String,
    phoneNumber: json['phone_number'] as String,
    hasChronicConditions: json['has_chronic_conditions'] as bool?,
    hasAllergies: json['has_allergies'] as bool?,
  );

  Map<String, dynamic> toJson() => {
    'patient_id': patientId,
    'full_name': fullName,
    'phone_number': phoneNumber,
    'has_chronic_conditions': hasChronicConditions,
    'has_allergies': hasAllergies,
  };
}

/// Approval decision outcome for a sensitive operation.
class ChangeDecision {
  final String decision;
  final String decidedByUserId;
  final String decidedAtUtc;
  final String? notes;

  const ChangeDecision({
    required this.decision,
    required this.decidedByUserId,
    required this.decidedAtUtc,
    this.notes,
  });

  factory ChangeDecision.fromJson(Map<String, dynamic> json) => ChangeDecision(
    decision: json['decision'] as String,
    decidedByUserId: json['decided_by_user_id'] as String,
    decidedAtUtc: json['decided_at_utc'] as String,
    notes: json['notes'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'decision': decision,
    'decided_by_user_id': decidedByUserId,
    'decided_at_utc': decidedAtUtc,
    'notes': notes,
  };
}

/// Approval request for sensitive operations requiring management authorization.
class ChangeRequest {
  final String requestId;
  final String tenantId;
  final String branchId;
  final String requestedByUserId;
  final String changeType;
  final String reason;
  final String status;
  final ChangeDecision? decision;
  final String createdAtUtc;

  const ChangeRequest({
    required this.requestId,
    required this.tenantId,
    required this.branchId,
    required this.requestedByUserId,
    required this.changeType,
    required this.reason,
    required this.status,
    this.decision,
    required this.createdAtUtc,
  });

  factory ChangeRequest.fromJson(Map<String, dynamic> json) => ChangeRequest(
    requestId: json['request_id'] as String,
    tenantId: json['tenant_id'] as String,
    branchId: json['branch_id'] as String,
    requestedByUserId: json['requested_by_user_id'] as String,
    changeType: json['change_type'] as String,
    reason: json['reason'] as String,
    status: json['status'] as String,
    decision: json['decision'] as ChangeDecision?,
    createdAtUtc: json['created_at_utc'] as String,
  );

  Map<String, dynamic> toJson() => {
    'request_id': requestId,
    'tenant_id': tenantId,
    'branch_id': branchId,
    'requested_by_user_id': requestedByUserId,
    'change_type': changeType,
    'reason': reason,
    'status': status,
    'decision': decision,
    'created_at_utc': createdAtUtc,
  };
}

/// Granular row-level change descriptor for delta synchronization.
class RecordChange {
  final String tableName;
  final String recordId;
  final String changeType;
  final String? dataJson;
  final String changedAtUtc;

  const RecordChange({
    required this.tableName,
    required this.recordId,
    required this.changeType,
    this.dataJson,
    required this.changedAtUtc,
  });

  factory RecordChange.fromJson(Map<String, dynamic> json) => RecordChange(
    tableName: json['table_name'] as String,
    recordId: json['record_id'] as String,
    changeType: json['change_type'] as String,
    dataJson: json['data_json'] as String?,
    changedAtUtc: json['changed_at_utc'] as String,
  );

  Map<String, dynamic> toJson() => {
    'table_name': tableName,
    'record_id': recordId,
    'change_type': changeType,
    'data_json': dataJson,
    'changed_at_utc': changedAtUtc,
  };
}

/// Compressed, ordered transmission of sync events from a branch to cloud.
class SyncBatch {
  final String batchId;
  final String branchId;
  final int clientSequenceStart;
  final int clientSequenceEnd;
  final List<SyncEventEnvelope> events;
  final String sentAtUtc;

  const SyncBatch({
    required this.batchId,
    required this.branchId,
    required this.clientSequenceStart,
    required this.clientSequenceEnd,
    required this.events,
    required this.sentAtUtc,
  });

  factory SyncBatch.fromJson(Map<String, dynamic> json) => SyncBatch(
    batchId: json['batch_id'] as String,
    branchId: json['branch_id'] as String,
    clientSequenceStart: json['client_sequence_start'] as int,
    clientSequenceEnd: json['client_sequence_end'] as int,
    events: json['events'] as List<SyncEventEnvelope>,
    sentAtUtc: json['sent_at_utc'] as String,
  );

  Map<String, dynamic> toJson() => {
    'batch_id': batchId,
    'branch_id': branchId,
    'client_sequence_start': clientSequenceStart,
    'client_sequence_end': clientSequenceEnd,
    'events': events,
    'sent_at_utc': sentAtUtc,
  };
}

/// Universal domain mutation event envelope for offline-first replication.
class SyncEventEnvelope {
  final String eventId;
  final String aggregateType;
  final String aggregateId;
  final String eventType;
  final String mutationKind;
  final String occurredAtUtc;
  final String tenantId;
  final String branchId;
  final String payloadJson;
  final String schemaVersion;

  const SyncEventEnvelope({
    required this.eventId,
    required this.aggregateType,
    required this.aggregateId,
    required this.eventType,
    required this.mutationKind,
    required this.occurredAtUtc,
    required this.tenantId,
    required this.branchId,
    required this.payloadJson,
    required this.schemaVersion,
  });

  factory SyncEventEnvelope.fromJson(Map<String, dynamic> json) => SyncEventEnvelope(
    eventId: json['event_id'] as String,
    aggregateType: json['aggregate_type'] as String,
    aggregateId: json['aggregate_id'] as String,
    eventType: json['event_type'] as String,
    mutationKind: json['mutation_kind'] as String,
    occurredAtUtc: json['occurred_at_utc'] as String,
    tenantId: json['tenant_id'] as String,
    branchId: json['branch_id'] as String,
    payloadJson: json['payload_json'] as String,
    schemaVersion: json['schema_version'] as String,
  );

  Map<String, dynamic> toJson() => {
    'event_id': eventId,
    'aggregate_type': aggregateType,
    'aggregate_id': aggregateId,
    'event_type': eventType,
    'mutation_kind': mutationKind,
    'occurred_at_utc': occurredAtUtc,
    'tenant_id': tenantId,
    'branch_id': branchId,
    'payload_json': payloadJson,
    'schema_version': schemaVersion,
  };
}

/// Tenant branch projection for physical branch or warehouse operations.
class Branch {
  final String id;
  final String chainId;
  final String? globalPharmacyId;
  final String branchCode;
  final String name;
  final bool? isWarehouse;
  final Address address;
  final String phone;
  final String? managerName;
  final bool isActive;

  const Branch({
    required this.id,
    required this.chainId,
    this.globalPharmacyId,
    required this.branchCode,
    required this.name,
    this.isWarehouse,
    required this.address,
    required this.phone,
    this.managerName,
    required this.isActive,
  });

  factory Branch.fromJson(Map<String, dynamic> json) => Branch(
    id: json['id'] as String,
    chainId: json['chain_id'] as String,
    globalPharmacyId: json['global_pharmacy_id'] as String?,
    branchCode: json['branch_code'] as String,
    name: json['name'] as String,
    isWarehouse: json['is_warehouse'] as bool?,
    address: json['address'] as Address,
    phone: json['phone'] as String,
    managerName: json['manager_name'] as String?,
    isActive: json['is_active'] as bool,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'chain_id': chainId,
    'global_pharmacy_id': globalPharmacyId,
    'branch_code': branchCode,
    'name': name,
    'is_warehouse': isWarehouse,
    'address': address,
    'phone': phone,
    'manager_name': managerName,
    'is_active': isActive,
  };
}

/// Multi-tenant organizational group representing a pharmacy chain or business.
class Chain {
  final String id;
  final String name;
  final String taxNumber;
  final String? commercialRegistration;
  final String subscriptionTier;
  final String provisionStatus;
  final String createdAtUtc;

  const Chain({
    required this.id,
    required this.name,
    required this.taxNumber,
    this.commercialRegistration,
    required this.subscriptionTier,
    required this.provisionStatus,
    required this.createdAtUtc,
  });

  factory Chain.fromJson(Map<String, dynamic> json) => Chain(
    id: json['id'] as String,
    name: json['name'] as String,
    taxNumber: json['tax_number'] as String,
    commercialRegistration: json['commercial_registration'] as String?,
    subscriptionTier: json['subscription_tier'] as String,
    provisionStatus: json['provision_status'] as String,
    createdAtUtc: json['created_at_utc'] as String,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'name': name,
    'tax_number': taxNumber,
    'commercial_registration': commercialRegistration,
    'subscription_tier': subscriptionTier,
    'provision_status': provisionStatus,
    'created_at_utc': createdAtUtc,
  };
}

/// Globally registered pharmacy license entity.
class Pharmacy {
  final String id;
  final String chainId;
  final String name;
  final String licenseNumber;
  final String? supervisingPharmacist;
  final bool isActive;

  const Pharmacy({
    required this.id,
    required this.chainId,
    required this.name,
    required this.licenseNumber,
    this.supervisingPharmacist,
    required this.isActive,
  });

  factory Pharmacy.fromJson(Map<String, dynamic> json) => Pharmacy(
    id: json['id'] as String,
    chainId: json['chain_id'] as String,
    name: json['name'] as String,
    licenseNumber: json['license_number'] as String,
    supervisingPharmacist: json['supervising_pharmacist'] as String?,
    isActive: json['is_active'] as bool,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'chain_id': chainId,
    'name': name,
    'license_number': licenseNumber,
    'supervising_pharmacist': supervisingPharmacist,
    'is_active': isActive,
  };
}

/// Lifecycle state of a tenant chain database and services.
enum TenantProvisionStatus {
  Pending,
  Provisioning,
  Active,
  Suspended,
  Decommissioned
}

/// Retail point-of-sale customer tender payment (Cash, Card, Voucher). Strictly separate from SaaS BillingPayment.
class Payment {
  final String paymentId;
  final String method;
  final Money amount;
  final Money? tenderedAmount;
  final Money? changeReturned;
  final String status;
  final String? cardReference;

  const Payment({
    required this.paymentId,
    required this.method,
    required this.amount,
    this.tenderedAmount,
    this.changeReturned,
    required this.status,
    this.cardReference,
  });

  factory Payment.fromJson(Map<String, dynamic> json) => Payment(
    paymentId: json['payment_id'] as String,
    method: json['method'] as String,
    amount: json['amount'] as Money,
    tenderedAmount: json['tendered_amount'] as Money?,
    changeReturned: json['change_returned'] as Money?,
    status: json['status'] as String,
    cardReference: json['card_reference'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'payment_id': paymentId,
    'method': method,
    'amount': amount,
    'tendered_amount': tenderedAmount,
    'change_returned': changeReturned,
    'status': status,
    'card_reference': cardReference,
  };
}

/// Daily or shift accounting financial summary.
class Settlement {
  final String settlementId;
  final String pharmacyId;
  final String businessDate;
  final Money totalSales;
  final Money totalReturns;
  final Money netSales;
  final Money cashTotal;
  final Money cardTotal;
  final String? settledByUserId;
  final String settledAtUtc;

  const Settlement({
    required this.settlementId,
    required this.pharmacyId,
    required this.businessDate,
    required this.totalSales,
    required this.totalReturns,
    required this.netSales,
    required this.cashTotal,
    required this.cardTotal,
    this.settledByUserId,
    required this.settledAtUtc,
  });

  factory Settlement.fromJson(Map<String, dynamic> json) => Settlement(
    settlementId: json['settlement_id'] as String,
    pharmacyId: json['pharmacy_id'] as String,
    businessDate: json['business_date'] as String,
    totalSales: json['total_sales'] as Money,
    totalReturns: json['total_returns'] as Money,
    netSales: json['net_sales'] as Money,
    cashTotal: json['cash_total'] as Money,
    cardTotal: json['card_total'] as Money,
    settledByUserId: json['settled_by_user_id'] as String?,
    settledAtUtc: json['settled_at_utc'] as String,
  );

  Map<String, dynamic> toJson() => {
    'settlement_id': settlementId,
    'pharmacy_id': pharmacyId,
    'business_date': businessDate,
    'total_sales': totalSales,
    'total_returns': totalReturns,
    'net_sales': netSales,
    'cash_total': cashTotal,
    'card_total': cardTotal,
    'settled_by_user_id': settledByUserId,
    'settled_at_utc': settledAtUtc,
  };
}

/// Cashier shift session tracking physical cash reconciliation.
class Shift {
  final String id;
  final int shiftNumber;
  final String pharmacyId;
  final String terminalId;
  final String cashierUserId;
  final Money openingCash;
  final Money? closingCash;
  final Money? expectedCash;
  final Money? cashDifference;
  final String status;
  final String openedAtUtc;
  final String? closedAtUtc;

  const Shift({
    required this.id,
    required this.shiftNumber,
    required this.pharmacyId,
    required this.terminalId,
    required this.cashierUserId,
    required this.openingCash,
    this.closingCash,
    this.expectedCash,
    this.cashDifference,
    required this.status,
    required this.openedAtUtc,
    this.closedAtUtc,
  });

  factory Shift.fromJson(Map<String, dynamic> json) => Shift(
    id: json['id'] as String,
    shiftNumber: json['shift_number'] as int,
    pharmacyId: json['pharmacy_id'] as String,
    terminalId: json['terminal_id'] as String,
    cashierUserId: json['cashier_user_id'] as String,
    openingCash: json['opening_cash'] as Money,
    closingCash: json['closing_cash'] as Money?,
    expectedCash: json['expected_cash'] as Money?,
    cashDifference: json['cash_difference'] as Money?,
    status: json['status'] as String,
    openedAtUtc: json['opened_at_utc'] as String,
    closedAtUtc: json['closed_at_utc'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'shift_number': shiftNumber,
    'pharmacy_id': pharmacyId,
    'terminal_id': terminalId,
    'cashier_user_id': cashierUserId,
    'opening_cash': openingCash,
    'closing_cash': closingCash,
    'expected_cash': expectedCash,
    'cash_difference': cashDifference,
    'status': status,
    'opened_at_utc': openedAtUtc,
    'closed_at_utc': closedAtUtc,
  };
}

/// Authoritative point-of-sale retail transaction, line items, and payment split.
class Transaction {
  final String id;
  final String invoiceNumber;
  final String pharmacyId;
  final String terminalId;
  final String cashierUserId;
  final String shiftId;
  final String? patientId;
  final String type;
  final String status;
  final Money subtotal;
  final Money discountAmount;
  final Money taxAmount;
  final Money totalAmount;
  final List<TransactionItem> items;
  final List<Payment> payments;
  final String createdAtUtc;
  final String? completedAtUtc;

  const Transaction({
    required this.id,
    required this.invoiceNumber,
    required this.pharmacyId,
    required this.terminalId,
    required this.cashierUserId,
    required this.shiftId,
    this.patientId,
    required this.type,
    required this.status,
    required this.subtotal,
    required this.discountAmount,
    required this.taxAmount,
    required this.totalAmount,
    required this.items,
    required this.payments,
    required this.createdAtUtc,
    this.completedAtUtc,
  });

  factory Transaction.fromJson(Map<String, dynamic> json) => Transaction(
    id: json['id'] as String,
    invoiceNumber: json['invoice_number'] as String,
    pharmacyId: json['pharmacy_id'] as String,
    terminalId: json['terminal_id'] as String,
    cashierUserId: json['cashier_user_id'] as String,
    shiftId: json['shift_id'] as String,
    patientId: json['patient_id'] as String?,
    type: json['type'] as String,
    status: json['status'] as String,
    subtotal: json['subtotal'] as Money,
    discountAmount: json['discount_amount'] as Money,
    taxAmount: json['tax_amount'] as Money,
    totalAmount: json['total_amount'] as Money,
    items: json['items'] as List<TransactionItem>,
    payments: json['payments'] as List<Payment>,
    createdAtUtc: json['created_at_utc'] as String,
    completedAtUtc: json['completed_at_utc'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'invoice_number': invoiceNumber,
    'pharmacy_id': pharmacyId,
    'terminal_id': terminalId,
    'cashier_user_id': cashierUserId,
    'shift_id': shiftId,
    'patient_id': patientId,
    'type': type,
    'status': status,
    'subtotal': subtotal,
    'discount_amount': discountAmount,
    'tax_amount': taxAmount,
    'total_amount': totalAmount,
    'items': items,
    'payments': payments,
    'created_at_utc': createdAtUtc,
    'completed_at_utc': completedAtUtc,
  };
}

/// Line item in a retail sales or return transaction.
class TransactionItem {
  final String itemId;
  final String drugId;
  final String drugName;
  final String packageTier;
  final Quantity quantity;
  final Money unitPrice;
  final Money discountAmount;
  final double? taxRate;
  final Money totalLineAmount;
  final String? lotId;

  const TransactionItem({
    required this.itemId,
    required this.drugId,
    required this.drugName,
    required this.packageTier,
    required this.quantity,
    required this.unitPrice,
    required this.discountAmount,
    this.taxRate,
    required this.totalLineAmount,
    this.lotId,
  });

  factory TransactionItem.fromJson(Map<String, dynamic> json) => TransactionItem(
    itemId: json['item_id'] as String,
    drugId: json['drug_id'] as String,
    drugName: json['drug_name'] as String,
    packageTier: json['package_tier'] as String,
    quantity: json['quantity'] as Quantity,
    unitPrice: json['unit_price'] as Money,
    discountAmount: json['discount_amount'] as Money,
    taxRate: json['tax_rate'] as double?,
    totalLineAmount: json['total_line_amount'] as Money,
    lotId: json['lot_id'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'item_id': itemId,
    'drug_id': drugId,
    'drug_name': drugName,
    'package_tier': packageTier,
    'quantity': quantity,
    'unit_price': unitPrice,
    'discount_amount': discountAmount,
    'tax_rate': taxRate,
    'total_line_amount': totalLineAmount,
    'lot_id': lotId,
  };
}

