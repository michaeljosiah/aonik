import 'package:flutter/widgets.dart';

import 'spending_category_detail_screen.dart';

class SpendingMerchantDetailScreen extends StatelessWidget {
  const SpendingMerchantDetailScreen({
    super.key,
    required this.merchantId,
  });

  final String merchantId;

  @override
  Widget build(BuildContext context) {
    return SpendingCategoryDetailScreen(categoryId: merchantId);
  }
}
