import '../../data/repositories/attachment_repository.dart';
import '../../data/repositories/spending_repository.dart';
import '../mock_behavior.dart';

/// In-memory mock implementation of [AttachmentRepository].
///
/// Stores attachments in a `Map<String, List<Attachment>>` keyed by
/// transaction ID. A handful of seed transactions receive sample
/// attachments so the UI is populated in demo mode.
class MockAttachmentRepository implements AttachmentRepository {
  MockAttachmentRepository() {
    _seedData();
  }

  /// In-memory store: transactionId → list of attachments.
  final Map<String, List<Attachment>> _store = <String, List<Attachment>>{};

  int _counter = 0;

  // ─────────────────────────────────────────────────────────
  //  Seed data
  // ─────────────────────────────────────────────────────────

  /// IDs must match seed transaction IDs from MockSpendingRepository.
  static const String _seedTx1 = 'txn-starling-001';
  static const String _seedTx2 = 'txn-starling-003';
  static const String _seedTx3 = 'txn-gtb-001';

  void _seedData() {
    _store[_seedTx1] = <Attachment>[
      Attachment(
        id: 'att-seed-001',
        fileName: 'tesco-receipt.jpg',
        mimeType: 'image/jpeg',
        url: 'https://mock.payabo.app/receipts/tesco-receipt.jpg',
        thumbnailUrl: 'https://mock.payabo.app/receipts/tesco-receipt_thumb.jpg',
        fileSizeBytes: 284000,
        createdAt: DateTime.now().subtract(const Duration(days: 2)),
      ),
    ];

    _store[_seedTx2] = <Attachment>[
      Attachment(
        id: 'att-seed-002',
        fileName: 'netflix-invoice.pdf',
        mimeType: 'application/pdf',
        url: 'https://mock.payabo.app/receipts/netflix-invoice.pdf',
        fileSizeBytes: 142000,
        createdAt: DateTime.now().subtract(const Duration(days: 5)),
      ),
      Attachment(
        id: 'att-seed-003',
        fileName: 'subscription-screenshot.png',
        mimeType: 'image/png',
        url: 'https://mock.payabo.app/receipts/subscription-screenshot.png',
        thumbnailUrl:
            'https://mock.payabo.app/receipts/subscription-screenshot_thumb.png',
        fileSizeBytes: 520000,
        createdAt: DateTime.now().subtract(const Duration(days: 5)),
      ),
    ];

    _store[_seedTx3] = <Attachment>[
      Attachment(
        id: 'att-seed-004',
        fileName: 'jumia-order-confirmation.jpg',
        mimeType: 'image/jpeg',
        url: 'https://mock.payabo.app/receipts/jumia-order-confirmation.jpg',
        thumbnailUrl:
            'https://mock.payabo.app/receipts/jumia-order-confirmation_thumb.jpg',
        fileSizeBytes: 196000,
        createdAt: DateTime.now().subtract(const Duration(days: 1)),
      ),
    ];
  }

  // ─────────────────────────────────────────────────────────
  //  AttachmentRepository
  // ─────────────────────────────────────────────────────────

  @override
  Future<List<Attachment>> getTransactionAttachments(
    String transactionId,
  ) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('attachment.getTransactionAttachments');

    final List<Attachment> list = _store[transactionId] ?? const <Attachment>[];
    return List<Attachment>.of(list);
  }

  @override
  Future<Attachment> addTransactionAttachment(
    String transactionId,
    String filePath,
    String fileName,
  ) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('attachment.addTransactionAttachment');

    _counter++;

    // Guess mime type from file extension.
    final String ext = fileName.split('.').last.toLowerCase();
    final String mimeType;
    switch (ext) {
      case 'jpg':
      case 'jpeg':
        mimeType = 'image/jpeg';
      case 'png':
        mimeType = 'image/png';
      case 'gif':
        mimeType = 'image/gif';
      case 'webp':
        mimeType = 'image/webp';
      case 'pdf':
        mimeType = 'application/pdf';
      default:
        mimeType = 'application/octet-stream';
    }

    final bool isImage = mimeType.startsWith('image/');
    final String mockUrl =
        'https://mock.payabo.app/uploads/mock-attachment-$_counter.$ext';

    final Attachment attachment = Attachment(
      id: 'att-mock-$_counter',
      fileName: fileName,
      mimeType: mimeType,
      url: mockUrl,
      thumbnailUrl: isImage ? '${mockUrl}_thumb.$ext' : null,
      fileSizeBytes: 250000, // arbitrary mock size
      createdAt: DateTime.now(),
    );

    _store
        .putIfAbsent(transactionId, () => <Attachment>[])
        .add(attachment);

    return attachment;
  }

  @override
  Future<void> deleteAttachment(String attachmentId) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('attachment.deleteAttachment');

    for (final list in _store.values) {
      list.removeWhere((Attachment a) => a.id == attachmentId);
    }
  }
}
