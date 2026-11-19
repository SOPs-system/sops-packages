/// Canonical Dart models and domain contracts for SOPs.
library sops_models;

// ==========================================
// 1. Common Types & Value Objects
// ==========================================

class Money {
  final int amountMinorUnits;
  final String currency;
  final String? decimalString;

  const Money({
    required this.amountMinorUnits,
    required this.currency,
    this.decimalString,
  });

  factory Money.fromJson(Map<String, dynamic> json) {
    final rawAmount = json['amountInMinorUnits'] ?? json['amount_minor_units'] ?? json['amount'];
    final rawCurrency = json['currency'] as String;
    final decStr = json['decimalString'] as String?;
    return Money(
      amountMinorUnits: (rawAmount as num).toInt(),
      currency: rawCurrency,
      decimalString: decStr,
    );
  }

  Map<String, dynamic> toJson() => {
        'amountInMinorUnits': amountMinorUnits,
        'amount_minor_units': amountMinorUnits,
        'currency': currency,
        if (decimalString != null) 'decimalString': decimalString,
      };

  double toDecimal([int decimalPlaces = 2]) {
    var factor = 1.0;
    for (var i = 0; i < decimalPlaces; i++) {
      factor *= 10;
    }
    return amountMinorUnits / factor;
  }

  @override
  String toString() => '${toDecimal().toStringAsFixed(2)} $currency';
}

class Quantity {
  final double value;
  final String unit;

  const Quantity({
    required this.value,
    required this.unit,
  });

  factory Quantity.fromJson(Map<String, dynamic> json) {
    return Quantity(
      value: (json['value'] as num).toDouble(),
      unit: json['unit'] as String,
    );
  }

  Map<String, dynamic> toJson() => {
        'value': value,
        'unit': unit,
      };

  @override
  String toString() => '$value $unit';
}

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
    this.countryCode = 'EG',
  });

  factory Address.fromJson(Map<String, dynamic> json) {
    return Address(
      streetLine1: json['street_line1'] as String,
      streetLine2: json['street_line2'] as String?,
      city: json['city'] as String,
      stateOrGovernorate: json['state_or_governorate'] as String,
      postalCode: json['postal_code'] as String?,
      countryCode: json['country_code'] as String? ?? 'EG',
    );
  }

  Map<String, dynamic> toJson() => {
        'street_line1': streetLine1,
        'street_line2': streetLine2,
        'city': city,
        'state_or_governorate': stateOrGovernorate,
        'postal_code': postalCode,
        'country_code': countryCode,
      };
}

class ErrorContract {
  final String code;
  final String message;
  final String? target;
  final Map<String, String>? details;

  const ErrorContract({
    required this.code,
    required this.message,
    this.target,
    this.details,
  });

  factory ErrorContract.fromJson(Map<String, dynamic> json) {
    return ErrorContract(
      code: json['code'] as String,
      message: json['message'] as String,
      target: json['target'] as String?,
      details: (json['details'] as Map<String, dynamic>?)?.map(
        (k, v) => MapEntry(k, v.toString()),
      ),
    );
  }

  Map<String, dynamic> toJson() => {
        'code': code,
        'message': message,
        'target': target,
        'details': details,
      };
}

class ResultEnvelope<T> {
  final bool isSuccess;
  final String correlationId;
  final T? data;
  final ErrorContract? error;

  const ResultEnvelope({
    required this.isSuccess,
    required this.correlationId,
    this.data,
    this.error,
  });
}

// ==========================================
// 2. Catalogue Domain
// ==========================================

enum DrugDosageForm {
  tablet,
  capsule,
  syrup,
  suspension,
  sachet,
  injection,
  drop,
  cream,
  ointment,
  gel,
  inhaler,
  patch,
  suppository,
  spray,
  lotion,
  other
}

enum StorageType {
  roomTemperature,
  refrigerated,
  frozen,
  controlledSubstance
}

enum DrugStatus {
  active,
  suspended,
  discontinued,
  recalled
}

class DrugPackage {
  final String tierName;
  final int conversionFactorToBase;
  final bool isDefaultDispensingUnit;

