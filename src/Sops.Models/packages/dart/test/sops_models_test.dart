import 'package:test/test.dart';
import 'package:sops_models/sops_models.dart';

void main() {
  group('SOPs Canonical Dart Models', () {
    test('Money initializes and formats correctly', () {
      const money = Money(
        amountMinorUnits: 1425000,
        currency: 'EGP',
      );
      expect(money.amountMinorUnits, equals(1425000));
      expect(money.currency, equals('EGP'));
      expect(money.toDecimal(), equals(14250.00));
    });

    test('Money serializes and deserializes cleanly', () {
      final json = {
        'amountInMinorUnits': 5000,
        'currency': 'USD',
      };
      final money = Money.fromJson(json);
      expect(money.amountMinorUnits, equals(5000));
      expect(money.currency, equals('USD'));
      expect(money.toJson()['currency'], equals('USD'));
    });
  });
}
