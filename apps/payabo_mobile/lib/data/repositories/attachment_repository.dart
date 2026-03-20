// ─────────────────────────────────────────────────────────
//  AttachmentRepository — abstract interface
//
//  Manages file attachments (photos, PDFs) on transactions.
//  Attachments are lazy-loaded on the detail screen, not
//  inline with the transaction list.
// ─────────────────────────────────────────────────────────

import 'spending_repository.dart';

abstract class AttachmentRepository {
  /// Returns all attachments for the given transaction.
  Future<List<Attachment>> getTransactionAttachments(String transactionId);

  /// Uploads a file and attaches it to the given transaction.
  ///
  /// [filePath] is the absolute path on the device file system.
  /// [fileName] is the display name (e.g. `receipt.jpg`).
  /// Returns the created [Attachment] with server-assigned ID and URL.
  Future<Attachment> addTransactionAttachment(
    String transactionId,
    String filePath,
    String fileName,
  );

  /// Deletes an attachment by its ID.
  Future<void> deleteAttachment(String attachmentId);
}