  const DrugPackage({
    required this.tierName,
    required this.conversionFactorToBase,
    this.isDefaultDispensingUnit = false,
  });

  factory DrugPackage.fromJson(Map<String, dynamic> json) {
    return DrugPackage(
      tierName: json['tier_name'] as String,
      conversionFactorToBase: json['conversion_factor_to_base'] as int,
      isDefaultDispensingUnit: json['is_default_dispensing_unit'] as bool? ?? false,
    );
  }

  Map<String, dynamic> toJson() => {
        'tier_name': tierName,
        'conversion_factor_to_base': conversionFactorToBase,
        'is_default_dispensing_unit': isDefaultDispensingUnit,
      };
}

class ActiveIngredient {
  final String name;
  final String strength;

  const ActiveIngredient({
    required this.name,
    required this.strength,
  });

  factory ActiveIngredient.fromJson(Map<String, dynamic> json) {
    return ActiveIngredient(
      name: json['name'] as String,
      strength: json['strength'] as String,
    );
  }

  Map<String, dynamic> toJson() => {
        'name': name,
        'strength': strength,
      };
}

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
  final List<ActiveIngredient> activeIngredients;
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
    this.isPriceRegulated = true,
    this.requiresPrescription = false,
    this.storageType = 'RoomTemperature',
    required this.packages,
    this.activeIngredients = const [],
    this.status = 'Active',
    this.catalogRevision = 1,
  });

  factory Drug.fromJson(Map<String, dynamic> json) {
    return Drug(
      id: json['id'] as String,
      sku: json['sku'] as String,
      barcode: json['barcode'] as String,
      nameEn: json['name_en'] as String,
      nameAr: json['name_ar'] as String,
      genericName: json['generic_name'] as String,
      categoryId: json['category_id'] as String?,
      dosageForm: json['dosage_form'] as String,
      officialPrice: Money.fromJson(json['official_price'] as Map<String, dynamic>),
      isPriceRegulated: json['is_price_regulated'] as bool? ?? true,
      requiresPrescription: json['requires_prescription'] as bool? ?? false,
      storageType: json['storage_type'] as String? ?? 'RoomTemperature',
      packages: (json['packages'] as List<dynamic>)
          .map((e) => DrugPackage.fromJson(e as Map<String, dynamic>))
          .toList(),
      activeIngredients: (json['active_ingredients'] as List<dynamic>?)
              ?.map((e) => ActiveIngredient.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const [],
      status: json['status'] as String? ?? 'Active',
      catalogRevision: json['catalog_revision'] as int? ?? 1,
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'sku': sku,
        'barcode': barcode,
        'name_en': nameEn,
        'name_ar': nameAr,
        'generic_name': genericName,
        'category_id': categoryId,
        'dosage_form': dosageForm,
        'official_price': officialPrice.toJson(),
        'is_price_regulated': isPriceRegulated,
        'requires_prescription': requiresPrescription,
        'storage_type': storageType,
        'packages': packages.map((e) => e.toJson()).toList(),
        'active_ingredients': activeIngredients.map((e) => e.toJson()).toList(),
        'status': status,
        'catalog_revision': catalogRevision,
      };
}

// ==========================================
// 3. Transactions Domain
// ==========================================

enum PaymentMethod {
  cash,
  creditCard,
  debitCard,
  voucher,
  insuranceClaim,
  split,
  loyaltyPoints
}

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
    this.status = 'Completed',
    this.cardReference,
  });

  factory Payment.fromJson(Map<String, dynamic> json) {
    return Payment(
      paymentId: json['payment_id'] as String,
      method: json['method'] as String,
      amount: Money.fromJson(json['amount'] as Map<String, dynamic>),
      tenderedAmount: json['tendered_amount'] != null
          ? Money.fromJson(json['tendered_amount'] as Map<String, dynamic>)
          : null,
      changeReturned: json['change_returned'] != null
          ? Money.fromJson(json['change_returned'] as Map<String, dynamic>)
          : null,
      status: json['status'] as String? ?? 'Completed',
      cardReference: json['card_reference'] as String?,
    );
  }

  Map<String, dynamic> toJson() => {
        'payment_id': paymentId,
        'method': method,
        'amount': amount.toJson(),
        'tendered_amount': tenderedAmount?.toJson(),
        'change_returned': changeReturned?.toJson(),
        'status': status,
        'card_reference': cardReference,
      };
}

class TransactionItem {
  final String itemId;
  final String drugId;
  final String drugName;
  final String packageTier;
  final Quantity quantity;
  final Money unitPrice;
  final Money discountAmount;
  final double taxRate;
  final Money totalLineAmount;
  final String? lotId;

  const TransactionItem({
    required this.itemId,
    required this.drugId,
    required this.drugName,
    this.packageTier = 'Unit',
    required this.quantity,
    required this.unitPrice,
    required this.discountAmount,
    this.taxRate = 0.0,
    required this.totalLineAmount,
    this.lotId,
  });

  factory TransactionItem.fromJson(Map<String, dynamic> json) {
    return TransactionItem(
      itemId: json['item_id'] as String,
      drugId: json['drug_id'] as String,
      drugName: json['drug_name'] as String,
      packageTier: json['package_tier'] as String? ?? 'Unit',
      quantity: Quantity.fromJson(json['quantity'] as Map<String, dynamic>),
      unitPrice: Money.fromJson(json['unit_price'] as Map<String, dynamic>),
      discountAmount: Money.fromJson(json['discount_amount'] as Map<String, dynamic>),
      taxRate: (json['tax_rate'] as num?)?.toDouble() ?? 0.0,
      totalLineAmount: Money.fromJson(json['total_line_amount'] as Map<String, dynamic>),
      lotId: json['lot_id'] as String?,
    );
  }

  Map<String, dynamic> toJson() => {
        'item_id': itemId,
        'drug_id': drugId,
        'drug_name': drugName,
        'package_tier': packageTier,
        'quantity': quantity.toJson(),
        'unit_price': unitPrice.toJson(),
        'discount_amount': discountAmount.toJson(),
        'tax_rate': taxRate,
        'total_line_amount': totalLineAmount.toJson(),
        'lot_id': lotId,
      };
}

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
    this.type = 'Sale',
    this.status = 'Completed',
    required this.subtotal,
    required this.discountAmount,
    required this.taxAmount,
    required this.totalAmount,
    required this.items,
    this.payments = const [],
    required this.createdAtUtc,
    this.completedAtUtc,
  });

  factory Transaction.fromJson(Map<String, dynamic> json) {
    return Transaction(
      id: json['id'] as String,
      invoiceNumber: json['invoice_number'] as String,
      pharmacyId: json['pharmacy_id'] as String,
      terminalId: json['terminal_id'] as String,
      cashierUserId: json['cashier_user_id'] as String,
      shiftId: json['shift_id'] as String,
      patientId: json['patient_id'] as String?,
      type: json['type'] as String? ?? 'Sale',
      status: json['status'] as String? ?? 'Completed',
      subtotal: Money.fromJson(json['subtotal'] as Map<String, dynamic>),
      discountAmount: Money.fromJson(json['discount_amount'] as Map<String, dynamic>),
      taxAmount: Money.fromJson(json['tax_amount'] as Map<String, dynamic>),
      totalAmount: Money.fromJson(json['total_amount'] as Map<String, dynamic>),
      items: (json['items'] as List<dynamic>)
          .map((e) => TransactionItem.fromJson(e as Map<String, dynamic>))
          .toList(),
      payments: (json['payments'] as List<dynamic>?)
              ?.map((e) => Payment.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const [],
      createdAtUtc: json['created_at_utc'] as String,
      completedAtUtc: json['completed_at_utc'] as String?,
    );
  }

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
        'subtotal': subtotal.toJson(),
        'discount_amount': discountAmount.toJson(),
        'tax_amount': taxAmount.toJson(),
        'total_amount': totalAmount.toJson(),
        'items': items.map((e) => e.toJson()).toList(),
        'payments': payments.map((e) => e.toJson()).toList(),
        'created_at_utc': createdAtUtc,
        'completed_at_utc': completedAtUtc,
      };
}
